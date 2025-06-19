using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Net;
using _4U.chat.Data;
using _4U.chat.Models;
using _4U.chat.Services;

namespace _4U.chat.Services;

public class BackgroundStreamingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundStreamingService> _logger;
    private readonly NotificationService _notificationService;
    private readonly ConcurrentDictionary<int, BackgroundStreamingState> _activeStreams = new();

    public BackgroundStreamingService(IServiceProvider serviceProvider, ILogger<BackgroundStreamingService> logger, NotificationService notificationService)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _notificationService = notificationService;
    }

    public class BackgroundStreamingState
    {
        public int ChatId { get; set; }
        public int MessageId { get; set; }
        public string Model { get; set; } = string.Empty;
        public string ModelDisplayName { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = string.Empty;
        public List<ChatMessage> ChatMessages { get; set; } = new();
        public string CurrentResponse { get; set; } = string.Empty;
        public string CurrentReasoning { get; set; } = string.Empty;
        public bool IsActivelyThinking { get; set; } = true;
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;
        public CancellationTokenSource CancellationToken { get; set; } = new();
        public bool IsWebSearchEnabled { get; set; } = false;
        public WebSearchOptions? WebSearchOptions { get; set; }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Streaming Service started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Process active streams
                var streamsToProcess = _activeStreams.Values.ToList();
                
                foreach (var stream in streamsToProcess)
                {
                    if (stream.CancellationToken.Token.IsCancellationRequested)
                    {
                        _activeStreams.TryRemove(stream.ChatId, out _);
                        continue;
                    }
                    
                    // Update last seen time for active streams
                    stream.LastUpdateTime = DateTime.UtcNow;
                }
                
                // Clean up old streams (older than 30 minutes)
                var oldStreams = _activeStreams.Where(kvp => 
                    DateTime.UtcNow - kvp.Value.LastUpdateTime > TimeSpan.FromMinutes(30)).ToList();
                
                foreach (var oldStream in oldStreams)
                {
                    _logger.LogInformation($"Cleaning up old stream for chat {oldStream.Key}");
                    oldStream.Value.CancellationToken.Cancel();
                    _activeStreams.TryRemove(oldStream.Key, out _);
                }
                
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background streaming service");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    public async Task<bool> StartStreamingAsync(int chatId, int messageId, string model, string modelDisplayName, string apiKey, 
        string systemPrompt, List<ChatMessage> chatMessages, bool isWebSearchEnabled = false, 
        WebSearchOptions? webSearchOptions = null)
    {
        try
        {
            // Cancel any existing stream for this chat
            if (_activeStreams.TryGetValue(chatId, out var existingStream))
            {
                existingStream.CancellationToken.Cancel();
                _activeStreams.TryRemove(chatId, out _);
            }

            var streamingState = new BackgroundStreamingState
            {
                ChatId = chatId,
                MessageId = messageId,
                Model = model,
                ModelDisplayName = modelDisplayName,
                ApiKey = apiKey,
                SystemPrompt = systemPrompt,
                ChatMessages = chatMessages,
                IsWebSearchEnabled = isWebSearchEnabled,
                WebSearchOptions = webSearchOptions,
                CancellationToken = new CancellationTokenSource()
            };

            _activeStreams[chatId] = streamingState;
            
            _logger.LogInformation($"Starting background streaming for chat {chatId}, message {messageId}");

            // Start streaming in background task
            _ = Task.Run(async () => await PerformBackgroundStreamingAsync(streamingState), streamingState.CancellationToken.Token);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to start background streaming for chat {chatId}");
            return false;
        }
    }

    private async Task PerformBackgroundStreamingAsync(BackgroundStreamingState streamingState)
    {
        try
        {
            const int maxRetries = 3;
            var attempt = 0;

            while (attempt < maxRetries)
            {
                attempt++;

                try
                {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var openRouterService = scope.ServiceProvider.GetRequiredService<OpenRouterService>();

                var message = await dbContext.Messages.FindAsync(streamingState.MessageId);
                if (message == null)
                {
                    _logger.LogWarning($"Message {streamingState.MessageId} not found for streaming");
                    return;
                }

                _logger.LogInformation($"Starting streaming for chat {streamingState.ChatId}, message {streamingState.MessageId} (attempt {attempt}/{maxRetries})");

                var chunkCount = 0;
                await foreach (var chunk in openRouterService.SendChatCompletionStreamWithReasoningAsync(
                    streamingState.Model,
                    streamingState.ChatMessages,
                    streamingState.ApiKey,
                    streamingState.CancellationToken.Token,
                    streamingState.IsWebSearchEnabled,
                    streamingState.WebSearchOptions,
                    streamingState.SystemPrompt,
                    streamingState.ModelDisplayName))
                {
                    chunkCount++;
                    streamingState.LastUpdateTime = DateTime.UtcNow;

                    // Handle reasoning tokens
                    if (chunk.IsReasoning)
                    {
                        streamingState.CurrentReasoning += chunk.Reasoning;
                    }

                    // Handle content tokens
                    if (chunk.IsContent)
                    {
                        if (streamingState.IsActivelyThinking)
                        {
                            streamingState.IsActivelyThinking = false;
                        }

                        streamingState.CurrentResponse += chunk.Content;
                    }

                    // Update database periodically
                    if (chunkCount % 5 == 0 || chunk.IsContent)
                    {
                        message.Content = streamingState.CurrentResponse;
                        message.Reasoning = streamingState.CurrentReasoning;
                        message.UpdatedAt = DateTime.UtcNow;
                        await dbContext.SaveChangesAsync();

                        _logger.LogDebug($"Updated database for chat {streamingState.ChatId} - Content: {streamingState.CurrentResponse.Length} chars");
                    }

                    // Small delay to prevent overwhelming
                    await Task.Delay(25, streamingState.CancellationToken.Token);
                }

                // Final save
                message.Content = streamingState.CurrentResponse;
                message.Reasoning = streamingState.CurrentReasoning;
                message.UpdatedAt = DateTime.UtcNow;

                var chat = await dbContext.Chats.FindAsync(streamingState.ChatId);
                if (chat != null)
                {
                    chat.UpdatedAt = DateTime.UtcNow;
                }

                await dbContext.SaveChangesAsync();

                _logger.LogInformation($"Completed streaming for chat {streamingState.ChatId} - Final content: {streamingState.CurrentResponse.Length} chars");
                return; // Success, exit retry loop
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation($"Streaming timeout for chat {streamingState.ChatId} (attempt {attempt}/{maxRetries})");

                if (attempt < maxRetries)
                {
                    var delay = TimeSpan.FromSeconds(5 * attempt); // Progressive delay for timeouts
                    _logger.LogInformation($"Timeout for chat {streamingState.ChatId}. Waiting {delay.TotalSeconds:F1}s before retry {attempt + 1}/{maxRetries}");

                    try
                    {
                        await Task.Delay(delay, streamingState.CancellationToken.Token);
                        continue; // Retry
                    }
                    catch (OperationCanceledException)
                    {
                        return; // Cancelled during delay
                    }
                }

                // Max retries reached for timeout
                await HandleStreamingError(streamingState, $"Request timed out after {maxRetries} attempts. The model may be overloaded. Please try again with a different model or wait a few minutes.");
                return;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation($"Streaming cancelled for chat {streamingState.ChatId}");
                return; // Don't retry cancellations
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, $"HTTP error in background streaming for chat {streamingState.ChatId} (attempt {attempt}/{maxRetries})");

                // Check if this is a retryable error
                var isRetryable = false;
                var statusCodeInt = 0;

                if (httpEx.Data.Contains("StatusCode") && httpEx.Data["StatusCode"] is HttpStatusCode statusCode)
                {
                    statusCodeInt = (int)statusCode;
                    isRetryable = OpenRouterErrorHandler.IsRetryableError(statusCodeInt);
                }

                if (isRetryable && attempt < maxRetries)
                {
                    var delay = OpenRouterErrorHandler.GetRetryDelay(statusCodeInt, attempt);
                    _logger.LogInformation($"Retryable error for chat {streamingState.ChatId}. Waiting {delay.TotalSeconds:F1}s before retry {attempt + 1}/{maxRetries}");

                    try
                    {
                        await Task.Delay(delay, streamingState.CancellationToken.Token);
                        continue; // Retry
                    }
                    catch (OperationCanceledException)
                    {
                        return; // Cancelled during delay
                    }
                }

                // Not retryable or max retries reached
                var errorMessage = httpEx.Message;
                if (attempt >= maxRetries && isRetryable)
                {
                    errorMessage += $"\n\n*Failed after {maxRetries} attempts. Please try again later or use a different model.*";
                }

                await HandleStreamingError(streamingState, errorMessage);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Unexpected error in background streaming for chat {streamingState.ChatId} (attempt {attempt}/{maxRetries})");

                // Don't retry unexpected errors
                await HandleStreamingError(streamingState, $"An unexpected error occurred: {ex.Message}\n\nPlease try again. If the problem persists, try a different model.");
                return;
            }
        }
        
            // If we reach here, all retries failed - this shouldn't happen due to early returns above
            _logger.LogError($"Unexpected exit from retry loop for chat {streamingState.ChatId}");
            await HandleStreamingError(streamingState, "All retry attempts failed unexpectedly. Please try again later.");
        }
        finally
        {
            // Clean up streaming state
            _activeStreams.TryRemove(streamingState.ChatId, out _);
            streamingState.CancellationToken.Dispose();
        }
    }

    private async Task HandleStreamingError(BackgroundStreamingState streamingState, string errorMessage)
    {
        try
        {
            // Add notification for immediate user feedback
            _notificationService.AddError($"Generation failed for chat: {errorMessage}", streamingState.ChatId);
            
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var message = await dbContext.Messages.FindAsync(streamingState.MessageId);
            
            if (message != null)
            {
                // Create a styled error message that's clearly visible to users
                var errorContent = "";
                
                // If we have partial content, save it with error message
                if (!string.IsNullOrEmpty(streamingState.CurrentResponse) || !string.IsNullOrEmpty(streamingState.CurrentReasoning))
                {
                    errorContent = streamingState.CurrentResponse + 
                        "\n\n---\n\n" +
                        "🚨 **Generation Error**\n\n" +
                        errorMessage + 
                        "\n\n*The partial response above was generated before the error occurred.*";
                    message.Reasoning = streamingState.CurrentReasoning;
                    
                    // Add warning notification for partial content
                    _notificationService.AddWarning($"Partial response saved with error in chat", streamingState.ChatId);
                }
                else
                {
                    // No content generated, just show error with clear styling
                    errorContent = "🚨 **Generation Failed**\n\n" + 
                        errorMessage + 
                        "\n\n*No content was generated due to this error.*";
                }
                
                message.Content = errorContent;
                message.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
                
                _logger.LogInformation($"Saved styled error message for chat {streamingState.ChatId}");
            }
        }
        catch (Exception saveEx)
        {
            _logger.LogError(saveEx, $"Failed to save error message for chat {streamingState.ChatId}");
            _notificationService.AddError($"Critical error: Failed to save error message", streamingState.ChatId);
        }
    }

    public bool IsStreamingActive(int chatId)
    {
        return _activeStreams.ContainsKey(chatId);
    }

    public BackgroundStreamingState? GetStreamingState(int chatId)
    {
        _activeStreams.TryGetValue(chatId, out var state);
        return state;
    }

    public void StopStreaming(int chatId)
    {
        if (_activeStreams.TryGetValue(chatId, out var stream))
        {
            stream.CancellationToken.Cancel();
            _activeStreams.TryRemove(chatId, out _);
        }
    }

    public void StopAllStreaming()
    {
        foreach (var stream in _activeStreams.Values)
        {
            stream.CancellationToken.Cancel();
        }
        _activeStreams.Clear();
    }

    public override void Dispose()
    {
        StopAllStreaming();
        base.Dispose();
    }
}