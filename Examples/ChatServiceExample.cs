// Example usage of the new SendMessageAsync and MergeQuoteData methods

/*
 * USAGE EXAMPLE:
 * 
 * This demonstrates how to use the new IChatService methods for conversational quote extraction.
 * The methods are compatible with .NET MAUI and use HttpClient.
 */

using WiseLabels.Models;
using WiseLabels.Services;

namespace WiseLabels.Examples
{
    public class ChatServiceExample
    {
        private readonly IChatService _chatService;
        private readonly IConfiguration _configuration;
        
        // Track conversation state
        private List<ChatMessage> _messageHistory = new();
        private QuoteData? _accumulatedQuoteData = null;

        public ChatServiceExample(IChatService chatService, IConfiguration configuration)
        {
            _chatService = chatService;
            _configuration = configuration;
            
            // Initialize with system prompt
            var systemPrompt = BuildSystemPrompt();
            _messageHistory.Add(new ChatMessage 
            { 
                Role = "system", 
                Content = systemPrompt 
            });
        }

        /// <summary>
        /// Send a user message and accumulate quote data across the conversation
        /// </summary>
        public async Task<(string message, QuoteData? quoteData, bool isComplete)> SendUserMessageAsync(
            string userMessage, 
            CancellationToken cancellationToken = default)
        {
            // Send the message with conversation history
            var response = await _chatService.SendMessageAsync(
                userMessage, 
                _messageHistory, 
                cancellationToken);

            // Add user message and assistant response to history
            _messageHistory.Add(new ChatMessage 
            { 
                Role = "user", 
                Content = userMessage 
            });
            _messageHistory.Add(new ChatMessage 
            { 
                Role = "assistant", 
                Content = response.Message 
            });

            // Merge the new quote data with accumulated data
            _accumulatedQuoteData = _chatService.MergeQuoteData(_accumulatedQuoteData, response.QuoteData);

            // Check if the quote is complete
            bool isComplete = _accumulatedQuoteData?.Complete ?? false;

            return (response.Message, _accumulatedQuoteData, isComplete);
        }

        /// <summary>
        /// Reset the conversation and accumulated data
        /// </summary>
        public void ResetConversation()
        {
            _messageHistory.Clear();
            _accumulatedQuoteData = null;
            
            var systemPrompt = BuildSystemPrompt();
            _messageHistory.Add(new ChatMessage 
            { 
                Role = "system", 
                Content = systemPrompt 
            });
        }

        /// <summary>
        /// Get the current accumulated quote data
        /// </summary>
        public QuoteData? GetAccumulatedQuoteData()
        {
            return _accumulatedQuoteData;
        }

        private string BuildSystemPrompt()
        {
            var promptSection = _configuration.GetSection("Chat:SystemPrompt");
            
            var parts = new[]
            {
                promptSection["Base"],
                promptSection["Role"],
                promptSection["Guidelines"],
                promptSection["StructuredOutput"]
            };
            
            return string.Join("\n\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }
    }

    /*
     * EXAMPLE CONVERSATION FLOW:
     * 
     * User: "I need some labels"
     * AI: "Great! What material would you like?" 
     * QuoteData: null
     * 
     * User: "vinyl"
     * AI: "Perfect! What size labels do you need?"
     * QuoteData: { material: "vinyl" }
     * 
     * User: "2x3 inches"
     * AI: "Got it! How many labels do you need?"
     * QuoteData: { material: "vinyl", labelWidth: "2", labelHeight: "3" }
     * 
     * User: "1000"
     * AI: "Excellent! What printing method?"
     * QuoteData: { material: "vinyl", labelWidth: "2", labelHeight: "3", totalQuantity: "1000" }
     * 
     * User: "digital"
     * AI: "And what finish would you like?"
     * QuoteData: { material: "vinyl", labelWidth: "2", labelHeight: "3", totalQuantity: "1000", printing: "digital" }
     * 
     * User: "gloss"
     * AI: "Perfect! I have all the details I need."
     * QuoteData: { 
     *   material: "vinyl", 
     *   labelWidth: "2", 
     *   labelHeight: "3", 
     *   totalQuantity: "1000", 
     *   printing: "digital",
     *   finish: "gloss",
     *   shape: "rectangle",
     *   complete: true 
     * }
     */
}
