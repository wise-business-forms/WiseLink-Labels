using System.Text;

namespace WiseLabels.Services
{
    public class CermAuthService : ICermAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<CermAuthService> _logger;

        public CermAuthService(IConfiguration configuration, ILogger<CermAuthService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string?> GetAccessTokenAsync()
        {
            var oauthUrl = _configuration["Cerm:OAuthUrl"] ?? "https://brandmark-api.cerm.be/oauth/token";
            var username = _configuration["Cerm:Username"];
            var password = _configuration["Cerm:Password"];
            var clientId = _configuration["Cerm:ClientId"];
            var clientSecret = _configuration["Cerm:ClientSecret"];

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                _logger.LogError("CERM API credentials not configured (Username, Password, ClientId, ClientSecret)");
                return null;
            }

            var (token1, err1) = await TryTokenBodyOnlyAsync(oauthUrl, username, password, clientId, clientSecret);
            if (!string.IsNullOrWhiteSpace(token1)) return token1;

            var (token2, err2) = await TryTokenBasicAuthPlusBodyAsync(oauthUrl, username, password, clientId, clientSecret);
            if (!string.IsNullOrWhiteSpace(token2)) return token2;

            _logger.LogError("CERM OAuth failed. BodyOnly: {Err1}. BasicAuth+Body: {Err2}", err1, err2);
            return null;
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
                oauthUrl = oauthUrl.TrimEnd('/');

                var formData = new List<KeyValuePair<string, string>>
                {
                    new("grant_type", "password"),
                    new("username", username),
                    new("password", password),
                    new("client_id", clientId),
                    new("client_secret", clientSecret)
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, oauthUrl)
                {
                    Content = new FormUrlEncodedContent(formData)
                };
                request.Headers.Add("Accept", "application/json");

                using var response = await httpClient.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    return (null, $"{response.StatusCode} - {json}");
                }

                var tokenData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
                if (tokenData.TryGetProperty("access_token", out var accessToken))
                {
                    return (accessToken.GetString(), null);
                }

                return (null, "access_token not found in response");
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
                oauthUrl = oauthUrl.TrimEnd('/');

                var formData = new List<KeyValuePair<string, string>>
                {
                    new("grant_type", "password"),
                    new("username", username),
                    new("password", password),
                    new("client_id", clientId),
                    new("client_secret", clientSecret)
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, oauthUrl)
                {
                    Content = new FormUrlEncodedContent(formData)
                };
                request.Headers.Add("Accept", "application/json");

                var clientCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", clientCredentials);

                using var response = await httpClient.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    return (null, $"{response.StatusCode} - {json}");
                }

                var tokenData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
                if (tokenData.TryGetProperty("access_token", out var accessToken))
                {
                    return (accessToken.GetString(), null);
                }

                return (null, "access_token not found in response");
            }
            catch (Exception ex)
            {
                return (null, ex.Message);
            }
        }
    }
}
