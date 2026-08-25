using System.Text.Json;
using System.Text.Json.Serialization;

namespace WiseLabels.Models
{
    public class QuoteRequest
    {
        // Metadata for server-side selection
        public string? CalculationId { get; set; }
        public string? EstimateId { get; set; }
        public string? CustomerId { get; set; }
        public string? CustomerDisplayName { get; set; }

        // Contact information
        public string? Name { get; set; }
        public string? Company { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Comments { get; set; }
        
        // Display values (for confirmation page)
        public string? ReferenceType { get; set; }
        public string? ReferenceValue { get; set; }
        public string? Description { get; set; }
        public string? Shape { get; set; }
        public string? LabelWidth { get; set; }
        public string? LabelHeight { get; set; }
        public string? Diameter { get; set; }
        public string? Corners { get; set; }
        public string? CuttingDie { get; set; }
        public string? DieSizeInfo { get; set; }
        public bool IsCustomDie { get; set; }
        public string? Printing { get; set; }
        public string? Material { get; set; }
        public string? ColorCode { get; set; }
        public string? Finish { get; set; }
        public string? PackingProcedure { get; set; }
        public int? PackingQuantity { get; set; }
        public string? ApplicationMethod { get; set; }
        public string? UnwindDirection { get; set; }
        public string? TotalQuantity { get; set; }
        public List<int>? Quantities { get; set; }
        public string? ArtworkOption { get; set; }

        // File upload information
        public string? UploadedFileName { get; set; }
        public string? UploadedFileOriginalName { get; set; }
        public string? UploadedFileContentType { get; set; }
        public long? UploadedFileSize { get; set; }

        // Form values for restoration
        public string? ShapeValue { get; set; }
        public string? CornersValue { get; set; }
        public string? MaterialValue { get; set; }
        public string? ColorCodeValue { get; set; }
        public string? FinishValue { get; set; }
        public string? ApplicationMethodValue { get; set; }
        public string? UnwindDirectionValue { get; set; }
        public string? ArtworkOptionValue { get; set; }
        public string? CuttingDieValue { get; set; }
        public string? PrintingValue { get; set; }
        public string? PackingProcedureValue { get; set; }
        
        public List<QuotePriceBreakdown>? PriceBreakdown { get; set; }
        public string? QuickQuoteResponseJson { get; set; }

        /// <summary>
        /// Charges selected for this quote. Replaces the former per-charge scalars
        /// (ColorChanges, DigitalVersionChanges, NeedsPressProof, PressProofQuantity,
        /// NeedsSpotColorPlateChange, SpotColorPlateChangeQuantity); those are still
        /// read from persisted JSON by <see cref="QuoteRequestCompat"/>.
        /// </summary>
        public List<QuoteLineItem>? LineItems { get; set; }

        /// <summary>
        /// Captures JSON properties this model no longer declares, so a QuoteRequest
        /// serialized by an earlier build (in TempData or in the session) can still be
        /// upgraded rather than silently losing its charges. See <see cref="QuoteRequestCompat"/>.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Extra { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}

