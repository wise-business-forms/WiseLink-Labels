using System.Text.Json;

namespace WiseLabels.Services
{
    public interface IChatService
    {
        /// <summary>
        /// Sends a chat message to the Ollama API and streams the response back.
        /// </summary>
        /// <param name="messages">Conversation history including the new user message</param>
        /// <param name="onChunk">Callback invoked for each chunk of the streaming response</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task<string> SendChatMessageAsync(List<ChatMessage> messages, Action<string> onChunk, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message to the Ollama API and returns a structured LabelQuoteResponse.
        /// </summary>
        /// <param name="userMessage">The user's message</param>
        /// <param name="messageHistory">The conversation history</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A LabelQuoteResponse containing the message and extracted quote data</returns>
        Task<Models.LabelQuoteResponse> SendMessageAsync(string userMessage, List<ChatMessage> messageHistory, CancellationToken cancellationToken = default);

        /// <summary>
        /// Merges new quote data into existing quote data, only overwriting non-null fields.
        /// </summary>
        /// <param name="existing">The existing quote data</param>
        /// <param name="newData">The new quote data to merge</param>
        /// <returns>The merged quote data</returns>
        Models.QuoteData MergeQuoteData(Models.QuoteData? existing, Models.QuoteData? newData);
    }

    public class ChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class ChatService : IChatService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ChatService> _logger;

        public ChatService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<ChatService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> SendChatMessageAsync(
            List<ChatMessage> messages,
            Action<string> onChunk,
            CancellationToken cancellationToken = default)
        {
            var endpoint = _configuration["Chat:OllamaEndpoint"];
            var model = _configuration["Chat:Model"];

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new InvalidOperationException("Chat:OllamaEndpoint configuration is missing");
            }

            if (string.IsNullOrWhiteSpace(model))
            {
                throw new InvalidOperationException("Chat:Model configuration is missing");
            }

            _logger.LogInformation("Sending chat message to Ollama at {Endpoint} using model {Model}", endpoint, model);

            var payload = new
            {
                model = model,
                messages = messages.Select(m => new
                {
                    role = m.Role,
                    content = m.Content,
                    images = new string[] { }
                }).ToList(),
                stream = true,
                keep_alive = "5m",
                options = new
                {
                    temperature = 0.5,          // Lower = more focused
                    seed = 42,
                    num_ctx = 1024,             // Smaller context for faster response
                    num_predict = 150,          // Shorter max response
                    top_k = 10,                 // Faster sampling
                    top_p = 0.8,                
                    repeat_penalty = 1.1,
                    repeat_last_n = 16,         
                    stop = new[] { "</s>", "User:", "\n\n" },
                    num_thread = 8              
                }
            };

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(5);

            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            _logger.LogInformation("Ollama Request Payload:\n{Payload}", jsonPayload);

            using var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

