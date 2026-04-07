using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WiseLabels.Models;
using WiseLabels.Services;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace WiseLabels.Pages
{
    public class SuccessModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SuccessModel> _logger;
        private readonly ICermAuthService _cermAuthService;

        public SuccessModel(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<SuccessModel> logger, ICermAuthService cermAuthService)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _cermAuthService = cermAuthService;
        }

        public string? QuoteId { get; set; }
        /// <summary>CERM calculation ID (Data.Id) from submission.</summary>
        public string? CermCalculationId { get; set; }
        /// <summary>CERM estimate ID (Data.EstimateId) from submission; PDF endpoint uses this.</summary>
        public string? CermEstimateId { get; set; }
        /// <summary>Quote ID displayed to user: CERM estimate ID when available, otherwise calculation ID or internal QuoteId.</summary>
        public string? DisplayQuoteId => CermEstimateId ?? CermCalculationId ?? QuoteId;
        /// <summary>Quote ID to show in the card: only the CERM ID (estimate or calculation), not the internal GUID.</summary>
        public string? DisplayQuoteIdForCard => !string.IsNullOrEmpty(CermEstimateId) ? CermEstimateId : CermCalculationId;
        public bool ApiSuccess { get; set; }
        /// <summary>Error message from CERM API when submission failed (for user display and debugging).</summary>
        public string? CermErrorMessage { get; set; }
        /// <summary>Raw JSON returned from the CERM API submission.</summary>
        public string? ApiResponseJson { get; set; }
        /// <summary>JSON payload sent to CERM API (for debugging).</summary>
        public string? ApiRequestJson { get; set; }
        /// <summary>Parsed pricing breakdown from the quick-quote response.</summary>
        public List<QuotePriceBreakdown> PriceBreakdown { get; } = new();
        public bool EmailSent { get; set; }
        public QuoteRequest? QuoteRequest { get; set; }
        public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;
        public decimal? CustomDiePrice { get; set; }
        public string? CustomDieUnit { get; set; }
        public decimal? ColorChangesPrice { get; set; }
        public string? ColorChangesUnit { get; set; }
        public int? ColorChangesQty { get; set; }
        public decimal? DigitalVersionChangesPrice { get; set; }
        public string? DigitalVersionChangesUnit { get; set; }
        public int? DigitalVersionChangesQty { get; set; }
        public decimal? PressProofPrice { get; set; }
        public string? PressProofUnit { get; set; }
        public int? PressProofQty { get; set; }
        public decimal? SpotColorPlateChangePrice { get; set; }
        public string? SpotColorPlateChangeUnit { get; set; }
        public int? SpotColorPlateChangeQty { get; set; }
        /// <summary>Submitted date/time in Eastern Time for display.</summary>
        public DateTime SubmittedDateEastern =>
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(SubmittedDate, DateTimeKind.Utc),
                GetEasternTimeZone());

        private static TimeZoneInfo GetEasternTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            }
            catch
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
            }
        }

        public void OnGet()
        {
            QuoteId = TempData["QuoteId"]?.ToString();
            CermCalculationId = TempData["CermCalculationId"]?.ToString();
            CermEstimateId = TempData["CermEstimateId"]?.ToString();
            
            if (bool.TryParse(TempData["ApiSuccess"]?.ToString(), out var apiSuccess))
            {
                ApiSuccess = apiSuccess;
            }

            CermErrorMessage = TempData["CermErrorMessage"]?.ToString();
            ApiRequestJson = TempData["CermApiRequest"]?.ToString();

            if (bool.TryParse(TempData["EmailSent"]?.ToString(), out var emailSent))
            {
                EmailSent = emailSent;
            }

            // Load custom die price and unit
            if (decimal.TryParse(TempData["CustomDiePrice"]?.ToString(), out var customDiePrice))
            {
                CustomDiePrice = customDiePrice;
            }

            CustomDieUnit = TempData["CustomDieUnit"]?.ToString();

            // Load color changes price and quantity
            if (decimal.TryParse(TempData["ColorChangesPrice"]?.ToString(), out var colorChangesPrice))
            {
                ColorChangesPrice = colorChangesPrice;
            }
            ColorChangesUnit = TempData["ColorChangesUnit"]?.ToString();
            if (int.TryParse(TempData["ColorChangesQty"]?.ToString(), out var colorChangesQty))
            {
                ColorChangesQty = colorChangesQty;
            }

            // Load digital version changes price and quantity
            if (decimal.TryParse(TempData["DigitalVersionChangesPrice"]?.ToString(), out var digitalVersionChangesPrice))
            {
                DigitalVersionChangesPrice = digitalVersionChangesPrice;
            }
            DigitalVersionChangesUnit = TempData["DigitalVersionChangesUnit"]?.ToString();
            if (int.TryParse(TempData["DigitalVersionChangesQty"]?.ToString(), out var digitalVersionChangesQty))
            {
                DigitalVersionChangesQty = digitalVersionChangesQty;
            }

            // Load press proof price and quantity
            if (decimal.TryParse(TempData["PressProofPrice"]?.ToString(), out var pressProofPrice))
            {
                PressProofPrice = pressProofPrice;
            }
            PressProofUnit = TempData["PressProofUnit"]?.ToString();
            if (int.TryParse(TempData["PressProofQty"]?.ToString(), out var pressProofQty))
            {
                PressProofQty = pressProofQty;
            }

            // Load spot color plate change price and quantity
            if (decimal.TryParse(TempData["SpotColorPlateChangePrice"]?.ToString(), out var spotColorPlateChangePrice))
            {
                SpotColorPlateChangePrice = spotColorPlateChangePrice;
            }
            SpotColorPlateChangeUnit = TempData["SpotColorPlateChangeUnit"]?.ToString();
            if (int.TryParse(TempData["SpotColorPlateChangeQty"]?.ToString(), out var spotColorPlateChangeQty))
            {
                SpotColorPlateChangeQty = spotColorPlateChangeQty;
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

            if (TempData.TryGetValue("PriceBreakdown", out var priceBreakdownJsonObj))
            {
                try
                {
                    var priceBreakdownJson = priceBreakdownJsonObj?.ToString();
                    if (!string.IsNullOrWhiteSpace(priceBreakdownJson))
                    {
                        var parsed = JsonSerializer.Deserialize<List<QuotePriceBreakdown>>(priceBreakdownJson);
                        if (parsed?.Count > 0)
                        {
                            PriceBreakdown.Clear();
                            PriceBreakdown.AddRange(parsed);
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize price breakdown data from TempData");
                }
            }

            if (TempData.TryGetValue("CermApiResponse", out var apiResponseJson))
            {
                ApiResponseJson = apiResponseJson?.ToString();
                var cermBreakdown = QuotePriceBreakdownParser.Parse(ApiResponseJson);
                if (cermBreakdown.Count > 0)
                {
                    PriceBreakdown.Clear();
                    PriceBreakdown.AddRange(cermBreakdown);
                }
            }
        }

        // Downloads the official CERM quote-letter PDF for a calculation.
        // Usage: /Success?handler=Pdf&calculationId=113045
        // calculationId must be the CERM calculation ID (e.g. 113045), not our internal quote ID (GUID).
        public async Task<IActionResult> OnGetPdfAsync(string? calculationId)
        {
            if (string.IsNullOrWhiteSpace(calculationId))
            {
                return BadRequest("calculationId is required");
            }

            // Optional override for testing (e.g. Cerm:CalculationIdOverride = "113045" in appsettings).
            var effectiveId = _configuration["Cerm:CalculationIdOverride"]?.Trim();
            if (string.IsNullOrEmpty(effectiveId))
                effectiveId = calculationId;

            // CERM expects its calculation ID (numeric). We use QuoteId from StoreQuote (a GUID) when
            // no CERM submission has been done. Fail fast with a clear message instead of CERM 400.
            // Skip when override is set (we're explicitly testing with a known ID).
            if (effectiveId == calculationId && IsInternalQuoteId(calculationId))
            {
                return BadRequest(
                    "PDF download requires the CERM calculation ID. This quote was not submitted to CERM (or only an internal reference exists). " +
                    "Submit the quote to CERM and use the returned calculation ID to download the PDF.");
            }

            var pdfTemplate = _configuration["Cerm:QuoteLetterPdfUrlTemplate"];

            if (string.IsNullOrWhiteSpace(pdfTemplate))
            {
                return StatusCode(500, "Server configuration error: Cerm:QuoteLetterPdfUrlTemplate not configured");
            }

            var pdfUrl = pdfTemplate.Replace("{calculationId}", Uri.EscapeDataString(effectiveId));

            try
            {
                var accessToken = await _cermAuthService.GetAccessTokenAsync();
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    return StatusCode(401, "Failed to authenticate with CERM API");
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
                    _logger.LogError("Failed to fetch quote PDF: {Status} - {Body}. Request: {PdfUrl}", resp.StatusCode, content, pdfUrl);

                    if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return NotFound($"Quote PDF not found for calculationId={effectiveId}");
                    }

                    var msg = $"Failed to fetch quote PDF: {resp.StatusCode}";
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        var truncated = content.Length > 500 ? content[..500] + "…" : content;
                        var oneline = Regex.Replace(truncated, @"\s+", " ").Trim();
                        msg += ". " + oneline;
                    }
                    return StatusCode((int)resp.StatusCode, msg);
                }

                var bytes = await resp.Content.ReadAsByteArrayAsync();
                var filename = $"quote-{effectiveId}-{DateTime.UtcNow:yyyy-MM-dd}.pdf";
                return File(bytes, "application/pdf", filename);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading quote PDF for calculationId={CalculationId}", effectiveId);
                return StatusCode(500, "Error downloading quote PDF");
            }
        }

        private static bool IsInternalQuoteId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            // Our StoreQuote returns Guid.NewGuid().ToString(); CERM uses numeric calculation IDs.
            return Regex.IsMatch(id.Trim(), @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$");
        }
    }

}

