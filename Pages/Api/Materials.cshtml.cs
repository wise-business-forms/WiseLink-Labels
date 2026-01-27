using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CERM.DataAccess;

namespace WiseLabels.Pages.Api
{
    public class ParameterResponse
    {
        [JsonPropertyName("Id")]
        public string? Id { get; set; }

        [JsonPropertyName("Descriptions")]
        public List<Descriptions>? Descriptions { get; set; }

        [JsonPropertyName("Website")]
        public string? Website { get; set; }

        [JsonPropertyName("AllowQuickQuote")]
        public bool AllowQuickQuote { get; set; }

        [JsonPropertyName("AllowRFQ")]
        public bool AllowRFQ { get; set; }

        [JsonPropertyName("Blocked")]
        public bool Blocked { get; set; }

        // Optional field used by Finishing Types:
        // 1 = Inline, 2 = Offline (see BUSINESS_RULES.md BR-FIN-FUTURE-001)
        [JsonPropertyName("FinishingType")]
        public int? FinishingType { get; set; }
    }

    public class Descriptions
    {
        [JsonPropertyName("ISOLanguageCode")]
        public string? ISOLanguageCode { get; set; }

        [JsonPropertyName("Description")]
        public string? Description { get; set; }
    }

    [IgnoreAntiforgeryToken]
    public class MaterialsModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MaterialsModel> _logger;
        private readonly CermDbContext _dbContext;

