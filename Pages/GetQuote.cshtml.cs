using System.Collections.Generic;
using System.Text.Json;
using CERM.DataAccess;
using CERM.DataAccess.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using WiseLabels;
using WiseLabels.Models;
using WiseLabels.Services;

namespace WiseLabels.Pages
{
    public class GetQuoteModel : PageModel
    {
        private readonly ILogger<GetQuoteModel> _logger;
        private readonly CermDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IQuoteService _quoteService;
        private readonly ILineItemCatalogService _lineItemCatalog;

        public GetQuoteModel(ILogger<GetQuoteModel> logger, CermDbContext context, IConfiguration configuration, IQuoteService quoteService, ILineItemCatalogService lineItemCatalog)
        {
            _logger = logger;
            _context = context;
            _configuration = configuration;
            _quoteService = quoteService;
            _lineItemCatalog = lineItemCatalog;
            PrintingFinishFilters = _configuration
                .GetSection("QuoteOptions:PrintingFinishFilters")
                .Get<Dictionary<string, string[]>>()
                ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        }

        public QuoteRequest? SavedQuoteRequest { get; set; }
        public bool Testing { get; set; }
        public string? SelectedCustomerName { get; private set; }
        public IReadOnlyDictionary<string, string[]> PrintingFinishFilters { get; }
        /// <summary>JSON payload that was sent to CERM API (for debugging on error).</summary>
        public string? ApiRequestJson { get; set; }
        /// <summary>JSON response from CERM API (for debugging on error).</summary>
        public string? ApiResponseJson { get; set; }
        /// <summary>Charges offered for this quote, with rule-driven pre-selection applied.</summary>
        public IReadOnlyList<QuoteLineItem> LineItemCatalog { get; private set; } = Array.Empty<QuoteLineItem>();

        /// <summary>
        /// Loads the charge catalogue for the current form state, merging in any
        /// selections carried by a saved quote so an edited quote round-trips.
        /// </summary>
        private async Task LoadLineItemCatalogAsync()
        {
            var context = new LineItemContext(
                CustomerId: SavedQuoteRequest?.CustomerId,
                PrintingId: SavedQuoteRequest?.PrintingValue,
                ShapeValue: SavedQuoteRequest?.ShapeValue,
                CornersValue: SavedQuoteRequest?.CornersValue,
                IsCustomDie: SavedQuoteRequest?.IsCustomDie ?? false,
                HasExistingDie: !string.IsNullOrWhiteSpace(SavedQuoteRequest?.CuttingDieValue)
                                || !string.IsNullOrWhiteSpace(SavedQuoteRequest?.CuttingDie));

            LineItemCatalog = await _lineItemCatalog.GetCatalogAsync(context, SavedQuoteRequest?.LineItems);
        }

        /// <summary>
        /// Reads the line item grid's single JSON field. Only selection and quantity are
        /// taken from it; everything price-bearing is re-resolved from CERM.
        /// </summary>
        private List<QuoteLineItem> ParseLineItemsFromForm(IFormCollection form)
        {
            var json = form["lineItemsJson"].ToString();
            if (string.IsNullOrWhiteSpace(json)) return new List<QuoteLineItem>();

            try
            {
                return JsonSerializer.Deserialize<List<QuoteLineItem>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<QuoteLineItem>();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Could not parse posted line items; the quote will be priced without charges.");
                return new List<QuoteLineItem>();
            }
        }

