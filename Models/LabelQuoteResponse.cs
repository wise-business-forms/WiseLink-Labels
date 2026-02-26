using System.Text.Json.Serialization;

namespace WiseLabels.Models
{
    /// <summary>
    /// Represents the response from the Ollama chat API for label quote extraction.
    /// </summary>
    public class LabelQuoteResponse
    {
        /// <summary>
        /// The conversational message to display to the user.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// The extracted quote data from the conversation. Null if no quote data was extracted.
        /// </summary>
        [JsonPropertyName("quoteData")]
        public QuoteData? QuoteData { get; set; }
    }

    /// <summary>
    /// Represents the extracted label quote data from a chat conversation.
    /// </summary>
    public class QuoteData
    {
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("shape")]
        public string? Shape { get; set; }

        [JsonPropertyName("labelWidth")]
        public string? LabelWidth { get; set; }

        [JsonPropertyName("labelHeight")]
        public string? LabelHeight { get; set; }

        [JsonPropertyName("diameter")]
        public string? Diameter { get; set; }

        [JsonPropertyName("material")]
        public string? Material { get; set; }

        [JsonPropertyName("printing")]
        public string? Printing { get; set; }

        [JsonPropertyName("finish")]
        public string? Finish { get; set; }

        [JsonPropertyName("totalQuantity")]
        public string? TotalQuantity { get; set; }

        /// <summary>
        /// Indicates whether all required fields have been collected.
        /// Set to true when shape, dimensions, material, printing, finish, and totalQuantity are all provided.
        /// </summary>
        [JsonPropertyName("complete")]
        public bool? Complete { get; set; }
    }
}
