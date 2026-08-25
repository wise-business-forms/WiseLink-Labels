using CERM.DataAccess.Models;
using CERM.DataAccess.Repositories.PriceList;
using Microsoft.Extensions.Options;
using WiseLabels.Configuration;
using WiseLabels.Models;

namespace WiseLabels.Services
{
    /// <inheritdoc cref="ILineItemCatalogService"/>
    public class LineItemCatalogService : ILineItemCatalogService
    {
        private readonly IPriceListItemRepository _priceListRepository;
        private readonly LineItemOptions _options;
        private readonly ILogger<LineItemCatalogService> _logger;

        /// <summary>Creates the service.</summary>
        public LineItemCatalogService(
            IPriceListItemRepository priceListRepository,
            IOptions<LineItemOptions> options,
            ILogger<LineItemCatalogService> logger)
        {
            _priceListRepository = priceListRepository;
            _options = options.Value;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<QuoteLineItem>> GetCatalogAsync(
            LineItemContext context,
            IEnumerable<QuoteLineItem>? existing = null)
        {
            var priceRows = await LoadPriceRowsAsync(context.CustomerId);
            var catalog = new List<QuoteLineItem>();

            // 1. Configured entries, in configured order.
            foreach (var definition in _options.Items.OrderBy(i => i.Order))
            {
                if (!priceRows.TryGetValue(definition.ItemRef ?? string.Empty, out var row))
                {
                    _logger.LogWarning(
                        "Line item {Key} is configured with item ref {ItemRef} but no active row was found in stdfpl__; it will not be offered.",
                        definition.Key, definition.ItemRef);
                    continue;
                }

                var item = Project(row, definition);
                if (item == null) continue;

                if (!Matches(definition.OfferWhen, context, offeredByDefault: true)) continue;

                item.Forced = Matches(definition.ForceWhen, context, offeredByDefault: false);
                item.Selected = item.Forced || Matches(definition.AutoSelectWhen, context, offeredByDefault: false);
                if (item.Forced) item.QuantityEditable = definition.QuantityEditable;

                catalog.Add(item);
            }

            // 2. Anything else the CERM price list flags for the web quote, so the
            //    catalogue can be extended from CERM without a code change.
            var configuredRefs = new HashSet<string>(
                _options.Items.Select(i => i.ItemRef ?? string.Empty), StringComparer.OrdinalIgnoreCase);

            var nextOrder = catalog.Count == 0 ? 0 : catalog.Max(i => i.Order);
            var prioritized = priceRows.Values
                .Where(r => !configuredRefs.Contains(r.ItemRef?.Trim() ?? string.Empty))
                .OrderBy(r => (r.Priority ?? string.Empty).Trim(), StringComparer.Ordinal)
                .ThenBy(r => r.ItemRef, StringComparer.Ordinal);

            foreach (var row in prioritized)
            {
                var item = Project(row, definition: null);
                if (item == null) continue;

                // Sorted behind the configured entries, in Priority order.
                item.Order = ++nextOrder + 1000;
                catalog.Add(item);
            }

            MergeExisting(catalog, existing);
            return catalog;
        }

        /// <inheritdoc />
        public async Task<List<QuoteLineItem>> ResolvePostedAsync(
            LineItemContext context,
            IEnumerable<QuoteLineItem>? posted)
        {
            var catalog = await GetCatalogAsync(context);
            if (catalog.Count == 0) return new List<QuoteLineItem>();

            var postedList = posted?.ToList() ?? new List<QuoteLineItem>();
            var resolved = new List<QuoteLineItem>();

            foreach (var item in catalog)
            {
                var match = postedList.FirstOrDefault(p => IsSameItem(p, item));

                // Only selection and quantity are ever taken from the client. Everything
                // that affects price comes back from CERM.
                if (match != null)
                {
                    item.Selected = match.Selected;
                    if (item.QuantityEditable)
                    {
                        item.Quantity = ClampQuantity(match.Quantity);
                    }
                    if (match.Selected && !Matches(FindDefinition(item.Key)?.AutoSelectWhen, context, false))
                    {
                        item.Source = LineItemSource.Manual;
                    }
                }

                // A forced item stays selected regardless of what was posted.
                if (item.Forced) item.Selected = true;

                if (item.Selected) resolved.Add(item);
            }

            var dropped = postedList.Count(p => !catalog.Any(c => IsSameItem(p, c)));
            if (dropped > 0)
            {
                _logger.LogWarning("Dropped {Count} posted line item(s) with no catalogue match.", dropped);
            }

            return resolved;
        }

        private LineItemDefinition? FindDefinition(string? key) =>
            string.IsNullOrWhiteSpace(key)
                ? null
                : _options.Items.FirstOrDefault(i => string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase));