            using var response = await httpClient.PostAsync(endpoint, content, cancellationToken);
            

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Ollama API returned {StatusCode}: {Body}", response.StatusCode, errorBody);
                throw new HttpRequestException($"Ollama API error: {response.StatusCode}");
            }

            var fullResponse = new System.Text.StringBuilder();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                _logger.LogDebug("Ollama response chunk: {Line}", line);

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("message", out var messageElement) &&
                        messageElement.TryGetProperty("content", out var contentElement))
                    {
                        var fragment = contentElement.GetString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(fragment))
                        {
                            fullResponse.Append(fragment);
                            onChunk(fragment);
                        }
                        else
                        {
                            _logger.LogDebug("Empty content fragment in chunk");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Chunk missing 'message.content' property. Raw: {Line}", line);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse chat chunk: {Line}", line);
                }
            }

            var finalMessage = fullResponse.ToString();
            _logger.LogInformation("Chat response complete, {Length} characters", finalMessage.Length);

            return finalMessage;
        }

        public async Task<Models.LabelQuoteResponse> SendMessageAsync(
            string userMessage,
            List<ChatMessage> messageHistory,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                throw new ArgumentException("User message cannot be empty", nameof(userMessage));
            }

            var endpoint = _configuration["Chat:OllamaEndpoint"];
            var model = _configuration["Chat:Model"];

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new InvalidOperationException("Chat:OllamaEndpoint configuration is missing");
            }

            if (string.IsNullOrWhiteSpace(model))
            {
                throw new InvalidOperationException("Chat:Model configuration is missing");
            }

            // Build the full message list with history
            var messages = new List<ChatMessage>(messageHistory)
            {
                new ChatMessage { Role = "user", Content = userMessage }
            };

            _logger.LogInformation("Sending message to Ollama at {Endpoint} using model {Model}", endpoint, model);

            var payload = new
            {
                model = model,
                messages = messages.Select(m => new
                {
                    role = m.Role,
                    content = m.Content
                }).ToList(),
                stream = false, // Non-streaming for structured response
                options = new
                {
                    temperature = 0.7,
                    num_ctx = 4096,
                    num_predict = 512
                }
            };

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(2);

            var jsonPayload = JsonSerializer.Serialize(payload);
            _logger.LogDebug("Ollama Request Payload: {Payload}", jsonPayload);

            using var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync(endpoint, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Ollama API returned {StatusCode}: {Body}", response.StatusCode, errorBody);
                throw new HttpRequestException($"Ollama API error: {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("Ollama Response: {Response}", responseBody);

            try
            {
                // Parse the Ollama response envelope
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                // Extract the message content from the response
                string messageContent = string.Empty;
                if (root.TryGetProperty("message", out var messageElement) &&
                    messageElement.TryGetProperty("content", out var contentElement))
                {
                    messageContent = contentElement.GetString() ?? string.Empty;
                }
                else
                {
                    _logger.LogWarning("Could not find message.content in Ollama response");
                    return new Models.LabelQuoteResponse
                    {
                        Message = "Error: Invalid response from AI model",
                        QuoteData = null
                    };
                }

                _logger.LogInformation("AI message content ({Length} chars): {Content}",
                    messageContent.Length,
                    messageContent.Length > 500 ? messageContent.Substring(0, 500) + "..." : messageContent);

                // Try to parse the content as LabelQuoteResponse
                try
                {
                    var labelResponse = JsonSerializer.Deserialize<Models.LabelQuoteResponse>(messageContent);
                    if (labelResponse != null)
                    {
                        _logger.LogInformation("Successfully parsed LabelQuoteResponse. Message: {Message}, HasQuoteData: {HasData}",
                            labelResponse.Message,
                            labelResponse.QuoteData != null);
                        return labelResponse;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse message content as LabelQuoteResponse. Content: {Content}", messageContent);
                }

                // If parsing fails, return the raw message content
                return new Models.LabelQuoteResponse
                {
                    Message = messageContent,
                    QuoteData = null
                };
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Ollama response as JSON: {Response}", responseBody);
                throw new InvalidOperationException("Invalid JSON response from Ollama API", ex);
            }
        }

        public Models.QuoteData MergeQuoteData(Models.QuoteData? existing, Models.QuoteData? newData)
        {
            // If no existing data, return new data (or empty if both null)
            if (existing == null)
            {
                return newData ?? new Models.QuoteData();
            }

            // If no new data, return existing
            if (newData == null)
            {
                return existing;
            }

            // Merge: new values overwrite existing only if non-null
            return new Models.QuoteData
            {
                Description = newData.Description ?? existing.Description,
                Shape = newData.Shape ?? existing.Shape,
                LabelWidth = newData.LabelWidth ?? existing.LabelWidth,
                LabelHeight = newData.LabelHeight ?? existing.LabelHeight,
                Diameter = newData.Diameter ?? existing.Diameter,
                Material = newData.Material ?? existing.Material,
                Printing = newData.Printing ?? existing.Printing,
                Finish = newData.Finish ?? existing.Finish,
                TotalQuantity = newData.TotalQuantity ?? existing.TotalQuantity,
                Complete = newData.Complete ?? existing.Complete
            };
        }
    }
}
