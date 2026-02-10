using WiseLabels.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using System.Net.Http.Headers;

namespace WiseLabels.Services
{
    public interface IQuoteService
    {
        Task<string> StoreQuoteAsync(QuoteRequest quote);
        Task<(bool Success, string? CalculationId, string? EstimateId, string? ErrorMessage)> SubmitToCermApiAsync(QuoteRequest quote);
        CermApiPayload TransformToApiPayload(QuoteRequest quote);
        /// <summary>
        /// Updates contact info (name, email, phone) in the CERM database for the given estimate ID.
        /// Requires the estimate ID returned from CERM (e.g. from SubmitToCermApiAsync).
        /// </summary>
        Task<bool> UpdateContactInfoAsync(string estimateId, string name, string email, string phone);
    }

    public class QuoteService : IQuoteService
    {
        private readonly ILogger<QuoteService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ICermAuthService _cermAuthService;
        private readonly IEmailService _emailService;

        public QuoteService(ILogger<QuoteService> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory, ICermAuthService cermAuthService, IEmailService emailService)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _cermAuthService = cermAuthService;
            _emailService = emailService;
        }

        public async Task<string> StoreQuoteAsync(QuoteRequest quote)
        {
            // TODO: Implement database storage
            // This is a stub - replace with actual database implementation using Entity Framework or similar
            
            _logger.LogInformation("Storing quote in database (stubbed)");
            _logger.LogDebug("Quote data: {QuoteData}", JsonSerializer.Serialize(quote));
            
            // Simulate async database operation
            await Task.Delay(100);
            
            // In real implementation, this would:
            // 1. Create DbContext
            // 2. Add quote to database
            // 3. Save changes
            // 4. Return the generated quote ID
            
            var quoteId = Guid.NewGuid().ToString();
            _logger.LogInformation("Quote stored with ID: {QuoteId}", quoteId);
            
            return quoteId;
        }

        /// <summary>
        /// Builds CERM API payload JSON, including only properties with non-empty values.
        /// </summary>
        private string BuildCermPayloadJson(QuoteRequest quote)
        {
            _logger.LogDebug("Transforming quote to API payload. FinishValue: '{FinishValue}', Finish: '{Finish}'",
                quote.FinishValue ?? "null", quote.Finish ?? "null");

            var root = new JsonObject();

            // Required / always-present
            root["CustomerId"] = _configuration["Cerm:CustomerID"] ?? "108620";
            root["ContactId"] = _configuration["Cerm:ContactID"] ?? "001";
            root["WindingId"] = "10";
            root["PackingProcedureId"] = "152";
            root["PackingPriority"] = "Diameter";
            root["PackingNumber"] = 500;
            root["NumberOfProducts"] = 1;

            // Optional - add only when non-empty
            var outline = MapShapeToCermOutline(quote.ShapeValue, quote.Shape);
            AddIfNotEmpty(root, "Outline", outline);
            AddIfNotEmpty(root, "DieSizeId", quote.CuttingDieValue ?? quote.CuttingDie);
            AddIfNotEmpty(root, "SubstrateId", quote.MaterialValue ?? quote.Material);
            AddIfNotEmpty(root, "Description", quote.Description);

            // Dimensions - for Circle, Width and Height = Radius (diameter/2)
            var isCircle = string.Equals(outline, "Circle", StringComparison.OrdinalIgnoreCase);
            if (isCircle && TryParseDouble(quote.Diameter, out var circleDiameter))
            {
                var radius = circleDiameter / 2.0;
                root["Width"] = radius;
                root["Height"] = radius;
                root["Radius"] = radius;
            }
            else
            {
                if (TryParseDouble(quote.LabelWidth, out var width))
                    root["Width"] = width;
                if (TryParseDouble(quote.LabelHeight, out var height))
                    root["Height"] = height;
                if (TryParseDouble(quote.Diameter, out var diameter))
                    root["Radius"] = diameter / 2.0;
            }

            // PressRuns - only add properties with values
            var colourCodeId = NullIfEmpty(quote.Printing ?? quote.PrintingValue ?? quote.ColorCodeValue ?? quote.ColorCode);
            var finishValue = NullIfEmpty(quote.FinishValue ?? quote.Finish);
            var pressRun = new JsonObject();
            if (!string.IsNullOrEmpty(colourCodeId))
                pressRun["ColourCodeIdFront"] = colourCodeId;
            if (!string.IsNullOrEmpty(finishValue))
                pressRun["FinishingTypes"] = new JsonArray(finishValue);
            root["PressRuns"] = new JsonArray(pressRun);

            // Quantities (array, max 5)
            var quantityList = quote.Quantities?.Where(q => q >= 1).Take(5).ToList();
            if (quantityList == null || quantityList.Count == 0)
            {
                if (int.TryParse(quote.TotalQuantity?.Trim(), out var singleQty) && singleQty >= 1)
                    quantityList = new List<int> { singleQty };
            }
            if (quantityList != null && quantityList.Count > 0)
            {
                var qtyArray = new JsonArray();
                foreach (var q in quantityList) qtyArray.Add(q);
                root["Quantities"] = qtyArray;
            }

            return root.ToJsonString();
        }

