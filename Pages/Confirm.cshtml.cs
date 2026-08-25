using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WiseLabels.Models;
using System.Text.Json;
using System.Net;
using Microsoft.Data.SqlClient;

namespace WiseLabels.Pages
{
    public class ConfirmModel : PageModel
    {
        private readonly ILogger<ConfirmModel> _logger;
        private readonly WiseLabels.Services.IQuoteService _quoteService;
        private readonly WiseLabels.Services.IEmailService _emailService;
        private readonly WiseLabels.Services.ICustomerContactService _customerContactService;
        private readonly WiseLabels.Services.ILineItemCatalogService _lineItemCatalog;
        private readonly IConfiguration _configuration;

        public ConfirmModel(
            ILogger<ConfirmModel> logger, 
            WiseLabels.Services.IQuoteService quoteService, 
            WiseLabels.Services.IEmailService emailService,
            WiseLabels.Services.ICustomerContactService customerContactService,
            WiseLabels.Services.ILineItemCatalogService lineItemCatalog,
            IConfiguration configuration)
        {
            _logger = logger;
            _quoteService = quoteService;
            _emailService = emailService;
            _customerContactService = customerContactService;
            _lineItemCatalog = lineItemCatalog;
            _configuration = configuration;
        }

        [BindProperty]
        public QuoteRequest QuoteRequest { get; set; } = new();

        public string? ApiPayloadJson { get; set; }
        public string? FormValuesJson { get; set; }
        public List<QuotePriceBreakdown> PriceBreakdown { get; } = new();

        public async Task OnGetAsync()
        {
            // Get quote data from TempData (passed from form submission)
            // Store it in the property for display, but also keep it in TempData for Edit redirect
            if (TempData.TryGetValue("QuoteRequest", out var quoteData))
            {
                try
                {
                    var quoteJson = Convert.ToString(quoteData) ?? "{}";
                    QuoteRequest = JsonSerializer.Deserialize<QuoteRequest>(quoteJson) ?? new QuoteRequest();
                    QuoteRequestCompat.UpgradeLegacyLineItems(QuoteRequest);
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
            await LoadLineItemPricesAsync();
        }

        /// <summary>
        /// Re-prices the quote's line items against the CERM price list.
        /// Prices are never taken from the posted form - see
        /// <see cref="WiseLabels.Services.ILineItemCatalogService.ResolvePostedAsync"/>.
        /// </summary>
        private async Task LoadLineItemPricesAsync()
        {
            if (QuoteRequest == null) return;

            var context = new WiseLabels.Services.LineItemContext(
                CustomerId: QuoteRequest.CustomerId,
                PrintingId: QuoteRequest.PrintingValue,
                ShapeValue: QuoteRequest.ShapeValue,
                CornersValue: QuoteRequest.CornersValue,
                IsCustomDie: QuoteRequest.IsCustomDie,
                HasExistingDie: !string.IsNullOrWhiteSpace(QuoteRequest.CuttingDieValue)
                                || !string.IsNullOrWhiteSpace(QuoteRequest.CuttingDie));

            QuoteRequest.LineItems = await _lineItemCatalog.ResolvePostedAsync(context, QuoteRequest.LineItems);

            _logger.LogInformation(
                "Resolved {Count} line item(s) totalling {Total:C2} for the quote.",
                QuoteRequest.LineItems.Count,
                LineItemPricing.TotalOf(QuoteRequest.LineItems));
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
                var quoteJson = Convert.ToString(quoteData) ?? "{}";
                quote = JsonSerializer.Deserialize<QuoteRequest>(quoteJson);
                if (quote == null)
                {
                    return RedirectToPage("/Index");
                }
                QuoteRequestCompat.UpgradeLegacyLineItems(quote);

                // Load the quote request into the property so LoadLineItemPricesAsync can access it
                QuoteRequest = quote;

                // Load line item pricing before storing and redirecting
                await LoadLineItemPricesAsync();

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

                // Update the comment fields in v1bon___ with customer contact information
                if (!string.IsNullOrWhiteSpace(estimateIdForContact))
                {
                    var commentsUpdated = await _customerContactService.UpdateQuoteCommentsAsync(estimateIdForContact, quote);
                    if (commentsUpdated)
                    {
                        _logger.LogInformation("Successfully updated quote comments for estimate {EstimateId}", estimateIdForContact);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to update quote comments for estimate {EstimateId}", estimateIdForContact);
                    }
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
                if (!string.IsNullOrWhiteSpace(ApiPayloadJson))
                    TempData["CermApiRequest"] = ApiPayloadJson;

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
                var quoteJson = Convert.ToString(quoteData) ?? "{}";
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

            // Edit must return to the quote form; /Index is the dashboard and cannot
            // consume the preserved TempData.
            return RedirectToPage("/GetQuote");
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

