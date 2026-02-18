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

        public GetQuoteModel(ILogger<GetQuoteModel> logger, CermDbContext context, IConfiguration configuration, IQuoteService quoteService)
        {
            _logger = logger;
            _context = context;
            _configuration = configuration;
            _quoteService = quoteService;
            PrintingFinishFilters = _configuration
                .GetSection("QuoteOptions:PrintingFinishFilters")
                .Get<Dictionary<string, string[]>>()
                ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        }

        public QuoteRequest? SavedQuoteRequest { get; set; }
        public bool Testing { get; set; }
        public string? SelectedCustomerName { get; private set; }
        public IReadOnlyDictionary<string, string[]> PrintingFinishFilters { get; }

        public async Task OnGetAsync()
        {
            string? estimateId = null;
            string? customerId = null;
            string? customerName = null;

            var selectedQuote = LoadSelectedQuoteFromSession();

            if (selectedQuote != null)
            {
                estimateId ??= selectedQuote.EstimateId;
                customerId ??= selectedQuote.CustomerId;
                customerName ??= selectedQuote.CustomerDisplayName ?? selectedQuote.Name;
            }

            Testing = string.Equals(Request.Query["testing"].ToString(), "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase);
            SelectedCustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName;

            if (TempData.TryGetValue("QuoteRequest", out var quoteData))
            {
                try
                {
                    SavedQuoteRequest = JsonSerializer.Deserialize<QuoteRequest>(quoteData.ToString() ?? "{}");
                    TempData.Keep("QuoteRequest");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing quote request from TempData");
                    SavedQuoteRequest = null;
                }
            }

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
        }

        public async Task<IActionResult> OnPostSubmitAsync()
        {
            var form = Request.Form;
            var materialId = GetFormValue(form, "material", "materialValue");
            var materialLabel = GetFormValue(form, "materialDisplay", "materialLabel");
            var rawShapeValue = GetFormValue(form, "shapeValue", "shapeKey");
            var shapeLabel = form["shape"].ToString();
            var resolvedShapeValue = ResolveShapeOutline(rawShapeValue, shapeLabel);

            var quoteRequest = new QuoteRequest
            {
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
                CuttingDie = form["cuttingDie"].ToString(),
                Printing = form["printing"].ToString(),
                Material = string.IsNullOrEmpty(materialLabel) ? materialId : materialLabel,
                ColorCode = form["colorCode"].ToString(),
                Finish = form["finish"].ToString(),
                ApplicationMethod = form["applicationMethod"].ToString(),
                UnwindDirection = form["unwindDirection"].ToString(),
                TotalQuantity = form["totalQuantity"].ToString(),
                Quantities = ParseQuantitiesFromForm(form),
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
                PrintingValue = form["printingValue"].ToString()
            };

            var pricingResult = await _quoteService.GetQuickQuotePricingAsync(quoteRequest);
            if (!pricingResult.Success)
            {
                ModelState.AddModelError(string.Empty, pricingResult.ErrorMessage ?? "Unable to retrieve quick quote pricing. Please try again.");
                SavedQuoteRequest = quoteRequest;
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
                ReferenceValue = estimate.Reference ?? estimate.EstimateId,
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
                "oval" => "4",
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
    }
}
