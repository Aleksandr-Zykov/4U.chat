using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using _4U.chat.Data;
using _4U.chat.Models;

namespace _4U.chat.Services;

public class BackgroundJobService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundJobService> _logger;
    private readonly ConcurrentDictionary<int, JobState> _activeJobs = new();

    public BackgroundJobService(IServiceProvider serviceProvider, ILogger<BackgroundJobService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public class JobState
    {
        public int MessageId { get; set; }
        public CancellationTokenSource CancellationToken { get; set; } = new();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Job Service started");
        await Task.CompletedTask;
    }

    public async Task<bool> StartImageGenerationJobAsync(int messageId, string model, string prompt, string apiKey)
    {
        if (_activeJobs.ContainsKey(messageId))
        {
            _logger.LogWarning($"Image generation job for message {messageId} is already running.");
            return false;
        }

        var jobState = new JobState
        {
            MessageId = messageId,
            CancellationToken = new CancellationTokenSource()
        };

        if (!_activeJobs.TryAdd(messageId, jobState))
        {
            _logger.LogWarning($"Failed to add image generation job for message {messageId}.");
            return false;
        }

        _logger.LogInformation($"Starting image generation job for message {messageId}");

        _ = Task.Run(async () => await PerformImageGenerationAsync(jobState, model, prompt, apiKey), jobState.CancellationToken.Token);

        return true;
    }

    private async Task PerformImageGenerationAsync(JobState jobState, string model, string prompt, string apiKey)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var googleService = scope.ServiceProvider.GetRequiredService<GoogleService>();

            var message = await dbContext.Messages.FindAsync(jobState.MessageId);
            if (message == null)
            {
                _logger.LogWarning($"Message {jobState.MessageId} not found for image generation job.");
                return;
            }

            try
            {
                var imageBase64List = await googleService.GenerateImageAsync(model, prompt, apiKey);
                
                var attachments = new List<MessageAttachment>();
                foreach (var imageBase64 in imageBase64List)
                {
                    attachments.Add(new MessageAttachment
                    {
                        FileName = $"generated_image_{DateTime.UtcNow:yyyyMMddHHmmssfff}.png",
                        ContentType = "image/png",
                        Base64Data = imageBase64,
                        FileSize = imageBase64.Length,
                        Type = AttachmentType.Image,
                        UploadedAt = DateTime.UtcNow
                    });
                }

                message.Attachments = attachments;
                var imageCount = message.Attachments.Count(a => a.Type == AttachmentType.Image);
                message.Content = $"*Generated {imageCount} image{(imageCount > 1 ? "s" : "")}*";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating image for message {jobState.MessageId}");
                message.Content = $"🚨 **Image Generation Failed**\n\n```\n{ex.Message}\n```";
            }
            finally
            {
                message.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
                _logger.LogInformation($"Finished image generation job for message {jobState.MessageId}");
            }
        }
        finally
        {
            _activeJobs.TryRemove(jobState.MessageId, out _);
            jobState.CancellationToken.Dispose();
        }
    }

    public bool IsJobActive(int messageId)
    {
        return _activeJobs.ContainsKey(messageId);
    }
}