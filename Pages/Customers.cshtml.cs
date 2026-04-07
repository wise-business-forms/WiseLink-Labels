using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CERM.DataAccess;
using CERM.DataAccess.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WiseLabels.Models;
using WiseLabels.Services;

namespace WiseLabels.Pages
{
    public class CustomersModel : PageModel
    {
        private readonly CermDbContext _context;
        private readonly ILogger<CustomersModel> _logger;
        private readonly IUserImpersonationService _impersonationService;

        public CustomersModel(
            CermDbContext context, 
            ILogger<CustomersModel> logger,
            IUserImpersonationService impersonationService)
        {
            _context = context;
            _logger = logger;
            _impersonationService = impersonationService;
        }

        public List<Customer> Customers { get; set; } = new();
        public bool IsFiltered { get; set; }
        public string? FilteredByUserId { get; set; }

        [BindProperty]
        public string? SelectedCalculationId { get; set; }

        [BindProperty]
        public string? SelectedEstimateId { get; set; }

        [BindProperty]
        public string? SelectedCustomerId { get; set; }

        [BindProperty]
        public string? SelectedCustomerName { get; set; }

        [BindProperty]
        public string? SelectedDescription { get; set; }

        [BindProperty]
        public string? SelectedMaterial { get; set; }

        [BindProperty]
        public string? SelectedSubstrateId { get; set; }

        [BindProperty]
        public string? SelectedReferenceAtCustomer { get; set; }

        [BindProperty]
        public string? SelectedSize { get; set; }

        [BindProperty]
        public string? SelectedLabelShape { get; set; }

        [BindProperty]
        public string? SelectedLabelWidth { get; set; }

        [BindProperty]
        public string? SelectedLabelHeight { get; set; }

        [BindProperty]
        public string? SelectedLabelRadius { get; set; }

        [BindProperty]
        public string? SelectedColour { get; set; }

        [BindProperty]
        public string? SelectedWinding { get; set; }

        [BindProperty]
        public string? SelectedPackingProcedureId { get; set; }

        [BindProperty]
        public string? SelectedColourCodeId { get; set; }

        [BindProperty]
        public string? SelectedFinishingTypeId { get; set; }

        [BindProperty]
        public string? SelectedMinCount { get; set; }