        private static void AddIfNotEmpty(JsonObject obj, string key, string? value)
        {
            var v = NullIfEmpty(value);
            if (!string.IsNullOrEmpty(v))
                obj[key] = v;
        }

        public CermApiPayload TransformToApiPayload(QuoteRequest quote)
        {
            // Kept for compatibility; BuildCermPayloadJson is used for API submission.
            var outline = MapShapeToCermOutline(quote.ShapeValue, quote.Shape);
            var payload = new CermApiPayload
            {
                CustomerId = _configuration["Cerm:CustomerID"] ?? "108620",
                ContactId = _configuration["Cerm:ContactID"] ?? "001",
                WindingId = "10",
                Outline = outline,
                DieSizeId = NullIfEmpty(quote.CuttingDieValue ?? quote.CuttingDie),
                SubstrateId = NullIfEmpty(quote.MaterialValue ?? quote.Material),
                Description = quote.Description,
                PackingProcedureId = "152",
                PackingPriority = "Diameter",
                PackingNumber = 500,
                NumberOfProducts = 1,
                Width = TryParseDouble(quote.LabelWidth, out var width) ? width : null,
                Height = TryParseDouble(quote.LabelHeight, out var height) ? height : null,
                Radius = TryParseDouble(quote.Diameter, out var diameter) ? diameter / 2.0 : null,
                PressRuns = new List<PressRun>
                {
                    new PressRun
                    {
                        ColourCodeIdFront = quote.Printing ?? quote.PrintingValue ?? quote.ColorCodeValue ?? quote.ColorCode,
                        FinishingTypes = (!string.IsNullOrWhiteSpace(quote.FinishValue) || !string.IsNullOrWhiteSpace(quote.Finish))
                            ? new List<string> { quote.FinishValue ?? quote.Finish ?? string.Empty }
                            : new List<string>()
                    }
                },
                Quantities = quote.Quantities?.Where(q => q >= 1).Take(5).ToList()
                    ?? (int.TryParse(quote.TotalQuantity?.Trim(), out var qty) && qty >= 1 ? new List<int> { qty } : new List<int>())
            };
            return payload;
        }

        private static string? NullIfEmpty(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static bool TryParseDouble(string? value, out double result)
        {
            result = 0;
            return !string.IsNullOrWhiteSpace(value) && double.TryParse(value.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out result);
        }

        /// <summary>
        /// Maps form shape value/ID to CERM API Outline (shape name): Rectangle, Circle, Oval, Special.
        /// </summary>
        private static string? MapShapeToCermOutline(string? shapeValue, string? shape)
        {
            var v = (shapeValue ?? shape ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(v)) return null;

            // Already PascalCase (e.g. from form display label)
            if (v is "rectangle" or "circle" or "oval" or "special")
                return char.ToUpperInvariant(v[0]) + v[1..];

            // Shape ID from data-shape-id: 1=Rectangle, 3=Circle, 4=Oval, 5=Special
            return v switch
            {
                "1" => "Rectangle",
                "3" => "Circle",
                "4" => "Oval",
                "5" => "Special",
                _ => null
            };
        }

        public async Task<(bool Success, string? CalculationId, string? EstimateId, string? ErrorMessage)> SubmitToCermApiAsync(QuoteRequest quote)
        {
            _logger.LogInformation("Submitting quote to CERM API");
            _logger.LogDebug("Quote data: {QuoteData}", JsonSerializer.Serialize(quote));

            try
            {
                var payloadJson = BuildCermPayloadJson(quote);
                _logger.LogInformation("Transformed API payload: {Payload}", payloadJson);

                var baseUrl = _configuration["Cerm:BaseUrl"] ?? "https://brandmark-api.cerm.be/quote-api/v1";
                var calculationsUrl = $"{baseUrl.TrimEnd('/')}/calculations";

                var accessToken = await _cermAuthService.GetAccessTokenAsync();
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    _logger.LogError("CERM OAuth failed; cannot submit quote");
                    return (false, null, null, "Authentication failed. Unable to connect to the quote service.");
                }

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(60);
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json");
                using var response = await httpClient.PostAsync(calculationsUrl, content);

                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("CERM API returned {StatusCode}: {Body}", response.StatusCode, responseBody);
                    var errorMessage = $"{response.StatusCode}";
                    if (!string.IsNullOrWhiteSpace(responseBody))
                        errorMessage += ": " + (responseBody.Length > 500 ? responseBody[..500] + "…" : responseBody);

                    var ex = new HttpRequestException(
                        $"CERM API returned {(int)response.StatusCode} {response.StatusCode}. Response: {responseBody}");
                    var parameters = new Dictionary<string, object?>
                    {
                        ["Payload"] = payloadJson,
                        ["StatusCode"] = (int)response.StatusCode,
                        ["StatusReason"] = response.ReasonPhrase ?? "",
                        ["ResponseBody"] = responseBody,
                        ["RequestUrl"] = calculationsUrl
                    };
                    await _emailService.SendExceptionNotificationAsync(
                        ex,
                        "QuoteService.SubmitToCermApiAsync - CERM API returned non-success",
                        parameters);
                    return (false, null, null, errorMessage);
                }

                // Parse response: { "Data": { "Id": "129071", "EstimateId": "113230", ... } }
                var (calculationId, estimateId) = ParseCermCalculationResponse(responseBody);
                _logger.LogInformation("CERM API success. CalculationId={CalculationId}, EstimateId={EstimateId}", calculationId, estimateId);
                return (true, calculationId, estimateId, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting quote to CERM API");
                var parameters = new Dictionary<string, object?>
                {
                    ["Description"] = quote.Description,
                    ["PrintingValue"] = quote.PrintingValue,
                    ["MaterialValue"] = quote.MaterialValue,
                    ["Quantities"] = quote.Quantities ?? new List<int>()
                };
                try
                {
                    parameters["Payload"] = BuildCermPayloadJson(quote);
                }
                catch { /* ignore if payload build fails */ }
                await _emailService.SendExceptionNotificationAsync(
                    ex,
                    "QuoteService.SubmitToCermApiAsync - unhandled exception",
                    parameters);
                return (false, null, null, ex.Message);
            }
        }

