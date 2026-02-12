using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace WiseLabels.Services
{
    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly IConfiguration _configuration;

        public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<bool> SendQuoteConfirmationAsync(string toEmail, string quoteId, string customerName)
        {
            try
            {
                var smtpHost = _configuration["Email:SmtpHost"];
                var smtpPort = _configuration.GetValue<int>("Email:SmtpPort", 587);
                var smtpUsername = _configuration["Email:SmtpUsername"];
                var smtpPassword = _configuration["Email:SmtpPassword"];
                var fromEmail = _configuration["Email:FromEmail"] ?? "noreply@wiselinklabels.com";
                var fromName = _configuration["Email:FromName"] ?? "WiseLink Labels";

                if (string.IsNullOrEmpty(smtpHost))
                {
                    _logger.LogWarning("Email:SmtpHost is not configured. Email will not be sent.");
                    return false;
                }

                var enableSsl = _configuration.GetValue<bool>("Email:EnableSsl", smtpPort != 25);

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = enableSsl
                };

                // Only set credentials if username is provided (allows unauthenticated internal relays)
                if (!string.IsNullOrEmpty(smtpUsername) && !string.IsNullOrEmpty(smtpPassword))
                {
                    client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
                }

                var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = $"Quote Request Confirmation - #{quoteId}",
                    Body = GenerateEmailBody(quoteId, customerName),
                    IsBodyHtml = true
                };

                message.To.Add(toEmail);

                await client.SendMailAsync(message);
                _logger.LogInformation("Confirmation email sent successfully to {Email} for quote {QuoteId}", toEmail, quoteId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending confirmation email to {Email} for quote {QuoteId}", toEmail, quoteId);
                return false;
            }
        }

        public async Task<bool> SendExceptionNotificationAsync(
            Exception exception,
            string? context = null,
            IReadOnlyDictionary<string, object?>? parameters = null)
        {
            var itContacts = _configuration["Email:ITContacts"]?
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .ToArray() ?? Array.Empty<string>();

            if (itContacts.Length == 0)
            {
                _logger.LogWarning("Email:ITContacts is not configured. Exception notification will not be sent.");
                return false;
            }

            try
            {
                var smtpHost = _configuration["Email:SmtpHost"];
                var smtpPort = _configuration.GetValue<int>("Email:SmtpPort", 587);
                var smtpUsername = _configuration["Email:SmtpUsername"];
                var smtpPassword = _configuration["Email:SmtpPassword"];
                var fromEmail = _configuration["Email:FromEmail"] ?? "noreply@wiselinklabels.com";
                var fromName = _configuration["Email:FromName"] ?? "WiseLink Labels";

                if (string.IsNullOrEmpty(smtpHost))
                {
                    _logger.LogWarning("Email:SmtpHost is not configured. Exception notification will not be sent.");
                    return false;
                }

                var enableSsl = _configuration.GetValue<bool>("Email:EnableSsl", smtpPort != 25);

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = enableSsl
                };

                if (!string.IsNullOrEmpty(smtpUsername) && !string.IsNullOrEmpty(smtpPassword))
                {
                    client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
                }

                var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = $"[WiseLink Labels] Exception: {exception.GetType().Name}",
                    Body = GenerateExceptionEmailBody(exception, context, parameters),
                    IsBodyHtml = true
                };

                foreach (var email in itContacts)
                {
                    message.To.Add(email.Trim());
                }

                await client.SendMailAsync(message);
                _logger.LogInformation("Exception notification sent to {Count} IT contact(s)", itContacts.Length);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending exception notification email");
                return false;
            }
        }

        public async Task<bool> SendCustomEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                var smtpHost = _configuration["Email:SmtpHost"];
                var smtpPort = _configuration.GetValue<int>("Email:SmtpPort", 587);
                var smtpUsername = _configuration["Email:SmtpUsername"];
                var smtpPassword = _configuration["Email:SmtpPassword"];
                var fromEmail = _configuration["Email:FromEmail"] ?? "noreply@wiselinklabels.com";
                var fromName = _configuration["Email:FromName"] ?? "WiseLink Labels";

                if (string.IsNullOrEmpty(smtpHost))
                {
                    _logger.LogWarning("Email:SmtpHost is not configured. Custom email will not be sent.");
                    return false;
                }

                var enableSsl = _configuration.GetValue<bool>("Email:EnableSsl", smtpPort != 25);

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    EnableSsl = enableSsl
                };

                if (!string.IsNullOrEmpty(smtpUsername) && !string.IsNullOrEmpty(smtpPassword))
                {
                    client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
                }

                var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                message.To.Add(toEmail);

                await client.SendMailAsync(message);
                _logger.LogInformation("Custom email '{Subject}' sent successfully to {Email}", subject, toEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending custom email '{Subject}' to {Email}", subject, toEmail);
                return false;
            }
        }

        private static string GenerateExceptionEmailBody(
            Exception exception,
            string? context,
            IReadOnlyDictionary<string, object?>? parameters)
        {
            Func<string?, string> enc = s => System.Net.WebUtility.HtmlEncode(s ?? "");
            var (methodName, fileName, lineNumber) = ExtractExceptionLocation(exception);
            var sourceBlock = $@"
<tr><td><strong>Method</strong></td><td>{enc(methodName ?? "(unknown)")}</td></tr>
<tr><td><strong>File / Line</strong></td><td>{enc(BuildFileLineText(fileName, lineNumber))}</td></tr>";

            var contextBlock = string.IsNullOrWhiteSpace(context) ? "" : $@"
<tr><td><strong>Context</strong></td><td><pre>{enc(context)}</pre></td></tr>";

            var parametersBlock = BuildParametersBlock(parameters, enc);

            var stackTrace = string.IsNullOrEmpty(exception.StackTrace)
                ? "(no stack trace)"
                : enc(exception.StackTrace);

            var innerBlocks = "";
            var inner = exception.InnerException;
            var depth = 0;
            while (inner != null && depth < 5)
            {
                innerBlocks += $@"
<tr><td><strong>Inner Exception ({depth + 1})</strong></td><td>{inner.GetType().FullName}: {enc(inner.Message)}</td></tr>
<tr><td><strong>Inner Stack Trace ({depth + 1})</strong></td><td><pre>{enc(inner.StackTrace ?? "(none)")}</pre></td></tr>";
                inner = inner.InnerException;
                depth++;
            }

            return $@"
<!DOCTYPE html>
<html>
<head><meta charset=""utf-8""><style>
body {{ font-family: monospace; font-size: 12px; margin: 20px; }}
table {{ border-collapse: collapse; width: 100%; }}
th, td {{ border: 1px solid #ccc; padding: 8px; text-align: left; vertical-align: top; }}
th {{ background: #f0f0f0; width: 150px; }}
pre {{ margin: 0; white-space: pre-wrap; word-wrap: break-word; font-size: 11px; }}
</style></head>
<body>
<h2>Exception Notification - WiseLink Labels</h2>
<table>
<tr><td><strong>Time (UTC)</strong></td><td>{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</td></tr>
<tr><td><strong>Type</strong></td><td>{exception.GetType().FullName}</td></tr>
<tr><td><strong>Message</strong></td><td>{enc(exception.Message)}</td></tr>
{sourceBlock}
{parametersBlock}
<tr><td><strong>Stack Trace</strong></td><td><pre>{stackTrace}</pre></td></tr>
{contextBlock}
{innerBlocks}
</table>
</body>
</html>";
        }

        private static string BuildFileLineText(string? fileName, int? lineNumber)
        {
            if (string.IsNullOrEmpty(fileName) && !lineNumber.HasValue)
                return "(unavailable)";

            if (string.IsNullOrEmpty(fileName))
                return lineNumber.HasValue ? $"(line {lineNumber})" : "(unavailable)";

            return lineNumber.HasValue ? $"{fileName} (line {lineNumber})" : fileName;
        }

        private static string BuildParametersBlock(
            IReadOnlyDictionary<string, object?>? parameters,
            Func<string?, string> enc)
        {
            if (parameters == null || parameters.Count == 0)
                return "";

            var rows = parameters.Select(p => $@"
<tr><td>{enc(p.Key)}</td><td><pre>{enc(FormatParameterValue(p.Value))}</pre></td></tr>");

            return $@"
<tr><td><strong>Parameters</strong></td><td>
    <table style=""width:100%; border-collapse:collapse;"">
        {string.Join(Environment.NewLine, rows)}
    </table>
</td></tr>";
        }

        private static (string? methodName, string? fileName, int? lineNumber) ExtractExceptionLocation(Exception exception)
        {
            try
            {
                var trace = new System.Diagnostics.StackTrace(exception, true);
                var frame = trace.GetFrames()?.FirstOrDefault(f => f.GetFileLineNumber() > 0)
                            ?? trace.GetFrame(0);
                if (frame == null) return (null, null, null);

                var method = frame.GetMethod();
                var methodName = method == null ? null : $"{method.DeclaringType?.FullName}.{method.Name}";
                var fileName = frame.GetFileName();
                var lineNumber = frame.GetFileLineNumber();

                return (methodName, fileName, lineNumber > 0 ? lineNumber : (int?)null);
            }
            catch
            {
                return (null, null, null);
            }
        }

        private static string FormatParameterValue(object? value)
        {
            if (value == null) return "(null)";

            if (value is string s) return s;

            if (value is System.Collections.IEnumerable enumerable)
            {
                var items = new List<string>();
                foreach (var item in enumerable)
                {
                    items.Add(FormatParameterValue(item));
                }

                return string.Join(", ", items);
            }

            return value.ToString() ?? "(null)";
        }

        private string GenerateEmailBody(string quoteId, string customerName)
        {
            var greeting = !string.IsNullOrWhiteSpace(customerName) ? $"Hello {customerName}," : "Hello,";
            
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f9f9f9; padding: 20px; }}
        .quote-id {{ background-color: #fff; padding: 15px; margin: 20px 0; border-left: 4px solid #007bff; }}
        .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Thank You for Your Quote Request</h1>
        </div>
        <div class=""content"">
            <p>{greeting}</p>
            <p>Thank you for requesting a quote from WiseLink Labels. We have successfully received your quote request and our team will review it shortly.</p>
            <div class=""quote-id"">
                <strong>Your Quote Reference:</strong><br>
                <span style=""font-size: 24px; font-weight: bold; color: #007bff;"">#{quoteId}</span>
            </div>
            <p>Please save this reference number for your records. We will contact you soon with pricing and additional details.</p>
            <p>If you have any questions, please don't hesitate to contact us.</p>
            <p>Best regards,<br>WiseLink Labels Team</p>
        </div>
        <div class=""footer"">
            <p>This is an automated confirmation email. Please do not reply to this message.</p>
        </div>
    </div>
</body>
</html>";
        }
    }
}

