using WiseLabels.Models;

namespace WiseLabels.Services
{
    /// <summary>
    /// Service for storing and managing customer contact information in CERM database
    /// </summary>
    public interface ICustomerContactService
    {
        /// <summary>
        /// Stores customer contact information from a quote request into CERM database
        /// </summary>
        /// <param name="quoteRequest">Quote request containing customer contact details</param>
        /// <param name="estimateId">Optional estimate ID to associate with the contact</param>
        /// <returns>True if successfully stored, false otherwise</returns>
        Task<bool> StoreCustomerContactAsync(QuoteRequest quoteRequest, string? estimateId = null);

        /// <summary>
        /// Updates existing customer contact information in CERM database
        /// </summary>
        /// <param name="contactId">Existing contact ID</param>
        /// <param name="name">Contact name</param>
        /// <param name="email">Contact email</param>
        /// <param name="phone">Contact phone</param>
        /// <param name="company">Contact company</param>
        /// <returns>True if successfully updated, false otherwise</returns>
        Task<bool> UpdateCustomerContactAsync(string contactId, string name, string email, string phone, string? company = null);

        /// <summary>
        /// Updates the comment fields in the v1bon___ table with customer contact information
        /// </summary>
        /// <param name="quoteNumber">The quote number (bon__ref)</param>
        /// <param name="quoteRequest">Quote request containing customer contact details</param>
        /// <returns>True if successfully updated, false otherwise</returns>
        Task<bool> UpdateQuoteCommentsAsync(string quoteNumber, QuoteRequest quoteRequest);
    }
}
