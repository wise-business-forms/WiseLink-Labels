using System.Text.Json.Serialization;

namespace WiseLabels.Models
{
    public class CermApiPayload
    {
        [JsonPropertyName("CustomerId")]
        public string CustomerId { get; set; } = "108620";

        [JsonPropertyName("ContactId")]
        public string ContactId { get; set; } = "001";

        [JsonPropertyName("PressRuns")]
        public List<PressRun> PressRuns { get; set; } = new();

        [JsonPropertyName("WindingId")]
        public string WindingId { get; set; } = "10"; // Sheeted

        [JsonPropertyName("Outline")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Outline { get; set; }

        [JsonPropertyName("DieSizeId")]
        public string? DieSizeId { get; set; }

        [JsonPropertyName("SubstrateId")]
        public string? SubstrateId { get; set; }

        [JsonPropertyName("Description")]
        public string? Description { get; set; }

        [JsonPropertyName("NumberOfProducts")]
        public int NumberOfProducts { get; set; } = 1;

        [JsonPropertyName("Quantities")]
        public List<int> Quantities { get; set; } = new();

        [JsonPropertyName("Width")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Width { get; set; }

        [JsonPropertyName("Height")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Height { get; set; }

        [JsonPropertyName("Radius")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Radius { get; set; }

        [JsonPropertyName("PackingProcedureId")]
        public string PackingProcedureId { get; set; } = "152";

        [JsonPropertyName("PackingPriority")]
        public string PackingPriority { get; set; } = "Diameter";

        [JsonPropertyName("PackingNumber")]
        public int PackingNumber { get; set; } = 500;
    }

    public class PressRun
    {
        [JsonPropertyName("ColourCodeIdFront")]
        public string? ColourCodeIdFront { get; set; }

        [JsonPropertyName("FinishingTypes")]
        public List<string> FinishingTypes { get; set; } = new();
    }
}
