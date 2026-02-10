using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WiseLabels.Models;
using System.Text.Json;

namespace WiseLabels.Pages
{
    public class ConfirmModel : PageModel
    {
        private readonly ILogger<ConfirmModel> _logger;
        private readonly WiseLabels.Services.IQuoteService _quoteService;
        private readonly WiseLabels.Services.IEmailService _emailService;

        public ConfirmModel(ILogger<ConfirmModel> logger, WiseLabels.Services.IQuoteService quoteService, WiseLabels.Services.IEmailService emailService)
        {
            _logger = logger;
            _quoteService = quoteService;
            _emailService = emailService;
        }

        [BindProperty]
        public QuoteRequest QuoteRequest { get; set; } = new();

        public string? ApiPayloadJson { get; set; }
        public string? FormValuesJson { get; set; }

        public void OnGet()
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
        }

        public async Task<IActionResult> OnPostConfirmAsync()
        {
            // Get quote data from TempData
            if (!TempData.TryGetValue("QuoteRequest", out var quoteData))
            {
                return RedirectToPage("/Index");
            }

            try
            {
                var quote = JsonSerializer.Deserialize<QuoteRequest>(quoteData.ToString() ?? "{}");
                if (quote == null)
                {
                    return RedirectToPage("/Index");
                }

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
                var (apiSuccess, cermCalculationId, cermEstimateId, cermErrorMessage) = await _quoteService.SubmitToCermApiAsync(quote);

                TempData["QuoteId"] = quoteId;
                TempData["ApiSuccess"] = apiSuccess.ToString();
                TempData["QuoteRequest"] = JsonSerializer.Serialize(quote);
                if (!string.IsNullOrWhiteSpace(cermCalculationId))
                    TempData["CermCalculationId"] = cermCalculationId;
                if (!string.IsNullOrWhiteSpace(cermEstimateId))
                    TempData["CermEstimateId"] = cermEstimateId;
                if (!string.IsNullOrWhiteSpace(cermErrorMessage))
                    TempData["CermErrorMessage"] = cermErrorMessage;

                // Update contact info in CERM database when we have the estimate ID
                var estimateIdForContact = cermEstimateId ?? cermCalculationId;
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
                var quoteRefForEmail = estimateIdForContact ?? quoteId;
                if (!string.IsNullOrWhiteSpace(quote.Email))
                {
                    try
                    {
                        var emailSent = await _emailService.SendQuoteConfirmationAsync(
                            quote.Email,
                            quoteRefForEmail,
                            quote.Name ?? ""
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
                _logger.LogInformation("Quote data preserved in TempData for edit redirect");
            }
            else
            {
                // Fallback: try to serialize from QuoteRequest property if it's populated
                if (QuoteRequest != null && !string.IsNullOrEmpty(QuoteRequest.Description))
                {
                    TempData["QuoteRequest"] = JsonSerializer.Serialize(QuoteRequest);
                    _logger.LogInformation("Quote data serialized from QuoteRequest property for edit redirect");
                }
                else
                {
                    _logger.LogWarning("No quote data found to preserve for edit - TempData is empty and QuoteRequest is null/empty");
                }
            }
            
            return RedirectToPage("/Index");
        }

    }
}

