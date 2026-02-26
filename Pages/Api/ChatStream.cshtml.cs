using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using System.Text.Json;
using WiseLabels.Services;

namespace WiseLabels.Pages.Api
{
    [IgnoreAntiforgeryToken]
    public class ChatStreamModel : PageModel
    {
        private readonly IChatService _chatService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ChatStreamModel> _logger;

        private const string ConversationSessionKey = "ChatConversation";

        public ChatStreamModel(IChatService chatService, IConfiguration configuration, ILogger<ChatStreamModel> logger)
        {
            _chatService = chatService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<IActionResult> OnPostAsync([FromBody] ChatRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.Message))
                {
                    return BadRequest(new { error = "Message is required" });
                }

                // Retrieve conversation history from session
                var conversation = GetConversationFromSession();

                // Add user message to conversation
                conversation.Add(new ChatMessage
                {
                    Role = "user",
                    Content = request.Message
                });

                // Set up Server-Sent Events (SSE) response
                Response.ContentType = "text/event-stream";
                Response.Headers.Add("Cache-Control", "no-cache");
                Response.Headers.Add("Connection", "keep-alive");

                var assistantMessage = new StringBuilder();

                // Collect complete response first (don't stream yet)
                await _chatService.SendChatMessageAsync(
                    conversation,
                    chunk =>
                    {
                        assistantMessage.Append(chunk);
                    },
                    cancellationToken
                );

                // Parse the complete response to extract message and quoteData
                var fullResponse = assistantMessage.ToString();
                string displayMessage = fullResponse;
                object? quoteData = null;

                _logger.LogInformation("Complete AI response ({Length} chars): {Response}", 
                    fullResponse.Length, 
                    fullResponse.Length > 500 ? fullResponse.Substring(0, 500) + "..." : fullResponse);

                if (string.IsNullOrWhiteSpace(fullResponse))
                {
                    _logger.LogError("AI returned empty response");
                    displayMessage = "The AI model returned an empty response. Please check:\n- Is Ollama running?\n- Is the model loaded?\n- Check server logs for errors.";
                }
                else
                {
                    // Try to extract JSON from the response (even if mixed with other text)
                    var jsonExtracted = TryExtractJson(fullResponse, out var extractedMessage, out var extractedQuoteData);

                    if (jsonExtracted)
                    {
                        displayMessage = extractedMessage ?? fullResponse;
                        quoteData = extractedQuoteData;
                        _logger.LogInformation("Successfully extracted JSON - Message: {Message}, QuoteData: {QuoteData}", 
                            displayMessage, quoteData != null ? JsonSerializer.Serialize(quoteData) : "null");
                    }
                    else
                    {
                        // No JSON found, use full response as message
                        displayMessage = fullResponse;
                        _logger.LogInformation("No JSON found in response, using full text as message");
                    }
                }

                // Validate we have something to send
                if (string.IsNullOrWhiteSpace(displayMessage))
                {
                    _logger.LogWarning("Display message is empty after parsing. Full response was: {Response}", fullResponse);
                    displayMessage = "I apologize, but I couldn't generate a proper response. Please try rephrasing your question.";
                }

                _logger.LogInformation("Streaming message to client: {Message}", displayMessage.Substring(0, Math.Min(100, displayMessage.Length)));

                // Stream the extracted message to the client (character by character for typing effect)
                foreach (char c in displayMessage)
                {
                    var eventData = $"data: {JsonSerializer.Serialize(new { content = c.ToString() })}\n\n";
                    var bytes = Encoding.UTF8.GetBytes(eventData);

                    try
                    {
                        await Response.Body.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);
                        await Task.Delay(5, cancellationToken); // Small delay for typing effect
                    }
                    catch
                    {
                        break; // Client disconnected
                    }
                }

