using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WiseLabels.Services;

namespace WiseLabels.Pages.Api
{
    public class ColorCodeResponse
    {
        [JsonPropertyName("ColourBacking")]
        public ColourBackingData? ColourBacking { get; set; }

        [JsonPropertyName("Blocked")]
        public bool? Blocked { get; set; }

        [JsonPropertyName("AllowRFQ")]
        public bool? AllowRFQ { get; set; }
    }

    public class ColourBackingData
    {
        [JsonPropertyName("Id")]
        public string? Id { get; set; }

        [JsonPropertyName("Descriptions")]
        public List<ColorCodeDescriptions>? Descriptions { get; set; }

        public List<ColorCodeDescriptions>? Discriptions { get; set; }
    }

    public class ColorCodeDescriptions
    {
        [JsonPropertyName("ISOLanguageCode")]
        public string? ISOLanguageCode { get; set; }

        [JsonPropertyName("Description")]
        public string? Description { get; set; }
    }

    [IgnoreAntiforgeryToken]
    public class ColorCodesModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ColorCodesModel> _logger;
        private readonly ICermAuthService _cermAuthService;

        public ColorCodesModel(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<ColorCodesModel> logger, ICermAuthService cermAuthService)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _cermAuthService = cermAuthService;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            _logger.LogInformation("ColorCodes OnGetAsync called");
            try
            {
                var colorCodesUrl = _configuration["Cerm:ColorCodesUrl"] ?? "https://brandmark-api.cerm.be/parameter-api/v1/calculation/quick-quote/colour-codes";

                _logger.LogInformation("ColorCodes endpoint called. URL: {ColorCodesUrl}", colorCodesUrl);

                var accessToken = await _cermAuthService.GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    return new JsonResult(new { error = "Failed to authenticate with CERM API" })
                    {
                        StatusCode = 401
                    };
                }

                // Fetch color codes
                var colorCodes = await FetchColorCodesAsync(colorCodesUrl, accessToken);
                                
                return new JsonResult(colorCodes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching color codes from CERM API");
                return new JsonResult(new { error = $"Error loading color codes: {ex.Message}" })
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

        private async Task<object> FetchColorCodesAsync(string colorCodesUrl, string accessToken)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

                var response = await httpClient.GetAsync(colorCodesUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to fetch color codes: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    throw new HttpRequestException($"Failed to fetch color codes: {response.StatusCode}");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Color Codes API response: {Response}", jsonResponse);

                // Try to deserialize - handle different response structures
                List<ParameterResponse>? paramResponse = null;
                
                try
                {
                    // First, try to parse as JsonElement to inspect structure
                    var jsonDoc = JsonSerializer.Deserialize<JsonElement>(jsonResponse);

                    if (jsonDoc.TryGetProperty("Data", out var data) && data.ValueKind == JsonValueKind.Array)
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        paramResponse = JsonSerializer.Deserialize<List<ParameterResponse>>(data.GetRawText(), options);
                    }

                    var result = new List<ParameterResponse>();

                    JsonElement arrayElement = new JsonElement();
                    List<ParameterResponse> paramResponses = new List<ParameterResponse>();

                    if (jsonDoc.ValueKind != JsonValueKind.Array) 
                    { 
                        arrayElement = jsonDoc; 
                    }
                    else if (jsonDoc.TryGetProperty("Data", out var dataOut) &&  data.ValueKind == JsonValueKind.Array) 
                    {
                        arrayElement = dataOut;
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        paramResponse = JsonSerializer.Deserialize<List<ParameterResponse>>(arrayElement.GetRawText(), options);
                    }

                    // Order printing options
                    paramResponse.OrderBy(cc => cc.Id);

                    return paramResponse;
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "JSON deserialization error. Response: {Response}", jsonResponse);
                    throw new Exception($"Failed to parse color codes response: {jsonEx.Message}. Response: {jsonResponse.Substring(0, Math.Min(500, jsonResponse.Length))}", jsonEx);
                }
                
                _logger.LogWarning("No color codes found in response");
                return new List<ParameterResponse>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while fetching color codes");
                throw;
            }
        }
    }
}