        public async Task OnGetAsync()
        {
            string? estimateId = null;
            string? customerId = null;
            string? customerName = null;
            string? contactName = null;
            string? contactEmail = null;
            string? contactPhone = null;

            // Check for customer and contact from query parameters (for "New Quote" button from Customers page)
            if (Request.Query.TryGetValue("customerId", out var customerIdQuery))
            {
                customerId = customerIdQuery.ToString();
            }

            if (Request.Query.TryGetValue("customerName", out var customerNameQuery))
            {
                customerName = customerNameQuery.ToString();
            }

            if (Request.Query.TryGetValue("contactName", out var contactNameQuery))
            {
                contactName = contactNameQuery.ToString();
            }

            if (Request.Query.TryGetValue("contactEmail", out var contactEmailQuery))
            {
                contactEmail = contactEmailQuery.ToString();
            }

            if (Request.Query.TryGetValue("contactPhone", out var contactPhoneQuery))
            {
                contactPhone = contactPhoneQuery.ToString();
            }

            var selectedQuote = LoadSelectedQuoteFromSession();

            if (selectedQuote != null)
            {
                estimateId ??= selectedQuote.EstimateId;
                customerId ??= selectedQuote.CustomerId;
                customerName ??= selectedQuote.CustomerDisplayName ?? selectedQuote.Name;

                // Use the selected quote as SavedQuoteRequest to pre-fill the form
                SavedQuoteRequest = selectedQuote;
            }

            // If we have customer info from query params, create a basic SavedQuoteRequest
            if (SavedQuoteRequest == null && !string.IsNullOrWhiteSpace(customerId))
            {
                SavedQuoteRequest = new QuoteRequest
                {
                    CustomerId = customerId,
                    CustomerDisplayName = customerName,
                    Company = customerName,
                    Name = contactName,
                    Email = contactEmail,
                    Phone = contactPhone
                };
                _logger.LogInformation("Creating new quote for customer {CustomerId} - {CustomerName} with contact {ContactName}", 
                    customerId, customerName, contactName);
            }

            Testing = string.Equals(Request.Query["testing"].ToString(), "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase);
            SelectedCustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName;

            // Check for query parameters from chat (highest priority - overrides session)
            var chatQuoteData = BuildQuoteRequestFromQueryParams();
            if (chatQuoteData != null)
            {
                SavedQuoteRequest = chatQuoteData;
                _logger.LogInformation("Prepopulating quote form from chat data");
                await LoadLineItemCatalogAsync();
                return;
            }

            // TempData has second priority (overrides session if present)
            if (TempData.TryGetValue("QuoteRequest", out var quoteData))
            {
                try
                {
                    var quoteJson = Convert.ToString(quoteData) ?? "{}";
                    SavedQuoteRequest = JsonSerializer.Deserialize<QuoteRequest>(quoteJson);
                    QuoteRequestCompat.UpgradeLegacyLineItems(SavedQuoteRequest);
                    TempData.Keep("QuoteRequest");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing quote request from TempData");
                    SavedQuoteRequest = null;
                }
            }

            // If we still don't have data but have an estimate ID, try loading from database
            if (SavedQuoteRequest == null && !string.IsNullOrWhiteSpace(estimateId))
            {
                try
                {
                    var estimate = await _context.Estimates
                        .AsNoTracking()
                        .FirstOrDefaultAsync(e => e.EstimateId == estimateId);

                    if (estimate != null)
                    {
                        SavedQuoteRequest = MapEstimateToQuoteRequest(estimate);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to load estimate {EstimateId} for quote prefill", estimateId);
                }
            }

            var selectedDescription = selectedQuote?.Description;
            if (!string.IsNullOrWhiteSpace(selectedDescription))
            {
                SavedQuoteRequest ??= new QuoteRequest();
                SavedQuoteRequest.Description = TruncateDescription(selectedDescription);
            }

            var selectedMaterial = selectedQuote?.Material;
            if (!string.IsNullOrWhiteSpace(selectedMaterial))
            {
                SavedQuoteRequest ??= new QuoteRequest();
                SavedQuoteRequest.Material = selectedMaterial;
            }

            if (SavedQuoteRequest == null && selectedQuote != null)
            {
                SavedQuoteRequest = selectedQuote;
            }

            await LoadLineItemCatalogAsync();
        }

        public async Task<IActionResult> OnPostSubmitAsync()
        {
            var form = Request.Form;

            // Handle file upload
            string? uploadedFileName = null;
            string? uploadedFileOriginalName = null;
            string? uploadedFileContentType = null;
            long? uploadedFileSize = null;

            var artworkFile = Request.Form.Files.GetFile("artworkFile");
            if (artworkFile != null && artworkFile.Length > 0)
            {
                try
                {
                    // Validate file size (50 MB max)
                    const long maxFileSize = 50 * 1024 * 1024;
                    if (artworkFile.Length > maxFileSize)
                    {
                        ModelState.AddModelError(string.Empty, "File size exceeds 50 MB limit.");
                        SavedQuoteRequest = BuildQuoteRequest(form);
                        await LoadLineItemCatalogAsync();
                        return Page();
                    }

                    // Create uploads directory if it doesn't exist
                    var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "quotes");
                    Directory.CreateDirectory(uploadsPath);

                    // Generate unique filename
                    var fileExtension = Path.GetExtension(artworkFile.FileName);
                    var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
                    var filePath = Path.Combine(uploadsPath, uniqueFileName);

                    // Save file
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await artworkFile.CopyToAsync(stream);
                    }

                    uploadedFileName = uniqueFileName;
                    uploadedFileOriginalName = artworkFile.FileName;
                    uploadedFileContentType = artworkFile.ContentType;
                    uploadedFileSize = artworkFile.Length;

                    _logger.LogInformation(
                        "File uploaded successfully: {OriginalName} -> {UniqueFileName} ({Size} bytes)",
                        uploadedFileOriginalName,
                        uploadedFileName,
                        uploadedFileSize);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading file");
                    ModelState.AddModelError(string.Empty, "Failed to upload file. Please try again.");
                    SavedQuoteRequest = BuildQuoteRequest(form);
                    await LoadLineItemCatalogAsync();
                    return Page();
                }
            }

            // Customer resolution has a session side effect (it clears the stashed
            // selection), so it stays on the submit path and is passed into the builder.
            var customerId = form["customerId"].ToString();
            var customerDisplayName = form["customerDisplayName"].ToString();
            if (string.IsNullOrWhiteSpace(customerId))
            {
                var selectedQuote = LoadSelectedQuoteFromSession();
                customerId = selectedQuote?.CustomerId;
                customerDisplayName = selectedQuote?.CustomerDisplayName ?? selectedQuote?.Name;
            }
            else
            {
                LoadSelectedQuoteFromSession(); // clear session
            }

            var quoteRequest = BuildQuoteRequest(
                form,
                customerId,
                customerDisplayName,
                uploadedFileName,
                uploadedFileOriginalName,
                uploadedFileContentType,
                uploadedFileSize);

            // Log the JSON request being submitted
            var jsonRequest = JsonSerializer.Serialize(quoteRequest, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            _logger.LogInformation("Quote submission JSON request:\n{JsonRequest}", jsonRequest);

            // Validate required dimensions based on shape
            var isRectangularShape = quoteRequest.ShapeValue == "1" || 
                                     (quoteRequest.Shape ?? "").Equals("rectangle", StringComparison.OrdinalIgnoreCase);
            var isCircleShape = quoteRequest.ShapeValue == "2" || quoteRequest.ShapeValue == "3" ||
                                (quoteRequest.Shape ?? "").Equals("circle", StringComparison.OrdinalIgnoreCase);

            if (isRectangularShape)
            {
                if (string.IsNullOrWhiteSpace(quoteRequest.LabelWidth) || !double.TryParse(quoteRequest.LabelWidth, out var w) || w <= 0)
                {
                    ModelState.AddModelError(string.Empty, "Width is required for rectangular labels.");
                }
                if (string.IsNullOrWhiteSpace(quoteRequest.LabelHeight) || !double.TryParse(quoteRequest.LabelHeight, out var h) || h <= 0)
                {
                    ModelState.AddModelError(string.Empty, "Height is required for rectangular labels.");
                }
            }
            else if (isCircleShape)
            {
                if (string.IsNullOrWhiteSpace(quoteRequest.Diameter) || !double.TryParse(quoteRequest.Diameter, out var d) || d <= 0)
                {
                    ModelState.AddModelError(string.Empty, "Diameter/radius is required for circular labels.");
                }
            }

            if (!ModelState.IsValid)
            {
                SavedQuoteRequest = quoteRequest;
                await LoadLineItemCatalogAsync();
                return Page();
            }

            var pricingResult = await _quoteService.GetQuickQuotePricingAsync(quoteRequest);
            if (!pricingResult.Success)
            {
                ModelState.AddModelError(string.Empty, pricingResult.ErrorMessage ?? "Unable to retrieve quick quote pricing. Please try again.");
                SavedQuoteRequest = quoteRequest;

                // Store the API payload for debugging
                var apiPayload = _quoteService.TransformToApiPayload(quoteRequest);
                ApiRequestJson = JsonSerializer.Serialize(apiPayload, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });
                ApiResponseJson = pricingResult.ResponseJson;

                await LoadLineItemCatalogAsync();
                return Page();
            }

            quoteRequest.QuickQuoteResponseJson = pricingResult.ResponseJson;
            if (pricingResult.Breakdown.Count > 0)
            {
                quoteRequest.PriceBreakdown = new List<QuotePriceBreakdown>(pricingResult.Breakdown);
            }

            TempData["QuoteRequest"] = JsonSerializer.Serialize(quoteRequest);
            return RedirectToPage("/Confirm");
        }

