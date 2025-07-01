using _4U.chat.Data;
using _4U.chat.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace _4U.chat.Services
{
    public class ChatService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
        private readonly UserManager<User> _userManager;
        private readonly OpenRouterService _openRouterService;
        private readonly GoogleService _googleService;
        private readonly BackgroundStreamingService _backgroundStreamingService;
        private readonly BackgroundJobService _backgroundJobService;
        private readonly ILogger<ChatService> _logger;
        private readonly NotificationService _notificationService;

        public ChatService(
            IDbContextFactory<ApplicationDbContext> dbContextFactory,
            UserManager<User> userManager,
            OpenRouterService openRouterService,
            GoogleService googleService,
            BackgroundStreamingService backgroundStreamingService,
            BackgroundJobService backgroundJobService,
            ILogger<ChatService> logger,
            NotificationService notificationService)
        {
            _dbContextFactory = dbContextFactory;
            _userManager = userManager;
            _openRouterService = openRouterService;
            _googleService = googleService;
            _backgroundStreamingService = backgroundStreamingService;
            _backgroundJobService = backgroundJobService;
            _logger = logger;
            _notificationService = notificationService;
        }

        public async Task<List<Chat>> GetUserChatsAsync(string userId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await dbContext.Chats
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();
        }

        public async Task<Chat?> GetChatWithMessagesAsync(int chatId, string userId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var chat = await dbContext.Chats
                .Include(c => c.Messages)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.Id == chatId && c.UserId == userId);

            if (chat != null)
            {
                chat.Messages = chat.Messages.OrderBy(m => m.CreatedAt).ToList();
            }
            return chat;
        }

        public async Task DeleteChatAsync(int chatId, string userId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var chatToDelete = await dbContext.Chats
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == chatId && c.UserId == userId);

            if (chatToDelete != null)
            {
                dbContext.Messages.RemoveRange(chatToDelete.Messages);
                dbContext.Chats.Remove(chatToDelete);
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task<bool> TogglePinChatAsync(int chatId, string userId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var chatToPin = await dbContext.Chats
                .FirstOrDefaultAsync(c => c.Id == chatId && c.UserId == userId);

            if (chatToPin != null)
            {
                chatToPin.IsPinned = !chatToPin.IsPinned;
                await dbContext.SaveChangesAsync();
                return true;
            }
            return false;
        }

        public async Task<Chat> CreateNewChatAsync(string userId, string title = "New Chat")
        {
            var newChat = new Chat
            {
                Title = title,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            dbContext.Chats.Add(newChat);
            await dbContext.SaveChangesAsync();

            return newChat;
        }

        public async Task<(Message userMessage, Chat chat)> AddUserMessageAsync(int chatId, string content, List<MessageAttachment> attachments)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            var chat = await dbContext.Chats.FindAsync(chatId);
            if(chat == null)
            {
                throw new InvalidOperationException("Chat not found");
            }

            var userMessage = new Message
            {
                Role = "user",
                Content = content,
                ChatId = chatId,
                CreatedAt = DateTime.UtcNow,
                Attachments = attachments
            };

            dbContext.Messages.Add(userMessage);
            await dbContext.SaveChangesAsync();

            return (userMessage, chat);
        }

        public async Task<Message?> GenerateResponseAsync(Chat chat, string selectedModel, bool isWebSearchEnabled, Func<Task<string>> buildSystemPrompt)
        {
            var user = await _userManager.FindByIdAsync(chat.UserId);
            if (user == null) return null;
            
            var isImageGen = OpenRouterService.IsImageGenerationModel(selectedModel);

            if (isImageGen)
            {
                return await GenerateImageAsync(chat, selectedModel, user.GoogleAiApiKey);
            }
            else
            {
                return await GenerateTextAsync(chat, selectedModel, user.OpenRouterApiKey, isWebSearchEnabled, await buildSystemPrompt());
            }
        }

        private async Task<Message?> GenerateImageAsync(Chat chat, string selectedModel, string? apiKey)
        {
            var userMessage = (await GetChatWithMessagesAsync(chat.Id, chat.UserId))?.Messages
                .LastOrDefault(m => m.Role == "user");
            
            if (userMessage == null) return null;

            if (string.IsNullOrEmpty(apiKey))
            {
                _notificationService.AddError("Google AI Studio API key is missing. Please add it in Settings.", chat.Id);
                return null;
            }

            var assistantMessage = new Message
            {
                Role = "assistant",
                Content = "*Generating images...*",
                ChatId = chat.Id,
                CreatedAt = DateTime.UtcNow,
                Model = GetActualModelId(selectedModel)
            };

            using (var dbContext = await _dbContextFactory.CreateDbContextAsync())
            {
                dbContext.Messages.Add(assistantMessage);
                await dbContext.SaveChangesAsync();
            }

            await _backgroundJobService.StartImageGenerationJobAsync(assistantMessage.Id, GetActualModelId(selectedModel), userMessage.Content, apiKey);
            return assistantMessage;
        }

        private async Task<Message?> GenerateTextAsync(Chat chat, string selectedModel, string? apiKey, bool isWebSearchEnabled, string systemPrompt)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                _notificationService.AddError("OpenRouter API key is missing. Please add it in Settings.", chat.Id);
                return null;
            }

            var assistantMessage = new Message
            {
                Role = "assistant",
                Content = "",
                ChatId = chat.Id,
                CreatedAt = DateTime.UtcNow,
                Model = GetActualModelId(selectedModel)
            };

            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            dbContext.Messages.Add(assistantMessage);
            await dbContext.SaveChangesAsync();

            var chatMessages = (await GetChatWithMessagesAsync(chat.Id, chat.UserId))!
                .Messages
                .Where(m => m.Id != assistantMessage.Id)
                .OrderBy(m => m.CreatedAt)
                .Select(ConvertToOpenRouterMessage)
                .ToList();

            var success = await _backgroundStreamingService.StartStreamingAsync(
                chat.Id,
                assistantMessage.Id,
                GetActualModelId(selectedModel),
                GetModelDisplayNameFromUniqueId(selectedModel),
                apiKey,
                systemPrompt,
                chatMessages,
                isWebSearchEnabled,
                isWebSearchEnabled ? new WebSearchOptions() : null
            );

            if (!success)
            {
                _logger.LogWarning($"Failed to start background streaming for chat {chat.Id}");
            }

            return assistantMessage;
        }
        
        public async Task<string?> GenerateChatNameAsync(string userMessage, string? apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                return userMessage.Length > 50 ? userMessage.Substring(0, 50) + "..." : userMessage;
            }
            var result = await _openRouterService.GenerateChatNameAsync(userMessage, apiKey);
            
            var newName = result?.Name;
            if (string.IsNullOrEmpty(newName))
            {
                 newName = userMessage.Length > 50 ? userMessage.Substring(0, 50) + "..." : userMessage;
            }

            return newName;
        }

        public async Task UpdateChatTitleAsync(int chatId, string newTitle)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var chat = await dbContext.Chats.FindAsync(chatId);
            if(chat != null)
            {
                chat.Title = newTitle;
                chat.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task RetryMessageAsync(int chatId, int messageId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var message = await dbContext.Messages.FindAsync(messageId);
            if (message == null) return;

            var messagesToDelete = await dbContext.Messages
                .Where(m => m.ChatId == chatId && m.CreatedAt > message.CreatedAt)
                .ToListAsync();

            if (messagesToDelete.Any())
            {
                dbContext.Messages.RemoveRange(messagesToDelete);
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task SaveEditedMessageAsync(int messageId, string newContent)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var message = await dbContext.Messages.FindAsync(messageId);
            if (message == null) return;

            message.Content = newContent.Trim();
            
            var messagesToDelete = await dbContext.Messages
                .Where(m => m.ChatId == message.ChatId && m.CreatedAt > message.CreatedAt)
                .ToListAsync();

            if (messagesToDelete.Any())
            {
                dbContext.Messages.RemoveRange(messagesToDelete);
            }

            await dbContext.SaveChangesAsync();
        }

        public async Task<Chat> BranchOffChatAsync(int originalChatId, int messageId, string userId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var originalChat = await dbContext.Chats.FindAsync(originalChatId);
            if (originalChat == null || originalChat.UserId != userId) throw new InvalidOperationException("Original chat not found or access denied.");
            
            var assistantMessage = await dbContext.Messages.FindAsync(messageId);
            if (assistantMessage == null || assistantMessage.ChatId != originalChatId) throw new InvalidOperationException("Branch message not found.");

            var branchedChat = new Chat
            {
                Title = originalChat.Title,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsBranched = true,
                OriginalChatId = originalChat.Id
            };

            dbContext.Chats.Add(branchedChat);
            await dbContext.SaveChangesAsync();

            var messagesToCopy = await dbContext.Messages
                .Where(m => m.ChatId == originalChatId && m.CreatedAt <= assistantMessage.CreatedAt)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            foreach (var originalMessage in messagesToCopy)
            {
                var copiedMessage = new Message
                {
                    Role = originalMessage.Role,
                    Content = originalMessage.Content,
                    ChatId = branchedChat.Id,
                    CreatedAt = originalMessage.CreatedAt,
                    Model = originalMessage.Model,
                    Attachments = originalMessage.Attachments.Select(a => new MessageAttachment
                    {
                        FileName = a.FileName,
                        ContentType = a.ContentType,
                        Base64Data = a.Base64Data,
                        FileSize = a.FileSize,
                        Type = a.Type,
                        UploadedAt = a.UploadedAt
                    }).ToList()
                };
                dbContext.Messages.Add(copiedMessage);
            }

            await dbContext.SaveChangesAsync();
            return branchedChat;
        }

        public async Task RetryAssistantMessageAsync(int messageId)
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            var message = await dbContext.Messages.FindAsync(messageId);
            if(message == null) return;

            var messagesToDelete = await dbContext.Messages
                .Where(m => m.ChatId == message.ChatId && m.CreatedAt >= message.CreatedAt)
                .ToListAsync();

            if (messagesToDelete.Any())
            {
                dbContext.Messages.RemoveRange(messagesToDelete);
                await dbContext.SaveChangesAsync();
            }
        }
        
        private ChatMessage ConvertToOpenRouterMessage(Message message)
        {
            if (!message.HasAttachments)
            {
                return ChatMessage.CreateTextMessage(message.Role, message.Content);
            }
            
            var chatAttachments = message.Attachments.Select(a => new ChatAttachment
            {
                FileName = a.FileName,
                ContentType = a.ContentType,
                Base64Data = a.Base64Data,
                Type = a.Type == Models.AttachmentType.Image ? ChatAttachmentType.Image : ChatAttachmentType.PDF
            }).ToList();
            
            return ChatMessage.CreateMessageWithAttachments(message.Role, message.Content, chatAttachments);
        }
        
        private string GetActualModelId(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return uniqueId;
            var parts = uniqueId.Split('|');
            return parts.Length > 0 ? parts[0] : uniqueId;
        }
        
        private string GetModelDisplayNameFromUniqueId(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId)) return string.Empty;
            var parts = uniqueId.Split('|');
            return parts.Length > 1 ? parts[1] : string.Empty;
        }
    }
}
