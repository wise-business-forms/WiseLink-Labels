using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Configuration;
using WiseLabels.Models;

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



        public async Task<bool> SendQuoteConfirmationAsync(
            string toEmail,
            string quoteId,
            string customerName,
            QuoteRequest quoteDetails,
            IReadOnlyList<QuotePriceBreakdown> priceBreakdown)
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

                if (!string.IsNullOrEmpty(smtpUsername) && !string.IsNullOrEmpty(smtpPassword))
                {
                    client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
                }

                var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = $"Quote Request Confirmation - #{quoteId}",
                    Body = GenerateEmailBody(quoteId, customerName, quoteDetails, priceBreakdown),
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

        private string GenerateEmailBody(
            string quoteId,
            string customerName,
            QuoteRequest quote,
            IReadOnlyList<QuotePriceBreakdown> priceBreakdown)
        {
            var greeting = !string.IsNullOrWhiteSpace(customerName) ? $"Hello {WebUtility.HtmlEncode(customerName)}," : "Hello,";
            var contactTable = BuildDefinitionTable(GetContactRows(quote));
            var detailTable = BuildDefinitionTable(GetDetailRows(quote));
            var pricingTable = BuildPricingTable(priceBreakdown);

            var sb = new StringBuilder();
            sb.Append("""
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <style>
        body { font-family: Arial, sans-serif; color: #333; margin: 0; padding: 24px; background-color: #f4f6f8; }
        .container { max-width: 720px; margin: 0 auto; background: #fff; border-radius: 8px; box-shadow: 0 6px 18px rgba(0,0,0,.06); overflow: hidden; }
        .header { background-color: #0b5ed7; color: #fff; padding: 24px; }
        .header h1 { margin: 0; font-size: 22px; }
        .content { padding: 24px; line-height: 1.6; }
        .quote-id { font-size: 26px; font-weight: 700; color: #0b5ed7; }
        .section { margin-top: 24px; }
        .section h3 { margin: 0 0 12px; font-size: 18px; color: #0b5ed7; }
        table.data-table { width: 100%; border-collapse: collapse; border: 1px solid #dee2e6; }
        table.data-table th { text-align: left; width: 35%; padding: 10px; background: #f8f9fa; border-right: 1px solid #dee2e6; }
        table.data-table td { padding: 10px; border-top: 1px solid #dee2e6; }
        table.data-table tr:first-child td { border-top: none; }
        table.price-table th, table.price-table td { border: 1px solid #dee2e6; padding: 10px; text-align: left; }
        table.price-table th { background: #f8f9fa; }
        .pre { white-space: pre-wrap; }
        .footer { padding: 16px 24px 24px; font-size: 12px; color: #6c757d; text-align: center; }
    </style>
</head>
<body>
    <div class="container">
        <div class="header">
            <h1>Quote Summary</h1>
            <div>Reference #<span class="quote-id">
""");
            sb.Append(WebUtility.HtmlEncode(quoteId));
            sb.Append("""
</span></div>
        </div>
        <div class="content">
            <p>
""");
            sb.Append(greeting);
            sb.Append("""
</p>
            <p>Thank you for your submission. Below is a copy of the information you reviewed on the confirmation page.</p>
""");

            if (!string.IsNullOrEmpty(contactTable))
            {
                sb.Append(@"<div class=""section""><h3>Contact Information</h3>" + contactTable + "</div>");
            }

            if (!string.IsNullOrEmpty(detailTable))
            {
                sb.Append(@"<div class=""section""><h3>Quote Details</h3>" + detailTable + "</div>");
            }

            if (!string.IsNullOrEmpty(pricingTable))
            {
                sb.Append(@"<div class=""section""><h3>Pricing Preview</h3>" + pricingTable + "</div>");
            }

            sb.Append("""
            <p>If anything looks incorrect, simply reply to this email and we'll help you make adjustments.</p>
        </div>
        <div class="footer">
            Sent automatically by WiseLink Labels • Please keep this email for your records.
        </div>
    </div>
</body>
</html>
""");
            return sb.ToString();
        }

        private static string BuildDefinitionTable(IEnumerable<(string Label, string? Value, bool PreserveWhitespace)> rows)
        {
            var filtered = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Value))
                .ToList();

            if (filtered.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.Append("<table class=\"data-table\"><tbody>");
            foreach (var (label, value, preserveWhitespace) in filtered)
            {
                var encodedValue = WebUtility.HtmlEncode(value);
                if (preserveWhitespace)
                {
                    encodedValue = encodedValue?.Replace("\r\n", "\n").Replace("\n", "<br />");
                }

                sb.Append("<tr><th>")
                  .Append(WebUtility.HtmlEncode(label))
                  .Append("</th><td")
                  .Append(preserveWhitespace ? " class=\"pre\"" : string.Empty)
                  .Append(">")
                  .Append(encodedValue)
                  .Append("</td></tr>");
            }

            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        private static string BuildPricingTable(IReadOnlyList<QuotePriceBreakdown> priceBreakdown)
        {
            if (priceBreakdown == null || priceBreakdown.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.Append("""
<table class="price-table" cellspacing="0" cellpadding="0">
    <thead>
        <tr>
            <th>Quantity</th>
            <th>Unit Price</th>
            <th>Total Price</th>
            <th>Currency</th>
        </tr>
    </thead>
    <tbody>
""");

            foreach (var price in priceBreakdown)
            {
                sb.Append("<tr>")
                  .Append("<td>").Append(price.Quantity?.ToString("N0", CultureInfo.CurrentCulture) ?? "-").Append("</td>")
                  .Append("<td>").Append(price.UnitPrice.HasValue ? price.UnitPrice.Value.ToString("C2", CultureInfo.CurrentCulture) : "-").Append("</td>")
                  .Append("<td>").Append(price.TotalPrice.HasValue ? price.TotalPrice.Value.ToString("C2", CultureInfo.CurrentCulture) : "-").Append("</td>")
                  .Append("<td>").Append(WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(price.Currency) ? "USD" : price.Currency)).Append("</td>")
                  .Append("</tr>");
            }

            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        private static IEnumerable<(string Label, string? Value, bool PreserveWhitespace)> GetContactRows(QuoteRequest quote) =>
            new List<(string, string?, bool)>
            {
                ("Name", quote.Name, false),
                ("Company", quote.Company, false),
                ("Email", quote.Email, false),
                ("Phone", quote.Phone, false),
                ("Comments", quote.Comments, true)
            };

        private static IEnumerable<(string Label, string? Value, bool PreserveWhitespace)> GetDetailRows(QuoteRequest quote)
        {
            string? formattedShape = string.IsNullOrWhiteSpace(quote.Shape)
                ? null
                : char.ToUpper(quote.Shape[0], CultureInfo.InvariantCulture) + quote.Shape[1..];

            string? dimensions = !string.IsNullOrWhiteSpace(quote.Diameter)
                ? $"{quote.Diameter}\""
                : (!string.IsNullOrWhiteSpace(quote.LabelWidth) || !string.IsNullOrWhiteSpace(quote.LabelHeight))
                    ? $"{quote.LabelWidth ?? "—"}\" × {quote.LabelHeight ?? "—"}\""
                    : null;

            string? quantities = quote.Quantities?.Count > 0
                ? string.Join(" / ", quote.Quantities)
                : quote.TotalQuantity;

            return new List<(string, string?, bool)>
            {
                ("Estimate ID", quote.EstimateId, false),
                (FormatReferenceType(quote.ReferenceType), quote.ReferenceValue, false),
                ("Description", quote.Description, false),
                ("Shape", formattedShape, false),
                ("Size", dimensions, false),
                ("Corners", quote.Corners, false),
                ("Cutting Die", quote.CuttingDie, false),
                ("Printing", quote.Printing, false),
                ("Material", quote.Material, false),
                ("Color Code", quote.ColorCode, false),
                ("Finish", quote.Finish, false),
                ("Application Method", quote.ApplicationMethod, false),
                ("Unwind Direction", quote.UnwindDirection, false),
                ("Quantities", quantities, false),
                ("Artwork", quote.ArtworkOption, false)
            };
        }

        private static string FormatReferenceType(string? referenceType) =>
            referenceType switch
            {
                "company-name" => "Company Name",
                "account-number" => "Account Number",
                "purchase-order-number" => "Purchase Order Number",
                "invoice-number" => "Invoice Number",
                _ => "Reference"
            };

    }
}

