using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;

namespace WiseLabels.Pages.Api
{
    /// <summary>
    /// Proxy endpoint to fetch calculation details from CERM API with OAuth authentication
    /// </summary>
    public class CermCalculationModel : PageModel
    {
        private readonly ILogger<CermCalculationModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly Services.ICermAuthService _cermAuthService;

        public CermCalculationModel(
            ILogger<CermCalculationModel> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            Services.ICermAuthService cermAuthService)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _cermAuthService = cermAuthService;
        }

        public async Task<IActionResult> OnGetAsync(string calculationId)
        {
            if (string.IsNullOrWhiteSpace(calculationId))
            {
                return BadRequest(new { error = "calculationId parameter is required" });
            }

            try
            {
                // Get OAuth token
                var accessToken = await _cermAuthService.GetAccessTokenAsync();
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    _logger.LogError("Failed to get CERM OAuth token");
                    return StatusCode(500, new { error = "Authentication failed" });
                }

                // Build CERM API URL
                var baseUrl = _configuration["Cerm:CalulationsUrl"] ?? "https://brandmark-api.cerm.be/quote-api/v1/calculations";
                var url = $"{baseUrl.TrimEnd('/')}/{calculationId}";

                _logger.LogInformation("Fetching calculation {CalculationId} from CERM API: {Url}", calculationId, url);

                // Call CERM API with authentication
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await httpClient.GetAsync(url);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "CERM API returned {StatusCode} for calculation {CalculationId}: {Response}",
                        response.StatusCode,
                        calculationId,
                        responseBody);

                    return StatusCode((int)response.StatusCode, new 
                    { 
                        error = $"CERM API error: {response.StatusCode}",
                        details = responseBody
                    });
                }

                _logger.LogInformation("Successfully fetched calculation {CalculationId}", calculationId);

                // Return the raw JSON response from CERM
                return Content(responseBody, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching calculation {CalculationId} from CERM API", calculationId);
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }
    }
}
