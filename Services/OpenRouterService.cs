
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;
using System.Net;
using Microsoft.Extensions.Configuration;

namespace _4U.chat.Services
{
    public class ModelConfiguration
    {
        public string ModelId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string UniqueId => $"{ModelId}|{DisplayName}";
        public string Description { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        
        public bool HasVisionSupport { get; set; } = false;
        public bool HasPdfSupport { get; set; } = false;
        public bool HasReasoningSupport { get; set; } = false;
        public bool IsImageGeneration { get; set; } = false;
        public bool IsFavorite { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public string InactiveReason { get; set; } = string.Empty;
    }

    public class OpenRouterService
    {
        private readonly HttpClient _httpClient;
        public static List<ModelConfiguration> ModelConfigurations { get; set; } = new();

        public OpenRouterService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
            
            var modelConfigurations = configuration.GetSection("ModelConfigurations").Get<List<ModelConfiguration>>();
            if (modelConfigurations != null)
            {
                ModelConfigurations = modelConfigurations;
            }
        }

        // Helper methods to access model configuration
        public static ModelConfiguration? GetModelConfiguration(string modelId)
        {
            return ModelConfigurations.FirstOrDefault(m => m.ModelId == modelId);
        }

        public static ModelConfiguration? GetModelConfigurationByUniqueId(string uniqueId)
        {
            return ModelConfigurations.FirstOrDefault(m => m.UniqueId == uniqueId);
        }

        public static List<ModelConfiguration> GetFavoriteModels()
        {
            return ModelConfigurations.Where(m => m.IsFavorite).ToList();
        }

        public static List<ModelConfiguration> GetActiveModels()
        {
            return ModelConfigurations.Where(m => m.IsActive).ToList();
        }

        public static List<ModelConfiguration> GetModelsWithVision()
        {
            return ModelConfigurations.Where(m => m.HasVisionSupport).ToList();
        }

        public static List<ModelConfiguration> GetModelsWithPdf()
        {
            return ModelConfigurations.Where(m => m.HasPdfSupport).ToList();
        }

        public static List<ModelConfiguration> GetModelsWithReasoning()
        {
            return ModelConfigurations.Where(m => m.HasReasoningSupport).ToList();
        }

        public static List<ModelConfiguration> GetImageGenerationModels()
        {
            return ModelConfigurations.Where(m => m.IsImageGeneration).ToList();
        }

        // User-specific favorite model methods
        public static List<string> GetDefaultFavoriteModelIds()
        {
            return ModelConfigurations.Where(m => m.IsFavorite).Select(m => m.UniqueId).ToList();
        }

        public static List<ModelConfiguration> GetUserFavoriteModels(string? userFavoritesJson)
        {
            if (string.IsNullOrEmpty(userFavoritesJson))
            {
                // Return default favorites if user hasn't set any
                return ModelConfigurations.Where(m => m.IsFavorite).ToList();
            }

            try
            {
                var favoriteIds = JsonSerializer.Deserialize<List<string>>(userFavoritesJson) ?? new List<string>();
                return ModelConfigurations.Where(m => favoriteIds.Contains(m.UniqueId)).ToList();
            }
            catch
            {
                // Return default favorites if JSON parsing fails
                return ModelConfigurations.Where(m => m.IsFavorite).ToList();
            }
        }

        public static bool IsUserFavoriteModel(string uniqueId, string? userFavoritesJson)
        {
            if (string.IsNullOrEmpty(userFavoritesJson))
            {
                // Use default favorites if user hasn't set any
                var config = GetModelConfigurationByUniqueId(uniqueId);
                return config?.IsFavorite ?? false;
            }

            try
            {
                var favoriteIds = JsonSerializer.Deserialize<List<string>>(userFavoritesJson) ?? new List<string>();
                return favoriteIds.Contains(uniqueId);
            }
            catch
            {
                // Use default favorites if JSON parsing fails
                var config = GetModelConfigurationByUniqueId(uniqueId);
                return config?.IsFavorite ?? false;
            }
        }

        public static string SerializeFavoriteModels(List<string> favoriteIds)
        {
            try
            {
                return JsonSerializer.Serialize(favoriteIds);
            }
            catch
            {
                return "[]";
            }
        }


        public async Task<T> SendChatCompletionWithStructuredOutputAsync<T>(string model, List<ChatMessage> messages, object jsonSchema, string? apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentException("API key is required for OpenRouter service");
            }

            var request = new
            {
                model = model,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                response_format = new
                {
                    type = "json_schema",
                    json_schema = jsonSchema
                }
            };

            var json = JsonSerializer.Serialize(request);
            System.Diagnostics.Debug.WriteLine($"Sending structured output request: {json}");
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = content
            };
            requestMessage.Headers.Add("Authorization", $"Bearer {apiKey}");

