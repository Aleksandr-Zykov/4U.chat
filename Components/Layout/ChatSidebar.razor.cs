using _4U.chat.Models;
using _4U.chat.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.JSInterop;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace _4U.chat.Components.Layout
{
    public partial class ChatSidebar : ComponentBase, IDisposable
    {
        [Parameter] public User? CurrentUser { get; set; }
        [Parameter] public Chat? CurrentChat { get; set; }
        [Parameter] public bool IsChatNameAnimating { get; set; }
        [Parameter] public string? AnimatingChatName { get; set; }

        [Parameter] public EventCallback<int> OnChatSelected { get; set; }
        [Parameter] public EventCallback OnNewChatClicked { get; set; }
        [Parameter] public EventCallback OnHomeClicked { get; set; }
        [Parameter] public EventCallback OnSettingsClicked { get; set; }
        [Parameter] public EventCallback<int> OnChatDeleted { get; set; }
        [Parameter] public EventCallback<int> OnPinChatToggled { get; set; }

        [Inject] private ChatService ChatService { get; set; } = default!;
        [Inject] public UIStateService UIStateService { get; set; } = default!;
        [Inject] private BackgroundStreamingService BackgroundStreamingService { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        private List<Chat>? userChats;
        private List<Chat>? filteredChats;
        private List<ChatSection>? cachedGroupedChats;
        private string searchQuery = string.Empty;
        private bool isInitializing = true;
        private bool isPrerendering = true;
        private bool _needsSelectionUpdate = false;
        
        public class ChatSection
        {
            public string Title { get; set; } = string.Empty;
            public List<Chat> Chats { get; set; } = new();
        }

        private List<ChatSection> GroupedChats => cachedGroupedChats ??= CalculateGroupedChats();
        
        private User? _previousCurrentUser;

        protected override void OnInitialized()
        {
            UIStateService.OnChange += StateHasChanged;
        }

        protected override async Task OnParametersSetAsync()
        {
            if (CurrentUser != _previousCurrentUser)
            {
                _previousCurrentUser = CurrentUser;
                if (CurrentUser != null)
                {
                    isInitializing = true;
                    StateHasChanged();
                    await LoadUserChats();
                    isInitializing = false;
                    StateHasChanged();
                }
            }
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                isPrerendering = false;
            }

            if (!isPrerendering && _needsSelectionUpdate)
            {
                _needsSelectionUpdate = false;
                if (CurrentChat != null)
                {
                    try
                    {
                        await JSRuntime.InvokeVoidAsync("setActiveChatItem", CurrentChat.Id);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to restore chat selection after filter: {ex.Message}");
                    }
                }
            }
        }

        public async Task RefreshChats()
        {
            await LoadUserChats();
            StateHasChanged();
        }

        private async Task LoadUserChats()
        {
            if (CurrentUser != null)
            {
                userChats = await ChatService.GetUserChatsAsync(CurrentUser.Id);
                FilterChats();
            }
        }
        
        private void FilterChats()
        {
            if (userChats == null) return;
            
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                filteredChats = userChats.ToList();
            }
            else
            {
                var query = searchQuery.Trim();
                filteredChats = userChats.Where(c => 
                    c.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }
            
            InvalidateGroupedChatsCache();
            _needsSelectionUpdate = true;
        }

        private void InvalidateGroupedChatsCache()
        {
            cachedGroupedChats = null;
        }

        private List<ChatSection> CalculateGroupedChats()
        {
            if (isInitializing || filteredChats == null || !filteredChats.Any())
                return new List<ChatSection>();
                
            var chatsCopy = filteredChats.ToList();

            var now = DateTime.UtcNow;
            var today = now.Date;
            var yesterday = today.AddDays(-1);
            var lastWeek = today.AddDays(-7);
            var lastMonth = today.AddDays(-30);

            var sections = new List<ChatSection>();

            var pinnedChats = chatsCopy.Where(c => c.IsPinned).OrderByDescending(c => c.UpdatedAt).ToList();
            if (pinnedChats.Any())
            {
                sections.Add(new ChatSection { Title = "Pinned", Chats = pinnedChats });
            }

            var unpinnedChats = chatsCopy.Where(c => !c.IsPinned).ToList();

            var todayChats = unpinnedChats.Where(c => c.UpdatedAt.Date == today).OrderByDescending(c => c.UpdatedAt).ToList();
            if (todayChats.Any())
            {
                sections.Add(new ChatSection { Title = "Today", Chats = todayChats });
            }

            var yesterdayChats = unpinnedChats.Where(c => c.UpdatedAt.Date == yesterday).OrderByDescending(c => c.UpdatedAt).ToList();
            if (yesterdayChats.Any())
            {
                sections.Add(new ChatSection { Title = "Yesterday", Chats = yesterdayChats });
            }

            var lastWeekChats = unpinnedChats.Where(c => c.UpdatedAt.Date < yesterday && c.UpdatedAt.Date >= lastWeek).OrderByDescending(c => c.UpdatedAt).ToList();
            if (lastWeekChats.Any())
            {
                sections.Add(new ChatSection { Title = "Last 7 days", Chats = lastWeekChats });
            }

            var lastMonthChats = unpinnedChats.Where(c => c.UpdatedAt.Date < lastWeek && c.UpdatedAt.Date >= lastMonth).OrderByDescending(c => c.UpdatedAt).ToList();
            if (lastMonthChats.Any())
            {
                sections.Add(new ChatSection { Title = "Last Month", Chats = lastMonthChats });
            }

            var olderChats = unpinnedChats.Where(c => c.UpdatedAt.Date < lastMonth).OrderByDescending(c => c.UpdatedAt).ToList();
            if (olderChats.Any())
            {
                sections.Add(new ChatSection { Title = "Older", Chats = olderChats });
            }

            return sections;
        }

        private bool IsChatGenerating(int chatId) => BackgroundStreamingService.IsStreamingActive(chatId);
        
        public void Dispose()
        {
            UIStateService.OnChange -= StateHasChanged;
        }
    }
}
