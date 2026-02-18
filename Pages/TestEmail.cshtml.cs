using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WiseLabels.Models;
using WiseLabels.Services;

namespace WiseLabels.Pages
{
    public class TestEmailModel : PageModel
    {
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TestEmailModel> _logger;

        public TestEmailModel(IEmailService emailService, IConfiguration configuration, ILogger<TestEmailModel> logger)
        {
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        // Form inputs
        [BindProperty]
        public string? ToEmail { get; set; }

        [BindProperty]
        public string? QuoteId { get; set; }

        [BindProperty]
        public string? CustomerName { get; set; }

        // Result display
        public string? ResultMessage { get; set; }
        public bool Success { get; set; }

        // Configuration display
        public string? SmtpHost { get; set; }
        public int SmtpPort { get; set; }
        public string? SmtpUsername { get; set; }
        public string? FromEmail { get; set; }
        public string? FromName { get; set; }
        public bool IsConfigured { get; set; }

        public void OnGet()
        {
            LoadConfiguration();
            
            // Default values
            QuoteId = "TEST-001";
            CustomerName = "Test User";
        }

        public async Task<IActionResult> OnPostAsync()
        {
            LoadConfiguration();

            if (string.IsNullOrWhiteSpace(ToEmail))
            {
                ResultMessage = "Please enter a recipient email address.";
                Success = false;
                return Page();
            }

            try
            {
                _logger.LogInformation("Sending test email to {Email}", ToEmail);

                var quoteDetails = new QuoteRequest
                {
                    Name = CustomerName ?? "Test User",
                    Email = ToEmail,
                    Description = "Test email payload",
                    ReferenceValue = QuoteId ?? "TEST-001",
                    ReferenceType = "invoice-number"
                };

                var result = await _emailService.SendQuoteConfirmationAsync(
                    ToEmail,
                    QuoteId ?? "TEST-001",
                    CustomerName ?? "Test User",
                    quoteDetails,
                    Array.Empty<QuotePriceBreakdown>()
                );

                if (result)
                {
                    ResultMessage = $"Email sent successfully to {ToEmail}!";
                    Success = true;
                    _logger.LogInformation("Test email sent successfully to {Email}", ToEmail);
                }
                else
                {
                    ResultMessage = "Failed to send email. Check the application logs and email configuration.";
                    Success = false;
                    _logger.LogWarning("Test email failed to send to {Email}", ToEmail);
                }
            }
            catch (Exception ex)
            {
                ResultMessage = $"Error sending email: {ex.Message}";
                Success = false;
                _logger.LogError(ex, "Exception sending test email to {Email}", ToEmail);
            }

            return Page();
        }

        private void LoadConfiguration()
        {
            SmtpHost = _configuration["Email:SmtpHost"];
            SmtpPort = _configuration.GetValue<int>("Email:SmtpPort", 587);
            SmtpUsername = _configuration["Email:SmtpUsername"];
            FromEmail = _configuration["Email:FromEmail"];
            FromName = _configuration["Email:FromName"];

            // Check if minimally configured (host required; username/password may be optional for internal relays)
            IsConfigured = !string.IsNullOrEmpty(SmtpHost);
        }
    }
}
