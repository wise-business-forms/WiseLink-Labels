using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WiseLabels.Services;

namespace WiseLabels.Pages
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public class ErrorModel : PageModel
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<ErrorModel> _logger;

        public ErrorModel(IEmailService emailService, ILogger<ErrorModel> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        public async Task OnGetAsync()
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

            var exceptionHandlerFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            var exception = exceptionHandlerFeature?.Error;
            if (exception != null)
            {
                var path = exceptionHandlerFeature?.Path ?? "(unknown)";
                var context = $"Path: {path}\nRequestId: {RequestId}";
                try
                {
                    await _emailService.SendExceptionNotificationAsync(exception, context);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send exception notification email");
                }
            }
        }
    }
}
