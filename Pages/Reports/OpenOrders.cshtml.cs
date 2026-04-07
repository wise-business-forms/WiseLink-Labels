using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using WiseLabels.Models;
using WiseLabels.Services;

namespace WiseLabels.Pages.Reports
{
    [Authorize]
    public class OpenOrdersModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<OpenOrdersModel> _logger;
        private readonly IUserImpersonationService _impersonationService;

        public OpenOrdersModel(
            IConfiguration configuration, 
            ILogger<OpenOrdersModel> logger,
            IUserImpersonationService impersonationService)
        {
            _configuration = configuration;
            _logger = logger;
            _impersonationService = impersonationService;
        }

        public List<OpenOrder> Orders { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public bool IsFiltered { get; set; }
        public string? FilteredByUserId { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                // Support both connection string names; Development uses "CermDbConnection", production uses "CermDatabase"
                var connectionString = _configuration.GetConnectionString("CermDbConnection")
                    ?? _configuration.GetConnectionString("CermDatabase");

                if (string.IsNullOrEmpty(connectionString))
                {
                    ErrorMessage = "Database connection not configured.";
                    _logger.LogError("No CermDbConnection or CermDatabase connection string found in configuration");
                    return;
                }

                // Determine if we should filter data
                var shouldFilter = await _impersonationService.ShouldFilterDataAsync();
                var effectiveUserId = await _impersonationService.GetEffectiveUserIdAsync();

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Build the query with optional filtering
                var query = @"
                    SELECT 
                        k.kla__rpn AS CustomerID,
                        k.naam____ AS CustomerName,
                        o.ord_begl AS OrderedBy,
                        '' AS CustomerOrderId,
                        '' AS Site,
                        o.best_dat AS OrderDate, 
                        o.leverdat AS OrderExpected,
                        o.off__dat AS ExpectedDate,
                        o.oplage__ AS OrderedUnit,
                        o.omschr__ AS ProductDescription,
                        vb.voorz_bd AS ExpectedAmount,
                        '' AS SHIPPED,
                        '' AS PRODUCTION,
                        '' AS INSTOCK,
                        o.bon__ref AS BonRef,
                        o.ord__ref AS OrderRef
                    FROM klabas__ k
                    JOIN order___ o ON o.kla__ref = k.kla__ref
                    JOIN v1bon___ vb ON vb.bon__ref = o.bon__ref
                    JOIN ordtoe__ ot ON ot.ord__ref = o.ord__ref 
                    WHERE ot.toestand IN ('4')
                        AND k.naam____ NOT LIKE '#%'
                        AND k.naam____ NOT LIKE '*%'
                        AND k.naam____ NOT LIKE '-%'
                        AND k.kla__rpn NOT LIKE '#%'
                        AND k.kla__rpn NOT LIKE '*%'
                        AND k.kla__rpn NOT LIKE '-%'";

                // Apply filtering if needed (user is not admin, or admin is impersonating)
                if (shouldFilter && !string.IsNullOrWhiteSpace(effectiveUserId))
                {
                    query += " AND k.vrt__ref = @RepresentativeId";
                    IsFiltered = true;
                    FilteredByUserId = effectiveUserId;
                    _logger.LogInformation("Filtering open orders for representative {RepId}", effectiveUserId);
                }
                else
                {
                    IsFiltered = false;
                    _logger.LogInformation("Showing all open orders (admin with no impersonation)");
                }

                query += " ORDER BY o.best_dat ASC";

                using var command = new SqlCommand(query, connection);
                command.CommandTimeout = 60; // 60 seconds timeout

                // Add parameter if filtering
                if (shouldFilter && !string.IsNullOrWhiteSpace(effectiveUserId))
                {
                    command.Parameters.AddWithValue("@RepresentativeId", effectiveUserId);
                }

                using var reader = await command.ExecuteReaderAsync();

                // Dictionary to store orders temporarily
                var orderDict = new Dictionary<string, OpenOrder>();

                while (await reader.ReadAsync())
                {
                    var bonRef = reader["BonRef"]?.ToString();
                    var orderRef = reader["OrderRef"]?.ToString();

                    if (string.IsNullOrEmpty(bonRef)) continue;

                    if (!orderDict.ContainsKey(bonRef))
                    {
                        orderDict[bonRef] = new OpenOrder
                        {
                            CustomerID = reader["CustomerID"]?.ToString(),
                            CustomerName = reader["CustomerName"]?.ToString(),
                            OrderedBy = reader["OrderedBy"]?.ToString(),
                            CustomerOrderId = reader["CustomerOrderId"]?.ToString(),
                            Site = reader["Site"]?.ToString(),
                            OrderDate = reader["OrderDate"] as DateTime?,
                            OrderExpected = reader["OrderExpected"] as DateTime?,
                            ExpectedDate = reader["ExpectedDate"] as DateTime?,
                            OrderedUnit = reader["OrderedUnit"] as int?,
                            ProductDescription = reader["ProductDescription"]?.ToString(),
                            ExpectedAmount = reader["ExpectedAmount"] as decimal?,
                            SHIPPED = reader["SHIPPED"]?.ToString(),
                            PRODUCTION = reader["PRODUCTION"]?.ToString(),
                            INSTOCK = reader["INSTOCK"]?.ToString(),
                            BonRef = bonRef
                        };
                    }
                }

                reader.Close();

                // Now fetch statuses for all orders
                if (orderDict.Any())
                {
                    var orderRefs = string.Join(",", orderDict.Values.Select(o => $"'{o.BonRef}'"));

                    var statusQuery = @"
                        WITH OrderStatuses AS (
                            SELECT 
                                o.bon__ref AS BonRef,
                                o.tstval01 AS StatusVal, 1 AS Position FROM order___ o WHERE o.bon__ref IN (" + orderRefs + @") AND o.tstval01 IS NOT NULL
                            UNION ALL SELECT o.bon__ref, o.tstval02, 2 FROM order___ o WHERE o.bon__ref IN (" + orderRefs + @") AND o.tstval02 IS NOT NULL
                            UNION ALL SELECT o.bon__ref, o.tstval03, 3 FROM order___ o WHERE o.bon__ref IN (" + orderRefs + @") AND o.tstval03 IS NOT NULL
                            UNION ALL SELECT o.bon__ref, o.tstval04, 4 FROM order___ o WHERE o.bon__ref IN (" + orderRefs + @") AND o.tstval04 IS NOT NULL
                            UNION ALL SELECT o.bon__ref, o.tstval05, 5 FROM order___ o WHERE o.bon__ref IN (" + orderRefs + @") AND o.tstval05 IS NOT NULL
                            UNION ALL SELECT o.bon__ref, o.tstval06, 6 FROM order___ o WHERE o.bon__ref IN (" + orderRefs + @") AND o.tstval06 IS NOT NULL
                            UNION ALL SELECT o.bon__ref, o.tstval07, 7 FROM order___ o WHERE o.bon__ref IN (" + orderRefs + @") AND o.tstval07 IS NOT NULL
                            UNION ALL SELECT o.bon__ref, o.tstval08, 8 FROM order___ o WHERE o.bon__ref IN (" + orderRefs + @") AND o.tstval08 IS NOT NULL
                            UNION ALL SELECT o.bon__ref, o.tstval09, 9 FROM order___ o WHERE o.bon__ref IN (" + orderRefs + @") AND o.tstval09 IS NOT NULL
                            UNION ALL SELECT o.bon__ref, o.tstval10, 10 FROM order___ o WHERE o.bon__ref IN (" + orderRefs + @") AND o.tstval10 IS NOT NULL
                            UNION ALL SELECT o.bon__ref, o.tstval11, 11 FROM order___ o WHERE o.bon__ref IN (" + orderRefs + @") AND o.tstval11 IS NOT NULL
                            UNION ALL SELECT o.bon__ref, o.tstval12, 12 FROM order___ o WHERE o.bon__ref IN (" + orderRefs + @") AND o.tstval12 IS NOT NULL
                            UNION ALL SELECT o.bon__ref, o.tstval13, 13 FROM order___ o WHERE o.bon__ref IN (" + orderRefs + @") AND o.tstval13 IS NOT NULL
                            UNION ALL SELECT o.bon__ref, o.tstval14, 14 FROM order___ o WHERE o.bon__ref IN (" + orderRefs + @") AND o.tstval14 IS NOT NULL
                            UNION ALL SELECT o.bon__ref, o.tstval15, 15 FROM order___ o WHERE o.bon__ref IN (" + orderRefs + @") AND o.tstval15 IS NOT NULL
                            UNION ALL SELECT o.bon__ref, o.tstval16, 16 FROM order___ o WHERE o.bon__ref IN (" + orderRefs + @") AND o.tstval16 IS NOT NULL
                            UNION ALL SELECT o.bon__ref, o.tstval17, 17 FROM order___ o WHERE o.bon__ref IN (" + orderRefs + @") AND o.tstval17 IS NOT NULL
                            UNION ALL SELECT o.bon__ref, o.tstval18, 18 FROM order___ o WHERE o.bon__ref IN (" + orderRefs + @") AND o.tstval18 IS NOT NULL
                            UNION ALL SELECT o.bon__ref, o.tstval19, 19 FROM order___ o WHERE o.bon__ref IN (" + orderRefs + @") AND o.tstval19 IS NOT NULL
                            UNION ALL SELECT o.bon__ref, o.tstval20, 20 FROM order___ o WHERE o.bon__ref IN (" + orderRefs + @") AND o.tstval20 IS NOT NULL
                        )
                        SELECT 
                            os.BonRef,
                            os.Position,
                            t.omschr__ AS Status
                        FROM OrderStatuses os
                        LEFT JOIN tstval__ t ON t.tstd_ref = os.StatusVal 
                            AND t.tabname_ = 'order___'
                        WHERE t.omschr__ IS NOT NULL
                        ORDER BY os.BonRef, os.Position";

                    using var statusCommand = new SqlCommand(statusQuery, connection);
                    statusCommand.CommandTimeout = 60;

                    using var statusReader = await statusCommand.ExecuteReaderAsync();

                    while (await statusReader.ReadAsync())
                    {
                        var bonRef = statusReader["BonRef"]?.ToString();
                        var status = statusReader["Status"]?.ToString();

                        if (!string.IsNullOrEmpty(bonRef) && !string.IsNullOrEmpty(status) && orderDict.ContainsKey(bonRef))
                        {
                            orderDict[bonRef].Statuses.Add(status);
                        }
                    }
                }

                Orders = orderDict.Values.ToList();

                _logger.LogInformation("Loaded {Count} open orders. Filtered: {IsFiltered}", Orders.Count, IsFiltered);
            }
            catch (Exception ex)
            {
                ErrorMessage = "An error occurred while loading the open orders.";
                _logger.LogError(ex, "Error loading open orders");
            }
        }
    }
}
