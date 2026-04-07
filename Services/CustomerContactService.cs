using CERM.DataAccess.Repositories.OrderConfirmation;
using Microsoft.Data.SqlClient;
using WiseLabels.Models;

namespace WiseLabels.Services
{
    /// <summary>
    /// Service for storing customer contact information in CERM database.
    /// </summary>
    public class CustomerContactService : ICustomerContactService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<CustomerContactService> _logger;
        private readonly IOrderConfirmationRepository _orderConfirmationRepository;

        public CustomerContactService(
            IConfiguration configuration, 
            ILogger<CustomerContactService> logger,
            IOrderConfirmationRepository orderConfirmationRepository)
        {
            _configuration = configuration;
            _logger = logger;
            _orderConfirmationRepository = orderConfirmationRepository;
        }

        /// <summary>
        /// Stores customer contact information from a quote request into CERM database.
        /// TODO: Map to actual CERM database table once table structure is known.
        /// </summary>
        public async Task<bool> StoreCustomerContactAsync(QuoteRequest quoteRequest, string? estimateId = null)
        {
            try
            {
                _logger.LogInformation(
                    "STUB: Would store customer contact - Name: {Name}, Email: {Email}, Phone: {Phone}, Company: {Company}, EstimateId: {EstimateId}",
                    quoteRequest.Name,
                    quoteRequest.Email,
                    quoteRequest.Phone,
                    quoteRequest.Company,
                    estimateId);

                // TODO: Uncomment and complete when table structure is known
                /*
                var connectionString = _configuration.GetConnectionString("CermDatabase");
                if (string.IsNullOrEmpty(connectionString))
                {
                    _logger.LogError("CermDatabase connection string not found");
                    return false;
                }

                var sql = @"
                    INSERT INTO [CERM_CONTACT_TABLE_NAME] 
                    (
                        [name_column],
                        [email_column],
                        [phone_column],
                        [company_column],
                        [estimate_id_column],
                        [created_date_column]
                    )
                    VALUES 
                    (
                        @Name,
                        @Email,
                        @Phone,
                        @Company,
                        @EstimateId,
                        GETDATE()
                    )";

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@Name", quoteRequest.Name ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Email", quoteRequest.Email ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Phone", quoteRequest.Phone ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Company", quoteRequest.Company ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@EstimateId", estimateId ?? (object)DBNull.Value);

                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        _logger.LogInformation(
                            "Customer contact stored successfully. Rows affected: {RowsAffected}",
                            rowsAffected);

                        return rowsAffected > 0;
                    }
                }
                */

                // For now, return true to not break the flow
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing customer contact information");
                return false;
            }
        }

        /// <summary>
        /// Updates existing customer contact information in CERM database.
        /// TODO: Map to actual CERM database table once table structure is known.
        /// </summary>
        public async Task<bool> UpdateCustomerContactAsync(string contactId, string name, string email, string phone, string? company = null)
        {
            try
            {
                _logger.LogInformation(
                    "STUB: Would update customer contact {ContactId} - Name: {Name}, Email: {Email}, Phone: {Phone}, Company: {Company}",
                    contactId,
                    name,
                    email,
                    phone,
                    company);

                // TODO: Uncomment and complete when table structure is known
                /*
                var connectionString = _configuration.GetConnectionString("CermDatabase");
                if (string.IsNullOrEmpty(connectionString))
                {
                    _logger.LogError("CermDatabase connection string not found");
                    return false;
                }

                var sql = @"
                    UPDATE [CERM_CONTACT_TABLE_NAME]
                    SET
                        [name_column] = @Name,
                        [email_column] = @Email,
                        [phone_column] = @Phone,
                        [company_column] = @Company,
                        [updated_date_column] = GETDATE()
                    WHERE
                        [contact_id_column] = @ContactId";

                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    using (var command = new SqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@ContactId", contactId);
                        command.Parameters.AddWithValue("@Name", name ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Email", email ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Phone", phone ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@Company", company ?? (object)DBNull.Value);

                        var rowsAffected = await command.ExecuteNonQueryAsync();

                        _logger.LogInformation(
                            "Customer contact updated successfully. Rows affected: {RowsAffected}",
                            rowsAffected);

                        return rowsAffected > 0;
                    }
                }
                */

                // For now, return true to not break the flow
                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer contact information for contact {ContactId}", contactId);
                return false;
            }
        }

        /// <summary>
        /// Updates the comment fields in the v1bon___ table with customer contact information.
        /// komment1: Full name and company (max 60 chars)
        /// komment2: Email and phone number (max 60 chars)
        /// komment3: Additional comments (max 60 chars)
        /// </summary>
        public async Task<bool> UpdateQuoteCommentsAsync(string quoteNumber, QuoteRequest quoteRequest)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(quoteNumber))
                {
                    _logger.LogWarning("Cannot update quote comments: quote number is empty");
                    return false;
                }

                // Log the incoming QuoteRequest values for debugging
                _logger.LogInformation(
                    "UpdateQuoteCommentsAsync called with QuoteNumber: {QuoteNumber}, Name: '{Name}', Company: '{Company}', Email: '{Email}', Phone: '{Phone}', Comments: '{Comments}'",
                    quoteNumber,
                    quoteRequest.Name ?? "(null)",
                    quoteRequest.Company ?? "(null)",
                    quoteRequest.Email ?? "(null)",
                    quoteRequest.Phone ?? "(null)",
                    quoteRequest.Comments ?? "(null)");

                // Format comment1: Full name and company (max 60 chars)
                var name = quoteRequest.Name ?? string.Empty;
                var company = quoteRequest.Company ?? string.Empty;
                var comment1 = name + (string.IsNullOrWhiteSpace(company) ? "" : " - " + company);
                if (comment1.Length > 60)
                    comment1 = comment1.Substring(0, 60);

                // Format comment2: Email and phone number (max 60 chars)
                var email = quoteRequest.Email ?? string.Empty;
                var phone = quoteRequest.Phone ?? string.Empty;
                var comment2 = email + (string.IsNullOrWhiteSpace(phone) ? "" : " | " + phone);
                if (comment2.Length > 60)
                    comment2 = comment2.Substring(0, 60);

                // Format comment3: Additional comments (max 60 chars)
                var comment3 = quoteRequest.Comments ?? string.Empty;
                if (comment3.Length > 60)
                    comment3 = comment3.Substring(0, 60);

                _logger.LogInformation(
                    "Formatted comments for quote {QuoteNumber} - Comment1: '{Comment1}' (length: {Len1}), Comment2: '{Comment2}' (length: {Len2}), Comment3: '{Comment3}' (length: {Len3})",
                    quoteNumber, comment1, comment1.Length, comment2, comment2.Length, comment3, comment3.Length);

                // Call the repository to update the comments
                var success = await _orderConfirmationRepository.UpdateCommentsAsync(
                    quoteNumber,
                    comment1,
                    comment2,
                    comment3);

                if (success)
                {
                    _logger.LogInformation("Successfully updated comments for quote {QuoteNumber}", quoteNumber);
                }
                else
                {
                    _logger.LogWarning("Failed to update comments for quote {QuoteNumber} - record not found", quoteNumber);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating quote comments for quote {QuoteNumber}", quoteNumber);
                return false;
            }
        }
    }
}