                // Add assistant response to conversation history (store the clean message)
                conversation.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = displayMessage
                });

                // Save updated conversation to session
                SaveConversationToSession(conversation);

                // Send quote data event if available
                if (quoteData != null)
                {
                    var quoteDataEvent = $"data: {JsonSerializer.Serialize(new { quoteData = quoteData })}\n\n";
                    var quoteDataBytes = Encoding.UTF8.GetBytes(quoteDataEvent);
                    await Response.Body.WriteAsync(quoteDataBytes, 0, quoteDataBytes.Length, cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }

                // Send completion event
                var completeEvent = "data: {\"done\":true}\n\n";
                var completeBytes = Encoding.UTF8.GetBytes(completeEvent);
                await Response.Body.WriteAsync(completeBytes, 0, completeBytes.Length, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);

                return new EmptyResult();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Chat request cancelled by client");
                return new EmptyResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat message");

                try
                {
                    var errorEvent = $"data: {JsonSerializer.Serialize(new { error = ex.Message, done = true })}\n\n";
                    var errorBytes = Encoding.UTF8.GetBytes(errorEvent);
                    await Response.Body.WriteAsync(errorBytes, 0, errorBytes.Length, cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
                catch
                {
                    // Response may already be closed
                }

                return new EmptyResult();
            }
        }

        public IActionResult OnPostReset()
        {
            // Clear conversation history
            HttpContext.Session.Remove(ConversationSessionKey);
            _logger.LogInformation("Chat conversation reset");
            return new JsonResult(new { success = true });
        }

        private List<ChatMessage> GetConversationFromSession()
        {
            var json = HttpContext.Session.GetString(ConversationSessionKey);

            if (string.IsNullOrEmpty(json))
            {
                // Build composite system message from configuration
                var systemPrompt = BuildSystemPrompt();

                return new List<ChatMessage>
                {
                    new ChatMessage
                    {
                        Role = "system",
                        Content = systemPrompt
                    }
                };
            }

            try
            {
                return JsonSerializer.Deserialize<List<ChatMessage>>(json) ?? new List<ChatMessage>();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize conversation from session, starting fresh");
                return new List<ChatMessage>();
            }
        }

        private void SaveConversationToSession(List<ChatMessage> conversation)
        {
            try
            {
                var json = JsonSerializer.Serialize(conversation);
                HttpContext.Session.SetString(ConversationSessionKey, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save conversation to session");
            }
        }

        private string BuildSystemPrompt()
        {
            try
            {
                // Try to build composite prompt from configuration parts
                var promptSection = _configuration.GetSection("Chat:SystemPrompt");

                if (!promptSection.Exists())
                {
                    return "You are a helpful assistant that helps WiseLabels estimators collaborate on quotes. Keep answers concise, actionable, and professional.";
                }

                var prompt = new StringBuilder();

                // Add base description
                var basePrompt = promptSection["Base"];
                if (!string.IsNullOrWhiteSpace(basePrompt))
                {
                    prompt.AppendLine(basePrompt);
                }

                // Add role definition
                var role = promptSection["Role"];
                if (!string.IsNullOrWhiteSpace(role))
                {
                    prompt.AppendLine();
                    prompt.AppendLine(role);
                }

                // Add guidelines
                var guidelines = promptSection["Guidelines"];
                if (!string.IsNullOrWhiteSpace(guidelines))
                {
                    prompt.AppendLine();
                    prompt.AppendLine(guidelines);
                }

                // Add structured output instructions
                var structuredOutput = promptSection["StructuredOutput"];
                if (!string.IsNullOrWhiteSpace(structuredOutput))
                {
                    prompt.AppendLine();
                    prompt.AppendLine(structuredOutput);
                }

                // Skip terminology section for small models - too complex
                // Terminology is now included in the StructuredOutput examples

                var finalPrompt = prompt.ToString().Trim();
                _logger.LogDebug("Built composite system prompt: {Prompt}", finalPrompt);

                return finalPrompt;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building composite system prompt, using fallback");
                return "You are a helpful assistant that helps WiseLabels estimators collaborate on quotes. Keep answers concise, actionable, and professional.";
            }
        }

        /// <summary>
        /// Attempts to extract JSON from a response that may contain mixed text and JSON.
        /// Handles JSON with comments, incomplete JSON, and various formats.
        /// </summary>
        private bool TryExtractJson(string response, out string? message, out object? quoteData)
        {
            message = null;
            quoteData = null;

            try
            {
                // Strategy 1: Look for JSON object pattern { ... }
                var jsonStart = response.IndexOf('{');
                var jsonEnd = response.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonCandidate = response.Substring(jsonStart, jsonEnd - jsonStart + 1);

                    // Clean up the JSON:
                    // 1. Remove JavaScript/C-style comments
                    jsonCandidate = System.Text.RegularExpressions.Regex.Replace(jsonCandidate, @"//.*?$", "", System.Text.RegularExpressions.RegexOptions.Multiline);

                    // 2. Remove trailing commas before } or ]
                    jsonCandidate = System.Text.RegularExpressions.Regex.Replace(jsonCandidate, @",(\s*[}\]])", "$1");

                    // 3. Fix common issues
                    jsonCandidate = jsonCandidate
                        .Replace("'", "\"")  // Single quotes to double quotes
                        .Replace(": None", ": null")
                        .Replace(": True", ": true")
                        .Replace(": False", ": false");

                    // Try to parse the cleaned JSON
                    try
                    {
                        using var doc = JsonDocument.Parse(jsonCandidate);
                        var root = doc.RootElement;

                        // Extract message field
                        if (root.TryGetProperty("message", out var messageElement))
                        {
                            message = messageElement.GetString();
                        }

                        // Extract quoteData field
                        if (root.TryGetProperty("quoteData", out var quoteDataElement) && 
                            quoteDataElement.ValueKind != JsonValueKind.Null)
                        {
                            // Convert to dictionary for easier handling
                            var quoteDict = new Dictionary<string, object>();

                            foreach (var prop in quoteDataElement.EnumerateObject())
                            {
                                quoteDict[prop.Name] = prop.Value.ValueKind switch
                                {
                                    JsonValueKind.String => prop.Value.GetString() ?? "",
                                    JsonValueKind.Number => prop.Value.TryGetInt32(out var intVal) ? intVal : prop.Value.GetDouble(),
                                    JsonValueKind.True => true,
                                    JsonValueKind.False => false,
                                    JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object>>(prop.Value.GetRawText()),
                                    _ => prop.Value.GetRawText()
                                };
                            }

                            quoteData = quoteDict;
                        }

                        // If we got at least a message or quoteData, consider it successful
                        if (message != null || quoteData != null)
                        {
                            return true;
                        }
                    }
                    catch (JsonException)
                    {
                        // JSON parsing failed, continue to fallback
                    }
                }

                // Strategy 2: Look for quoteData field even without full JSON structure
                var quoteDataPattern = @"[""']?quoteData[""']?\s*:\s*\{([^}]+)\}";
                var match = System.Text.RegularExpressions.Regex.Match(response, quoteDataPattern, System.Text.RegularExpressions.RegexOptions.Singleline);

                if (match.Success)
                {
                    var quoteDataJson = "{" + match.Groups[1].Value + "}";
                    quoteDataJson = quoteDataJson.Replace("'", "\"").Replace(": None", ": null");

                    try
                    {
                        quoteData = JsonSerializer.Deserialize<Dictionary<string, object>>(quoteDataJson);
                        // Use the text before the JSON as the message
                        message = response.Substring(0, match.Index).Trim();
                        if (string.IsNullOrWhiteSpace(message))
                        {
                            message = "I've extracted some quote details for you.";
                        }
                        return true;
                    }
                    catch (JsonException)
                    {
                        // Couldn't parse the quoteData
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during JSON extraction from response");
                return false;
            }
        }
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
    }
}
