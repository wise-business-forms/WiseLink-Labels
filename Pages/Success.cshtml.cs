using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WiseLabels.Models;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace WiseLabels.Pages
{
    public class SuccessModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SuccessModel> _logger;

        public SuccessModel(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<SuccessModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public string? QuoteId { get; set; }
        public bool ApiSuccess { get; set; }
        public QuoteRequest? QuoteRequest { get; set; }
        public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;

        public void OnGet()
        {
            // Get quote ID and API success status from TempData
            QuoteId = TempData["QuoteId"]?.ToString();
            
            if (bool.TryParse(TempData["ApiSuccess"]?.ToString(), out var apiSuccess))
            {
                ApiSuccess = apiSuccess;
            }

            // Get quote request data for PDF generation
            if (TempData.TryGetValue("QuoteRequest", out var quoteData))
            {
                try
                {
                    QuoteRequest = JsonSerializer.Deserialize<QuoteRequest>(quoteData.ToString() ?? "{}");
                    if (QuoteRequest != null && QuoteRequest.CreatedDate != default)
                    {
                        SubmittedDate = QuoteRequest.CreatedDate;
                    }
                }
                catch
                {
                    QuoteRequest = null;
                }
            }
        }

        // Downloads the official CERM quote-letter PDF for a calculation.
        // Usage: /Success?handler=Pdf&calculationId=113045
        public async Task<IActionResult> OnGetPdfAsync(string? calculationId)
        {
            if (string.IsNullOrWhiteSpace(calculationId))
            {
                return BadRequest("calculationId is required");
            }

            var oauthUrl = _configuration["Cerm:OAuthUrl"] ?? "https://brandmark-api.cerm.be/oauth/token";
            var pdfTemplate = _configuration["Cerm:QuoteLetterPdfUrlTemplate"];
            var username = _configuration["Cerm:Username"];
            var password = _configuration["Cerm:Password"];
            var clientId = _configuration["Cerm:ClientId"];
            var clientSecret = _configuration["Cerm:ClientSecret"];

            if (string.IsNullOrWhiteSpace(pdfTemplate))
            {
                return StatusCode(500, "Server configuration error: Cerm:QuoteLetterPdfUrlTemplate not configured");
            }

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                return StatusCode(500, "Server configuration error: CERM API credentials not configured");
            }

            var pdfUrl = pdfTemplate.Replace("{calculationId}", Uri.EscapeDataString(calculationId));

            try
            {
                var (accessToken, authError) = await GetAccessTokenAsync(oauthUrl, username, password, clientId, clientSecret);
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    return StatusCode(401, authError ?? "Failed to authenticate with CERM API");
                }

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(60);
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                httpClient.DefaultRequestHeaders.Add("Accept", "application/pdf");

                using var resp = await httpClient.GetAsync(pdfUrl);
                if (!resp.IsSuccessStatusCode)
                {
                    var content = await resp.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to fetch quote PDF: {Status} - {Body}", resp.StatusCode, content);

                    if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return NotFound($"Quote PDF not found for calculationId={calculationId}");
                    }

                    return StatusCode((int)resp.StatusCode, $"Failed to fetch quote PDF: {resp.StatusCode}");
                }

                var bytes = await resp.Content.ReadAsByteArrayAsync();
                var filename = $"quote-{calculationId}-{DateTime.UtcNow:yyyy-MM-dd}.pdf";
                return File(bytes, "application/pdf", filename);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading quote PDF for calculationId={CalculationId}", calculationId);
                return StatusCode(500, "Error downloading quote PDF");
            }
        }

        private async Task<(string? accessToken, string? errorMessage)> GetAccessTokenAsync(
            string oauthUrl,
            string username,
            string password,
            string clientId,
            string clientSecret)
        {
            // Same overall approach as other CERM proxy endpoints: try body-only first,
            // then retry with Basic Auth header variations for compatibility.
            oauthUrl = oauthUrl.TrimEnd('/');

            var (token1, err1) = await TryTokenBodyOnly(oauthUrl, username, password, clientId, clientSecret);
            if (!string.IsNullOrWhiteSpace(token1)) return (token1, null);

            var (token2, err2) = await TryTokenBasicAuthPlusBody(oauthUrl, username, password, clientId, clientSecret);
            if (!string.IsNullOrWhiteSpace(token2)) return (token2, null);

            return (null, $"OAuth authentication failed. BodyOnly: {err1}. BasicAuth+Body: {err2}");
        }

        private async Task<(string? accessToken, string? errorMessage)> TryTokenBodyOnly(
            string oauthUrl,
            string username,
            string password,
            string clientId,
            string clientSecret)
        {
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

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

                var tokenData = JsonSerializer.Deserialize<JsonElement>(json);
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

        private async Task<(string? accessToken, string? errorMessage)> TryTokenBasicAuthPlusBody(
            string oauthUrl,
            string username,
            string password,
            string clientId,
            string clientSecret)
        {
            try
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

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

                var tokenData = JsonSerializer.Deserialize<JsonElement>(json);
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

