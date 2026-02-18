using System;
using System.Collections.Generic;
using System.Text.Json;

namespace WiseLabels.Models
{
    public class QuotePriceBreakdown
    {
        public int? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? TotalPrice { get; set; }
        public string? Currency { get; set; }
        public bool? ValidQuantity { get; set; }
        public string? ValidErrorCode { get; set; }
    }

    public static class QuotePriceBreakdownParser
    {
        public static IReadOnlyList<QuotePriceBreakdown> Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<QuotePriceBreakdown>();
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Data", out var dataElement))
                {
                    root = dataElement;
                }

                if (root.ValueKind != JsonValueKind.Array)
                {
                    return Array.Empty<QuotePriceBreakdown>();
                }

                var results = new List<QuotePriceBreakdown>();
                foreach (var item in root.EnumerateArray())
                {
                    var breakdown = new QuotePriceBreakdown
                    {
                        Quantity = item.TryGetProperty("Quantity", out var qtyEl) && qtyEl.TryGetInt32(out var qty)
                            ? qty
                            : null,
                        UnitPrice = item.TryGetProperty("UnitPrice", out var unitEl) && unitEl.TryGetDecimal(out var unit)
                            ? unit
                            : null,
                        TotalPrice = item.TryGetProperty("TotalPrice", out var totalEl) && totalEl.TryGetDecimal(out var total)
                            ? total
                            : null,
                        Currency = item.TryGetProperty("Currency", out var currencyEl)
                            ? currencyEl.GetString()
                            : null,
                        ValidQuantity = item.TryGetProperty("ValidQuantity", out var validEl)
                            ? validEl.GetBoolean()
                            : (bool?)null,
                        ValidErrorCode = item.TryGetProperty("ValidErrorCode", out var codeEl)
                            ? codeEl.GetString()
                            : null
                    };

                    results.Add(breakdown);
                }

                return results;
            }
            catch
            {
                return Array.Empty<QuotePriceBreakdown>();
            }
        }
    }

    public class QuickQuotePricingResult
    {
        public bool Success { get; init; }
        public string? ResponseJson { get; init; }
        public string? ErrorMessage { get; init; }
        public IReadOnlyList<QuotePriceBreakdown> Breakdown { get; init; } = Array.Empty<QuotePriceBreakdown>();
    }
}