        [BindProperty]
        public string? SelectedMaxCount { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                // Determine if we should filter data
                var shouldFilter = await _impersonationService.ShouldFilterDataAsync();
                var effectiveUserId = await _impersonationService.GetEffectiveUserIdAsync();

                IQueryable<Customer> query = _context.Customers.AsNoTracking();

                // Filter out special accounts that begin with #, *, or -
                query = query.Where(c => 
                    !c.Name.StartsWith("#") && 
                    !c.Name.StartsWith("*") &&
                    !c.Name.StartsWith("-") &&
                    !c.Id.StartsWith("#") && 
                    !c.Id.StartsWith("*") &&
                    !c.Id.StartsWith("-"));

                // Apply filtering if needed (user is not admin, or admin is impersonating)
                if (shouldFilter && !string.IsNullOrWhiteSpace(effectiveUserId))
                {
                    // Filter customers by RepresentativeId (vrt__ref)
                    query = query.Where(c => c.RepresentativeId == effectiveUserId);
                    IsFiltered = true;
                    FilteredByUserId = effectiveUserId;
                    _logger.LogInformation("Filtering customers for user {UserId}", effectiveUserId);
                }
                else
                {
                    IsFiltered = false;
                    _logger.LogInformation("Showing all customers (admin with no impersonation)");
                }

                Customers = await query.OrderBy(c => c.Name).ToListAsync();

                _logger.LogInformation("Loaded {Count} customers (excluding special accounts). Filtered: {IsFiltered}", 
                    Customers.Count, IsFiltered);

                var representativeIds = Customers
                    .Select(c => c.RepresentativeId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct()
                    .ToList();

                if (representativeIds.Count > 0)
                {
                    var representatives = await _context.Representatives
                        .Where(r => representativeIds.Contains(r.Id))
                        .AsNoTracking()
                        .ToDictionaryAsync(r => r.Id);

                    foreach (var customer in Customers)
                    {
                        if (!string.IsNullOrWhiteSpace(customer.RepresentativeId) && representatives.TryGetValue(customer.RepresentativeId, out var rep))
                        {
                            customer.Representative = rep;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to load customers list");
                Customers = new List<Customer>();
            }
        }

        public IActionResult OnPostUseQuote()
        {
            // Allow either EstimateId (for existing quotes) or CustomerId (for new quotes)
            if (string.IsNullOrWhiteSpace(SelectedEstimateId) && string.IsNullOrWhiteSpace(SelectedCustomerId))
            {
                TempData["QuoteError"] = "Select a customer or quote before continuing.";
                return RedirectToPage();
            }

            // Parse Size field (e.g., "4 x 6 \" ( 0.125)" or "3.248 x 4.015")
            string? width = SelectedLabelWidth;
            string? height = SelectedLabelHeight;
            string? diameter = SelectedLabelRadius;
            string? corners = null;

            if (!string.IsNullOrWhiteSpace(SelectedSize))
            {
                var sizeParts = ParseSizeField(SelectedSize);
                width = sizeParts.width ?? width;
                height = sizeParts.height ?? height;
                diameter = sizeParts.radius ?? diameter;

                // If there's a radius value, it indicates rounded corners
                if (!string.IsNullOrWhiteSpace(diameter))
                {
                    corners = "rounded";
                }
            }

            // Map CERM LabelShape codes to form values
            string? shapeValue = MapLabelShape(SelectedLabelShape);

            // Generate quantity breakdowns from min/max count if available
            List<int>? quantities = null;
            if (!string.IsNullOrWhiteSpace(SelectedMinCount) && !string.IsNullOrWhiteSpace(SelectedMaxCount))
            {
                if (int.TryParse(SelectedMinCount, out var minCount) && int.TryParse(SelectedMaxCount, out var maxCount))
                {
                    quantities = GenerateQuantityBreakdowns(minCount, maxCount);
                }
            }

            var selection = new QuoteRequest
            {
                CalculationId = SelectedCalculationId,
                EstimateId = SelectedEstimateId,
                CustomerId = SelectedCustomerId,
                CustomerDisplayName = SelectedCustomerName,
                Company = SelectedCustomerName, // Populate company field with customer name
                Description = SelectedDescription,
                Material = SelectedMaterial,
                ReferenceType = "Past Quote",
                ReferenceValue = SelectedReferenceAtCustomer ?? SelectedEstimateId,

                // Dimensions
                LabelWidth = width,
                LabelHeight = height,
                Diameter = diameter,
                ShapeValue = shapeValue,
                Shape = GetShapeName(shapeValue),
                Corners = corners,
                CornersValue = corners,

                // Additional fields
                Printing = SelectedColour,
                PrintingValue = SelectedColourCodeId ?? SelectedColour,
                Finish = SelectedFinishingTypeId,
                FinishValue = SelectedFinishingTypeId,
                PackingProcedure = SelectedPackingProcedureId,
                MaterialValue = SelectedSubstrateId ?? SelectedMaterial, // Use SubstrateId if available, fallback to description
                Quantities = quantities,
                UnwindDirection = MapWindingToLabel(SelectedWinding),
                UnwindDirectionValue = SelectedWinding // Store the raw CERM winding ID
            };

            HttpContext.Session.SetString(SessionKeys.SelectedQuote, JsonSerializer.Serialize(selection));

            return RedirectToPage("/GetQuote");
        }

        private static List<int> GenerateQuantityBreakdowns(int minCount, int maxCount)
        {
            // Generate up to 5 quantity breakdowns between min and max
            var quantities = new List<int>();

            if (minCount == maxCount)
            {
                quantities.Add(minCount);
                return quantities;
            }

            var range = maxCount - minCount;
            var step = range / 4.0; // Divide into 5 points (including min and max)

            for (int i = 0; i < 5; i++)
            {
                var qty = (int)Math.Round(minCount + (step * i));
                if (!quantities.Contains(qty))
                {
                    quantities.Add(qty);
                }
            }

            // Ensure we have the max count
            if (!quantities.Contains(maxCount))
            {
                if (quantities.Count >= 5)
                {
                    quantities[4] = maxCount;
                }
                else
                {
                    quantities.Add(maxCount);
                }
            }

            return quantities.Take(5).ToList();
        }

        private static string? MapWindingToLabel(string? cermWindingCode)
        {
            // CERM returns winding codes that map to unwind directions in our form
            // Map CERM codes to our form's unwind direction labels
            return cermWindingCode switch
            {
                "1" => "1 - Top off first",     // Head off first
                "2" => "2 - Bottom off first",  // Foot off first  
                "3" => "3 - Right off first",
                "4" => "4 - Left off first",
                "5" => "1 - Top off first",     // Alternative Head off first
                "6" => "2 - Bottom off first",  // Alternative Foot off first
                "7" => "3 - Right off first",   // Alternative
                "8" => "4 - Left off first",    // Alternative
                "10" => null,  // Sheeted - no unwind direction
                "11" => null,  // Fanfold - no unwind direction
                "-" => null,   // Not Applicable
                "0" => null,   // No Preference / TBD
                _ => null
            };
        }

        private static (string? width, string? height, string? radius) ParseSizeField(string size)
        {
            // Examples: "4 x 6 \" ( 0.125)", "3.248 x 4.015", " 4.5 x 6.25"
            string? width = null;
            string? height = null;
            string? radius = null;

            try
            {
                // Remove quotes and extra whitespace
                var cleaned = size.Replace("\"", "").Trim();

                // Extract radius from parentheses if present
                var radiusMatch = System.Text.RegularExpressions.Regex.Match(cleaned, @"\(([0-9.]+)\)");
                if (radiusMatch.Success)
                {
                    radius = radiusMatch.Groups[1].Value;
                    cleaned = cleaned.Substring(0, radiusMatch.Index).Trim();
                }

                // Split by 'x' to get width and height
                var parts = cleaned.Split('x', System.StringSplitOptions.TrimEntries);
                if (parts.Length >= 2)
                {
                    width = parts[0].Trim();
                    height = parts[1].Trim();
                }
            }
            catch
            {
                // If parsing fails, return nulls
            }

            return (width, height, radius);
        }

        private static string? MapLabelShape(string? cermShapeCode)
        {
            // CERM LabelShape: "1"=Rectangle, "2"=Square, "3"=Circle, "4"=Oval
            return cermShapeCode switch
            {
                "1" => "1", // Rectangle
                "2" => "2", // Square
                "3" => "3", // Circle
                "4" => "4", // Oval
                _ => null
            };
        }

        private static string? GetShapeName(string? shapeValue)
        {
            return shapeValue switch
            {
                "1" => "Rectangle",
                "2" => "Square",
                "3" => "Circle",
                "4" => "Oval",
                _ => null
            };
        }
    }
}