        private static List<int> ParseQuantitiesFromForm(IFormCollection form)
        {
            var list = new List<int>();
            var values = form["quantity"];
            if (values.Count == 0) return list;
            foreach (var v in values)
            {
                if (int.TryParse(v?.Trim(), out var n) && n >= 1)
                {
                    list.Add(n);
                }
            }
            return list;
        }

        private static string TruncateDescription(string description)
        {
            if (string.IsNullOrEmpty(description)) return "";
            return description.Length > 50 ? description.Substring(0, 50) : description;
        }

        private static QuoteRequest MapEstimateToQuoteRequest(Estimate estimate)
        {
            return new QuoteRequest
            {
                Name = estimate.CustomerName ?? estimate.ContactName,
                Company = estimate.CustomerName,
                Email = estimate.Email,
                Phone = estimate.PhoneNumber,
                ReferenceType = "Past Quote",
                ReferenceValue = estimate.EstimateId,
                Description = TruncateDescription(estimate.Description ?? string.Empty)
            };
        }

        private static string ResolveShapeOutline(string? shapeValue, string? shapeLabel)
        {
            if (!string.IsNullOrWhiteSpace(shapeValue))
            {
                return shapeValue.Trim();
            }

            if (string.IsNullOrWhiteSpace(shapeLabel))
            {
                return string.Empty;
            }

            return shapeLabel.Trim().ToLowerInvariant() switch
            {
                "rectangle" => "1",
                "square" => "2",
                "circle" => "3",
                _ => shapeLabel.Trim()
            };
        }

