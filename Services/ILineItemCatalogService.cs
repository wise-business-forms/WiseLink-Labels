using WiseLabels.Models;

namespace WiseLabels.Services
{
    /// <summary>
    /// Facts about a quote that decide which line items are offered and pre-ticked.
    /// </summary>
    /// <param name="CustomerId">CERM customer, used to pick customer-specific pricing.</param>
    /// <param name="PrintingId">Selected printing/colour code ID (never the label text).</param>
    /// <param name="ShapeValue">Selected shape value.</param>
    /// <param name="CornersValue">Selected corners value.</param>
    /// <param name="IsCustomDie">Whether dimension matching flagged a custom die.</param>
    /// <param name="HasExistingDie">Whether the user picked an existing die from the list.</param>
    public record LineItemContext(
        string? CustomerId = null,
        string? PrintingId = null,
        string? ShapeValue = null,
        string? CornersValue = null,
        bool IsCustomDie = false,
        bool HasExistingDie = false);

    /// <summary>
    /// Loads the selectable charge catalogue for a quote from the CERM standard price
    /// list, applies the configured labelling and applicability rules, and merges in any
    /// selections already made on the quote.
    /// </summary>
    public interface ILineItemCatalogService
    {
        /// <summary>
        /// Builds the catalogue for a quote. Never throws: a database failure yields an
        /// empty catalogue so the quote stays submittable, matching prior behaviour.
        /// </summary>
        /// <param name="context">Quote facts used to evaluate the rules.</param>
        /// <param name="existing">Selections already made, merged in by item ref/key.</param>
        Task<IReadOnlyList<QuoteLineItem>> GetCatalogAsync(
            LineItemContext context,
            IEnumerable<QuoteLineItem>? existing = null);

        /// <summary>
        /// Re-resolves posted line items against the catalogue: descriptions, units,
        /// prices and price bases come from CERM, and only selection and quantity are
        /// taken from the caller. Posted items with no catalogue match are dropped.
        /// </summary>
        /// <remarks>
        /// This is the trust boundary. The quote form is user-editable, so a posted
        /// price must never be believed.
        /// </remarks>
        Task<List<QuoteLineItem>> ResolvePostedAsync(
            LineItemContext context,
            IEnumerable<QuoteLineItem>? posted);
    }
}
