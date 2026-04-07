using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WiseLabels.Pages.Reports
{
    [Authorize]
    public class ShippedOrdersModel : PageModel
    {
        private readonly ILogger<ShippedOrdersModel> _logger;

        public ShippedOrdersModel(ILogger<ShippedOrdersModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
            _logger.LogInformation("Shipped Orders page accessed");
        }
    }
}