        private static string GetFormValue(IFormCollection form, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (StringValues.IsNullOrEmpty(form[key]))
                {
                    continue;
                }

                return form[key].ToString().Trim();
            }

            return string.Empty;
        }

        private QuoteRequest? LoadSelectedQuoteFromSession()
        {
            var value = HttpContext.Session.GetString(SessionKeys.SelectedQuote);
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            try
            {
                var selection = JsonSerializer.Deserialize<QuoteRequest>(value);
                QuoteRequestCompat.UpgradeLegacyLineItems(selection);
                HttpContext.Session.Remove(SessionKeys.SelectedQuote);
                return selection;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize selected quote session data.");
                HttpContext.Session.Remove(SessionKeys.SelectedQuote);
                return null;
            }
        }

        private QuoteRequest? BuildQuoteRequestFromQueryParams()
        {
            var query = Request.Query;

            // Check if any quote-related query parameters exist
            if (!query.ContainsKey("description") && !query.ContainsKey("shape") && 
                !query.ContainsKey("labelWidth") && !query.ContainsKey("material"))
            {
                return null;
            }

            var quoteRequest = new QuoteRequest();
            bool hasData = false;

            if (query.TryGetValue("description", out var description) && !string.IsNullOrWhiteSpace(description))
            {
                quoteRequest.Description = description.ToString();
                hasData = true;
            }

            if (query.TryGetValue("shape", out var shape) && !string.IsNullOrWhiteSpace(shape))
            {
                quoteRequest.Shape = shape.ToString();
                quoteRequest.ShapeValue = MapShapeToValue(shape.ToString());
                hasData = true;
            }

            if (query.TryGetValue("labelWidth", out var width) && !string.IsNullOrWhiteSpace(width))
            {
                quoteRequest.LabelWidth = width.ToString();
                hasData = true;
            }

            if (query.TryGetValue("labelHeight", out var height) && !string.IsNullOrWhiteSpace(height))
            {
                quoteRequest.LabelHeight = height.ToString();
                hasData = true;
            }

            if (query.TryGetValue("diameter", out var diameter) && !string.IsNullOrWhiteSpace(diameter))
            {
                quoteRequest.Diameter = diameter.ToString();
                hasData = true;
            }

            if (query.TryGetValue("material", out var material) && !string.IsNullOrWhiteSpace(material))
            {
                quoteRequest.Material = material.ToString();
                hasData = true;
            }

            if (query.TryGetValue("printing", out var printing) && !string.IsNullOrWhiteSpace(printing))
            {
                quoteRequest.Printing = printing.ToString();
                hasData = true;
            }

            if (query.TryGetValue("finish", out var finish) && !string.IsNullOrWhiteSpace(finish))
            {
                quoteRequest.Finish = finish.ToString();
                hasData = true;
            }

            if (query.TryGetValue("totalQuantity", out var quantity) && !string.IsNullOrWhiteSpace(quantity))
            {
                quoteRequest.TotalQuantity = quantity.ToString();
                hasData = true;
            }

            return hasData ? quoteRequest : null;
        }

