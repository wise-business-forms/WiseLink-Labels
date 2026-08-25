using System.Text.Json.Serialization;

namespace WiseLabels.Models
{
    /// <summary>
    /// Where a line item on a quote came from. Mirrors the "Kind" column on the
    /// CERM desktop's invoice-lines grid.
    /// </summary>
    public enum LineItemSource
    {
        /// <summary>Proposed by an applicability rule (the common case).</summary>
        Rule = 0,
        /// <summary>Came from the customer's own price list in CERM.</summary>
        Customer = 1,
        /// <summary>Ticked by the user even though no rule proposed it.</summary>
        Manual = 2
    }

    /// <summary>
    /// One selectable charge on a quote, sourced from the CERM standard price list
    /// (<c>stdfpl__</c>) and destined - once selected - for a CERM calculation
    /// invoice line (<c>v1facl__</c>).
    /// </summary>
    /// <remarks>
    /// There is deliberately no <c>Total</c> property. System.Text.Json serializes
    /// get-only properties, which would persist a value that is ignored on read and
    /// then drift from the recomputed one. Use <see cref="LineItemPricing.Total"/>.
    /// </remarks>
    public class QuoteLineItem
    {
        /// <summary>CERM price list item reference (<c>stdfpl__.fpl__ref</c>).</summary>
        public string ItemRef { get; set; } = string.Empty;

        /// <summary>The <c>ktrk_ref</c> the price row was read from, for traceability.</summary>
        public string? CustomerRef { get; set; }

        /// <summary>Stable configuration key, e.g. "CustomDie". Independent of the item ref.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>First description line (<c>fkttxt1{lang}</c>).</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Second description line (<c>fkttxt2{lang}</c>). CERM treats the two as two
        /// LINES of one description and joins them with a newline on the quote letter.
        /// </summary>
        public string? Description2 { get; set; }

        /// <summary>Unit text (<c>omsaant{lang}</c>), e.g. "each".</summary>
        public string Unit { get; set; } = "each";

        /// <summary>Unit price excluding tax (<c>prijs_bm</c>), captured when the quote was priced.</summary>
        public decimal UnitPrice { get; set; }

        /// <summary>Quantity the charge is billed for. Maps to <c>v1facl__.f_aantal</c>.</summary>
        public decimal Quantity { get; set; } = 1;

        /// <summary>Whether this charge is included on the quote.</summary>
        public bool Selected { get; set; }

        /// <summary>
        /// True when the charge is mandatory for this quote and cannot be unticked
        /// (currently: the die charge when no existing die was chosen).
        /// </summary>
        public bool Forced { get; set; }

        /// <summary>Whether the quantity is user-editable, or pinned at <see cref="Quantity"/>.</summary>
        public bool QuantityEditable { get; set; } = true;

        /// <summary>CERM price category (<c>prys_srt</c>). See <see cref="LineItemPricing"/>.</summary>
        public string PriceBasis { get; set; } = LineItemPricing.PerPiece;

        /// <summary>Currency of <see cref="UnitPrice"/> (<c>munt_ref</c>).</summary>
        public string? Currency { get; set; }

        /// <summary>How this item came to be on the quote.</summary>
        public LineItemSource Source { get; set; } = LineItemSource.Rule;

        /// <summary>Display order within the grid.</summary>
        public int Order { get; set; }

        /// <summary>Convenience for views; not serialized.</summary>
        [JsonIgnore]
        public decimal Total => LineItemPricing.Total(PriceBasis, UnitPrice, Quantity);
    }
}
