using System.ComponentModel.DataAnnotations;

namespace _4U.chat.Models
{
    public class Chat
    {
        public int Id { get; set; }
        
        [Required]
        public string Title { get; set; } = string.Empty;
        
        public string UserId { get; set; } = string.Empty;
        public User User { get; set; } = null!;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsPinned { get; set; } = false;
        public bool IsBranched { get; set; } = false;
        public int? OriginalChatId { get; set; } = null;
        
        public List<Message> Messages { get; set; } = new();
    }
}