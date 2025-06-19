using System.Text.Json;

namespace _4U.chat.Services;

public class OpenRouterErrorHandler
{
    public class OpenRouterError
    {
        public int Code { get; set; }
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class ErrorResponse
    {
        public OpenRouterError Error { get; set; } = new();
    }

    public class ModerationErrorMetadata
    {
        public string[] Reasons { get; set; } = Array.Empty<string>();
        public string FlaggedInput { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        public string ModelSlug { get; set; } = string.Empty;
    }

    public class ProviderErrorMetadata
    {
        public string ProviderName { get; set; } = string.Empty;
        public object? Raw { get; set; }
    }

    public static string ProcessOpenRouterError(HttpResponseMessage response, string? responseContent = null)
    {
        var statusCode = (int)response.StatusCode;
        
        // Try to parse OpenRouter error response
        if (!string.IsNullOrEmpty(responseContent))
        {
            try
            {
                var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (errorResponse?.Error != null)
                {
                    return FormatOpenRouterError(errorResponse.Error);
                }
            }
            catch
            {
                // If JSON parsing fails, fall back to status code interpretation
            }
        }

        // Fall back to HTTP status code interpretation
        return statusCode switch
        {
            400 => "Bad Request: The request is invalid or missing required parameters. Please check your input and try again.",
            401 => "Unauthorized: Invalid API key or expired session. Please check your OpenRouter API key in Settings.",
            402 => "Payment Required: Your account has insufficient credits. Please add more credits to your OpenRouter account and try again.",
            403 => "Forbidden: Your input was flagged by content moderation. Please modify your message and try again.",
            408 => "Request Timeout: The request took too long to process. Please try again or use a different model.",
            429 => "Rate Limited: You're sending requests too quickly. Please wait a moment and try again.",
            502 => "Bad Gateway: The selected model is currently unavailable. Please try a different model or wait a few moments.",
            503 => "Service Unavailable: No model providers are currently available for your request. Please try again later or select a different model.",
            _ => $"An unexpected error occurred (HTTP {statusCode}). Please try again later."
        };
    }

    private static string FormatOpenRouterError(OpenRouterError error)
    {
        var baseMessage = error.Code switch
        {
            400 => "Bad Request",
            401 => "Unauthorized Access",
            402 => "Insufficient Credits",
            403 => "Content Flagged",
            408 => "Request Timeout",
            429 => "Rate Limited",
            502 => "Model Unavailable",
            503 => "Service Unavailable",
            _ => "API Error"
        };

        var message = $"{baseMessage}: {error.Message}";

        // Add specific metadata information
        if (error.Metadata != null)
        {
            if (error.Code == 403) // Moderation error
            {
                var moderationInfo = ParseModerationMetadata(error.Metadata);
                if (moderationInfo != null)
                {
                    message += $"\n\nYour input was flagged for: {string.Join(", ", moderationInfo.Reasons)}";
                    if (!string.IsNullOrEmpty(moderationInfo.FlaggedInput))
                    {
                        message += $"\nFlagged content: \"{moderationInfo.FlaggedInput}\"";
                    }
                    if (!string.IsNullOrEmpty(moderationInfo.ProviderName))
                    {
                        message += $"\nProvider: {moderationInfo.ProviderName}";
                    }
                }
            }
            else if (error.Code == 502) // Provider error
            {
                var providerInfo = ParseProviderMetadata(error.Metadata);
                if (providerInfo != null && !string.IsNullOrEmpty(providerInfo.ProviderName))
                {
                    message += $"\nProvider: {providerInfo.ProviderName}";
                }
            }
        }

        // Add helpful suggestions based on error type
        message += error.Code switch
        {
            401 => "\n\nPlease check your OpenRouter API key in Settings.",
            402 => "\n\nPlease add credits to your OpenRouter account at https://openrouter.ai/credits",
            403 => "\n\nPlease modify your message to comply with content policies.",
            408 => "\n\nTry using a faster model or simplifying your request.",
            429 => "\n\nPlease wait a moment before sending another message.",
            502 => "\n\nTry selecting a different model or wait a few minutes.",
            503 => "\n\nTry again in a few minutes or select a different model.",
            _ => "\n\nIf this problem persists, please try a different model."
        };

        return message;
    }

    private static ModerationErrorMetadata? ParseModerationMetadata(Dictionary<string, object> metadata)
    {
        try
        {
            var json = JsonSerializer.Serialize(metadata);
            return JsonSerializer.Deserialize<ModerationErrorMetadata>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    private static ProviderErrorMetadata? ParseProviderMetadata(Dictionary<string, object> metadata)
    {
        try
        {
            var json = JsonSerializer.Serialize(metadata);
            return JsonSerializer.Deserialize<ProviderErrorMetadata>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    public static bool IsRetryableError(int statusCode)
    {
        return statusCode switch
        {
            408 => true,  // Request Timeout
            429 => true,  // Rate Limited
            502 => true,  // Bad Gateway
            503 => true,  // Service Unavailable
            // Non-retryable errors:
            400 => false, // Bad Request
            401 => false, // Unauthorized
            402 => false, // Payment Required (insufficient credits)
            403 => false, // Forbidden (content flagged)
            _ => false
        };
    }

    public static TimeSpan GetRetryDelay(int statusCode, int attemptNumber)
    {
        var baseDelay = statusCode switch
        {
            408 => TimeSpan.FromSeconds(2),   // Quick retry for timeouts
            429 => TimeSpan.FromSeconds(30),  // Longer delay for rate limits
            502 => TimeSpan.FromSeconds(5),   // Medium delay for bad gateway
            503 => TimeSpan.FromSeconds(10),  // Medium delay for service unavailable
            _ => TimeSpan.FromSeconds(5)
        };

        // Exponential backoff with jitter
        var multiplier = Math.Pow(2, attemptNumber - 1);
        var jitter = new Random().NextDouble() * 0.1; // 10% jitter
        
        return TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * multiplier * (1 + jitter));
    }
}