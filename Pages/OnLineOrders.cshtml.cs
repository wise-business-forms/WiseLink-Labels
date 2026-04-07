using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace WiseLabels.Pages
{
    public class OnLineOrdersModel : PageModel
    {
        private readonly ILogger<OnLineOrdersModel> _logger;

        public OnLineOrdersModel(ILogger<OnLineOrdersModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
            _logger.LogInformation("OnLineOrders page loaded");
        }
    }
}