        public MaterialsModel(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<MaterialsModel> logger, CermDbContext dbContext)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Get credentials from configuration
                var oauthUrl = _configuration["Cerm:OAuthUrl"] ?? "https://brandmark-api.cerm.be/oauth/token";
                var materialsUrl = _configuration["Cerm:MaterialsUrl"] ?? "https://brandmark-api.cerm.be/parameter-api/v1/calculation/substrates";
                var username = _configuration["Cerm:Username"];
                var password = _configuration["Cerm:Password"];
                var clientId = _configuration["Cerm:ClientId"];
                var clientSecret = _configuration["Cerm:ClientSecret"];

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) ||
                    string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    _logger.LogError("CERM API credentials are missing from configuration");
                    return new JsonResult(new { error = "Server configuration error: CERM API credentials not configured" })
                    {
                        StatusCode = 500
                    };
                }

                // Authenticate and get access token
                var (accessToken, authError) = await GetAccessTokenAsync(oauthUrl, username, password, clientId, clientSecret);
                if (string.IsNullOrEmpty(accessToken))
                {
                    return new JsonResult(new { error = authError ?? "Failed to authenticate with CERM API" })
                    {
                        StatusCode = 401
                    };
                }

                // Fetch materials from API (no filtering for now)
                var materials = await FetchMaterialsAsync(materialsUrl, accessToken);

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

        private async Task<(string? accessToken, string? errorMessage)> GetAccessTokenAsync(string oauthUrl, string username, string password, string clientId, string clientSecret)
        {
            HttpClient? httpClient = null;
            try
            {
                // Ensure URL doesn't have trailing slash
                oauthUrl = oauthUrl.TrimEnd('/');

                // Create handler that allows self-signed certificates for internal APIs
                var handler = new HttpClientHandler();
                if (oauthUrl.Contains("192.168.") || oauthUrl.Contains("localhost") || oauthUrl.Contains("127.0.0.1"))
                {
                    handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                }

                httpClient = new HttpClient(handler);
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                _logger.LogInformation("OAuth URL: {OAuthUrl}", oauthUrl);

                // Method 1: Try credentials in body only (matching Postman "Send client credentials in body")
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

                try
                {
                    // First, try to parse as JsonElement to inspect structure
                    var jsonDoc = JsonSerializer.Deserialize<JsonElement>(jsonResponse);

                    // Log the structure of the first item if it's an array
                    if (jsonDoc.ValueKind == JsonValueKind.Array && jsonDoc.GetArrayLength() > 0)
                    {
                        var firstItem = jsonDoc[0];
                        var propertyNames = firstItem.EnumerateObject().Select(p => p.Name).ToList();
                        _logger.LogInformation("First item structure - All Properties: {Properties}", string.Join(", ", propertyNames));

                        // Check for common property names that might contain the material data
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

                    // Check if it's a direct array
                    if (jsonDoc.ValueKind == JsonValueKind.Array)
                    {
                        // Try deserializing with case-insensitive property matching
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        paramResponse = JsonSerializer.Deserialize<List<ParameterResponse>>(jsonResponse, options);
                    }
                    // Check if it's wrapped in a Data property
                    else if (jsonDoc.TryGetProperty("Data", out var data) && data.ValueKind == JsonValueKind.Array)
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        paramResponse = JsonSerializer.Deserialize<List<ParameterResponse>>(data.GetRawText(), options);
                    }
                    // Check if it's wrapped in other common properties
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
                        _logger.LogWarning("Unexpected JSON structure. Root kind: {ValueKind}", jsonDoc.ValueKind);
                        // Try direct deserialization with case-insensitive matching
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        paramResponse = JsonSerializer.Deserialize<List<ParameterResponse>>(jsonResponse, options);
                    }

                    // If Id is still null after deserialization, try manual mapping
                    if (paramResponse != null && paramResponse.Count > 0 && string.IsNullOrEmpty(paramResponse[0].Id))
                    {
                        _logger.LogWarning("Id is null after deserialization. Attempting manual mapping...");
                        paramResponse = ManualMapMaterials(jsonDoc);
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "JSON deserialization error. Response: {Response}", jsonResponse);
                    throw new Exception($"Failed to parse materials response: {jsonEx.Message}. Response: {jsonResponse.Substring(0, Math.Min(500, jsonResponse.Length))}", jsonEx);
                }

                if (paramResponse != null && paramResponse.Count > 0)
                {
                    // Filter only on AllowRFQ flag being true
                    paramResponse = paramResponse.Where(m => m.AllowRFQ).ToList();

                    // Sort materials alphabetically by their display description (en-US)
                    paramResponse = paramResponse.OrderBy(material =>
                    {
                        if (material.Descriptions != null && material.Descriptions.Count > 0)
                        {
                            // Prefer en-US description
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

                // Get Id (case-insensitive)
                if (item.TryGetProperty("Id", out var idProp) || item.TryGetProperty("id", out idProp))
                {
                    param.Id = idProp.GetString();
                }

                // Get Descriptions array (case-insensitive)
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

                //// Get AllowRFQ (case-insensitive)
                //if (item.TryGetProperty("AllowRFQ", out var allowRFQProp) || 
                //    item.TryGetProperty("allowRFQ", out allowRFQProp))
                //{
                //    param.AllowRFQ = allowRFQProp.GetBoolean();
                //}

                // Get Blocked (case-insensitive)
                if (item.TryGetProperty("Blocked", out var blockedProp) || 
                    item.TryGetProperty("blocked", out blockedProp))
                {
                    param.Blocked = blockedProp.GetBoolean();
                }

                // Get Website (optional)
                if (item.TryGetProperty("Website", out var websiteProp) || 
                    item.TryGetProperty("website", out websiteProp))
                {
                    param.Website = websiteProp.GetString();
                }

                // Get AllowQuickQuote (optional)
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
        
        /// <summary>
        /// Queries the database to get material IDs that are allowed for the given printing selection.
        /// This method is called server-side only - no database information is exposed to the client.
        /// </summary>
        /// <param name="printing">The printing selection (ColorCode ID or description text)</param>
        /// <returns>List of allowed material IDs, or null if no filtering should be applied</returns>
        private async Task<List<string>?> GetAllowedMaterialIdsAsync(string printing)
        {
            try
            {
                // TODO: Replace this with the actual database query based on your schema
                // The query should return material IDs that are compatible with the given printing type
                // 
                // Example query structure (adjust table/column names to match your schema):
                // SELECT DISTINCT MaterialId 
                // FROM MaterialPrintingTypes 
                // WHERE PrintingTypeId = @printingId OR PrintingTypeDescription LIKE @printing
                //
                // Or if the relationship is stored differently:
                // SELECT DISTINCT s.SubstrateId
                // FROM Substrates s
                // INNER JOIN SubstratePrintingTypes spt ON s.SubstrateId = spt.SubstrateId
                // INNER JOIN PrintingTypes pt ON spt.PrintingTypeId = pt.PrintingTypeId
                // WHERE pt.PrintingTypeId = @printingId OR pt.Description LIKE @printing
                
                _logger.LogInformation("Querying database for materials allowed for printing: {Printing}", printing);
                
                // For now, return null to indicate no filtering (returns all materials)
                // Once you provide the table/column names, I'll implement the actual query
                // 
                // You can use either:
                // 1. Raw SQL: _dbContext.Database.SqlQueryRaw<string>("SELECT ...")
                // 2. LINQ: _dbContext.TableName.Where(...).Select(...).ToListAsync()
                // 3. Repository pattern: _substrateRepository.GetMaterialsByPrintingAsync(printing)
                
                await Task.CompletedTask; // Placeholder for async operation
                
                _logger.LogWarning("GetAllowedMaterialIdsAsync not yet implemented - returning null (no filtering)");
                return null; // Return null to indicate no filtering should be applied
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying database for allowed materials. Printing: {Printing}", printing);
                // Return null on error to allow all materials rather than blocking everything
                return null;
            }
        }

    }
}

