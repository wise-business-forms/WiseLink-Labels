using System.Text.Json;
using CERM.DataAccess;
using CERM.DataAccess.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WiseLabels;
using WiseLabels.Models;

namespace WiseLabels.Pages
{
    public class GetQuoteModel : PageModel
    {
        private readonly ILogger<GetQuoteModel> _logger;
        private readonly CermDbContext _context;

        public GetQuoteModel(ILogger<GetQuoteModel> logger, CermDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public QuoteRequest? SavedQuoteRequest { get; set; }
        public bool Testing { get; set; }
        public string? SelectedCustomerName { get; private set; }

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

        public IActionResult OnPostSubmit()
        {
            var quoteRequest = new QuoteRequest
            {
                Name = Request.Form["name"].ToString().Trim(),
                Email = Request.Form["email"].ToString().Trim(),
                Phone = Request.Form["phone"].ToString().Trim(),
                ReferenceType = Request.Form["referenceType"].ToString().Trim(),
                ReferenceValue = Request.Form["referenceValue"].ToString().Trim(),
                Description = TruncateDescription(Request.Form["description"].ToString()),
                Shape = Request.Form["shape"].ToString(),
                LabelWidth = Request.Form["labelWidth"].ToString(),
                LabelHeight = Request.Form["labelHeight"].ToString(),
                Diameter = Request.Form["diameter"].ToString(),
                Corners = Request.Form["corners"].ToString(),
                CuttingDie = Request.Form["cuttingDie"].ToString(),
                Printing = Request.Form["printing"].ToString(),
                Material = Request.Form["material"].ToString(),
                ColorCode = Request.Form["colorCode"].ToString(),
                Finish = Request.Form["finish"].ToString(),
                ApplicationMethod = Request.Form["applicationMethod"].ToString(),
                UnwindDirection = Request.Form["unwindDirection"].ToString(),
                TotalQuantity = Request.Form["totalQuantity"].ToString(),
                Quantities = ParseQuantitiesFromForm(Request.Form),
                ArtworkOption = Request.Form["artworkOption"].ToString(),
                ShapeValue = Request.Form["shapeValue"].ToString(),
                CornersValue = Request.Form["cornersValue"].ToString(),
                MaterialValue = Request.Form["materialValue"].ToString(),
                ColorCodeValue = Request.Form["colorCodeValue"].ToString(),
                FinishValue = Request.Form["finishValue"].ToString(),
                ApplicationMethodValue = Request.Form["applicationMethodValue"].ToString(),
                UnwindDirectionValue = Request.Form["unwindDirectionValue"].ToString(),
                ArtworkOptionValue = Request.Form["artworkOptionValue"].ToString(),
                CuttingDieValue = Request.Form["cuttingDieValue"].ToString(),
                PrintingValue = Request.Form["printingValue"].ToString()
            };

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
                Email = estimate.Email,
                Phone = estimate.PhoneNumber,
                ReferenceType = "Past Quote",
                ReferenceValue = estimate.Reference ?? estimate.EstimateId,
                Description = TruncateDescription(estimate.Description ?? string.Empty)
            };
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
