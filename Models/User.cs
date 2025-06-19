using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace _4U.chat.Models
{
    public class User : IdentityUser
    {
        public List<Chat> Chats { get; set; } = new();
        
        // User identity
        [MaxLength(100)]
        public string? Name { get; set; }
        
        // API Keys
        public string? OpenRouterApiKey { get; set; }
        public string? OpenAiApiKey { get; set; }
        
        // Profile customization - stored as base64 data URI
        public string? ProfilePictureData { get; set; }
        
        // Customization settings
        [MaxLength(50)]
        public string? DisplayName { get; set; }
        
        [MaxLength(100)]
        public string? Occupation { get; set; }
        
        // Traits stored as JSON string (comma-separated list of traits)
        [MaxLength(5000)]
        public string? Traits { get; set; }
        
        [MaxLength(3000)]
        public string? CustomPrompt { get; set; }
        
        // Favorite models stored as JSON array of unique IDs
        [MaxLength(2000)]
        public string? FavoriteModels { get; set; }
    }
}