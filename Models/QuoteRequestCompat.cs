using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace WiseLabels.Models
{
    /// <summary>
    /// One-release compatibility shim. A <see cref="QuoteRequest"/> serialized by an
    /// earlier build carries the six per-charge scalars instead of a
    /// <see cref="QuoteRequest.LineItems"/> collection. Those blobs live in TempData
    /// (a quote in flight across a deploy) and in the session under
    /// <c>SelectedQuoteSelection</c> (written by a different page, 30-minute lifetime),
    /// so they outlive the deploy that removes the properties.
    ///
    /// Delete this file, the call sites, and <see cref="QuoteRequest.Extra"/> one
    /// release after the model change has shipped.
    /// </summary>
    public static class QuoteRequestCompat
    {
        /// <summary>Legacy scalar name to line item configuration key.</summary>
        private static readonly (string Legacy, string Key, string? QuantityProperty)[] LegacyCharges =
        {
            ("needsPressProof",           "PressProof",           "pressProofQuantity"),
            ("needsSpotColorPlateChange", "SpotColorPlateChange", "spotColorPlateChangeQuantity")
        };

        private static readonly (string Legacy, string Key)[] LegacyCounts =
        {
            ("colorChanges",          "ColorChanges"),
            ("digitalVersionChanges", "DigitalVersionChanges")
        };

        /// <summary>
        /// Rebuilds <see cref="QuoteRequest.LineItems"/> from legacy scalars found in
        /// <see cref="QuoteRequest.Extra"/>. Prices are not recovered - they are
        /// re-resolved from the catalogue by item ref - so only selection and quantity
        /// carry over, which is all that was ever user-supplied.
        /// No-op when the quote already has line items or carries no legacy data.
        /// </summary>
        public static void UpgradeLegacyLineItems(QuoteRequest? quote)
        {
            if (quote?.Extra == null || quote.Extra.Count == 0) return;
            if (quote.LineItems is { Count: > 0 }) { quote.Extra = null; return; }

            var recovered = new List<QuoteLineItem>();

            foreach (var (legacy, key) in LegacyCounts)
            {
                var count = ReadInt(quote.Extra, legacy);
                if (count is > 0)
                {
                    recovered.Add(new QuoteLineItem
                    {
                        Key = key,
                        Selected = true,
                        Quantity = count.Value,
                        Source = LineItemSource.Rule
                    });
                }
            }

            foreach (var (legacy, key, quantityProperty) in LegacyCharges)
            {
                if (ReadBool(quote.Extra, legacy) == true)
                {
                    var quantity = quantityProperty == null ? null : ReadInt(quote.Extra, quantityProperty);
                    recovered.Add(new QuoteLineItem
                    {
                        Key = key,
                        Selected = true,
                        Quantity = quantity is > 0 ? quantity.Value : 1,
                        Source = LineItemSource.Rule
                    });
                }
            }

            if (recovered.Count > 0)
            {
                quote.LineItems = recovered;
            }

            quote.Extra = null;
        }

        private static bool TryGet(Dictionary<string, JsonElement> extra, string name, out JsonElement value)
        {
            // TempData is written with default (PascalCase) options on some paths and
            // camelCase on others, so match case-insensitively.
            foreach (var pair in extra)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }

        private static int? ReadInt(Dictionary<string, JsonElement> extra, string name)
        {
            if (!TryGet(extra, name, out var element)) return null;
            return element.ValueKind switch
            {
                JsonValueKind.Number when element.TryGetInt32(out var n) => n,
                JsonValueKind.String when int.TryParse(element.GetString(), out var s) => s,
                _ => null
            };
        }

        private static bool? ReadBool(Dictionary<string, JsonElement> extra, string name)
        {
            if (!TryGet(extra, name, out var element)) return null;
            return element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(element.GetString(), out var b) => b,
                _ => null
            };
        }
    }
}
