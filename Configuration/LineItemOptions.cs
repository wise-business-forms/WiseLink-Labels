using System.Collections.Generic;

namespace WiseLabels.Configuration
{
    /// <summary>
    /// Configuration for the quote line item catalogue, bound from
    /// <c>QuoteOptions:LineItems</c>.
    /// </summary>
    public class LineItemOptions
    {
        /// <summary>Configuration section path.</summary>
        public const string SectionName = "QuoteOptions:LineItems";

        /// <summary>
        /// The <c>stdfpl__.ktrk_ref</c> that holds the standard (non customer-specific)
        /// price list. Empty string is the usual sentinel; confirm against the data.
        /// </summary>
        public string StandardCustomerRef { get; set; } = string.Empty;

        /// <summary>
        /// Which CERM language column set to read descriptions from - 1 selects
        /// <c>fkttxt11</c>/<c>fkttxt21</c>/<c>omsaant1</c>.
        /// </summary>
        public int LanguageIndex { get; set; } = 1;

        /// <summary>
        /// When true a customer's own price row wins over the standard list.
        /// </summary>
        public bool PreferCustomerPricing { get; set; } = true;

        /// <summary>
        /// Sentinel held in <c>stdfpl__.kolom_10</c> marking a price list row as visible
        /// to the quote web page - the equivalent of the <c>weblabel</c>/<c>rfqonw4l</c>
        /// flags the other CERM parameter tables carry but this one does not.
        /// Leave empty to use only the explicitly configured <see cref="Items"/>.
        /// </summary>
        public string WebFlagValue { get; set; } = string.Empty;

        /// <summary>Maximum quantity accepted for a single line item.</summary>
        public int MaxQuantity { get; set; } = 9999;

        /// <summary>Explicitly configured catalogue entries, keyed by CERM item ref.</summary>
        public List<LineItemDefinition> Items { get; set; } = new();
    }

    /// <summary>
    /// A configured catalogue entry: which CERM price list row it maps to, how it is
    /// labelled, and when it should be offered or pre-ticked.
    /// </summary>
    public class LineItemDefinition
    {
        /// <summary>Stable key used in code and in persisted quotes, e.g. "CustomDie".</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>CERM price list item reference (<c>stdfpl__.fpl__ref</c>).</summary>
        public string ItemRef { get; set; } = string.Empty;

        /// <summary>Optional display label overriding the CERM description. Display only.</summary>
        public string? Label { get; set; }

        /// <summary>Display order in the grid.</summary>
        public int Order { get; set; }

        /// <summary>Quantity proposed when the row is first ticked.</summary>
        public decimal DefaultQuantity { get; set; } = 1;

        /// <summary>Whether the user may change the quantity.</summary>
        public bool QuantityEditable { get; set; } = true;

        /// <summary>
        /// Conditions under which the row is offered at all. Empty means always offered -
        /// which is the default, and the fix for charges that used to be unreachable.
        /// </summary>
        public List<LineItemCondition> OfferWhen { get; set; } = new();

        /// <summary>
        /// Conditions under which the row is pre-ticked. The user can always untick it.
        /// </summary>
        public List<LineItemCondition> AutoSelectWhen { get; set; } = new();

        /// <summary>
        /// Conditions under which the row is mandatory: ticked and not untickable.
        /// </summary>
        public List<LineItemCondition> ForceWhen { get; set; } = new();
    }

    /// <summary>
    /// One applicability condition. All populated members must match for the condition
    /// to hold; a rule list matches if ANY of its conditions hold.
    /// </summary>
    /// <remarks>
    /// Conditions key on printing <em>IDs</em>, never on the printing label text - the
    /// previous implementation sniffed the label for "spot"/"process color", which broke
    /// as soon as a label was reworded.
    /// </remarks>
    public class LineItemCondition
    {
        /// <summary>Matches when the selected printing ID is in this list.</summary>
        public List<string>? PrintingIdIn { get; set; }

        /// <summary>Matches when the selected printing ID starts with one of these.</summary>
        public List<string>? PrintingIdPrefixIn { get; set; }

        /// <summary>Matches when the shape value/name is in this list.</summary>
        public List<string>? ShapeIn { get; set; }

        /// <summary>Matches when the corners value is NOT in this list.</summary>
        public List<string>? CornersNotIn { get; set; }

        /// <summary>Matches when "a custom die is required" equals this value.</summary>
        public bool? CustomDie { get; set; }

        /// <summary>Matches when "no existing die was selected" equals this value.</summary>
        public bool? NoExistingDie { get; set; }
    }
}
