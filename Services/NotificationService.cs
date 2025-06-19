using System.Collections.Concurrent;

namespace _4U.chat.Services;

public class NotificationService
{
    private readonly ConcurrentQueue<Notification> _notifications = new();
    
    public class Notification
    {
        public string Type { get; set; } = "error";
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int ChatId { get; set; }
    }
    
    public void AddError(string message, int chatId = 0)
    {
        _notifications.Enqueue(new Notification
        {
            Type = "error",
            Message = message,
            ChatId = chatId
        });
    }
    
    public void AddWarning(string message, int chatId = 0)
    {
        _notifications.Enqueue(new Notification
        {
            Type = "warning", 
            Message = message,
            ChatId = chatId
        });
    }
    
    public void AddInfo(string message, int chatId = 0)
    {
        _notifications.Enqueue(new Notification
        {
            Type = "info",
            Message = message,
            ChatId = chatId
        });
    }
    
    public List<Notification> GetNotifications()
    {
        var notifications = new List<Notification>();
        
        while (_notifications.TryDequeue(out var notification))
        {
            notifications.Add(notification);
        }
        
        return notifications;
    }
    
    public List<Notification> GetNotificationsForChat(int chatId)
    {
        var allNotifications = GetNotifications();
        return allNotifications.Where(n => n.ChatId == chatId || n.ChatId == 0).ToList();
    }
    
    public int GetNotificationCount()
    {
        return _notifications.Count;
    }
    
    public void Clear()
    {
        while (_notifications.TryDequeue(out _)) { }
    }
}