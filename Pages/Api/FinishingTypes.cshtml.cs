using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WiseLabels.Pages.Api
{
    /// <summary>
    /// Proxy Razor Page model that retrieves finishing types from the external CERM API
    /// and returns them as JSON to the client. This decouples the client from direct
    /// CORS/auth requirements and centralizes token handling and mapping logic.
    /// </summary>
    public class FinishingTypesModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FinishingTypesModel> _logger;

        /// <summary>
        /// Constructor - dependencies are injected by the Razor Pages framework.
        /// </summary>
        public FinishingTypesModel(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<FinishingTypesModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// GET handler invoked by client-side code at "/Api/FinishingTypes".
        /// - Reads CERM configuration values from appsettings.
        /// - Authenticates against the OAuth endpoint to retrieve an access token.
        /// - Calls the configured FinishingTypesUrl and returns a JSON payload.
        /// - Returns guarded error responses for configuration, authentication, and fetch failures.
        /// </summary>
        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Get credentials from configuration (fallback defined for OAuth URL only)
                var oauthUrl = _configuration["Cerm:OAuthUrl"] ?? "https://brandmark-api.cerm.be/oauth/token";
                var finishingTypesUrl = _configuration["Cerm:FinishingTypesUrl"] ?? "";
                var username = _configuration["Cerm:Username"];
                var password = _configuration["Cerm:Password"];
                var clientId = _configuration["Cerm:ClientId"];
                var clientSecret = _configuration["Cerm:ClientSecret"];

                // Validate required configuration and return a 500 with a helpful message when missing.
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) ||
                    string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    _logger.LogError("CERM API credentials are missing from configuration");
                    return new JsonResult(new { error = "Server configuration error: CERM API credentials not configured" })
                    {
                        StatusCode = 500
                    };
                }

                if (string.IsNullOrEmpty(finishingTypesUrl))
                {
                    _logger.LogError("Cerm:FinishingTypesUrl is missing from configuration (add to appsettings.json or appsettings.Production.json)");
                    return new JsonResult(new { error = "Server configuration error: FinishingTypesUrl not configured" })
                    {
                        StatusCode = 500
                    };
                }

                // Authenticate and get access token. GetAccessTokenAsync implements multiple
                // authentication strategies to handle different OAuth server expectations.
                var (accessToken, authError) = await GetAccessTokenAsync(oauthUrl, username, password, clientId, clientSecret);
                if (string.IsNullOrEmpty(accessToken))
                {
                    // Return 401 so client-side code can surface an authentication-related error.
                    return new JsonResult(new { error = authError ?? "Failed to authenticate with CERM API" })
                    {
                        StatusCode = 401
                    };
                }

                // Fetch finishing types using the obtained bearer token. The method attempts
                // to parse a variety of JSON shapes and will normalize to a List<ParameterResponse>.
                var materials = await FetchMaterialsAsync(finishingTypesUrl, accessToken);

                // Return normalized materials as JSON.
                return new JsonResult(new { materials = materials });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching materials from CERM API");
                return new JsonResult(new { error = $"Error loading materials: {ex.Message}" })
                {
                    StatusCode = 500
                };
            }
        }

        /// <summary>
        /// Attempts to retrieve an access token from the OAuth endpoint using several strategies.
        /// Returns a tuple containing the access token (or null) and an error message (or null).
        /// </summary>
        private async Task<(string? accessToken, string? errorMessage)> GetAccessTokenAsync(string oauthUrl, string username, string password, string clientId, string clientSecret)
        {
            HttpClient? httpClient = null;
            try
            {
                // Normalize URL and allow permissive certificate handling for local/internal hosts.
                oauthUrl = oauthUrl.TrimEnd('/');
                var handler = new HttpClientHandler();
                if (oauthUrl.Contains("192.168.") || oauthUrl.Contains("localhost") || oauthUrl.Contains("127.0.0.1"))
                {
                    handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                }

                httpClient = new HttpClient(handler);
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                _logger.LogInformation("OAuth URL: {OAuthUrl}", oauthUrl);

                // Method 1: Send credentials in the request body (some OAuth servers expect this).
                var formData = new List<KeyValuePair<string, string>>
                {
                    new("grant_type", "password"),
                    new("username", username),
                    new("password", password),
                    new("client_id", clientId),
                    new("client_secret", clientSecret)
                };

                var request = new HttpRequestMessage(HttpMethod.Post, oauthUrl)
                {
                    Content = new FormUrlEncodedContent(formData)
                };
                request.Headers.Add("Accept", "application/json");

                _logger.LogInformation("Attempting OAuth authentication (method 1 - credentials in body only) to {OAuthUrl}", oauthUrl);

                var response = await httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorMessage = $"OAuth authentication failed: {response.StatusCode} - {errorContent}";
                    _logger.LogError("OAuth authentication failed (method 1 - credentials in body only): {StatusCode} - {Error}. Request URL: {OAuthUrl}",
                        response.StatusCode, errorContent, oauthUrl);

                    // Try method 2: Basic Auth header + credentials in body
                    _logger.LogInformation("Retrying with Basic Auth header + credentials in body");
                    var (retryToken2, retryError2) = await TryWithBasicAuthHeader(oauthUrl, username, password, clientId, clientSecret);
                    if (!string.IsNullOrEmpty(retryToken2))
                    {
                        return (retryToken2, null);
                    }

                    // Try method 3: Basic Auth only (credentials in header only, not in body)
                    _logger.LogInformation("Retrying with Basic Auth only (credentials in header only)");
                    var (retryToken3, retryError3) = await TryBasicAuthOnly(oauthUrl, username, password, clientId, clientSecret);
                    if (!string.IsNullOrEmpty(retryToken3))
                    {
                        return (retryToken3, null);
                    }

                    // All methods failed, return detailed error
                    return (null, $"All methods failed. Method 1: {errorMessage}. Method 2: {retryError2}. Method 3: {retryError3}");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<JsonElement>(jsonResponse);

                if (tokenData.TryGetProperty("access_token", out var accessToken))
                {
                    _logger.LogInformation("OAuth authentication successful");
                    return (accessToken.GetString(), null);
                }

                var missingTokenError = "Access token not found in OAuth response";
                _logger.LogError(missingTokenError);
                return (null, missingTokenError);
            }
            catch (Exception ex)
            {
                var exceptionError = $"Exception during OAuth authentication: {ex.Message}";
                _logger.LogError(ex, exceptionError);
                return (null, exceptionError);
            }
            finally
            {
                httpClient?.Dispose();
            }
        }

        /// <summary>
        /// Secondary authentication attempt: include both Basic Auth header and body parameters
        /// (some servers expect client credentials in both places).
        /// </summary>
        private async Task<(string? accessToken, string? errorMessage)> TryWithBasicAuthHeader(string oauthUrl, string username, string password, string clientId, string clientSecret)
        {
            HttpClient? httpClient = null;
            try
            {
                var handler = new HttpClientHandler();
                if (oauthUrl.Contains("192.168.") || oauthUrl.Contains("localhost") || oauthUrl.Contains("127.0.0.1"))
                {
                    handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                }

                httpClient = new HttpClient(handler);
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                // Client credentials in BOTH body AND Basic Auth header
                var formData = new List<KeyValuePair<string, string>>
                {
                    new("grant_type", "password"),
                    new("username", username),
                    new("password", password),
                    new("client_id", clientId),
                    new("client_secret", clientSecret)
                };

                var request = new HttpRequestMessage(HttpMethod.Post, oauthUrl)
                {
                    Content = new FormUrlEncodedContent(formData)
                };
                request.Headers.Add("Accept", "application/json");

                // Add Basic Auth header with client credentials
                var clientCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", clientCredentials);

                _logger.LogInformation("Trying OAuth authentication (method 2 - Basic Auth header + credentials in body)");

                var response = await httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorMessage = $"OAuth authentication failed: {response.StatusCode} - {errorContent}";
                    _logger.LogError("OAuth authentication failed (method 2): {StatusCode} - {Error}", response.StatusCode, errorContent);
                    return (null, errorMessage);
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<JsonElement>(jsonResponse);

                if (tokenData.TryGetProperty("access_token", out var accessToken))
                {
                    _logger.LogInformation("OAuth authentication successful (method 2)");
                    return (accessToken.GetString(), null);
                }

                return (null, "Access token not found in OAuth response (method 2)");
            }
            catch (Exception ex)
            {
                var exceptionError = $"Exception during OAuth authentication (method 2): {ex.Message}";
                _logger.LogError(ex, exceptionError);
                return (null, exceptionError);
            }
            finally
            {
                httpClient?.Dispose();
            }
        }

        /// <summary>
        /// Third authentication attempt: use Basic Auth for client credentials while keeping
        /// user credentials in the body (client_id/client_secret omitted from body).
        /// </summary>
        private async Task<(string? accessToken, string? errorMessage)> TryBasicAuthOnly(string oauthUrl, string username, string password, string clientId, string clientSecret)
        {
            HttpClient? httpClient = null;
            try
            {
                var handler = new HttpClientHandler();
                if (oauthUrl.Contains("192.168.") || oauthUrl.Contains("localhost") || oauthUrl.Contains("127.0.0.1"))
                {
                    handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                }

                httpClient = new HttpClient(handler);
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                // Client credentials ONLY in Basic Auth header, NOT in body
                var formData = new List<KeyValuePair<string, string>>
                {
                    new("grant_type", "password"),
                    new("username", username),
                    new("password", password)
                    // client_id and client_secret NOT in body
                };

                var request = new HttpRequestMessage(HttpMethod.Post, oauthUrl)
                {
                    Content = new FormUrlEncodedContent(formData)
                };
                request.Headers.Add("Accept", "application/json");

                // Client credentials as Basic Auth in headers only
                var clientCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", clientCredentials);

                _logger.LogInformation("Retrying OAuth authentication with Basic Auth only (no client credentials in body)");

                var response = await httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorMessage = $"OAuth authentication failed with Basic Auth only: {response.StatusCode} - {errorContent}";
                    _logger.LogError(errorMessage);
                    return (null, errorMessage);
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<JsonElement>(jsonResponse);

                if (tokenData.TryGetProperty("access_token", out var accessToken))
                {
                    _logger.LogInformation("OAuth authentication successful with Basic Auth only");
                    return (accessToken.GetString(), null);
                }

                return (null, "Access token not found in OAuth response (Basic Auth method)");
            }
            catch (Exception ex)
            {
                var exceptionError = $"Exception during OAuth authentication (Basic Auth only): {ex.Message}";
                _logger.LogError(ex, exceptionError);
                return (null, exceptionError);
            }
            finally
            {
                httpClient?.Dispose();
            }
        }

        /// <summary>
        /// Fetches the materials/finishing types from the provided URL using the bearer token,
        /// attempts to handle multiple JSON shapes returned by the upstream API, and returns
        /// a normalized List of ParameterResponse. The method:
        /// - Tries to parse direct arrays, Data/items/results wrappers.
        /// - Falls back to a manual mapping routine when automatic deserialization yields missing Ids.
        /// - Filters on AllowRFQ and sorts by the en-US description.
        /// </summary>
        private async Task<object> FetchMaterialsAsync(string materialsUrl, string accessToken)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

                var response = await httpClient.GetAsync(materialsUrl);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to fetch materials: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    throw new HttpRequestException($"Failed to fetch materials: {response.StatusCode}");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();

                // Try to deserialize - handle different response structures
                List<ParameterResponse>? paramResponse = null;
                JsonElement jsonDoc = default;
                var hasJsonDoc = false;

                try
                {
                    // First, try to parse as JsonElement to inspect structure
                    jsonDoc = JsonSerializer.Deserialize<JsonElement>(jsonResponse);
                    hasJsonDoc = true;

                    // If an array is returned, log structure details to aid future debugging.
                    if (jsonDoc.ValueKind == JsonValueKind.Array && jsonDoc.GetArrayLength() > 0)
                    {
                        var firstItem = jsonDoc[0];
                        var propertyNames = firstItem.EnumerateObject().Select(p => p.Name).ToList();
                        _logger.LogInformation("First item structure - All Properties: {Properties}", string.Join(", ", propertyNames));

                        // Log subproperty names for object properties to help identify mapping candidates.
                        foreach (var propName in propertyNames)
                        {
                            var prop = firstItem.GetProperty(propName);
                            _logger.LogInformation("Property '{PropertyName}' type: {Type}, ValueKind: {ValueKind}",
                                propName, prop.GetType().Name, prop.ValueKind);
                            if (prop.ValueKind == JsonValueKind.Object)
                            {
                                var subProps = prop.EnumerateObject().Select(sp => sp.Name).ToList();
                                _logger.LogInformation("  Sub-properties of '{PropertyName}': {SubProperties}",
                                    propName, string.Join(", ", subProps));
                            }
                        }
                    }

                    // Try several common wrapper shapes, falling back to a direct array parse.
                    if (jsonDoc.ValueKind == JsonValueKind.Array)
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        paramResponse = JsonSerializer.Deserialize<List<ParameterResponse>>(jsonResponse, options);
                    }
                    else if (jsonDoc.TryGetProperty("Data", out var data) && data.ValueKind == JsonValueKind.Array)
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        paramResponse = JsonSerializer.Deserialize<List<ParameterResponse>>(data.GetRawText(), options);
                    }
                    else if (jsonDoc.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        paramResponse = JsonSerializer.Deserialize<List<ParameterResponse>>(items.GetRawText(), options);
                    }
                    else if (jsonDoc.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        paramResponse = JsonSerializer.Deserialize<List<ParameterResponse>>(results.GetRawText(), options);
                    }
                    else
                    {
                        // Unexpected shapes are attempted to be parsed directly with case-insensitive matching.
                        _logger.LogWarning("Unexpected JSON structure. Root kind: {ValueKind}", jsonDoc.ValueKind);
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        paramResponse = JsonSerializer.Deserialize<List<ParameterResponse>>(jsonResponse, options);
                    }

                    // If parsed items appear to be missing critical fields like Id, attempt manual mapping.
                    if (paramResponse != null && paramResponse.Count > 0 && string.IsNullOrEmpty(paramResponse[0].Id))
                    {
                        _logger.LogWarning("Id is null after deserialization. Attempting manual mapping...");
                        paramResponse = ManualMapMaterials(jsonDoc);
                    }
                }
                catch (JsonException jsonEx)
                {
                    // Provide a helpful log and throw with a truncated response to keep logs readable.
                    _logger.LogError(jsonEx, "JSON deserialization error. Response: {Response}", jsonResponse);
                    throw new Exception($"Failed to parse materials response: {jsonEx.Message}. Response: {jsonResponse.Substring(0, Math.Min(500, jsonResponse.Length))}", jsonEx);
                }

                if (paramResponse != null && paramResponse.Count > 0)
                {
                    // Only include items where AllowRFQ == true.
                    paramResponse = paramResponse.Where(m => m.AllowRFQ).ToList();

                    // Normalize FinishingType (Inline=1 / Offline=2) from upstream payload.
                    // Some upstream responses carry this as FinishingTypeId/Type/etc; we extract it from
                    // the raw JsonElement and copy it onto ParameterResponse.FinishingType.
                    if (hasJsonDoc)
                    {
                        var finishingTypeById = ExtractFinishingTypeById(jsonDoc);
                        if (finishingTypeById.Count > 0)
                        {
                            foreach (var item in paramResponse)
                            {
                                if (!string.IsNullOrWhiteSpace(item.Id) && finishingTypeById.TryGetValue(item.Id, out var finishingType))
                                {
                                    item.FinishingType = finishingType;
                                }
                            }
                        }
                    }

                    // Sort by the en-US description when available, otherwise fallback to first description.
                    paramResponse = paramResponse.OrderBy(material =>
                    {
                        if (material.Descriptions != null && material.Descriptions.Count > 0)
                        {
                            var enUSDesc = material.Descriptions.FirstOrDefault(d =>
                                d.ISOLanguageCode != null && d.ISOLanguageCode.Equals("en-US", StringComparison.OrdinalIgnoreCase));
                            var desc = enUSDesc ?? material.Descriptions[0];
                            return desc.Description ?? string.Empty;
                        }
                        return string.Empty;
                    }, StringComparer.OrdinalIgnoreCase).ToList();

                    _logger.LogInformation("Returning {Count} materials after filtering and sorting", paramResponse.Count);
                    return (paramResponse);
                }

                _logger.LogWarning("No materials found in response");
                return (new List<ParameterResponse>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while fetching materials");
                throw;
            }
        }

        /// <summary>
        /// Manually maps a JsonElement (array or wrapper) to a List&lt;ParameterResponse&gt; when
        /// automatic deserialization is insufficient (e.g. unexpected property names or casing).
        /// This routine attempts case-insensitive lookup for common property names.
        /// </summary>
        private List<ParameterResponse> ManualMapMaterials(JsonElement jsonDoc)
        {
            var result = new List<ParameterResponse>();

            JsonElement arrayElement;
            if (jsonDoc.ValueKind == JsonValueKind.Array)
            {
                arrayElement = jsonDoc;
            }
            else if (jsonDoc.TryGetProperty("Data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                arrayElement = data;
            }
            else if (jsonDoc.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                arrayElement = items;
            }
            else if (jsonDoc.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
            {
                arrayElement = results;
            }
            else
            {
                _logger.LogWarning("Cannot find array in JSON structure");
                return result;
            }

            foreach (var item in arrayElement.EnumerateArray())
            {
                var param = new ParameterResponse();

                // Id (case-insensitive)
                if (item.TryGetProperty("Id", out var idProp) || item.TryGetProperty("id", out idProp))
                {
                    param.Id = idProp.GetString();
                }

                // Descriptions array (case-insensitive) - map to local Descriptions type
                if ((item.TryGetProperty("Descriptions", out var descriptionsProp) ||
                     item.TryGetProperty("descriptions", out descriptionsProp)) &&
                    descriptionsProp.ValueKind == JsonValueKind.Array)
                {
                    var descriptions = new List<Descriptions>();
                    foreach (var desc in descriptionsProp.EnumerateArray())
                    {
                        var description = new Descriptions();
                        if (desc.TryGetProperty("ISOLanguageCode", out var langCodeProp) ||
                            desc.TryGetProperty("isoLanguageCode", out langCodeProp) ||
                            desc.TryGetProperty("ISOLanguagecode", out langCodeProp))
                        {
                            description.ISOLanguageCode = langCodeProp.GetString();
                        }
                        if (desc.TryGetProperty("Description", out var descProp) ||
                            desc.TryGetProperty("description", out descProp))
                        {
                            description.Description = descProp.GetString();
                        }
                        descriptions.Add(description);
                    }
                    param.Descriptions = descriptions;
                }

                // Blocked flag (case-insensitive)
                if (item.TryGetProperty("Blocked", out var blockedProp) ||
                    item.TryGetProperty("blocked", out blockedProp))
                {
                    param.Blocked = blockedProp.GetBoolean();
                }

                // Website (optional)
                if (item.TryGetProperty("Website", out var websiteProp) ||
                    item.TryGetProperty("website", out websiteProp))
                {
                    param.Website = websiteProp.GetString();
                }

                // AllowQuickQuote (optional)
                if (item.TryGetProperty("AllowQuickQuote", out var allowQuickQuoteProp) ||
                    item.TryGetProperty("allowQuickQuote", out allowQuickQuoteProp))
                {
                    param.AllowQuickQuote = allowQuickQuoteProp.GetBoolean();
                }

                result.Add(param);
            }

            _logger.LogInformation("Manually mapped {Count} materials", result.Count);
            return result;
        }

        private static Dictionary<string, int> ExtractFinishingTypeById(JsonElement jsonDoc)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            JsonElement arrayElement;
            if (jsonDoc.ValueKind == JsonValueKind.Array)
            {
                arrayElement = jsonDoc;
            }
            else if (jsonDoc.TryGetProperty("Data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                arrayElement = data;
            }
            else if (jsonDoc.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                arrayElement = items;
            }
            else if (jsonDoc.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
            {
                arrayElement = results;
            }
            else
            {
                return map;
            }

            foreach (var item in arrayElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;

                var id = TryGetStringPropertyCaseInsensitive(item, "Id");
                if (string.IsNullOrWhiteSpace(id)) continue;

                var finishingType = TryExtractFinishingType(item);
                if (finishingType is 1 or 2)
                {
                    map[id] = finishingType.Value;
                }
            }

            return map;
        }

        private static int? TryExtractFinishingType(JsonElement item)
        {
            // Prefer explicit names containing both "finishing" and "type"
            foreach (var prop in item.EnumerateObject())
            {
                var nameLower = prop.Name.ToLowerInvariant();
                if (nameLower.Contains("finishing") && nameLower.Contains("type"))
                {
                    var val = ReadIntFlexible(prop.Value);
                    if (val != null) return val;
                }
            }

            // Common explicit keys
            var explicitKeys = new[] { "FinishingType", "FinishingTypeId", "FinishingTypeID", "Type" };
            foreach (var key in explicitKeys)
            {
                if (TryGetPropertyCaseInsensitive(item, key, out var el))
                {
                    var val = ReadIntFlexible(el);
                    if (val != null) return val;
                }
            }

            return null;
        }

        private static int? ReadIntFlexible(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
            {
                return n;
            }

            if (el.ValueKind == JsonValueKind.String)
            {
                var str = el.GetString();
                if (int.TryParse(str, out var s))
                {
                    return s;
                }

                // Some upstream payloads may encode finishing type as text.
                // Normalize to: 1 = Inline, 2 = Offline (per BUSINESS_RULES.md)
                if (!string.IsNullOrWhiteSpace(str))
                {
                    var lower = str.ToLowerInvariant();
                    if (lower.Contains("inline") || lower.Contains("in-line") || lower.Contains("in line"))
                    {
                        return 1;
                    }

                    if (lower.Contains("offline") || lower.Contains("off-line") || lower.Contains("off line"))
                    {
                        return 2;
                    }

                    // Best-effort synonyms (only used if upstream uses printing terms)
                    if (lower.Contains("flexo") || lower.Contains("rotary"))
                    {
                        return 1;
                    }

                    if (lower.Contains("digital"))
                    {
                        return 2;
                    }
                }
            }

            if (el.ValueKind == JsonValueKind.Object)
            {
                // Sometimes the field is an object with an Id
                if (TryGetPropertyCaseInsensitive(el, "Id", out var idEl) || TryGetPropertyCaseInsensitive(el, "Value", out idEl))
                {
                    return ReadIntFlexible(idEl);
                }
            }

            return null;
        }

        private static bool TryGetPropertyCaseInsensitive(JsonElement obj, string name, out JsonElement value)
        {
            if (obj.ValueKind == JsonValueKind.Object)
            {
                // Fast path: exact match
                if (obj.TryGetProperty(name, out value))
                {
                    return true;
                }

                foreach (var prop in obj.EnumerateObject())
                {
                    if (prop.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        value = prop.Value;
                        return true;
                    }
                }
            }

            value = default;
            return false;
        }

        private static string? TryGetStringPropertyCaseInsensitive(JsonElement obj, string name)
        {
            if (TryGetPropertyCaseInsensitive(obj, name, out var el) && el.ValueKind == JsonValueKind.String)
            {
                return el.GetString();
            }

            return null;
        }

    }
}