        private string MapShapeToValue(string shape)
        {
            return shape.ToLowerInvariant() switch
            {
                "rectangle" => "1",
                "square" => "2",
                "circle" => "3",
                _ => shape
            };
        }

        /// <summary>
        /// Builds a <see cref="QuoteRequest"/> from the posted form.
        /// This is the single binder used by the submit path and by both file-upload
        /// failure paths, so a validation error never silently drops fields.
        /// </summary>
        private QuoteRequest BuildQuoteRequest(
            IFormCollection form,
            string? customerId = null,
            string? customerDisplayName = null,
            string? uploadedFileName = null,
            string? uploadedFileOriginalName = null,
            string? uploadedFileContentType = null,
            long? uploadedFileSize = null)
        {
            var materialId = GetFormValue(form, "material", "materialValue");
            var materialLabel = GetFormValue(form, "materialDisplay", "materialLabel");
            var rawShapeValue = GetFormValue(form, "shapeValue", "shapeKey");
            var shapeLabel = form["shape"].ToString();
            var resolvedShapeValue = ResolveShapeOutline(rawShapeValue, shapeLabel);

            if (string.IsNullOrWhiteSpace(customerId))
            {
                customerId = form["customerId"].ToString();
            }
            if (string.IsNullOrWhiteSpace(customerDisplayName))
            {
                customerDisplayName = form["customerDisplayName"].ToString();
            }

            return new QuoteRequest
            {
                CustomerId = customerId,
                CustomerDisplayName = customerDisplayName,
                Name = GetFormValue(form, "name", "contactName"),
                Company = GetFormValue(form, "company", "contactCompany"),
                Email = GetFormValue(form, "email", "contactEmail"),
                Phone = GetFormValue(form, "phone", "contactPhone"),
                Comments = GetFormValue(form, "comments", "contactComments"),
                ReferenceType = form["referenceType"].ToString().Trim(),
                ReferenceValue = form["referenceValue"].ToString().Trim(),
                Description = TruncateDescription(form["description"].ToString()),
                Shape = shapeLabel,
                LabelWidth = form["labelWidth"].ToString(),
                LabelHeight = form["labelHeight"].ToString(),
                Diameter = form["diameter"].ToString(),
                Corners = form["corners"].ToString(),
                CuttingDie = form["existingDie"].ToString(),
                DieSizeInfo = form["dieSizeInfo"].ToString(),
                IsCustomDie = form["isCustomDie"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase),
                Printing = form["printing"].ToString(),
                Material = string.IsNullOrEmpty(materialLabel) ? materialId : materialLabel,
                ColorCode = form["colorCode"].ToString(),
                Finish = form["finish"].ToString(),
                PackingProcedure = form["packingProcedure"].ToString(),
                // Both of these were posted by the form but never bound, so QuoteService
                // always fell back to its Sheet/500 defaults.
                PackingProcedureValue = form["packingProcedureValue"].ToString(),
                PackingQuantity = int.TryParse(form["packingQuantity"].ToString(), out var packingQuantity) ? packingQuantity : (int?)null,
                ApplicationMethod = form["applicationMethod"].ToString(),
                UnwindDirection = form["unwindDirection"].ToString(),
                TotalQuantity = form["totalQuantity"].ToString(),
                Quantities = ParseQuantitiesFromForm(form),
                LineItems = ParseLineItemsFromForm(form),
                ArtworkOption = form["artworkOption"].ToString(),
                ShapeValue = resolvedShapeValue,
                CornersValue = form["cornersValue"].ToString(),
                MaterialValue = materialId,
                ColorCodeValue = form["colorCodeValue"].ToString(),
                FinishValue = form["finishValue"].ToString(),
                ApplicationMethodValue = form["applicationMethodValue"].ToString(),
                UnwindDirectionValue = form["unwindDirectionValue"].ToString(),
                ArtworkOptionValue = form["artworkOptionValue"].ToString(),
                CuttingDieValue = form["cuttingDieValue"].ToString(),
                PrintingValue = form["printingValue"].ToString(),
                UploadedFileName = uploadedFileName,
                UploadedFileOriginalName = uploadedFileOriginalName,
                UploadedFileContentType = uploadedFileContentType,
                UploadedFileSize = uploadedFileSize
            };
        }
    }
}
