using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WiseLabels.Models;
using System.Text.Json;
using System.Net;
using Microsoft.Data.SqlClient;

namespace WiseLabels.Pages
{
    public class LineItemCharge
    {
        public string ItemRef { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    public class ConfirmModel : PageModel
    {
        private readonly ILogger<ConfirmModel> _logger;
        private readonly WiseLabels.Services.IQuoteService _quoteService;
        private readonly WiseLabels.Services.IEmailService _emailService;
        private readonly WiseLabels.Services.ICustomerContactService _customerContactService;
        private readonly IConfiguration _configuration;

        public ConfirmModel(
            ILogger<ConfirmModel> logger, 
            WiseLabels.Services.IQuoteService quoteService, 
            WiseLabels.Services.IEmailService emailService,
            WiseLabels.Services.ICustomerContactService customerContactService,
            IConfiguration configuration)
        {
            _logger = logger;
            _quoteService = quoteService;
            _emailService = emailService;
            _customerContactService = customerContactService;
            _configuration = configuration;
        }

        [BindProperty]
        public QuoteRequest QuoteRequest { get; set; } = new();

        public string? ApiPayloadJson { get; set; }
        public string? FormValuesJson { get; set; }
        public List<QuotePriceBreakdown> PriceBreakdown { get; } = new();
        public Dictionary<string, LineItemCharge> AdditionalCharges { get; set; } = new();

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
            await LoadLineItemPricesAsync();
        }

        private async Task LoadLineItemPricesAsync()
        {
            // Load line item prices based on quote requirements
            if (QuoteRequest?.IsCustomDie == true)
            {
                _logger.LogInformation(
                    "Custom die detected. IsCustomDie={IsCustomDie}, DieSizeInfo={DieSizeInfo}",
                    QuoteRequest.IsCustomDie,
                    QuoteRequest.DieSizeInfo ?? "NULL");

                await LoadLineItemPriceAsync("100001", "CustomDie");
            }

            // Load color changes line item if spot printing with color changes
            if (QuoteRequest?.ColorChanges > 0)
            {
                _logger.LogInformation(
                    "Color changes detected. ColorChanges={ColorChanges}",
                    QuoteRequest.ColorChanges);

                await LoadLineItemPriceAsync("100018", "ColorChanges");
            }

            // Load digital version changes line item if process color printing with version changes
            if (QuoteRequest?.DigitalVersionChanges > 0)
            {
                _logger.LogInformation(
                    "Digital version changes detected. DigitalVersionChanges={DigitalVersionChanges}",
                    QuoteRequest.DigitalVersionChanges);

                await LoadLineItemPriceAsync("100035", "DigitalVersionChanges");
            }

            // Load press proof line item if requested
            if (QuoteRequest?.NeedsPressProof == true)
            {
                _logger.LogInformation(
                    "Press proof requested. Quantity={PressProofQuantity}",
                    QuoteRequest.PressProofQuantity);

                await LoadLineItemPriceAsync("100031", "PressProof");
            }

            // Load spot color plate change line item if requested
            if (QuoteRequest?.NeedsSpotColorPlateChange == true)
            {
                _logger.LogInformation(
                    "Spot color plate change requested. Quantity={SpotColorPlateChangeQuantity}",
                    QuoteRequest.SpotColorPlateChangeQuantity);

                await LoadLineItemPriceAsync("100017", "SpotColorPlateChange");
            }

            // Easy to add more line items in the future:
            // await LoadLineItemPriceAsync("100002", "SetupCharge");
            // await LoadLineItemPriceAsync("100003", "PlateCharge");
        }

        private async Task LoadLineItemPriceAsync(string itemRef, string chargeKey)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("CermDatabase");
                if (string.IsNullOrEmpty(connectionString))
                {
                    _logger.LogError("CermDatabase connection string not found");
                    return;
                }

                // Query stdfpl__ table directly (like CuttingDie does with stnspr__)
                var sql = @"
                    SELECT TOP 1 
                        fpl__ref,
                        fpl__rpn,
                        fkttxt11,
                        prijs_bm,
                        omsaant1
                    FROM stdfpl__ 
                    WHERE fpl__ref = @itemRef 
                        AND (geblokk_ IS NULL OR geblokk_ != '1')";

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@itemRef", itemRef);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                var charge = new LineItemCharge
                                {
                                    ItemRef = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                                    Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                    Price = reader.IsDBNull(3) ? 0 : (decimal)reader.GetDouble(3),
                                    Unit = reader.IsDBNull(4) ? "each" : reader.GetString(4).Trim()
                                };

                                AdditionalCharges[chargeKey] = charge;

                                _logger.LogInformation(
                                    "Loaded line item {ItemRef} ({Key}): {Price} {Unit} - {Description}",
                                    itemRef, chargeKey, charge.Price, charge.Unit, charge.Description);
                            }
                            else
                            {
                                _logger.LogWarning("Line item {ItemRef} not found in stdfpl__ table", itemRef);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading line item price for {ItemRef}", itemRef);
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

                // Pass line item charges to Success page
                if (AdditionalCharges.TryGetValue("CustomDie", out var customDieCharge))
                {
                    TempData["CustomDiePrice"] = customDieCharge.Price.ToString();
                    TempData["CustomDieUnit"] = customDieCharge.Unit;
                }

                if (AdditionalCharges.TryGetValue("ColorChanges", out var colorChangesCharge))
                {
                    TempData["ColorChangesPrice"] = colorChangesCharge.Price.ToString();
                    TempData["ColorChangesUnit"] = colorChangesCharge.Unit;
                    TempData["ColorChangesQty"] = QuoteRequest.ColorChanges?.ToString() ?? "0";
                }

                if (AdditionalCharges.TryGetValue("DigitalVersionChanges", out var digitalVersionChangesCharge))
                {
                    TempData["DigitalVersionChangesPrice"] = digitalVersionChangesCharge.Price.ToString();
                    TempData["DigitalVersionChangesUnit"] = digitalVersionChangesCharge.Unit;
                    TempData["DigitalVersionChangesQty"] = QuoteRequest.DigitalVersionChanges?.ToString() ?? "0";
                }

                if (AdditionalCharges.TryGetValue("PressProof", out var pressProofCharge))
                {
                    TempData["PressProofPrice"] = pressProofCharge.Price.ToString();
                    TempData["PressProofUnit"] = pressProofCharge.Unit;
                    TempData["PressProofQty"] = QuoteRequest.PressProofQuantity?.ToString() ?? "1";
                }

                if (AdditionalCharges.TryGetValue("SpotColorPlateChange", out var spotColorPlateChangeCharge))
                {
                    TempData["SpotColorPlateChangePrice"] = spotColorPlateChangeCharge.Price.ToString();
                    TempData["SpotColorPlateChangeUnit"] = spotColorPlateChangeCharge.Unit;
                    TempData["SpotColorPlateChangeQty"] = QuoteRequest.SpotColorPlateChangeQuantity?.ToString() ?? "1";
                }

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

