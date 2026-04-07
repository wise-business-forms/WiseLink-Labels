using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;

namespace WiseLabels.Pages.Api
{
    /// <summary>
    /// Proxy endpoint to fetch customer calculations from CERM API with OAuth authentication
    /// </summary>
    public class CermCustomerCalculationsModel : PageModel
    {
        private readonly ILogger<CermCustomerCalculationsModel> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly Services.ICermAuthService _cermAuthService;

        public CermCustomerCalculationsModel(
            ILogger<CermCustomerCalculationsModel> logger,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            Services.ICermAuthService cermAuthService)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _cermAuthService = cermAuthService;
        }

        public async Task<IActionResult> OnGetAsync(string customerId)
        {
            if (string.IsNullOrWhiteSpace(customerId))
            {
                return BadRequest(new { error = "customerId parameter is required" });
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

                // Build CERM API URL for customer calculations
                var baseUrl = _configuration["Cerm:BaseUrl"] ?? "https://brandmark-api.cerm.be/";
                var url = $"{baseUrl.TrimEnd('/')}/quote-api/v1/customers/{customerId}/calculations";

                _logger.LogInformation("Fetching calculations for customer {CustomerId} from CERM API: {Url}", customerId, url);

                // Call CERM API with authentication
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await httpClient.GetAsync(url);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "CERM API returned {StatusCode} for customer {CustomerId}: {Response}",
                        response.StatusCode,
                        customerId,
                        responseBody);

                    return StatusCode((int)response.StatusCode, new 
                    { 
                        error = $"CERM API error: {response.StatusCode}",
                        details = responseBody
                    });
                }

                _logger.LogInformation("Successfully fetched calculations for customer {CustomerId}", customerId);

                // Return the raw JSON response from CERM
                return Content(responseBody, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching calculations for customer {CustomerId} from CERM API", customerId);
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }
    }
}
