using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WiseLabels.Models;
using System.Text.Json;
using System.Net;
using CERM.DataAccess.Repositories.PriceList;

namespace WiseLabels.Pages
{
    public class ConfirmModel : PageModel
    {
        private readonly ILogger<ConfirmModel> _logger;
        private readonly WiseLabels.Services.IQuoteService _quoteService;
        private readonly WiseLabels.Services.IEmailService _emailService;
        private readonly IPriceListItemRepository _priceListRepo;

        public ConfirmModel(
            ILogger<ConfirmModel> logger, 
            WiseLabels.Services.IQuoteService quoteService, 
            WiseLabels.Services.IEmailService emailService,
            IPriceListItemRepository priceListRepo)
        {
            _logger = logger;
            _quoteService = quoteService;
            _emailService = emailService;
            _priceListRepo = priceListRepo;
        }

        [BindProperty]
        public QuoteRequest QuoteRequest { get; set; } = new();

        public string? ApiPayloadJson { get; set; }
        public string? FormValuesJson { get; set; }
        public List<QuotePriceBreakdown> PriceBreakdown { get; } = new();
        public decimal? CustomDiePrice { get; set; }
        public string? CustomDieUnit { get; set; }

        public async Task OnGetAsync()
        {
            // Get quote data from TempData (passed from form submission)
            // Store it in the property for display, but also keep it in TempData for Edit redirect
            if (TempData.TryGetValue("QuoteRequest", out var quoteData))
            {
                try
                {
                    var quoteJson = quoteData.ToString() ?? "{}";
                    QuoteRequest = JsonSerializer.Deserialize<QuoteRequest>(quoteJson) ?? new QuoteRequest();
                    // Keep the original JSON in TempData so it's available for Edit redirect
                    TempData["QuoteRequest"] = quoteJson;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing quote request from TempData");
                    QuoteRequest = new QuoteRequest();
                }
            }

            LoadPriceBreakdown();
            await LoadCustomDiePriceAsync();
        }

        private async Task LoadCustomDiePriceAsync()
        {
            // If custom die is required, fetch the price from CERM price list
            if (QuoteRequest?.IsCustomDie == true)
            {
                _logger.LogInformation(
                    "Custom die detected. IsCustomDie={IsCustomDie}, DieSizeInfo={DieSizeInfo}",
                    QuoteRequest.IsCustomDie,
                    QuoteRequest.DieSizeInfo ?? "NULL");

                try
                {
                    // Line item 100001 is for custom die fabrication (standard price for all customers)
                    var priceListItem = await _priceListRepo.GetByItemRefAsync("100001");

                    if (priceListItem != null)
                    {
                        CustomDiePrice = (decimal)priceListItem.PriceExcludingTax;
                        CustomDieUnit = !string.IsNullOrWhiteSpace(priceListItem.QuantityDescription1) 
                            ? priceListItem.QuantityDescription1.Trim() 
                            : "each";

                        _logger.LogInformation(
                            "Custom die price loaded from item 100001: {Price} {Unit} (from prijs_bm={PriceRaw}, omsaant1={Unit})", 
                            CustomDiePrice,
                            CustomDieUnit,
                            priceListItem.PriceExcludingTax,
                            priceListItem.QuantityDescription1);
                    }
                    else
                    {
                        _logger.LogWarning("Custom die price item 100001 not found in stdfpl__ table");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading custom die price for item 100001");
                }
            }
            else
            {
                if (QuoteRequest?.IsCustomDie != true)
                {
                    _logger.LogDebug("Not a custom die - skipping price lookup");
                }
            }
        }

        public async Task<IActionResult> OnPostConfirmAsync()
        {
            // Get quote data from TempData
            if (!TempData.TryGetValue("QuoteRequest", out var quoteData))
            {
            return Redirect("~/GetQuote");
            }

            QuoteRequest? quote = null;

            try
            {
                quote = JsonSerializer.Deserialize<QuoteRequest>(quoteData.ToString() ?? "{}");
                if (quote == null)
                {
                    return RedirectToPage("/Index");
                }

                // Load the quote request into the property so LoadCustomDiePriceAsync can access it
                QuoteRequest = quote;

                // Load custom die pricing before storing and redirecting
                await LoadCustomDiePriceAsync();

                // Store in database
                var quoteId = await _quoteService.StoreQuoteAsync(quote);

                // Transform to API payload format
                var apiPayload = _quoteService.TransformToApiPayload(quote);

                // Serialize the API payload for display
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };
                ApiPayloadJson = JsonSerializer.Serialize(apiPayload, jsonOptions);

                // Serialize the form values for display
                var formJsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                FormValuesJson = JsonSerializer.Serialize(quote, formJsonOptions);

                // Submit to CERM API
                var (apiSuccess, cermCalculationId, cermEstimateId, cermErrorMessage, cermResponseJson) = await _quoteService.SubmitToCermApiAsync(quote);

                var estimateIdForContact = cermEstimateId ?? cermCalculationId;
                if (!string.IsNullOrWhiteSpace(estimateIdForContact))
                {
                    quote.EstimateId = estimateIdForContact;
                }

                TempData["QuoteId"] = quoteId;
                TempData["ApiSuccess"] = apiSuccess.ToString();
                TempData["QuoteRequest"] = JsonSerializer.Serialize(quote);
                if (!string.IsNullOrWhiteSpace(cermCalculationId))
                    TempData["CermCalculationId"] = cermCalculationId;
                if (!string.IsNullOrWhiteSpace(cermEstimateId))
                    TempData["CermEstimateId"] = cermEstimateId;
                if (!string.IsNullOrWhiteSpace(cermErrorMessage))
                    TempData["CermErrorMessage"] = cermErrorMessage;
                if (!string.IsNullOrWhiteSpace(cermResponseJson))
                    TempData["CermApiResponse"] = cermResponseJson;

                // Pass custom die information to Success page
                if (CustomDiePrice.HasValue)
                    TempData["CustomDiePrice"] = CustomDiePrice.Value.ToString();
                if (!string.IsNullOrWhiteSpace(CustomDieUnit))
                    TempData["CustomDieUnit"] = CustomDieUnit;

                if (string.IsNullOrWhiteSpace(cermCalculationId) && string.IsNullOrWhiteSpace(cermEstimateId))
                {
                    var subject = "CERM quote submission missing reference number";
                    var body = $@"<p>No quote number was returned for a submission.</p>
                        <ul>
                            <li><strong>QuoteId:</strong> {quoteId}</li>
                            <li><strong>ApiSuccess:</strong> {apiSuccess}</li>
                            <li><strong>Description:</strong> {WebUtility.HtmlEncode(quote.Description ?? string.Empty)}</li>
                            <li><strong>Error:</strong> {WebUtility.HtmlEncode(cermErrorMessage ?? "(none)")}</li>
                        </ul>";
                    try
                    {
                        await _emailService.SendCustomEmailAsync("pmenefee@wbf.com", subject, body);
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx, "Failed sending debug email when quote number was missing");
                    }
                }

                // Update contact info in CERM database when we have the estimate ID
                if (!string.IsNullOrWhiteSpace(estimateIdForContact))
                {
                    var contactUpdated = await _quoteService.UpdateContactInfoAsync(
                        estimateIdForContact,
                        quote.Name ?? "",
                        quote.Email ?? "",
                        quote.Phone ?? ""
                    );
                    if (!contactUpdated)
                    {
                        _logger.LogWarning("Failed to update contact info in CERM for estimate {EstimateId}", estimateIdForContact);
                    }
                }

                // Send confirmation email to customer (use estimate ID as quote reference when available)
                var priceBreakdownSnapshot = BuildPriceBreakdownSnapshot(quote);
                TempData["PriceBreakdown"] = JsonSerializer.Serialize(priceBreakdownSnapshot);
                var quoteRefForEmail = estimateIdForContact ?? quoteId;
                if (!string.IsNullOrWhiteSpace(quote.Email))
                {
                    try
                    {
                        var emailSent = await _emailService.SendQuoteConfirmationAsync(
                            quote.Email,
                            quoteRefForEmail,
                            quote.Name ?? "",
                            quote,
                            priceBreakdownSnapshot
                        );
                        TempData["EmailSent"] = emailSent.ToString();
                        if (emailSent)
                        {
                            _logger.LogInformation("Quote confirmation email sent to {Email} for quote {QuoteId}", quote.Email, quoteId);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to send confirmation email to {Email} for quote {QuoteId}", quote.Email, quoteId);
                        }
                    }
                    catch (Exception emailEx)
                    {
                        _logger.LogError(emailEx, "Error sending confirmation email to {Email} for quote {QuoteId}", quote.Email, quoteId);
                        TempData["EmailSent"] = "False";
                    }
                }

                // Redirect to Success page
                return RedirectToPage("/Success");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing quote confirmation");
                // Preserve quote data in case of error
                TempData.Keep("QuoteRequest");
                ModelState.AddModelError("", "An error occurred while processing your quote. Please try again.");
                QuoteRequest = quote ?? new QuoteRequest();
                LoadPriceBreakdown();
                return Page();
            }
        }

