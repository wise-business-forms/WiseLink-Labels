using System;
using System.Collections.Generic;

namespace WiseLabels.Models
{
    /// <summary>
    /// The single source of truth for turning a CERM price basis, unit price and
    /// quantity into a line total. Used by the quote form, the Confirm and Success
    /// pages, the confirmation email, and (when enabled) the CERM invoice-line writer,
    /// so those surfaces cannot disagree.
    /// </summary>
    /// <remarks>
    /// Bases are the CERM "Price category" codes stored in <c>prys_srt</c>. Their order
    /// is documented in the CERM help (Parameters / Accounting parameters / Invoice
    /// price lines): Text, Discount/supplement, Fixed amount, /kg, /page, /piece, /100,
    /// /1.000, /100.000, /1.000 kg, /counter unit, /m, /m2 - i.e. zero-indexed, so
    /// /piece is '5' and /1.000 is '7'. This matches the arithmetic in
    /// CERM-SQL/common/sql/estimates/Quote_Letter_Prices_ByCalculationId.sql, which
    /// warns that confusing the two is "off by a factor of 1,000".
    ///
    /// Supported bases are an explicit WHITELIST. An unsupported basis is rejected when
    /// the catalogue is loaded and can therefore never reach a multiplication here.
    /// </remarks>
    public static class LineItemPricing
    {
        /// <summary>Free text; carries no price.</summary>
        public const string TextOnly = "0";
        /// <summary>A fixed amount, independent of quantity.</summary>
        public const string FixedAmount = "2";
        /// <summary>Price per piece.</summary>
        public const string PerPiece = "5";
        /// <summary>Price per 100.</summary>
        public const string Per100 = "6";
        /// <summary>Price per 1,000.</summary>
        public const string Per1000 = "7";
        /// <summary>Price per 100,000.</summary>
        public const string Per100000 = "8";

        private static readonly HashSet<string> Supported = new(StringComparer.Ordinal)
        {
            TextOnly, FixedAmount, PerPiece, Per100, Per1000, Per100000
        };

        /// <summary>
        /// Whether a price basis can be priced by <see cref="Total"/>. Bases that depend
        /// on data the quote form does not hold - '1' (percentage over other lines),
        /// '3' (/kg), '4' (/page), '9' (/1.000 kg) and the letter codes - return false
        /// and are dropped at catalogue load.
        /// </summary>
        public static bool IsSupportedBasis(string? priceBasis) =>
            !string.IsNullOrWhiteSpace(priceBasis) && Supported.Contains(priceBasis.Trim());

        /// <summary>
        /// Computes the line total for a price basis, unit price and quantity.
        /// Returns zero for an unsupported basis rather than guessing a multiplier.
        /// </summary>
        public static decimal Total(string? priceBasis, decimal unitPrice, decimal quantity)
        {
            var basis = (priceBasis ?? string.Empty).Trim();
            return basis switch
            {
                TextOnly => 0m,
                FixedAmount => unitPrice,
                PerPiece => unitPrice * quantity,
                Per100 => unitPrice * quantity / 100m,
                Per1000 => unitPrice * quantity / 1000m,
                Per100000 => unitPrice * quantity / 100000m,
                _ => 0m
            };
        }

        /// <summary>Sums the totals of the selected line items.</summary>
        public static decimal TotalOf(IEnumerable<QuoteLineItem>? items)
        {
            if (items == null) return 0m;
            var sum = 0m;
            foreach (var item in items)
            {
                if (item.Selected)
                {
                    sum += Total(item.PriceBasis, item.UnitPrice, item.Quantity);
                }
            }
            return sum;
        }

        /// <summary>Whether the basis prices per unit (so a quantity is meaningful).</summary>
        public static bool IsQuantityBased(string? priceBasis)
        {
            var basis = (priceBasis ?? string.Empty).Trim();
            return basis is PerPiece or Per100 or Per1000 or Per100000;
        }

        /// <summary>Short human label for the basis, for the "Units" column.</summary>
        public static string BasisLabel(string? priceBasis) =>
            (priceBasis ?? string.Empty).Trim() switch
            {
                TextOnly => "",
                FixedAmount => "fixed",
                PerPiece => "each",
                Per100 => "per 100",
                Per1000 => "per 1,000",
                Per100000 => "per 100,000",
                _ => ""
            };
    }
}