            var response = await _httpClient.SendAsync(requestMessage);
            var responseContent = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"API Response: {responseContent}");

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"OpenRouter API error: {responseContent}");
            }

            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
            var contentString = result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
            System.Diagnostics.Debug.WriteLine($"Content string: {contentString}");
            
            var deserializedResult = JsonSerializer.Deserialize<T>(contentString);
            System.Diagnostics.Debug.WriteLine($"Deserialized result: {deserializedResult}");
            
            return deserializedResult ?? throw new Exception("Failed to deserialize structured output");
        }


        public async Task<ChatNameResult> GenerateChatNameAsync(string userMessage, string? apiKey)
        {
            var messages = new List<ChatMessage>
            {
                new ChatMessage 
                { 
                    Role = "user", 
                    Content = $"Generate a short, descriptive name for a chat based on this user message: \"{userMessage}\". The name should be maximum 3 words and capture the main topic or intent." 
                }
            };

            var schema = new
            {
                name = "chat_name",
                strict = true,
                schema = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new
                        {
                            type = "string",
                            description = "A short, descriptive name for the chat (maximum 3 words)"
                        }
                    },
                    required = new[] { "name" },
                    additionalProperties = false
                }
            };

            return await SendChatCompletionWithStructuredOutputAsync<ChatNameResult>("google/gemini-2.0-flash-lite-001", messages, schema, apiKey);
        }
        
        private object? GetProviderRouting(string model)
        {
            // Configure provider routing for specific models
            return model switch
            {
                "deepseek/deepseek-r1-0528" => new { only = new[] { "lambda/fp8" } },
                "deepseek/deepseek-r1-distill-llama-70b" => new { only = new[] { "lambda/fp8" } },
                "deepseek/deepseek-chat-v3-0324" => new { only = new[] { "lambda/fp8" } },
                _ => null
            };
        }

        private static object? GetReasoningConfig(string displayName)
        {
            // Configure reasoning parameters for models that support it based on display name
            return displayName switch
            {
                "Gemini 2.5 Flash Lite Thinking" => new { max_tokens = 10000 },
                "Gemini 2.5 Flash Thinking" => new { max_tokens = 10000 },
                "Gemini 2.5 Pro" => new { max_tokens = 10000 },
                "Claude 4 Sonnet Thinking" => new { max_tokens = 10000 },
                "Claude 4 Opus Thinking" => new { max_tokens = 10000 },
                _ => null // No reasoning config for non-reasoning models
            };
        }


        public async IAsyncEnumerable<string> SendChatCompletionStreamAsync(string model, List<ChatMessage> messages, string? apiKey, [EnumeratorCancellation] CancellationToken cancellationToken = default, bool enableWebSearch = false, WebSearchOptions? webSearchOptions = null, string? systemPrompt = null, string? modelDisplayName = null)
        {
            await foreach (var chunk in SendChatCompletionStreamWithReasoningAsync(model, messages, apiKey, cancellationToken, enableWebSearch, webSearchOptions, systemPrompt, modelDisplayName))
            {
                if (chunk.IsContent)
                {
                    yield return chunk.Content;
                }
            }
        }


        public async IAsyncEnumerable<StreamingChunk> SendChatCompletionStreamWithReasoningAsync(string model, List<ChatMessage> messages, string? apiKey, [EnumeratorCancellation] CancellationToken cancellationToken = default, bool enableWebSearch = false, WebSearchOptions? webSearchOptions = null, string? systemPrompt = null, string? modelDisplayName = null)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentException("API key is required for OpenRouter service");
            }

            // Ensure system prompt is first message if provided
            var processedMessages = PrepareMessagesWithSystemPrompt(messages, systemPrompt);

            var request = new
            {
                model = enableWebSearch ? GetWebSearchModel(model) : model,
                messages = processedMessages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                stream = true,
                transforms = new[] { "middle-out" },
                provider = GetProviderRouting(model),
                reasoning = !string.IsNullOrEmpty(modelDisplayName) ? GetReasoningConfig(modelDisplayName) : null,
                plugins = GetPlugins(model, enableWebSearch, webSearchOptions),
                web_search_options = webSearchOptions?.ToApiObject()
            };

            var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = content
            };
            requestMessage.Headers.Add("Authorization", $"Bearer {apiKey}");

            var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = OpenRouterErrorHandler.ProcessOpenRouterError(response, errorContent);
                throw new HttpRequestException(errorMessage, null, response.StatusCode);
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (line.StartsWith("data: "))
                {
                    var data = line.Substring(6);
                    if (data == "[DONE]") break;

                    var chunk = ParseStreamingChunk(data);
                    if (chunk != null && (chunk.IsReasoning || chunk.IsContent))
                    {
                        yield return chunk;
                    }
                }
            }
        }

        private StreamingChunk? ParseStreamingChunk(string data)
        {
            try
            {
                var responseJson = JsonSerializer.Deserialize<JsonElement>(data);
                
                // Check for errors in the stream
                if (responseJson.TryGetProperty("error", out var errorProp))
                {
                    var errorCode = errorProp.TryGetProperty("code", out var codeProp) ? codeProp.GetInt32() : 0;
                    var errorMessage = errorProp.TryGetProperty("message", out var messageProp) ? messageProp.GetString() : "Unknown error";
                    
                    var error = new OpenRouterErrorHandler.OpenRouterError
                    {
                        Code = errorCode,
                        Message = errorMessage ?? "Unknown error"
                    };
                    
                    if (errorProp.TryGetProperty("metadata", out var metadataProp))
                    {
                        error.Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metadataProp.GetRawText());
                    }
                    
                    var formattedError = OpenRouterErrorHandler.ProcessOpenRouterError(new HttpResponseMessage((HttpStatusCode)errorCode), null);
                    throw new HttpRequestException(formattedError, null, (HttpStatusCode)errorCode);
                }
                
                var delta = responseJson.GetProperty("choices")[0].GetProperty("delta");
                
                var chunk = new StreamingChunk();
                
                if (delta.TryGetProperty("reasoning", out var reasoningProp))
                {
                    chunk.Reasoning = reasoningProp.GetString() ?? string.Empty;
                }
                
                if (delta.TryGetProperty("content", out var contentProp))
                {
                    chunk.Content = contentProp.GetString() ?? string.Empty;
                }

                return chunk;
            }
            catch (JsonException)
            {
                // Skip malformed JSON
                return null;
            }
        }
        
        private string GetWebSearchModel(string model)
        {
            // Use :online suffix for simple web search enablement
            return $"{model}:online";
        }
        
        private object[]? GetWebSearchPlugins(WebSearchOptions? options)
        {
            if (options == null)
            {
                return new[] { new { id = "web" } };
            }
            
            return new[]
            {
                new
                {
                    id = "web",
                    max_results = options.MaxResults,
                    search_prompt = options.SearchPrompt
                }
            };
        }
        
        private object[]? GetPlugins(string model, bool enableWebSearch = false, WebSearchOptions? webSearchOptions = null)
        {
            var plugins = new List<object>();
            
            // Add web search plugin if enabled
            if (enableWebSearch)
            {
                var webPlugins = GetWebSearchPlugins(webSearchOptions);
                if (webPlugins != null)
                {
                    plugins.AddRange(webPlugins);
                }
            }
            
            // Add file parser plugin for PDF support on non-native models
            if (!ModelSupportsNativeFiles(model))
            {
                plugins.Add(new
                {
                    id = "file-parser",
                    pdf = new
                    {
                        engine = "mistral-ocr" // Will automatically fallback for non-native models
                    }
                });
            }
            
            return plugins.Any() ? plugins.ToArray() : null;
        }
        
        public static bool ModelSupportsFiles(string uniqueId)
        {
            var config = GetModelConfigurationByUniqueId(uniqueId);
            return config?.HasVisionSupport == true || config?.HasPdfSupport == true;
        }
        
        public static bool ModelSupportsImages(string uniqueId)
        {
            var config = GetModelConfigurationByUniqueId(uniqueId);
            return config?.HasVisionSupport ?? false;
        }

        public static bool ModelSupportsPdf(string uniqueId)
        {
            var config = GetModelConfigurationByUniqueId(uniqueId);
            return config?.HasPdfSupport ?? false;
        }

        public static bool ModelSupportsReasoning(string uniqueId)
        {
            var config = GetModelConfigurationByUniqueId(uniqueId);
            return config?.HasReasoningSupport ?? false;
        }

        public static bool IsImageGenerationModel(string uniqueId)
        {
            var config = GetModelConfigurationByUniqueId(uniqueId);
            return config?.IsImageGeneration ?? false;
        }
        
        public static bool ModelSupportsNativeFiles(string uniqueId)
        {
            var config = GetModelConfigurationByUniqueId(uniqueId);
            return config?.HasPdfSupport == true;
        }
        
        private List<Annotation> ParseAnnotations(JsonElement annotationsElement)
        {
            var annotations = new List<Annotation>();
            
            if (annotationsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var annotationElement in annotationsElement.EnumerateArray())
                {
                    if (annotationElement.TryGetProperty("type", out var typeElement))
                    {
                        var annotation = new Annotation
                        {
                            Type = typeElement.GetString() ?? ""
                        };
                        
                        if (annotation.Type == "url_citation" && 
                            annotationElement.TryGetProperty("url_citation", out var urlCitationElement))
                        {
                            annotation.UrlCitation = new UrlCitation
                            {
                                Url = urlCitationElement.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? "" : "",
                                Title = urlCitationElement.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "",
                                Content = urlCitationElement.TryGetProperty("content", out var contentProp) ? contentProp.GetString() ?? "" : "",
                                StartIndex = urlCitationElement.TryGetProperty("start_index", out var startProp) ? startProp.GetInt32() : 0,
                                EndIndex = urlCitationElement.TryGetProperty("end_index", out var endProp) ? endProp.GetInt32() : 0
                            };
                        }
                        
                        annotations.Add(annotation);
                    }
                }
            }
            
            return annotations;
        }

        private List<ChatMessage> PrepareMessagesWithSystemPrompt(List<ChatMessage> messages, string? systemPrompt)
        {
            var processedMessages = new List<ChatMessage>();
            
            // Add system prompt as first message if provided
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                processedMessages.Add(ChatMessage.CreateTextMessage("system", systemPrompt));
            }
            
            // Add all non-system messages (in case there are existing system messages, remove them to avoid duplicates)
            processedMessages.AddRange(messages.Where(m => m.Role != "system"));
            
            return processedMessages;
        }
    }

    public class ChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public object Content { get; set; } = string.Empty;
        
        // Helper method to create a simple text message
        public static ChatMessage CreateTextMessage(string role, string content)
        {
            return new ChatMessage { Role = role, Content = content };
        }
        
        // Helper method to create a message with attachments
        public static ChatMessage CreateMessageWithAttachments(string role, string text, List<ChatAttachment> attachments)
        {
            var contentItems = new List<object>();
            
            // Add text content first
            if (!string.IsNullOrEmpty(text))
            {
                contentItems.Add(new { type = "text", text = text });
            }
            
            // Add file attachments
            foreach (var attachment in attachments)
            {
                if (attachment.Type == ChatAttachmentType.Image)
                {
                    contentItems.Add(new
                    {
                        type = "image_url",
                        image_url = new
                        {
                            url = $"data:{attachment.ContentType};base64,{attachment.Base64Data}"
                        }
                    });
                }
                else if (attachment.Type == ChatAttachmentType.PDF)
                {
                    contentItems.Add(new
                    {
                        type = "file",
                        file = new
                        {
                            filename = attachment.FileName,
                            file_data = $"data:{attachment.ContentType};base64,{attachment.Base64Data}"
                        }
                    });
                }
            }
            
            return new ChatMessage 
            {
                Role = role, 
                Content = contentItems.Count == 1 && contentItems[0] is string 
                    ? contentItems[0] 
                    : contentItems.ToArray()
            };
        }
    }
    
    public class ChatAttachment
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string Base64Data { get; set; } = string.Empty;
        public ChatAttachmentType Type { get; set; }
    }
    
    public enum ChatAttachmentType
    {
        Image,
        PDF
    }

    public class ChatNameResult
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class StreamingChunk
    {
        public string Content { get; set; } = string.Empty;
        public string Reasoning { get; set; } = string.Empty;
        public bool IsReasoning => !string.IsNullOrEmpty(Reasoning);
        public bool IsContent => !string.IsNullOrEmpty(Content);
    }
    
    public class ChatCompletionResult
    {
        public string Content { get; set; } = string.Empty;
        public List<Annotation> Annotations { get; set; } = new();
    }
    
    public class Annotation
    {
        public string Type { get; set; } = string.Empty;
        public UrlCitation? UrlCitation { get; set; }
    }
    
    public class UrlCitation
    {
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
    }
    
    public class WebSearchOptions
    {
        public int MaxResults { get; set; } = 10;
        public string SearchPrompt { get; set; } = "A web search was conducted. Incorporate the following web search results into your response.\n\nIMPORTANT: Cite them using markdown links named using the domain of the source.\nExample: [nytimes.com](https://nytimes.com/some-page).";
        public string SearchContextSize { get; set; } = "medium"; // low, medium, high
        
        public object ToApiObject()
        {
            return new
            {
                search_context_size = SearchContextSize
            };
        }
    }
}
