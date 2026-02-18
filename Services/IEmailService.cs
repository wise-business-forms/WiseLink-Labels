using System.Collections.Generic;
using WiseLabels.Models;

namespace WiseLabels.Services
{
    public interface IEmailService
    {
        Task<bool> SendQuoteConfirmationAsync(string toEmail, string quoteId, string customerName, QuoteRequest quoteDetails, IReadOnlyList<QuotePriceBreakdown> priceBreakdown);
        Task<bool> SendExceptionNotificationAsync(
            Exception exception,
            string? context = null,
            IReadOnlyDictionary<string, object?>? parameters = null);
        Task<bool> SendCustomEmailAsync(string toEmail, string subject, string htmlBody);
    }
}