        /// <summary>
        /// Parses the CERM calculations POST response to extract Calculation ID (Data.Id) and Estimate ID (Data.EstimateId).
        /// </summary>
        private static (string? CalculationId, string? EstimateId) ParseCermCalculationResponse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return (null, null);

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("Data", out var data))
                    return (null, null);

                string? calculationId = null;
                string? estimateId = null;

                if (data.TryGetProperty("Id", out var idProp))
                    calculationId = idProp.GetString()?.Trim();
                if (string.IsNullOrEmpty(calculationId) && data.TryGetProperty("Id", out var idLower))
                    calculationId = idLower.GetString()?.Trim();

                if (data.TryGetProperty("EstimateId", out var estIdProp))
                    estimateId = estIdProp.GetString()?.Trim();
                if (string.IsNullOrEmpty(estimateId) && data.TryGetProperty("estimateId", out var estIdLower))
                    estimateId = estIdLower.GetString()?.Trim();

                return (calculationId, estimateId);
            }
            catch (JsonException)
            {
                return (null, null);
            }
        }

        public async Task<bool> UpdateContactInfoAsync(string estimateId, string name, string email, string phone)
        {
            if (string.IsNullOrWhiteSpace(estimateId))
            {
                _logger.LogWarning("UpdateContactInfoAsync called with empty estimateId; skipping contact update");
                return false;
            }

            var connectionString = _configuration.GetConnectionString("CermDatabase");
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("CermDatabase connection string not configured");
                return false;
            }

            try
            {
                // Replace the SQL below with your UPDATE statement. Parameters: @estimateId, @name, @email, @phone
                var sql = @"UPDATE v1bon___ SET 
                        komment1 = @name, 
                        komment2 = @email, 
                        komment3 = @phone 
                    WHERE bon__ref = @estimateId";

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@estimateId", estimateId);
                command.Parameters.AddWithValue("@name", name ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@email", email ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@phone", phone ?? (object)DBNull.Value);

                int rowsAffected = await command.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                {
                    _logger.LogWarning("No records updated for estimate {EstimateId}", estimateId);
                    return false;
                }

                _logger.LogInformation("Contact info updated for estimate {EstimateId}", estimateId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating contact info for estimate {EstimateId}", estimateId);
                var parameters = new Dictionary<string, object?>
                {
                    ["EstimateId"] = estimateId,
                    ["Name"] = name,
                    ["Email"] = email,
                    ["Phone"] = phone
                };
                await _emailService.SendExceptionNotificationAsync(
                    ex,
                    "QuoteService.UpdateContactInfoAsync failure",
                    parameters);
                return false;
            }
        }
    }
}

