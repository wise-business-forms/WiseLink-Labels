using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace WiseLabels.Services
{
    public class CermAuthService : ICermAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<CermAuthService> _logger;
        private readonly IMemoryCache _cache;
        private static readonly SemaphoreSlim _tokenLock = new(1, 1);
        private const string CacheKey = "cerm_access_token";
        private const string FailureCacheKey = "cerm_token_failure";

        public CermAuthService(IConfiguration configuration, ILogger<CermAuthService> logger, IMemoryCache cache)
        {
            _configuration = configuration;
            _logger = logger;
            _cache = cache;
        }

        public async Task<string?> GetAccessTokenAsync()
        {
            if (_cache.TryGetValue(CacheKey, out string? cachedToken) && !string.IsNullOrEmpty(cachedToken))
                return cachedToken;

            // If auth recently failed, bail out immediately instead of queuing another OAuth attempt.
            if (_cache.TryGetValue(FailureCacheKey, out _))
                return null;

            await _tokenLock.WaitAsync();
            try
            {
                // Double-check after acquiring the lock in case another request just refreshed it.
                if (_cache.TryGetValue(CacheKey, out cachedToken) && !string.IsNullOrEmpty(cachedToken))
                    return cachedToken;

                if (_cache.TryGetValue(FailureCacheKey, out _))
                    return null;

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

                var (token1, err1, expiresIn1) = await TryTokenBodyOnlyAsync(oauthUrl, username, password, clientId, clientSecret);
                if (!string.IsNullOrWhiteSpace(token1))
                {
                    CacheToken(token1, expiresIn1);
                    return token1;
                }

                var (token2, err2, expiresIn2) = await TryTokenBasicAuthPlusBodyAsync(oauthUrl, username, password, clientId, clientSecret);
                if (!string.IsNullOrWhiteSpace(token2))
                {
                    CacheToken(token2, expiresIn2);
                    return token2;
                }

                // Cache the failure for 15 seconds so waiting requests fail fast instead of re-queuing.
                _cache.Set(FailureCacheKey, true, TimeSpan.FromSeconds(15));
                _logger.LogError("CERM OAuth failed. BodyOnly: {Err1}. BasicAuth+Body: {Err2}", err1, err2);
                return null;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        private void CacheToken(string token, int expiresIn)
        {
            // Subtract a 60-second safety buffer; use 5-minute default when expires_in is absent.
            var cacheSeconds = Math.Max(expiresIn > 0 ? expiresIn - 60 : 300, 60);
            _cache.Set(CacheKey, token, TimeSpan.FromSeconds(cacheSeconds));
            _logger.LogInformation("CERM access token cached for {Seconds}s", cacheSeconds);
        }

        private static async Task<(string? accessToken, string? errorMessage, int expiresIn)> TryTokenBodyOnlyAsync(
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

                var bodyString = $"grant_type=password&username={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}&client_id={Uri.EscapeDataString(clientId)}&client_secret={Uri.EscapeDataString(clientSecret)}";
                var requestContent = new StringContent(bodyString);
                requestContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");

                using var request = new HttpRequestMessage(HttpMethod.Post, oauthUrl)
                {
                    Content = requestContent
                };
                request.Headers.Add("Accept", "application/json");

                using var response = await httpClient.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    return (null, $"{response.StatusCode} - {json}", 0);
                }

                var tokenData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
                if (tokenData.TryGetProperty("access_token", out var accessToken))
                {
                    var expiresIn = tokenData.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var expVal) ? expVal : 0;
                    return (accessToken.GetString(), null, expiresIn);
                }

                return (null, "access_token not found in response", 0);
            }
            catch (Exception ex)
            {
                return (null, ex.Message, 0);
            }
        }

        private static async Task<(string? accessToken, string? errorMessage, int expiresIn)> TryTokenBasicAuthPlusBodyAsync(
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

                var bodyString = $"grant_type=password&username={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}&client_id={Uri.EscapeDataString(clientId)}&client_secret={Uri.EscapeDataString(clientSecret)}";
                var requestContent = new StringContent(bodyString);
                requestContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");

                using var request = new HttpRequestMessage(HttpMethod.Post, oauthUrl)
                {
                    Content = requestContent
                };
                request.Headers.Add("Accept", "application/json");

                var clientCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", clientCredentials);

                using var response = await httpClient.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    return (null, $"{response.StatusCode} - {json}", 0);
                }

                var tokenData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
                if (tokenData.TryGetProperty("access_token", out var accessToken))
                {
                    var expiresIn = tokenData.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var expVal) ? expVal : 0;
                    return (accessToken.GetString(), null, expiresIn);
                }

                return (null, "access_token not found in response", 0);
            }
            catch (Exception ex)
            {
                return (null, ex.Message, 0);
            }
        }
    }
}
