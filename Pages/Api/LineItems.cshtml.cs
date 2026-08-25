using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WiseLabels.Services;

namespace WiseLabels.Pages.Api
{
    /// <summary>
    /// Returns the selectable charge catalogue for a quote, with the applicability rules
    /// already evaluated server-side so the browser never has to reimplement them.
    /// </summary>
    [IgnoreAntiforgeryToken]
    public class LineItemsModel : PageModel
    {
        private readonly ILogger<LineItemsModel> _logger;
        private readonly ILineItemCatalogService _lineItemCatalog;

        /// <summary>Creates the endpoint.</summary>
        public LineItemsModel(ILogger<LineItemsModel> logger, ILineItemCatalogService lineItemCatalog)
        {
            _logger = logger;
            _lineItemCatalog = lineItemCatalog;
        }

        /// <summary>
        /// GET /Api/LineItems?customerId=&amp;printingId=&amp;shapeValue=&amp;cornersValue=&amp;isCustomDie=&amp;hasExistingDie=
        /// </summary>
        public async Task<IActionResult> OnGetAsync(
            string? customerId = null,
            string? printingId = null,
            string? shapeValue = null,
            string? cornersValue = null,
            bool isCustomDie = false,
            bool hasExistingDie = false)
        {
            try
            {
                var context = new LineItemContext(
                    CustomerId: customerId,
                    PrintingId: printingId,
                    ShapeValue: shapeValue,
                    CornersValue: cornersValue,
                    IsCustomDie: isCustomDie,
                    HasExistingDie: hasExistingDie);

                var catalog = await _lineItemCatalog.GetCatalogAsync(context);

                return new JsonResult(new { lineItems = catalog });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading the line item catalogue for customer {CustomerId}", customerId);
                return StatusCode(500, new { error = "Unable to load line items." });
            }
        }
    }
}
