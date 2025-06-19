using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace _4U.chat.Models
{
    public class Message
    {
        public int Id { get; set; }
        
        [Required]
        public string Role { get; set; } = string.Empty; // "user" or "assistant"
        
        [Required]
        public string Content { get; set; } = string.Empty;
        
        public string? Reasoning { get; set; } // Reasoning tokens for thinking models
        
        public int ChatId { get; set; }
        public Chat Chat { get; set; } = null!;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        public string? Model { get; set; } // The LLM model used for assistant messages
        
        // File attachments stored as JSON
        public string? AttachmentsJson { get; set; }
        
        // Helper property to get/set attachments as objects
        public List<MessageAttachment> Attachments
        {
            get
            {
                if (string.IsNullOrEmpty(AttachmentsJson))
                    return new List<MessageAttachment>();
                    
                try
                {
                    return JsonSerializer.Deserialize<List<MessageAttachment>>(AttachmentsJson) ?? new List<MessageAttachment>();
                }
                catch
                {
                    return new List<MessageAttachment>();
                }
            }
            set
            {
                AttachmentsJson = value?.Any() == true ? JsonSerializer.Serialize(value) : null;
            }
        }
        
        public bool HasAttachments => !string.IsNullOrEmpty(AttachmentsJson);
    }
    
    public class MessageAttachment
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string Base64Data { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public AttachmentType Type { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
    
    public enum AttachmentType
    {
        Image,
        PDF
    }
}