        public IActionResult OnPostEdit()
        {
            // When Edit button is clicked, retrieve quote data from TempData
            // (it was preserved in OnGet) and repopulate it for the redirect
            if (TempData.TryGetValue("QuoteRequest", out var quoteData))
            {
                // Repopulate TempData to ensure it survives the redirect to Index
                var quoteJson = quoteData.ToString() ?? "{}";
                TempData["QuoteRequest"] = quoteJson;
                TempData.Keep("QuoteRequest");
                _logger.LogInformation("Quote data preserved in TempData for edit redirect");
            }
            else
            {
                // Fallback: try to serialize from QuoteRequest property if it's populated
                if (QuoteRequest != null && !string.IsNullOrEmpty(QuoteRequest.Description))
                {
                    TempData["QuoteRequest"] = JsonSerializer.Serialize(QuoteRequest);
                    TempData.Keep("QuoteRequest");
                    _logger.LogInformation("Quote data serialized from QuoteRequest property for edit redirect");
                }
                else
                {
                    _logger.LogWarning("No quote data found to preserve for edit - TempData is empty and QuoteRequest is null/empty");
                }
            }
            
            return RedirectToPage("/Index");
        }

        private void LoadPriceBreakdown()
        {
            PriceBreakdown.Clear();
            if (QuoteRequest?.PriceBreakdown is { Count: > 0 } cached)
            {
                PriceBreakdown.AddRange(cached);
                return;
            }

            if (!string.IsNullOrWhiteSpace(QuoteRequest?.QuickQuoteResponseJson))
            {
                PriceBreakdown.AddRange(QuotePriceBreakdownParser.Parse(QuoteRequest.QuickQuoteResponseJson));
            }
        }

        private static IReadOnlyList<QuotePriceBreakdown> BuildPriceBreakdownSnapshot(QuoteRequest quote)
        {
            if (quote.PriceBreakdown is { Count: > 0 } cached)
            {
                return cached;
            }

            if (!string.IsNullOrWhiteSpace(quote.QuickQuoteResponseJson))
            {
                return QuotePriceBreakdownParser.Parse(quote.QuickQuoteResponseJson);
            }

            return Array.Empty<QuotePriceBreakdown>();
        }

    }
}

