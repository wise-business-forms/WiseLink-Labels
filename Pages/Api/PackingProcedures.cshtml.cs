using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WiseLabels.Pages.Api
{
    /// <summary>
    /// Proxy Razor Page model that retrieves packing procedures from the external CERM API
    /// and returns them as JSON to the client. This decouples the client from direct
    /// CORS/auth requirements and centralizes token handling and mapping logic.
    /// </summary>
    public class PackingProceduresModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PackingProceduresModel> _logger;

        /// <summary>
        /// Constructor - dependencies are injected by the Razor Pages framework.
        /// </summary>
        public PackingProceduresModel(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<PackingProceduresModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// GET handler invoked by client-side code at "/Api/PackingProcedures".
        /// - Reads CERM configuration values from appsettings.
        /// - Authenticates against the OAuth endpoint to retrieve an access token.
        /// - Calls the configured PackingProceduresUrl and returns a JSON payload.
        /// - Returns guarded error responses for configuration, authentication, and fetch failures.
        /// </summary>
        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // Get credentials from configuration
                var oauthUrl = _configuration["Cerm:OAuthUrl"] ?? "https://brandmark-api.cerm.be/oauth/token";
                var packingProceduresUrl = _configuration["Cerm:PackingProceduresUrl"] ?? "";
                var username = _configuration["Cerm:Username"];
                var password = _configuration["Cerm:Password"];
                var clientId = _configuration["Cerm:ClientId"];
                var clientSecret = _configuration["Cerm:ClientSecret"];

                // Validate required configuration
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) ||
                    string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    _logger.LogError("CERM API credentials are missing from configuration");
                    return new JsonResult(new { error = "Server configuration error: CERM API credentials not configured" })
                    {
                        StatusCode = 500
                    };
                }

                if (string.IsNullOrEmpty(packingProceduresUrl))
                {
                    _logger.LogError("PackingProceduresUrl is not configured");
                    return new JsonResult(new { error = "Server configuration error: PackingProceduresUrl not configured" })
                    {
                        StatusCode = 500
                    };
                }

                // Step 1: Get OAuth token
                var tokenResult = await GetAccessTokenAsync(oauthUrl, username, password, clientId, clientSecret);
                if (tokenResult.Error != null)
                {
                    _logger.LogError("OAuth token retrieval failed: {Error}", tokenResult.Error);
                    return new JsonResult(new { error = $"Authentication failed: {tokenResult.Error}" })
                    {
                        StatusCode = 500
                    };
                }

                if (string.IsNullOrEmpty(tokenResult.AccessToken))
                {
                    _logger.LogError("OAuth token is empty");
                    return new JsonResult(new { error = "Authentication failed: no token received" })
                    {
                        StatusCode = 500
                    };
                }

                // Step 2: Call the packing procedures endpoint
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);

                var response = await httpClient.GetAsync(packingProceduresUrl);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("CERM PackingProcedures API call failed: {Status} {Body}", response.StatusCode, errorBody);
                    return new JsonResult(new { error = $"Failed to fetch packing procedures: {response.StatusCode}" })
                    {
                        StatusCode = (int)response.StatusCode
                    };
                }

                // Step 3: Return the raw JSON from the CERM API
                var jsonContent = await response.Content.ReadAsStringAsync();
                return Content(jsonContent, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in PackingProcedures API handler");
                return new JsonResult(new { error = "An unexpected error occurred while fetching packing procedures" })
                {
                    StatusCode = 500
                };
            }
        }

        /// <summary>
        /// Attempts to retrieve an OAuth access token from the CERM API.
        /// Tries two approaches: body-only and basic auth + body.
        /// </summary>
        private async Task<(string? AccessToken, string? Error)> GetAccessTokenAsync(
            string oauthUrl,
            string username,
            string password,
            string clientId,
            string clientSecret)
        {
            // Try body-only approach first
            var (token1, err1) = await TryTokenBodyOnlyAsync(oauthUrl, username, password, clientId, clientSecret);
            if (!string.IsNullOrWhiteSpace(token1))
            {
                return (token1, null);
            }

            // Try basic auth + body approach
            var (token2, err2) = await TryTokenBasicAuthPlusBodyAsync(oauthUrl, username, password, clientId, clientSecret);
            if (!string.IsNullOrWhiteSpace(token2))
            {
                return (token2, null);
            }

            return (null, $"Both OAuth attempts failed. BodyOnly: {err1}. BasicAuth+Body: {err2}");
        }

        private static async Task<(string? accessToken, string? errorMessage)> TryTokenBodyOnlyAsync(
            string oauthUrl,
            string username,
            string password,
            string clientId,
            string clientSecret)
        {
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var body = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "password" },
                    { "username", username },
                    { "password", password },
                    { "client_id", clientId },
                    { "client_secret", clientSecret }
                });

                var response = await httpClient.PostAsync(oauthUrl, body);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    return (null, $"Status {response.StatusCode}: {errorBody}");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonResponse);
                if (tokenData != null && tokenData.TryGetValue("access_token", out var token))
                {
                    return (token.GetString(), null);
                }

                return (null, "Token not found in response");
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }

        private static async Task<(string? accessToken, string? errorMessage)> TryTokenBasicAuthPlusBodyAsync(
            string oauthUrl,
            string username,
            string password,
            string clientId,
            string clientSecret)
        {
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

                // Set Basic Auth header
                var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);

                var body = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "password" },
                    { "username", username },
                    { "password", password }
                });

                var response = await httpClient.PostAsync(oauthUrl, body);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    return (null, $"Status {response.StatusCode}: {errorBody}");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonResponse);
                if (tokenData != null && tokenData.TryGetValue("access_token", out var token))
                {
                    return (token.GetString(), null);
                }

                return (null, "Token not found in response");
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }
    }
}