        private static bool IsSameItem(QuoteLineItem a, QuoteLineItem b) =>
            (!string.IsNullOrWhiteSpace(a.ItemRef) && string.Equals(a.ItemRef, b.ItemRef, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(a.Key) && string.Equals(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));

        private decimal ClampQuantity(decimal quantity)
        {
            if (quantity < 0) return 0;
            var max = _options.MaxQuantity > 0 ? _options.MaxQuantity : 9999;
            return quantity > max ? max : decimal.Truncate(quantity);
        }

        private static void MergeExisting(List<QuoteLineItem> catalog, IEnumerable<QuoteLineItem>? existing)
        {
            if (existing == null) return;
            foreach (var saved in existing)
            {
                var target = catalog.FirstOrDefault(c => IsSameItem(saved, c));
                if (target == null) continue;
                target.Selected = saved.Selected || target.Forced;
                if (target.QuantityEditable && saved.Quantity > 0)
                {
                    target.Quantity = saved.Quantity;
                }
                if (saved.Source == LineItemSource.Manual) target.Source = LineItemSource.Manual;
            }
        }

        private async Task<Dictionary<string, PriceListItem>> LoadPriceRowsAsync(string? customerId)
        {
            var rows = new Dictionary<string, PriceListItem>(StringComparer.OrdinalIgnoreCase);
            var preferred = _options.PreferCustomerPricing ? customerId : null;

            try
            {
                var configuredRefs = _options.Items
                    .Select(i => i.ItemRef)
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .ToList()!;

                if (configuredRefs.Count > 0)
                {
                    foreach (var row in await _priceListRepository.GetActiveByItemRefsAsync(
                                 configuredRefs!, preferred, _options.StandardCustomerRef))
                    {
                        rows[row.ItemRef?.Trim() ?? string.Empty] = row;
                    }
                }

                if (_options.IncludePrioritizedItems)
                {
                    foreach (var row in await _priceListRepository.GetWebQuoteItemsAsync(
                                 preferred, _options.StandardCustomerRef))
                    {
                        rows.TryAdd(row.ItemRef?.Trim() ?? string.Empty, row);
                    }
                }
            }
            catch (Exception ex)
            {
                // A pricing outage must not block a quote. Prior behaviour was to log and
                // render no charges; keep that.
                _logger.LogError(ex, "Failed to load the line item price list; the quote will be offered without charges.");
                return new Dictionary<string, PriceListItem>(StringComparer.OrdinalIgnoreCase);
            }

            return rows;
        }

        /// <summary>
        /// Projects a CERM price list row into a quote line item, selecting the language
        /// columns and rejecting price bases this application cannot price.
        /// </summary>
        private QuoteLineItem? Project(PriceListItem row, LineItemDefinition? definition)
        {
            var basis = (row.PriceType ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(basis)) basis = LineItemPricing.PerPiece;

            if (!LineItemPricing.IsSupportedBasis(basis))
            {
                _logger.LogWarning(
                    "Line item {ItemRef} has price basis '{Basis}', which this application cannot price; it will not be offered.",
                    row.ItemRef, basis);
                return null;
            }

            var line1 = SelectLanguage(row, lineNumber: 1);
            var line2 = SelectLanguage(row, lineNumber: 2);
            var unit = SelectUnit(row);

            var quantityEditable = definition?.QuantityEditable ?? true;
            if (!LineItemPricing.IsQuantityBased(basis)) quantityEditable = false;

            var defaultQuantity = definition?.DefaultQuantity ?? 1;
            if (defaultQuantity <= 0) defaultQuantity = 1;

            return new QuoteLineItem
            {
                ItemRef = row.ItemRef?.Trim() ?? string.Empty,
                CustomerRef = row.CustomerRef?.Trim(),
                Key = definition?.Key ?? row.ItemRef?.Trim() ?? string.Empty,
                Description = !string.IsNullOrWhiteSpace(definition?.Label) ? definition!.Label! : line1,
                Description2 = line2,
                Unit = string.IsNullOrWhiteSpace(unit) ? LineItemPricing.BasisLabel(basis) : unit,
                UnitPrice = (decimal)row.PriceExcludingTax,
                Quantity = defaultQuantity,
                QuantityEditable = quantityEditable,
                PriceBasis = basis,
                Currency = string.IsNullOrWhiteSpace(row.CurrencyRef) ? null : row.CurrencyRef.Trim(),
                Order = definition?.Order ?? 0,
                Source = string.IsNullOrWhiteSpace(row.CustomerRef) ? LineItemSource.Rule : LineItemSource.Customer
            };
        }

        /// <summary>
        /// CERM stores descriptions as <c>fkttxt{line}{language}</c> - two LINES of one
        /// description per language, joined with a newline on the quote letter. Reading
        /// only <c>fkttxt11</c> silently drops the second line.
        /// </summary>
        private string SelectLanguage(PriceListItem row, int lineNumber)
        {
            var language = _options.LanguageIndex is >= 1 and <= 9 ? _options.LanguageIndex : 1;
            var value = (lineNumber, language) switch
            {
                (1, 1) => row.InvoiceText11,
                (1, 2) => row.InvoiceText12,
                (1, 3) => row.InvoiceText13,
                (2, 1) => row.InvoiceText21,
                (2, 2) => row.InvoiceText22,
                (2, 3) => row.InvoiceText23,
                _ => lineNumber == 1 ? row.InvoiceText11 : row.InvoiceText21
            };
            return (value ?? string.Empty).Trim();
        }

        private string SelectUnit(PriceListItem row)
        {
            var language = _options.LanguageIndex is >= 1 and <= 9 ? _options.LanguageIndex : 1;
            var value = language switch
            {
                1 => row.QuantityDescription1,
                2 => row.QuantityDescription2,
                3 => row.QuantityDescription3,
                _ => row.QuantityDescription1
            };
            return (value ?? string.Empty).Trim();
        }

        /// <summary>
        /// Evaluates a rule list. A list matches if ANY condition holds; an empty or
        /// missing list falls back to <paramref name="offeredByDefault"/>, which is how
        /// "always offered" is expressed.
        /// </summary>
        private static bool Matches(List<LineItemCondition>? conditions, LineItemContext context, bool offeredByDefault)
        {
            if (conditions == null || conditions.Count == 0) return offeredByDefault;
            return conditions.Any(c => Matches(c, context));
        }

        private static bool Matches(LineItemCondition condition, LineItemContext context)
        {
            if (condition.PrintingIdIn is { Count: > 0 })
            {
                if (string.IsNullOrWhiteSpace(context.PrintingId)) return false;
                if (!condition.PrintingIdIn.Any(p => string.Equals(p?.Trim(), context.PrintingId.Trim(), StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            if (condition.PrintingIdPrefixIn is { Count: > 0 })
            {
                if (string.IsNullOrWhiteSpace(context.PrintingId)) return false;
                if (!condition.PrintingIdPrefixIn.Any(p =>
                        !string.IsNullOrWhiteSpace(p) &&
                        context.PrintingId.Trim().StartsWith(p.Trim(), StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            if (condition.ShapeIn is { Count: > 0 })
            {
                if (string.IsNullOrWhiteSpace(context.ShapeValue)) return false;
                if (!condition.ShapeIn.Any(s => string.Equals(s?.Trim(), context.ShapeValue.Trim(), StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            if (condition.CornersNotIn is { Count: > 0 })
            {
                var corners = context.CornersValue?.Trim() ?? string.Empty;
                if (condition.CornersNotIn.Any(c => string.Equals(c?.Trim(), corners, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            if (condition.CustomDie.HasValue && condition.CustomDie.Value != context.IsCustomDie) return false;

            if (condition.NoExistingDie.HasValue && condition.NoExistingDie.Value != !context.HasExistingDie) return false;

            return true;
        }
    }
}
