namespace WiseLabels.Services
{
    public interface IEmailService
    {
        Task<bool> SendQuoteConfirmationAsync(string toEmail, string quoteId, string customerName);
        Task<bool> SendExceptionNotificationAsync(
            Exception exception,
            string? context = null,
            IReadOnlyDictionary<string, object?>? parameters = null);
    }
}

