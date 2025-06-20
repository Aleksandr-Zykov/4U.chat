using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using System.Text.Json;
using _4U.chat.Data;
using _4U.chat.Models;
using _4U.chat.Services;

namespace _4U.chat.Components.Pages;

public partial class Home : ComponentBase, IDisposable
{
    [Parameter] public int? ChatId { get; set; }
    
    [Inject] private IDbContextFactory<ApplicationDbContext> DbContextFactory { get; set; } = default!;
    [Inject] private UserManager<User> UserManager { get; set; } = default!;
    [Inject] private OpenRouterService OpenRouterService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private MarkdownService MarkdownService { get; set; } = default!;
    [Inject] private BackgroundStreamingService BackgroundStreamingService { get; set; } = default!;
    [Inject] private NotificationService NotificationService { get; set; } = default!;
    
    private User? currentUser;
    private List<Chat>? userChats;
    private List<Chat>? filteredChats;
    private List<ChatSection>? cachedGroupedChats;
    private int? cachedCurrentChatId;
    private bool isInitializing = true;
    private bool isDisposed = false;
    private bool isPrerendering = true;
    private readonly SemaphoreSlim loadingSemaphore = new(1, 1);
    private CancellationTokenSource disposalTokenSource = new();
    private Chat? currentChat;
    private List<Message>? messages;
    private Dictionary<int, string> processedMessageContent = new();
    private string messageInput = string.Empty;
    private string searchQuery = string.Empty;
    private string selectedModel = "google/gemini-2.5-flash-lite-preview-06-17|Gemini 2.5 Flash Lite";
    private ElementReference messageTextArea;
    private bool isDarkTheme = true; // Default, but will be overridden by script
    private bool isSidebarCollapsed = false;
    private bool showModelSelector = false;
    private string modelSearchQuery = string.Empty;
    private bool showAllModels = false;
    private bool shouldHighlightCode = false;
    private bool isGeneratingChatName = false;
    private string animatingChatName = string.Empty;
    private bool isChatNameAnimating = false;
    private int? editingMessageId = null;
    private string editingMessageContent = string.Empty;
    private readonly HashSet<int> expandedThinkingSections = new();
    private DateTime lastStateChangedTime = DateTime.MinValue;
    private const int StateChangedThrottleMs = 50; // Minimum time between StateHasChanged calls
    private bool isModelLoaded = false; // Track if model selection has been loaded from localStorage
    private bool isChatLoaded = true; // Track if chat data has been loaded (true by default for welcome screen)
    private bool isWebSearchEnabled = false;
    private DotNetObjectReference<Home>? citationHandlerReference;
    
    // Suggestion category state
    private string? selectedSuggestionCategory = null;
    private Dictionary<string, List<string>> categoryExamples = new();
    
    // File attachment state
    private List<MessageAttachment> currentAttachments = new();
    private InputFile? fileInputComponent;
    private bool showFileDropZone = false;
    
    // Streaming state polling
    private Timer? _streamingUpdateTimer;
    private DateTime _lastMessageUpdate = DateTime.MinValue;
    
    private int? CurrentChatId 
    { 
        get 
        {
            // Prioritize currentChat?.Id over ChatId parameter to handle navigation timing issues
            // This ensures streaming UI works correctly during chat transitions (especially for branched chats)
            var actualChatId = currentChat?.Id ?? ChatId;
            
            // Update cache if it's different (for debugging/tracking purposes)
            if (cachedCurrentChatId != actualChatId)
            {
                System.Diagnostics.Debug.WriteLine($"CurrentChatId changed from {cachedCurrentChatId} to {actualChatId}");
                cachedCurrentChatId = actualChatId;
            }
            
            return actualChatId;
        } 
    }
    
    // Helper methods for background streaming state
    private bool IsCurrentChatGenerating => CurrentChatId.HasValue && BackgroundStreamingService.IsStreamingActive(CurrentChatId.Value);
    private string GetCurrentChatResponse() => CurrentChatId.HasValue ? GetStreamingResponse(CurrentChatId.Value) : string.Empty;
    private string GetCurrentChatReasoning() => CurrentChatId.HasValue ? GetStreamingReasoning(CurrentChatId.Value) : string.Empty;
    private bool GetCurrentChatHasReasoning() => !string.IsNullOrEmpty(GetCurrentChatReasoning());
    private bool IsCurrentChatActivelyThinking() => CurrentChatId.HasValue && GetStreamingThinking(CurrentChatId.Value);
    private int? GetCurrentStreamingMessageId() => CurrentChatId.HasValue ? GetStreamingMessageId(CurrentChatId.Value) : null;
    private bool IsChatGenerating(int chatId) => BackgroundStreamingService.IsStreamingActive(chatId);
    private bool IsWaitingForFirstToken() => IsCurrentChatGenerating && !GetCurrentChatHasReasoning() && string.IsNullOrEmpty(GetCurrentChatResponse());
    
    private string GetStreamingResponse(int chatId)
    {
        var state = BackgroundStreamingService.GetStreamingState(chatId);
        return state?.CurrentResponse ?? string.Empty;
    }
    
    private string GetStreamingReasoning(int chatId)
    {
        var state = BackgroundStreamingService.GetStreamingState(chatId);
        return state?.CurrentReasoning ?? string.Empty;
    }
    
    private bool GetStreamingThinking(int chatId)
    {
        var state = BackgroundStreamingService.GetStreamingState(chatId);
        return state?.IsActivelyThinking ?? false;
    }
    
    private int? GetStreamingMessageId(int chatId)
    {
        var state = BackgroundStreamingService.GetStreamingState(chatId);
        return state?.MessageId;
    }
    
    // Helper methods for thinking section state
    private void ToggleThinkingSection(int? messageId)
    {
        if (!messageId.HasValue) return;
        
        if (expandedThinkingSections.Contains(messageId.Value))
        {
            expandedThinkingSections.Remove(messageId.Value);
        }
        else
        {
            expandedThinkingSections.Add(messageId.Value);
        }
        StateHasChanged();
    }
    
    private bool IsThinkingExpanded(int? messageId)
    {
        return messageId.HasValue && expandedThinkingSections.Contains(messageId.Value);
    }
    
    private string GetCapitalizedUsername()
    {
        // Use Name first, then UserName as fallback
        var name = currentUser?.Name ?? currentUser?.UserName;
        if (string.IsNullOrEmpty(name)) return "";
        
        // If it's a full name, just capitalize first letter
        if (name.Contains(' '))
        {
            return name;
        }
        
        // If it's a username, capitalize first letter and lowercase the rest
        return char.ToUpper(name[0]) + name.Substring(1).ToLower();
    }
    
    private ChatMessage ConvertToOpenRouterMessage(Message message)
    {
        if (!message.HasAttachments)
        {
            return ChatMessage.CreateTextMessage(message.Role, message.Content);
        }
        
        // Convert MessageAttachment to ChatAttachment
        var chatAttachments = message.Attachments.Select(a => new ChatAttachment
        {
            FileName = a.FileName,
            ContentType = a.ContentType,
            Base64Data = a.Base64Data,
            Type = a.Type == _4U.chat.Models.AttachmentType.Image ? ChatAttachmentType.Image : ChatAttachmentType.PDF
        }).ToList();
        
        return ChatMessage.CreateMessageWithAttachments(message.Role, message.Content, chatAttachments);
    }

    public class ChatSection
    {
        public string Title { get; set; } = string.Empty;
        public List<Chat> Chats { get; set; } = new();
    }

    public class ModelInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string ProviderIcon { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public string InactiveReason { get; set; } = string.Empty;
        public string[] RequiredProviders { get; set; } = Array.Empty<string>();
    }

    // Get available models from centralized configuration
    private List<ModelInfo> availableModels
    {
        get
        {
            return _4U.chat.Services.OpenRouterService.ModelConfigurations
                .Where(config => showAllModels || IsUserFavoriteModel(config.UniqueId)) // Show all models or only user favorites
                .Select(config => new ModelInfo
                {
                    Id = config.UniqueId,
                    Name = config.DisplayName,
                    Description = config.Description,
                    Provider = config.Provider,
                    ProviderIcon = config.ProviderIcon,
                    IsActive = config.IsActive,
                    InactiveReason = config.InactiveReason
                })
                .ToList();
        }
    }


    private List<ChatSection> GroupedChats
    {
        get
        {
            // Return cached result if available
            if (cachedGroupedChats != null)
            {
                return cachedGroupedChats;
            }

            // Calculate and cache the result
            cachedGroupedChats = CalculateGroupedChats();
            return cachedGroupedChats;
        }
    }
    
    private List<ChatSection> GetGroupedChats()
    {
        return GroupedChats;
    }
    
    private List<ChatSection> CalculateGroupedChats()
    {
        try
        {
            if (isInitializing || filteredChats == null || !filteredChats.Any())
                return new List<ChatSection>();
                
            // Create a copy to avoid collection modification exceptions
            var chatsCopy = filteredChats.ToList();

        var now = DateTime.UtcNow;
        var today = now.Date;
        var yesterday = today.AddDays(-1);
        var lastWeek = today.AddDays(-7);
        var lastMonth = today.AddDays(-30);

        var sections = new List<ChatSection>();

        // Pinned chats (always first)
        var pinnedChats = chatsCopy.Where(c => c.IsPinned).OrderByDescending(c => c.UpdatedAt).ToList();
        if (pinnedChats.Any())
        {
            sections.Add(new ChatSection { Title = "Pinned", Chats = pinnedChats });
        }

        // Non-pinned chats grouped by time
        var unpinnedChats = chatsCopy.Where(c => !c.IsPinned).ToList();

        // Today
        var todayChats = unpinnedChats.Where(c => c.UpdatedAt.Date == today).OrderByDescending(c => c.UpdatedAt).ToList();
        if (todayChats.Any())
        {
            sections.Add(new ChatSection { Title = "Today", Chats = todayChats });
        }

        // Yesterday
        var yesterdayChats = unpinnedChats.Where(c => c.UpdatedAt.Date == yesterday).OrderByDescending(c => c.UpdatedAt).ToList();
        if (yesterdayChats.Any())
        {
            sections.Add(new ChatSection { Title = "Yesterday", Chats = yesterdayChats });
        }

        // Last 7 days (excluding today and yesterday)
        var lastWeekChats = unpinnedChats.Where(c => c.UpdatedAt.Date < yesterday && c.UpdatedAt.Date >= lastWeek).OrderByDescending(c => c.UpdatedAt).ToList();
        if (lastWeekChats.Any())
        {
            sections.Add(new ChatSection { Title = "Last 7 days", Chats = lastWeekChats });
        }

        // Last Month (excluding last 7 days)
        var lastMonthChats = unpinnedChats.Where(c => c.UpdatedAt.Date < lastWeek && c.UpdatedAt.Date >= lastMonth).OrderByDescending(c => c.UpdatedAt).ToList();
        if (lastMonthChats.Any())
        {
            sections.Add(new ChatSection { Title = "Last Month", Chats = lastMonthChats });
        }

        // Older
        var olderChats = unpinnedChats.Where(c => c.UpdatedAt.Date < lastMonth).OrderByDescending(c => c.UpdatedAt).ToList();
        if (olderChats.Any())
        {
            sections.Add(new ChatSection { Title = "Older", Chats = olderChats });
        }

            return sections;
        }
        catch (InvalidOperationException ex)
        {
            System.Diagnostics.Debug.WriteLine($"CalculateGroupedChats InvalidOperationException: {ex.Message}");
            return new List<ChatSection>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CalculateGroupedChats Exception: {ex.Message}");
            return new List<ChatSection>();
        }
    }
    
    private void InvalidateGroupedChatsCache()
    {
        cachedGroupedChats = null;
    }
    
    
    private async Task ThrottledStateHasChanged()
    {
        var now = DateTime.UtcNow;
        if (now - lastStateChangedTime < TimeSpan.FromMilliseconds(StateChangedThrottleMs))
        {
            return; // Skip this update to avoid too frequent re-renders
        }
        
        lastStateChangedTime = now;
        if (!isDisposed)
        {
            try
            {
                await InvokeAsync(StateHasChanged);
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"ThrottledStateHasChanged InvalidOperationException: {ex.Message}");
            }
        }
    }
    
    private async Task ForceStateHasChanged()
    {
        lastStateChangedTime = DateTime.UtcNow;
        if (!isDisposed)
        {
            try
            {
                await InvokeAsync(StateHasChanged);
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"ForceStateHasChanged InvalidOperationException: {ex.Message}");
            }
        }
    }

    private string GetProcessedMessageContent(Message message)
    {
        if (!processedMessageContent.TryGetValue(message.Id, out var processedContent))
        {
            processedContent = MarkdownService.ToHtml(message.Content);
            processedMessageContent[message.Id] = processedContent;
        }
        return processedContent;
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"OnInitializedAsync - Start (IsDisposed: {isDisposed}, IsPrerendering: {isPrerendering})");
            
            if (isDisposed) return;
            
            await loadingSemaphore.WaitAsync(disposalTokenSource.Token);
            
            if (isDisposed) return;
            
            // Set loading state immediately if we have a ChatId parameter to prevent flash
            if (ChatId.HasValue)
            {
                isChatLoaded = false;
            }
            
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            if (authState.User.Identity?.IsAuthenticated == true)
            {
                if (isDisposed) return;
                
                currentUser = await UserManager.GetUserAsync(authState.User);
                
                if (isDisposed) return;

                // Initialize user favorites if they haven't been set yet
                await InitializeUserFavorites();
                
                // Initialize suggestion categories
                InitializeSuggestionCategories();
                
                if (isDisposed) return;
                
                await LoadUserChats();
                
                if (isDisposed) return;
                
                if (ChatId.HasValue)
                {
                    // Don't navigate during initialization - we're already at the correct URL
                    await SelectChat(ChatId.Value, shouldNavigate: false);
                    
                    // Ensure sidebar selection is set after DOM is ready (for page refresh scenarios)
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(100); // Wait for DOM to be ready
                        if (!isDisposed && !isPrerendering)
                        {
                            try
                            {
                                await JSRuntime.InvokeVoidAsync("setActiveChatItem", disposalTokenSource.Token, ChatId.Value);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"setActiveChatItem retry failed: {ex.Message}");
                            }
                        }
                    });
                }
                
                if (isDisposed) return;
                
                // Background streaming service handles continuation automatically
            }
            
            if (isDisposed) return;
            
            // Load persisted settings
            await LoadModelSelection();
            await LoadWebSearchState();
            
            if (isDisposed) return;
            
            // Theme is now handled entirely by script in App.razor
            
            if (!isDisposed)
            {
                isInitializing = false;
            }
            
            System.Diagnostics.Debug.WriteLine($"OnInitializedAsync - Complete (IsDisposed: {isDisposed}, IsPrerendering: {isPrerendering})");
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("OnInitializedAsync - Cancelled due to disposal");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnInitializedAsync Exception: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"OnInitializedAsync StackTrace: {ex.StackTrace}");
        }
        finally
        {
            if (!disposalTokenSource.Token.IsCancellationRequested)
            {
                loadingSemaphore.Release();
            }
        }
    }
    
    protected override async Task OnParametersSetAsync()
    {
        if (isInitializing || isDisposed || isPrerendering) return;
        
        try
        {
            System.Diagnostics.Debug.WriteLine($"OnParametersSetAsync - ChatId: {ChatId}, Current: {currentChat?.Id} (IsDisposed: {isDisposed}, IsPrerendering: {isPrerendering})");
            
            if (isDisposed) return;
            
            await loadingSemaphore.WaitAsync(disposalTokenSource.Token);
            
            if (isDisposed) return;
            
            // Only handle parameter changes if they come from actual navigation (not our history.replaceState)
            if (ChatId.HasValue && currentChat?.Id != ChatId.Value)
            {
                // Set loading state immediately to prevent flash
                isChatLoaded = false;
                
                // This is a real navigation (e.g., browser back/forward, direct URL, etc.)
                await SelectChat(ChatId.Value, shouldNavigate: false);
                
                if (isDisposed) return;
                
                // Background streaming service handles continuation automatically
            }
            else if (!ChatId.HasValue && currentChat != null)
            {
                // Clear active chat selection immediately via JavaScript
                if (!isPrerendering)
                {
                    try
                    {
                        await JSRuntime.InvokeVoidAsync("setActiveChatItem", disposalTokenSource.Token, (object?)null);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"setActiveChatItem clear in params failed: {ex.Message}");
                    }
                }
                
                // Clear current chat when navigating to home
                currentChat = null;
                messages = null;
                processedMessageContent.Clear();
                cachedCurrentChatId = null; // Reset cache tracking
                
                // Mark as loaded since we're going to welcome screen
                isChatLoaded = true;
                if (!isDisposed)
                {
                    await ForceStateHasChanged();
                }
            }
            
            if (isDisposed) return;
            
            // Theme is now handled entirely by script in App.razor
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("OnParametersSetAsync - Cancelled due to disposal");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnParametersSetAsync Exception: {ex.Message}");
        }
        finally
        {
            if (!disposalTokenSource.Token.IsCancellationRequested)
            {
                loadingSemaphore.Release();
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (isDisposed) return;
        
        // Mark that we're no longer prerendering after first render
        if (firstRender)
        {
            isPrerendering = false;
            
            // Load persisted settings after first render when JS is available
            try
            {
                await LoadModelSelection();
                await LoadWebSearchState();
                
                // Force a state update to reflect any loaded settings
                if (!isDisposed)
                {
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Loading persisted settings after first render failed: {ex.Message}");
            }
            
            // Set initial active chat state if we have one
            if (CurrentChatId.HasValue)
            {
                try
                {
                    await JSRuntime.InvokeVoidAsync("setActiveChatItem", disposalTokenSource.Token, CurrentChatId.Value);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Initial setActiveChatItem failed: {ex.Message}");
                }
            }
            
            // Start polling for streaming updates
            StartStreamingUpdateTimer();
        }
        
        // Sync component state with DOM state after renders
        if (!isPrerendering)
        {
            try
            {
                var hasLightClass = await JSRuntime.InvokeAsync<bool>("document.body.classList.contains", disposalTokenSource.Token, "light-theme");
                isDarkTheme = !hasLightClass;
            }
            catch
            {
                // If we can't read DOM, assume dark
                isDarkTheme = true;
            }
        }
        
        // Only highlight code when necessary and not prerendering
        if (!isPrerendering && (shouldHighlightCode || firstRender))
        {
            shouldHighlightCode = false;
            try
            {
                await JSRuntime.InvokeVoidAsync("autoHighlightCode", disposalTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // Component disposed, ignore
            }
            catch
            {
                // Ignore JS errors
            }
        }
        
        // Initialize citation feature - only on first render to avoid multiple setups
        if (firstRender && !isPrerendering)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Setting up citation feature...");
                
                // Create and store the DotNet reference to prevent GC
                citationHandlerReference = DotNetObjectReference.Create(this);
                
                // Use the proper JavaScript function to set the handler
                var handlerSetResult = await JSRuntime.InvokeAsync<bool>("setBlazorCitationHandler", disposalTokenSource.Token, citationHandlerReference);
                System.Diagnostics.Debug.WriteLine($"Blazor handler setup result: {handlerSetResult}");
                
                // Initialize citation feature
                await JSRuntime.InvokeVoidAsync("initializeCitationFeature", disposalTokenSource.Token);
                
                // Verify setup status
                var setupStatus = await JSRuntime.InvokeAsync<string>("eval", disposalTokenSource.Token, 
                    @"JSON.stringify({
                        hasHandler: !!window.blazorCitationHandler,
                        handlerType: typeof window.blazorCitationHandler,
                        hasInitFunction: typeof window.initializeCitationFeature === 'function',
                        isInitialized: !!(window.citationFeature && window.citationFeature.isInitialized),
                        hasButton: !!(window.citationFeature && window.citationFeature.button)
                    })");
                
                System.Diagnostics.Debug.WriteLine($"Final citation setup status: {setupStatus}");
                
                if (!handlerSetResult)
                {
                    System.Diagnostics.Debug.WriteLine("WARNING: Blazor handler setup failed!");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to setup citation feature: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
        
        // Render LaTeX when necessary and not prerendering
        if (!isPrerendering)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("renderLatex", disposalTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // Component disposed, ignore
            }
            catch
            {
                // Ignore JS errors
            }
        }
        
    }

    private async Task LoadUserChats()
    {
        if (currentUser != null)
        {
            using var dbContext = await DbContextFactory.CreateDbContextAsync();
            var chats = await dbContext.Chats
                .Where(c => c.UserId == currentUser.Id)
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();
            
            userChats = chats;
            filteredChats = chats.ToList(); // Create a copy
            InvalidateGroupedChatsCache(); // Clear cache when data changes
        }
    }

    private async Task SelectChat(int chatId, bool shouldNavigate = true)
    {
        // If we're already on this chat, don't do anything
        if (currentChat?.Id == chatId) return;
        
        // Set loading state to prevent flash
        isChatLoaded = false;
        
        // Reset cache tracking when selecting a new chat
        cachedCurrentChatId = null;
        
        // Immediately update visual state via JavaScript to avoid Blazor re-render delays
        if (!isPrerendering)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("setActiveChatItem", disposalTokenSource.Token, chatId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"setActiveChatItem failed: {ex.Message}");
            }
        }
        
        using var dbContext = await DbContextFactory.CreateDbContextAsync();
        currentChat = await dbContext.Chats
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == chatId && c.UserId == currentUser!.Id);
            
        if (currentChat != null)
        {
            messages = currentChat.Messages.OrderBy(m => m.CreatedAt).ToList();
            
            // Find the last user message for scroll positioning
            var lastUserMessage = messages.LastOrDefault(m => m.Role == "user");
            
            // Clear processed content cache for new chat
            processedMessageContent.Clear();
            
            // Trigger syntax highlighting for the new chat
            shouldHighlightCode = true;
            
            // Update URL directly without navigation to avoid component lifecycle triggers
            if (shouldNavigate && !isPrerendering)
            {
                try
                {
                    await JSRuntime.InvokeVoidAsync("history.replaceState", disposalTokenSource.Token, null, "", $"/chat/{chatId}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"history.replaceState failed: {ex.Message}");
                }
                
                // Only update the main content area, don't re-render sidebar
                if (!isDisposed)
                {
                    await ForceStateHasChanged();
                }
                
                // Scroll to last user message after render
                if (lastUserMessage != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await JSRuntime.InvokeVoidAsync("scrollToMessage", disposalTokenSource.Token, lastUserMessage.Id);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"scrollToMessage failed: {ex.Message}");
                        }
                    });
                }
            }
            else if (!shouldNavigate)
            {
                // Force update for the main content area only
                if (!isDisposed)
                {
                    await ForceStateHasChanged();
                }
                
                // Scroll to last user message after render
                if (lastUserMessage != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await JSRuntime.InvokeVoidAsync("scrollToMessage", disposalTokenSource.Token, lastUserMessage.Id);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"scrollToMessage failed: {ex.Message}");
                        }
                    });
                }
            }
            
            // Background streaming service handles continuation automatically
        }
        else
        {
            // Chat not found - clear current state
            messages = null;
        }
        
        // Mark chat as loaded (whether found or not) to prevent loading state flash
        isChatLoaded = true;
    }

    private async Task CreateNewChat()
    {
        if (currentUser == null) return;

        // Clear active chat selection immediately via JavaScript
        if (!isPrerendering)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("setActiveChatItem", disposalTokenSource.Token, (object?)null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"setActiveChatItem clear failed: {ex.Message}");
            }
        }

        // Reset to home state (clear current chat)
        currentChat = null;
        messages = null;
        processedMessageContent.Clear();
        
        // Mark as loaded since we're going to welcome screen
        isChatLoaded = true;
        
        if (!isDisposed)
        {
            StateHasChanged();
        }
        
        // Navigate to homepage
        Navigation.NavigateTo("/");
    }

    private async Task OnSearchInput(ChangeEventArgs e)
    {
        searchQuery = e.Value?.ToString() ?? string.Empty;
        await FilterChats();
    }

    private async Task FilterChats()
    {
        if (isInitializing || isDisposed || userChats == null) return;
        
        try
        {
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                filteredChats = userChats.ToList(); // Create a copy
            }
            else
            {
                var query = searchQuery.Trim();
                filteredChats = userChats.Where(c => 
                    c.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }
            
            InvalidateGroupedChatsCache(); // Clear cache when filtered data changes
            
            // Trigger UI update to refresh the grouped sections
            if (!isDisposed)
            {
                await ForceStateHasChanged();
                
                // Restore chat selection after re-render
                if (CurrentChatId.HasValue && !isPrerendering)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(50); // Small delay to ensure DOM is updated
                        if (!isDisposed)
                        {
                            try
                            {
                                await JSRuntime.InvokeVoidAsync("setActiveChatItem", disposalTokenSource.Token, CurrentChatId.Value);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to restore chat selection after filter: {ex.Message}");
                            }
                        }
                    });
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            System.Diagnostics.Debug.WriteLine($"FilterChats InvalidOperationException: {ex.Message}");
        }
    }

    private async Task SendMessage()
    {
        if ((string.IsNullOrWhiteSpace(messageInput) && !currentAttachments.Any()) || currentUser == null || IsCurrentChatGenerating) return;

        var content = messageInput.Trim();
        var attachments = currentAttachments.ToList(); // Copy attachments before clearing
        messageInput = string.Empty;
        currentAttachments.Clear();
        
        using var dbContext = await DbContextFactory.CreateDbContextAsync();
        
        // Create chat if none exists (when sending message from homepage)
        bool isNewChat = false;
        if (currentChat == null)
        {
            var newChat = new Chat
            {
                Title = "New Chat",
                UserId = currentUser.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.Chats.Add(newChat);
            await dbContext.SaveChangesAsync();
            
            // Set up chat state WITHOUT navigation to avoid interrupting message flow
            currentChat = await dbContext.Chats
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == newChat.Id && c.UserId == currentUser.Id);
            
            if (currentChat != null)
            {
                messages = currentChat.Messages.OrderBy(m => m.CreatedAt).ToList();
                processedMessageContent.Clear();
                shouldHighlightCode = true;
                isNewChat = true;
            }
            
            // Refresh chat list after creating new chat
            await LoadUserChats();
        }

        // Add user message
        var userMessage = new Message
        {
            Role = "user",
            Content = content,
            ChatId = currentChat.Id,
            CreatedAt = DateTime.UtcNow,
            Attachments = attachments
        };

        dbContext.Messages.Add(userMessage);
        messages?.Add(userMessage);
        
        // Update chat title if it's still "New Chat"
        if (currentChat.Title == "New Chat")
        {
            _ = GenerateChatNameAsync(content, currentChat.Id);
        }

        await dbContext.SaveChangesAsync();
        await LoadUserChats();
        
        // Trigger syntax highlighting for new user message
        shouldHighlightCode = true;
        await ForceStateHasChanged();

        // Scroll to the newly sent message after render
        _ = Task.Run(async () =>
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("scrollToMessage", disposalTokenSource.Token, userMessage.Id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"scrollToMessage after send failed: {ex.Message}");
            }
        });

        // Reset textarea height after sending
        await AutoResizeTextarea();
        
        // Generate AI response
        await StartGeneration();
        
        // Update URL without full navigation to avoid interrupting streaming (only for new chats)
        if (isNewChat && currentChat != null && !isPrerendering)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("history.replaceState", disposalTokenSource.Token, null, "", $"/chat/{currentChat.Id}");
                
                // Visually select the new chat in the sidebar
                await JSRuntime.InvokeVoidAsync("setActiveChatItem", disposalTokenSource.Token, currentChat.Id);
            }
            catch (OperationCanceledException)
            {
                // Component disposed, ignore
            }
            catch (NotSupportedException)
            {
                // JS not available during prerendering
            }
            catch
            {
                // Ignore JS interop errors
            }
        }
        
        // Clear input immediately but don't focus yet (focus will happen after streaming completes)
        messageInput = string.Empty;
        await ForceStateHasChanged();
    }
    
    private async Task StartGeneration()
    {
        if (currentChat == null || messages == null) return;
        
        // Force cache refresh to ensure CurrentChatId is synchronized
        var previousCachedId = cachedCurrentChatId;
        cachedCurrentChatId = currentChat.Id;
        if (previousCachedId != currentChat.Id)
        {
            System.Diagnostics.Debug.WriteLine($"StartGeneration: Corrected cached chat ID from {previousCachedId} to {currentChat.Id}");
        }
        
        // Check API key before starting any streaming UI
        var apiKey = currentUser?.OpenRouterApiKey;
        if (string.IsNullOrEmpty(apiKey))
        {
            // Show notification for immediate feedback
            try
            {
                await JSRuntime.InvokeVoidAsync("showApiKeyError", disposalTokenSource.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to show API key error notification: {ex.Message}");
            }
            
            // Also show error message in the legacy way as fallback
            try
            {
                await JSRuntime.InvokeVoidAsync("displayErrorMessage", disposalTokenSource.Token, "Please set your OpenRouter API key in Settings to use AI chat.");
            }
            catch
            {
                // Ignore errors with legacy error display
            }
            
            return;
        }
        
        // Create placeholder assistant message
        var assistantMessage = new Message
        {
            Role = "assistant",
            Content = "",
            ChatId = currentChat.Id,
            CreatedAt = DateTime.UtcNow,
            Model = GetActualModelId(selectedModel)
        };
        
        using var dbContext = await DbContextFactory.CreateDbContextAsync();
        dbContext.Messages.Add(assistantMessage);
        await dbContext.SaveChangesAsync();
        messages.Add(assistantMessage);
        
        // Prepare chat messages for the API
        var chatMessages = messages
            .Where(m => m.Id != assistantMessage.Id)
            .OrderBy(m => m.CreatedAt)
            .Select(m => ConvertToOpenRouterMessage(m))
            .ToList();
        
        // Build system prompt from user personalization
        var systemPrompt = await BuildSystemPrompt();
        
        // Start background streaming
        var success = await BackgroundStreamingService.StartStreamingAsync(
            currentChat.Id,
            assistantMessage.Id,
            GetActualModelId(selectedModel),
            GetModelDisplayNameFromUniqueId(selectedModel),
            apiKey,
            systemPrompt,
            chatMessages,
            isWebSearchEnabled,
            isWebSearchEnabled ? new WebSearchOptions() : null
        );
        
        if (success)
        {
            System.Diagnostics.Debug.WriteLine($"Started background streaming for chat {currentChat.Id}, message {assistantMessage.Id}");
            _lastMessageUpdate = DateTime.UtcNow;
            // Force immediate UI update to show stop button and thinking animation
            await ForceStateHasChanged();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"Failed to start background streaming for chat {currentChat.Id}");
            
            // Show styled error message to user
            assistantMessage.Content = "🚨 **Generation Failed**\n\n" +
                "Failed to start AI generation. This could be due to:\n\n" +
                "• Invalid or missing OpenRouter API key\n" +
                "• Network connectivity issues\n" +
                "• Selected model is currently unavailable\n\n" +
                "**What to try:**\n" +
                "1. Check your OpenRouter API key in Settings\n" +
                "2. Verify your internet connection\n" +
                "3. Try selecting a different model\n" +
                "4. Wait a moment and try again\n\n" +
                "*If the problem persists, please check the OpenRouter service status.*";
            assistantMessage.UpdatedAt = DateTime.UtcNow;
            
            using var updateDbContext = await DbContextFactory.CreateDbContextAsync();
            updateDbContext.Update(assistantMessage);
            await updateDbContext.SaveChangesAsync();
            
            await ForceStateHasChanged();
        }
    }
    
    
    private async Task StopCurrentChatGeneration()
    {
        if (CurrentChatId.HasValue && BackgroundStreamingService.IsStreamingActive(CurrentChatId.Value))
        {
            BackgroundStreamingService.StopStreaming(CurrentChatId.Value);
            System.Diagnostics.Debug.WriteLine($"Stopped generation for chat {CurrentChatId.Value}");
            await InvokeAsync(StateHasChanged);
        }
    }

    
    private async Task AutoResizeTextarea()
    {
        if (isDisposed || isPrerendering) return;
        
        try
        {
            // Reset height to measure content
            await JSRuntime.InvokeVoidAsync("autoResizeTextarea", disposalTokenSource.Token, messageTextArea);
        }
        catch (OperationCanceledException)
        {
            // Component disposed, ignore
        }
        catch (NotSupportedException)
        {
            // JS not available during prerendering
        }
        catch
        {
            // Ignore JS interop errors
        }
    }
    
    private async Task SuggestMessage(string suggestion)
    {
        messageInput = suggestion;
        await AutoResizeTextarea();
        StateHasChanged();
    }
    
    private void InitializeSuggestionCategories()
    {
        categoryExamples = new Dictionary<string, List<string>>
        {
            ["Create"] = new List<string>
            {
                "Write a creative short story",
                "Design a marketing campaign",
                "Create a presentation outline",
                "Generate creative writing prompts"
            },
            ["Explore"] = new List<string>
            {
                "Explain quantum computing",
                "What are the benefits of renewable energy?",
                "How does blockchain technology work?",
                "Discuss the history of space exploration"
            },
            ["Code"] = new List<string>
            {
               "Write a Python function to sort a list",
               "Explain object-oriented programming",
               "Implement a binary search algorithm",
               "Create a REST API endpoint"
            },
            ["Learn"] = new List<string>
            {
                "Help me learn Spanish basics",
                "Teach me about photosynthesis",
                "Explain calculus concepts",
                "What are the fundamentals of music theory?"
            }
        };
    }
    
    private void SelectSuggestionCategory(string category)
    {
        if (selectedSuggestionCategory == category)
        {
            // Clicking the same category again returns to default
            selectedSuggestionCategory = null;
        }
        else
        {
            // Select the new category
            selectedSuggestionCategory = category;
        }
        StateHasChanged();
    }
    
    private List<string> GetCurrentSuggestionExamples()
    {
        if (selectedSuggestionCategory != null && categoryExamples.TryGetValue(selectedSuggestionCategory, out var examples))
        {
            return examples;
        }
        
        // Default examples
        return new List<string>
        {
            "How does AI work?",
            "Are black holes real?",
            "How many Rs are in the word \"strawberry\"?",
            "What is the meaning of life?"
        };
    }
    
    private async Task ToggleTheme()
    {
        if (isDisposed || isPrerendering) return;
        
        isDarkTheme = !isDarkTheme;
        
        // Apply theme and save to localStorage atomically to prevent race conditions
        await ApplyThemeAndSave();
    }
    
    private async Task ApplyThemeAndSave()
    {
        if (isDisposed || isPrerendering) return;
        
        var themeValue = isDarkTheme ? "dark" : "light";
        
        try
        {
            // Save to localStorage first, then apply to DOM to ensure consistency
            await JSRuntime.InvokeVoidAsync("localStorage.setItem", disposalTokenSource.Token, "theme", themeValue);
            
            // Apply theme to DOM immediately after saving
            if (isDarkTheme)
            {
                await JSRuntime.InvokeVoidAsync("document.body.classList.remove", disposalTokenSource.Token, "light-theme");
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("document.body.classList.add", disposalTokenSource.Token, "light-theme");
            }
            
            await JSRuntime.InvokeVoidAsync("console.log", disposalTokenSource.Token, $"Theme toggled and saved: {themeValue}");
        }
        catch (OperationCanceledException)
        {
            // Component disposed, ignore
        }
        catch (NotSupportedException)
        {
            // JS not available during prerendering
        }
        catch (Exception ex)
        {
            // If localStorage save fails, revert the theme state to maintain consistency
            isDarkTheme = !isDarkTheme;
            System.Diagnostics.Debug.WriteLine($"Theme toggle failed: {ex.Message}");
        }
    }
    
    private async Task EnsureThemeApplied()
    {
        if (isDisposed || isPrerendering) return;
        
        try
        {
            // Wait a small amount to ensure any pending localStorage writes are complete
            await Task.Delay(50, disposalTokenSource.Token);
            
            var savedTheme = await JSRuntime.InvokeAsync<string>("localStorage.getItem", disposalTokenSource.Token, "theme");
            var shouldBeLight = savedTheme == "light";
            
            // Update component state to match localStorage
            isDarkTheme = !shouldBeLight;
            
            // Apply theme to body (for navigation scenarios)
            if (shouldBeLight)
            {
                await JSRuntime.InvokeVoidAsync("document.body.classList.add", disposalTokenSource.Token, "light-theme");
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("document.body.classList.remove", disposalTokenSource.Token, "light-theme");
            }
            
            await JSRuntime.InvokeVoidAsync("console.log", disposalTokenSource.Token, $"EnsureThemeApplied - applied theme: {(shouldBeLight ? "light" : "dark")}");
        }
        catch (OperationCanceledException)
        {
            // Component disposed, ignore
        }
        catch (NotSupportedException)
        {
            // Expected during prerendering
        }
        catch (Exception ex)
        {
            await JSRuntime.InvokeVoidAsync("console.log", disposalTokenSource.Token, $"EnsureThemeApplied error: {ex.Message}");
        }
    }
    
    private async Task ApplyTheme()
    {
        if (isDisposed || isPrerendering) return;
        
        await JSRuntime.InvokeVoidAsync("console.log", disposalTokenSource.Token, $"ApplyTheme called - isDarkTheme: {isDarkTheme}");
        
        try
        {
            if (isDarkTheme)
            {
                await JSRuntime.InvokeVoidAsync("document.body.classList.remove", disposalTokenSource.Token, "light-theme");
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("document.body.classList.add", disposalTokenSource.Token, "light-theme");
            }
        }
        catch (OperationCanceledException)
        {
            // Component disposed, ignore
        }
        catch (NotSupportedException)
        {
            // JS not available during prerendering
        }
        catch
        {
            // Ignore JS interop errors
        }
    }
    
    private async Task SaveModelSelection()
    {
        if (isDisposed || isPrerendering) return;
        
        try
        {
            await JSRuntime.InvokeVoidAsync("localStorage.setItem", disposalTokenSource.Token, "selectedModel", selectedModel);
        }
        catch (OperationCanceledException)
        {
            // Component disposed, ignore
        }
        catch (NotSupportedException)
        {
            // JS not available during prerendering
        }
        catch
        {
            // Ignore if localStorage is not available
        }
    }
    
    private async Task LoadModelSelection()
    {
        if (isDisposed || isPrerendering) return;
        
        try
        {
            var savedModel = await JSRuntime.InvokeAsync<string>("localStorage.getItem", disposalTokenSource.Token, "selectedModel");
            if (!string.IsNullOrEmpty(savedModel))
            {
                // First check if the model exists in the full model list (regardless of current view mode)
                var modelConfig = _4U.chat.Services.OpenRouterService.ModelConfigurations
                    .FirstOrDefault(c => c.UniqueId == savedModel);
                
                if (modelConfig != null)
                {
                    selectedModel = savedModel;
                    // If the selected model is not in user favorites but was previously selected,
                    // we should still restore it even if we're in "Show less" mode
                }
                else
                {
                    // Handle backward compatibility - try to find model by old ModelId format
                    var actualModelId = GetActualModelId(savedModel);
                    var backwardCompatibleConfig = _4U.chat.Services.OpenRouterService.ModelConfigurations
                        .FirstOrDefault(c => c.ModelId == actualModelId);
                    if (backwardCompatibleConfig != null)
                    {
                        selectedModel = backwardCompatibleConfig.UniqueId;
                        // Save the updated format
                        await SaveModelSelection();
                    }
                }
            }
        }
        catch (NotSupportedException)
        {
            // Expected during prerendering
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadModelSelection error: {ex.Message}");
        }
        finally
        {
            // Mark model as loaded regardless of success/failure to prevent flash
            isModelLoaded = true;
        }
    }
    
    private async Task SaveWebSearchState()
    {
        if (isDisposed || isPrerendering) return;
        
        try
        {
            await JSRuntime.InvokeVoidAsync("localStorage.setItem", disposalTokenSource.Token, "webSearchEnabled", isWebSearchEnabled.ToString().ToLower());
        }
        catch (OperationCanceledException)
        {
            // Component disposed, ignore
        }
        catch (NotSupportedException)
        {
            // JS not available during prerendering
        }
        catch
        {
            // Ignore if localStorage is not available
        }
    }
    
    private async Task LoadWebSearchState()
    {
        if (isDisposed || isPrerendering) return;
        
        try
        {
            var savedWebSearch = await JSRuntime.InvokeAsync<string>("localStorage.getItem", disposalTokenSource.Token, "webSearchEnabled");
            if (!string.IsNullOrEmpty(savedWebSearch))
            {
                isWebSearchEnabled = savedWebSearch.ToLower() == "true";
            }
        }
        catch (NotSupportedException)
        {
            // Expected during prerendering
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadWebSearchState error: {ex.Message}");
        }
    }
    
    private void ToggleSidebar()
    {
        isSidebarCollapsed = !isSidebarCollapsed;
    }
    
    private async Task GoToHome()
    {
        if (isDisposed) return;
        
        // Clear active chat selection immediately via JavaScript
        if (!isPrerendering)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("setActiveChatItem", disposalTokenSource.Token, (object?)null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"setActiveChatItem clear failed: {ex.Message}");
            }
        }
        
        // Reset to home state without navigation to preserve theme
        currentChat = null;
        messages = null;
        processedMessageContent.Clear();
        
        // Use proper navigation to clear ChatId parameter and update routing state
        Navigation.NavigateTo("/", forceLoad: false);
    }
    
    private void GoToSettings()
    {
        Navigation.NavigateTo("/settings");
    }
    
    private void ToggleModelSelector()
    {
        showModelSelector = !showModelSelector;
        StateHasChanged();
    }
    
    private async Task SelectModel(string uniqueId)
    {
        var modelInfo = availableModels.FirstOrDefault(m => m.Id == uniqueId);
        if (modelInfo != null && !modelInfo.IsActive)
        {
            // Don't select inactive models
            return;
        }
        
        selectedModel = uniqueId;
        showModelSelector = false;
        await SaveModelSelection();
        StateHasChanged();
    }
    
    private string GetModelDisplayName(string uniqueId)
    {
        var config = _4U.chat.Services.OpenRouterService.GetModelConfigurationByUniqueId(uniqueId);
        return config?.DisplayName ?? "Unknown Model";
    }
    
    private string GetActualModelId(string uniqueId)
    {
        // Extract the actual ModelId from UniqueId (format: "ModelId|DisplayName")
        if (string.IsNullOrEmpty(uniqueId)) return uniqueId;
        var parts = uniqueId.Split('|');
        return parts.Length > 0 ? parts[0] : uniqueId;
    }
    
    private string GetModelDisplayNameFromUniqueId(string uniqueId)
    {
        // Extract the DisplayName from UniqueId (format: "ModelId|DisplayName")
        if (string.IsNullOrEmpty(uniqueId)) return string.Empty;
        var parts = uniqueId.Split('|');
        return parts.Length > 1 ? parts[1] : string.Empty;
    }
    
    private List<ModelInfo> GetFilteredModels()
    {
        var models = availableModels.AsEnumerable();
        
        // Apply search filter
        if (!string.IsNullOrWhiteSpace(modelSearchQuery))
        {
            var query = modelSearchQuery.Trim().ToLowerInvariant();
            models = models.Where(m => 
                m.Name.ToLowerInvariant().Contains(query) ||
                m.Description.ToLowerInvariant().Contains(query) ||
                m.Provider.ToLowerInvariant().Contains(query)
            );
        }
        
        // Note: Filtering by favorites is now handled in availableModels property
        // when showAllModels is false, so no additional filtering needed here
        
        return models.ToList();
    }
    
    private void FilterModels()
    {
        StateHasChanged();
    }
    
    private void ToggleShowAllModels()
    {
        showAllModels = !showAllModels;
        StateHasChanged();
    }
    
    private async Task ToggleWebSearch()
    {
        isWebSearchEnabled = !isWebSearchEnabled;
        await SaveWebSearchState();
        StateHasChanged();
    }
    
    private async Task DeleteChat(int chatId)
    {
        if (currentUser == null) return;
        
        using var dbContext = await DbContextFactory.CreateDbContextAsync();
        var chatToDelete = await dbContext.Chats
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == chatId && c.UserId == currentUser.Id);
            
        if (chatToDelete != null)
        {
            // Delete all messages first
            dbContext.Messages.RemoveRange(chatToDelete.Messages);
            
            // Delete the chat
            dbContext.Chats.Remove(chatToDelete);
            
            await dbContext.SaveChangesAsync();
            
            // If we deleted the current chat, navigate to home
            if (CurrentChatId == chatId)
            {
                processedMessageContent.Clear();
                await GoToHome();
            }
            
            // Refresh the chat list
            await LoadUserChats();
            StateHasChanged();
        }
    }

    private async Task TogglePinChat(int chatId)
    {
        if (currentUser == null) return;
        
        using var dbContext = await DbContextFactory.CreateDbContextAsync();
        var chatToPin = await dbContext.Chats
            .FirstOrDefaultAsync(c => c.Id == chatId && c.UserId == currentUser.Id);
            
        if (chatToPin != null)
        {
            chatToPin.IsPinned = !chatToPin.IsPinned;
            
            // Don't update UpdatedAt when changing pin status
            // This preserves the original timestamp for correct time-based placement
            
            await dbContext.SaveChangesAsync();
            
            // Refresh the chat list
            await LoadUserChats();
            
            // Restore selection if this was the current chat
            if (currentChat?.Id == chatId)
            {
                // Force a state update to ensure the sidebar renders with updated pin status
                StateHasChanged();
                
                // Restore the sidebar selection
                if (!isPrerendering)
                {
                    try
                    {
                        await JSRuntime.InvokeVoidAsync("setActiveChatItem", disposalTokenSource.Token, chatId);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"setActiveChatItem restore failed: {ex.Message}");
                    }
                }
            }
            
            StateHasChanged();
        }
    }

    private async Task GenerateChatNameAsync(string userMessage, int chatId)
    {
        try
        {
            isGeneratingChatName = true;
            System.Diagnostics.Debug.WriteLine($"Generating chat name for: {userMessage}");
            
            // Get user's API key - required for OpenRouter access
            var apiKey = currentUser?.OpenRouterApiKey;
            if (string.IsNullOrEmpty(apiKey))
            {
                return; // Skip auto-naming if no API key
            }
            var result = await OpenRouterService.GenerateChatNameAsync(userMessage, apiKey);
            System.Diagnostics.Debug.WriteLine($"API result: {result?.Name ?? "NULL"}");
            
            using var dbContext = await DbContextFactory.CreateDbContextAsync();
            var chat = await dbContext.Chats.FindAsync(chatId);
            if (chat != null && chat.Title == "New Chat" && !string.IsNullOrEmpty(result?.Name))
            {
                System.Diagnostics.Debug.WriteLine($"Animating name: {result.Name}");
                await AnimateChatName(result.Name, chat);
            }
            else if (chat != null && chat.Title == "New Chat")
            {
                System.Diagnostics.Debug.WriteLine("Using fallback - API result was empty");
                // Fallback if result is empty
                chat.Title = userMessage.Length > 50 ? userMessage.Substring(0, 50) + "..." : userMessage;
                chat.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
                await LoadUserChats();
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"API call failed: {ex.Message}");
            // Fallback to original logic if API fails
            using var dbContext = await DbContextFactory.CreateDbContextAsync();
            var chat = await dbContext.Chats.FindAsync(chatId);
            if (chat != null && chat.Title == "New Chat")
            {
                chat.Title = userMessage.Length > 50 ? userMessage.Substring(0, 50) + "..." : userMessage;
                chat.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
                await LoadUserChats();
                StateHasChanged();
            }
        }
        finally
        {
            isGeneratingChatName = false;
        }
    }

    private async Task AnimateChatName(string newName, Chat chat)
    {
        if (isDisposed) return;
        
        // If prerendering, just update directly without animation
        if (isPrerendering)
        {
            using var dbContext = await DbContextFactory.CreateDbContextAsync();
            var chatToUpdate = await dbContext.Chats.FindAsync(chat.Id);
            if (chatToUpdate != null)
            {
                chatToUpdate.Title = newName;
                chatToUpdate.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
                
                // Also update the local currentChat object if it's the same chat
                if (currentChat?.Id == chat.Id)
                {
                    currentChat.Title = newName;
                }
            }
            await LoadUserChats();
            return;
        }
        
        isChatNameAnimating = true;
        animatingChatName = newName;
        
        if (!isDisposed)
        {
            StateHasChanged();
        }

        try
        {
            // Animate each character
            await JSRuntime.InvokeVoidAsync("animateChatName", disposalTokenSource.Token, newName);
            
            if (isDisposed) return;
            
            // Wait for animation to complete
            await Task.Delay(newName.Length * 100 + 500, disposalTokenSource.Token);
            
            if (isDisposed) return;
            
            // Animation complete - now update the database
            using var dbContext = await DbContextFactory.CreateDbContextAsync();
            var chatToUpdate = await dbContext.Chats.FindAsync(chat.Id);
            if (chatToUpdate != null)
            {
                chatToUpdate.Title = newName;
                chatToUpdate.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
                
                // Also update the local currentChat object if it's the same chat
                if (currentChat?.Id == chat.Id)
                {
                    currentChat.Title = newName;
                }
            }
            await LoadUserChats();
            
            if (!isDisposed)
            {
                isChatNameAnimating = false;
                StateHasChanged();
            }
        }
        catch (OperationCanceledException)
        {
            // Component disposed, ignore
            if (!isDisposed)
            {
                isChatNameAnimating = false;
            }
        }
        catch (NotSupportedException)
        {
            // JS not available, fallback to direct update
            using var dbContext = await DbContextFactory.CreateDbContextAsync();
            var chatToUpdate = await dbContext.Chats.FindAsync(chat.Id);
            if (chatToUpdate != null)
            {
                chatToUpdate.Title = newName;
                chatToUpdate.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
                
                // Also update the local currentChat object if it's the same chat
                if (currentChat?.Id == chat.Id)
                {
                    currentChat.Title = newName;
                }
            }
            await LoadUserChats();
            
            if (!isDisposed)
            {
                isChatNameAnimating = false;
                StateHasChanged();
            }
        }
    }
    
    private void StartStreamingUpdateTimer()
    {
        if (_streamingUpdateTimer != null) return;
        
        _streamingUpdateTimer = new Timer(async _ => await CheckStreamingUpdates(), null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
        System.Diagnostics.Debug.WriteLine("Started streaming update timer");
    }
    
    private void StopStreamingUpdateTimer()
    {
        _streamingUpdateTimer?.Dispose();
        _streamingUpdateTimer = null;
        System.Diagnostics.Debug.WriteLine("Stopped streaming update timer");
    }
    
    private async Task CheckStreamingUpdates()
    {
        if (isDisposed || isPrerendering || currentChat == null) return;
        
        try
        {
            // Check for and display new notifications
            await CheckAndDisplayNotifications();
            
            var isCurrentlyStreaming = BackgroundStreamingService.IsStreamingActive(currentChat.Id);
            
            // Check for message updates in database (both during streaming and after completion)
            var lastMessage = messages?.LastOrDefault(m => m.Role == "assistant");
            if (lastMessage != null)
            {
                using var dbContext = await DbContextFactory.CreateDbContextAsync();
                // Reload the message from database to get latest content
                await dbContext.Entry(lastMessage).ReloadAsync();
                
                // Check if content has changed
                var newUpdateTime = lastMessage.UpdatedAt;
                if (newUpdateTime > _lastMessageUpdate)
                {
                    _lastMessageUpdate = newUpdateTime;
                    
                    // Invalidate processed content cache
                    processedMessageContent.Remove(lastMessage.Id);
                    
                    // Update UI
                    if (!isDisposed)
                    {
                        await InvokeAsync(StateHasChanged);
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Updated streaming content for message {lastMessage.Id} - Length: {lastMessage.Content?.Length ?? 0}, IsStreaming: {isCurrentlyStreaming}");
                }
                // If streaming just completed, force a final UI update to ensure stop button disappears
                else if (!isCurrentlyStreaming && DateTime.UtcNow - _lastMessageUpdate < TimeSpan.FromSeconds(2))
                {
                    System.Diagnostics.Debug.WriteLine($"Forcing final UI update after streaming completion for chat {currentChat.Id}");
                    
                    // Trigger syntax highlighting when streaming completes
                    shouldHighlightCode = true;
                    
                    if (!isDisposed)
                    {
                        await InvokeAsync(StateHasChanged);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking streaming updates: {ex.Message}");
        }
    }
    
    private async Task CheckAndDisplayNotifications()
    {
        if (isDisposed || isPrerendering) return;
        
        try
        {
            // Get notifications for current chat (or global notifications)
            var notifications = currentChat != null 
                ? NotificationService.GetNotificationsForChat(currentChat.Id)
                : NotificationService.GetNotifications();
            
            foreach (var notification in notifications)
            {
                try
                {
                    // Check if the function exists before calling it
                    var functionExists = await JSRuntime.InvokeAsync<bool>("eval", 
                        disposalTokenSource.Token, 
                        "typeof window.showErrorNotification === 'function'");
                    
                    if (functionExists)
                    {
                        // Show JavaScript notification
                        await JSRuntime.InvokeVoidAsync("showErrorNotification", 
                            disposalTokenSource.Token, 
                            notification.Message, 
                            notification.Type);
                        
                        System.Diagnostics.Debug.WriteLine($"Displayed {notification.Type} notification: {notification.Message}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"JavaScript notification function not available. Message: {notification.Message}");
                        
                        // Try alternative notification method
                        try
                        {
                            await JSRuntime.InvokeVoidAsync("console.error", 
                                disposalTokenSource.Token, 
                                $"[ERROR] {notification.Message}");
                        }
                        catch
                        {
                            // Ignore console errors
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to display notification: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking notifications: {ex.Message}");
        }
    }
    

    private async Task RetryMessage(Message userMessage)
    {
        if (currentChat == null || IsCurrentChatGenerating) return;

        using var dbContext = await DbContextFactory.CreateDbContextAsync();
        // Delete any messages that came after this user message
        var messagesToDelete = await dbContext.Messages
            .Where(m => m.ChatId == currentChat.Id && m.CreatedAt > userMessage.CreatedAt)
            .ToListAsync();

        if (messagesToDelete.Any())
        {
            dbContext.Messages.RemoveRange(messagesToDelete);
            
            // Update local messages list
            if (messages != null)
            {
                var messagesToRemove = messages.Where(m => m.CreatedAt > userMessage.CreatedAt).ToList();
                foreach (var msg in messagesToRemove)
                {
                    messages.Remove(msg);
                }
            }
        }

        await dbContext.SaveChangesAsync();
        
        // Clear processed content cache and update UI
        processedMessageContent.Clear();
        shouldHighlightCode = true;
        StateHasChanged();

        // Start new generation after retrying
        if (!IsCurrentChatGenerating)
        {
            await StartGeneration();
        }
    }

    private void StartEditMessage(Message userMessage)
    {
        editingMessageId = userMessage.Id;
        editingMessageContent = userMessage.Content;
        StateHasChanged();
    }

    private async Task SaveEditedMessage(Message userMessage)
    {
        if (currentChat == null || string.IsNullOrWhiteSpace(editingMessageContent)) return;

        using var dbContext = await DbContextFactory.CreateDbContextAsync();
        // Update the message content
        userMessage.Content = editingMessageContent.Trim();
        
        // Delete any messages that came after this one
        var messagesToDelete = await dbContext.Messages
            .Where(m => m.ChatId == currentChat.Id && m.CreatedAt > userMessage.CreatedAt)
            .ToListAsync();

        if (messagesToDelete.Any())
        {
            dbContext.Messages.RemoveRange(messagesToDelete);
            
            // Update local messages list
            if (messages != null)
            {
                var messagesToRemove = messages.Where(m => m.CreatedAt > userMessage.CreatedAt).ToList();
                foreach (var msg in messagesToRemove)
                {
                    messages.Remove(msg);
                }
            }
        }

        await dbContext.SaveChangesAsync();

        // Exit edit mode
        editingMessageId = null;
        editingMessageContent = string.Empty;
        
        // Clear processed content cache and update UI
        processedMessageContent.Clear();
        shouldHighlightCode = true;
        StateHasChanged();

        // Start new generation after editing
        if (!IsCurrentChatGenerating)
        {
            await StartGeneration();
        }
    }

    private void CancelEdit()
    {
        editingMessageId = null;
        editingMessageContent = string.Empty;
        StateHasChanged();
    }

    private async Task CopyMessage(Message message)
    {
        if (isDisposed || isPrerendering) return;
        
        try
        {
            await JSRuntime.InvokeVoidAsync("copyMessage", disposalTokenSource.Token, message.Content);
        }
        catch (OperationCanceledException)
        {
            // Component disposed, ignore
        }
        catch (NotSupportedException)
        {
            // JS not available during prerendering
        }
        catch (Exception ex)
        {
            // Fallback or error handling
            System.Diagnostics.Debug.WriteLine($"Failed to copy message: {ex.Message}");
        }
    }

    [JSInvokable]
    public async Task InsertCitationFromJS(string selectedText)
    {
        System.Diagnostics.Debug.WriteLine($"InsertCitationFromJS called with text: '{selectedText}'");
        try
        {
            await InsertCitationText(selectedText);
            System.Diagnostics.Debug.WriteLine("Citation insertion completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in InsertCitationFromJS: {ex.Message}");
            throw;
        }
    }
    
    private async Task InsertCitationText(string selectedText)
    {
        System.Diagnostics.Debug.WriteLine($"InsertCitationText called with: '{selectedText}'");
        
        if (string.IsNullOrWhiteSpace(selectedText)) 
        {
            System.Diagnostics.Debug.WriteLine("Selected text is empty, returning");
            return;
        }
        
        // Format the citation with > prefix and clean up the text
        var citation = $"> {selectedText.Trim()}\n\n";
        System.Diagnostics.Debug.WriteLine($"Formatted citation: '{citation}'");
        
        // Add to current message input
        var previousInput = messageInput;
        if (string.IsNullOrEmpty(messageInput))
        {
            messageInput = citation;
        }
        else
        {
            messageInput = messageInput.TrimEnd() + "\n\n" + citation;
        }
        
        System.Diagnostics.Debug.WriteLine($"Message input changed from: '{previousInput}' to: '{messageInput}'");
        
        // Auto-resize textarea and focus it
        await AutoResizeTextarea();
        
        if (!isPrerendering)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("focusElementById", disposalTokenSource.Token, "message-input-field");
                System.Diagnostics.Debug.WriteLine("Focused input field");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to focus input field: {ex.Message}");
            }
        }
        
        StateHasChanged();
        System.Diagnostics.Debug.WriteLine("StateHasChanged called");
    }

    private async Task BranchOffMessage(Message assistantMessage)
    {
        if (currentChat == null || currentUser == null || IsCurrentChatGenerating) return;

        using var dbContext = await DbContextFactory.CreateDbContextAsync();
        
        // Create a new branched chat with the same title
        var branchedChat = new Chat
        {
            Title = currentChat.Title,
            UserId = currentUser.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsBranched = true,
            OriginalChatId = currentChat.Id
        };

        dbContext.Chats.Add(branchedChat);
        await dbContext.SaveChangesAsync();

        // Copy all messages up to and including the selected assistant message
        var messagesToCopy = await dbContext.Messages
            .Where(m => m.ChatId == currentChat.Id && m.CreatedAt <= assistantMessage.CreatedAt)
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
                Model = originalMessage.Model
            };
            dbContext.Messages.Add(copiedMessage);
        }

        await dbContext.SaveChangesAsync();
        
        // Clear processed content cache and invalidate chat ID cache
        processedMessageContent.Clear();
        cachedCurrentChatId = null; // Reset cache tracking before navigation
        
        // Refresh chat list and navigate to the new branched chat
        await LoadUserChats();
        
        // Force a state update to ensure the sidebar renders with the new chat
        StateHasChanged();
        
        await SelectChat(branchedChat.Id, shouldNavigate: true);
        
        // Force immediate UI update to ensure streaming state is properly reflected
        await ForceStateHasChanged();
    }

    private async Task RetryAssistantMessage(Message assistantMessage)
    {
        if (currentChat == null || IsCurrentChatGenerating) return;

        using var dbContext = await DbContextFactory.CreateDbContextAsync();
        // Delete this assistant message and any messages that came after it
        var messagesToDelete = await dbContext.Messages
            .Where(m => m.ChatId == currentChat.Id && m.CreatedAt >= assistantMessage.CreatedAt)
            .ToListAsync();

        if (messagesToDelete.Any())
        {
            dbContext.Messages.RemoveRange(messagesToDelete);
            
            // Update local messages list
            if (messages != null)
            {
                var messagesToRemove = messages.Where(m => m.CreatedAt >= assistantMessage.CreatedAt).ToList();
                foreach (var msg in messagesToRemove)
                {
                    messages.Remove(msg);
                }
            }
        }

        await dbContext.SaveChangesAsync();
        
        // Clear processed content cache and update UI
        processedMessageContent.Clear();
        shouldHighlightCode = true;
        StateHasChanged();

        // Start new generation from the last user message
        if (!IsCurrentChatGenerating)
        {
            await StartGeneration();
        }
    }

    
    // File upload methods
    private async Task HandleFileUpload()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("HandleFileUpload called");
            await JSRuntime.InvokeVoidAsync("triggerFileInput");
            System.Diagnostics.Debug.WriteLine("triggerFileInput JavaScript called");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HandleFileUpload error: {ex.Message}");
        }
    }
    
    private async Task OnFileChange(InputFileChangeEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"OnFileChange called with {e.FileCount} files");
            foreach (var file in e.GetMultipleFiles(10)) // Max 10 files
            {
                // Check file type
                if (!IsValidFileType(file.ContentType))
                {
                    continue; // Skip invalid files
                }
                
                // Check file size with dynamic limit based on database configuration
                var maxFileSizeBytes = 10 * 1024 * 1024; // 10MB limit
                if (file.Size > maxFileSizeBytes)
                {
                    continue; // Skip large files
                }
                
                // Check model compatibility
                var attachmentType = GetAttachmentType(file.ContentType);
                
                // Skip images if model doesn't support vision (PDFs are allowed for all models)
                if (attachmentType == _4U.chat.Models.AttachmentType.Image && !_4U.chat.Services.OpenRouterService.ModelSupportsImages(selectedModel))
                {
                    System.Diagnostics.Debug.WriteLine($"Skipping image file {file.Name} - model {selectedModel} doesn't support images");
                    continue; // Skip images if model doesn't support them
                }
                
                // Read file as base64
                using var stream = file.OpenReadStream(maxFileSizeBytes);
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                var base64Data = Convert.ToBase64String(memoryStream.ToArray());
                
                var attachment = new MessageAttachment
                {
                    FileName = file.Name,
                    ContentType = file.ContentType,
                    Base64Data = base64Data,
                    FileSize = file.Size,
                    Type = attachmentType,
                    UploadedAt = DateTime.UtcNow
                };
                
                currentAttachments.Add(attachment);
            }
            
            StateHasChanged();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnFileChange error: {ex.Message}");
        }
    }
    
    private bool IsValidFileType(string contentType)
    {
        var validTypes = new[]
        {
            "image/png", "image/jpeg", "image/jpg", "image/webp",
            "application/pdf"
        };
        
        return validTypes.Contains(contentType.ToLower());
    }
    
    private _4U.chat.Models.AttachmentType GetAttachmentType(string contentType)
    {
        return contentType.ToLower() switch
        {
            "application/pdf" => _4U.chat.Models.AttachmentType.PDF,
            _ => _4U.chat.Models.AttachmentType.Image
        };
    }

    private string GetMaxFileSizeText()
    {
        return "10MB";
    }
    
    private void RemoveAttachment(int index)
    {
        if (index >= 0 && index < currentAttachments.Count)
        {
            currentAttachments.RemoveAt(index);
            StateHasChanged();
        }
    }
    
    private string GetAttachmentDisplayName(MessageAttachment attachment)
    {
        return attachment.FileName.Length > 30 
            ? attachment.FileName.Substring(0, 27) + "..." 
            : attachment.FileName;
    }
    
    private string GetAttachmentIcon(_4U.chat.Models.AttachmentType type)
    {
        return type switch
        {
            _4U.chat.Models.AttachmentType.Image => """
                <svg version="1.2" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="16" height="16">
                    <style>
                        .s0 { fill: none;stroke: currentColor;stroke-linecap: round;stroke-linejoin: round;stroke-width: 2 } 
                    </style>
                    <g>
                        <path class="s0" d="m21 11.5v7.5q0 0.4-0.2 0.8-0.1 0.3-0.4 0.6-0.3 0.3-0.6 0.4-0.4 0.2-0.8 0.2h-14q-0.4 0-0.8-0.2-0.3-0.1-0.6-0.4-0.3-0.3-0.4-0.6-0.2-0.4-0.2-0.8v-14q0-0.4 0.2-0.8 0.1-0.3 0.4-0.6 0.3-0.3 0.6-0.4 0.4-0.2 0.8-0.2h7.5"/>
                        <path class="s0" d="m11.5 3h7.5q0.4 0 0.8 0.2 0.3 0.1 0.6 0.4 0.3 0.3 0.4 0.6 0.2 0.4 0.2 0.8v14q0 0.4-0.2 0.8-0.1 0.3-0.4 0.6-0.3 0.3-0.6 0.4-0.4 0.2-0.8 0.2h-14q-0.4 0-0.8-0.2-0.3-0.1-0.6-0.4-0.3-0.3-0.4-0.6-0.2-0.4-0.2-0.8v-7.5"/>
                        <path class="s0" d="m21 15l-3.1-3.1q-0.3-0.3-0.6-0.4-0.4-0.2-0.8-0.2-0.4 0-0.8 0.2-0.3 0.1-0.6 0.4l-9.1 9.1"/>
                        <path class="s0" d="m9 11c-1.1 0-2-0.9-2-2 0-1.1 0.9-2 2-2 1.1 0 2 0.9 2 2 0 1.1-0.9 2-2 2z"/>
                    </g>
                </svg>
                """,
            _4U.chat.Models.AttachmentType.PDF => """
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <path d="M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z"></path>
                    <path d="M14 2v4a2 2 0 0 0 2 2h4"></path>
                    <path d="M10 9H8"></path>
                    <path d="M16 13H8"></path>
                    <path d="M16 17H8"></path>
                </svg>
                """,
            _ => """
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                    <path d="m21.44 11.05-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48"/>
                </svg>
                """
        };
    }
    
    private string GetAcceptedFileTypes()
    {
        var accepts = new List<string>();
        
        // Always allow PDFs for all models
        accepts.Add(".pdf");
        
        // Allow images only if model supports vision
        if (_4U.chat.Services.OpenRouterService.ModelSupportsImages(selectedModel))
        {
            accepts.AddRange(new[] { ".png", ".jpg", ".jpeg", ".webp" });
        }
        
        return string.Join(",", accepts);
    }
    
    private async Task<string> BuildSystemPrompt()
    {
        if (currentUser == null) return string.Empty;
        
        // Reload user with latest customization data
        var user = await UserManager.Users.FirstOrDefaultAsync(u => u.Id == currentUser.Id);
        if (user == null) return string.Empty;
        
        var prompt = new List<string>();
        
        // Base system instruction
        prompt.Add("You are 4U Chat, an AI assistant designed to be helpful, accurate, and engaging.");
        
        // User personalization
        if (!string.IsNullOrEmpty(user.DisplayName))
        {
            prompt.Add($"The user prefers to be called '{user.DisplayName}'.");
        }
        
        if (!string.IsNullOrEmpty(user.Occupation))
        {
            prompt.Add($"The user works as: {user.Occupation}");
        }
        
        // AI personality traits
        if (!string.IsNullOrEmpty(user.Traits))
        {
            try
            {
                var traits = JsonSerializer.Deserialize<List<string>>(user.Traits);
                if (traits != null && traits.Any())
                {
                    var traitsText = string.Join(", ", traits); // Use all selected traits (up to 50 as per UI limit)
                    prompt.Add($"You should embody these personality traits: {traitsText}.");
                }
            }
            catch
            {
                // Fallback to comma-separated parsing
                var traits = user.Traits.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                      .Select(t => t.Trim())
                                      .ToList();
                if (traits.Any())
                {
                    var traitsText = string.Join(", ", traits);
                    prompt.Add($"You should embody these personality traits: {traitsText}.");
                }
            }
        }
        
        // Additional custom context
        if (!string.IsNullOrEmpty(user.CustomPrompt))
        {
            prompt.Add($"Additional context about the user: {user.CustomPrompt}");
        }
        
        // Citation format explanation
        prompt.Add("When the user includes text prefixed with '>' (like '> some text'), treat this as a citation or reference to previous parts of the conversation. The user is referring to or quoting that specific text.");
        
        // Final instruction
        prompt.Add("Respond in a way that reflects these preferences and traits while being helpful and accurate.");
        
        return string.Join(" ", prompt);
    }

    private bool IsImageGenerationModel(string uniqueId)
    {
        return _4U.chat.Services.OpenRouterService.IsImageGenerationModel(uniqueId);
    }
    
    private _4U.chat.Services.ModelConfiguration? GetModelConfig(string uniqueId)
    {
        return _4U.chat.Services.OpenRouterService.GetModelConfigurationByUniqueId(uniqueId);
    }
    
    private async Task ToggleFavorite(string uniqueId)
    {
        if (currentUser == null) return;

        // Get current user favorites
        var currentFavorites = GetUserFavoriteModelIds();
        
        // Toggle the model in favorites list
        if (currentFavorites.Contains(uniqueId))
        {
            currentFavorites.Remove(uniqueId);
        }
        else
        {
            currentFavorites.Add(uniqueId);
        }

        // Save to database
        currentUser.FavoriteModels = _4U.chat.Services.OpenRouterService.SerializeFavoriteModels(currentFavorites);
        
        try
        {
            await UserManager.UpdateAsync(currentUser);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving favorite models: {ex.Message}");
        }
    }

    private List<string> GetUserFavoriteModelIds()
    {
        if (currentUser?.FavoriteModels == null)
        {
            return _4U.chat.Services.OpenRouterService.GetDefaultFavoriteModelIds();
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(currentUser.FavoriteModels) ?? new List<string>();
        }
        catch
        {
            return _4U.chat.Services.OpenRouterService.GetDefaultFavoriteModelIds();
        }
    }

    private bool IsUserFavoriteModel(string uniqueId)
    {
        return _4U.chat.Services.OpenRouterService.IsUserFavoriteModel(uniqueId, currentUser?.FavoriteModels);
    }

    private List<_4U.chat.Services.ModelConfiguration> GetUserFavoriteModels()
    {
        return _4U.chat.Services.OpenRouterService.GetUserFavoriteModels(currentUser?.FavoriteModels);
    }

    private async Task InitializeUserFavorites()
    {
        if (currentUser == null) return;

        // If user doesn't have favorites set yet, initialize with defaults
        if (string.IsNullOrEmpty(currentUser.FavoriteModels))
        {
            var defaultFavorites = _4U.chat.Services.OpenRouterService.GetDefaultFavoriteModelIds();
            currentUser.FavoriteModels = _4U.chat.Services.OpenRouterService.SerializeFavoriteModels(defaultFavorites);
            
            try
            {
                await UserManager.UpdateAsync(currentUser);
                System.Diagnostics.Debug.WriteLine($"Initialized user favorites with {defaultFavorites.Count} default models");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing user favorites: {ex.Message}");
            }
        }
    }
    
    private void StopCurrentStreaming()
    {
        if (CurrentChatId.HasValue)
        {
            BackgroundStreamingService.StopStreaming(CurrentChatId.Value);
            System.Diagnostics.Debug.WriteLine($"Stopped streaming for chat {CurrentChatId.Value}");
        }
    }
    
    private async Task ContinueGenerationFromPartial(Message assistantMessage)
    {
        if (currentUser == null || currentChat == null || IsCurrentChatGenerating) 
        {
            System.Diagnostics.Debug.WriteLine("Cannot continue generation - user, chat, or already generating");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"Continuing generation from partial message {assistantMessage.Id}");

        // Prepare chat messages for the API (excluding the current assistant message)
        var chatMessages = messages!
            .Where(m => m.Id != assistantMessage.Id)
            .OrderBy(m => m.CreatedAt)
            .Select(m => ConvertToOpenRouterMessage(m))
            .ToList();
        
        // Get user API key and system prompt
        var apiKey = currentUser.OpenRouterApiKey;
        var systemPrompt = await BuildSystemPrompt();
        
        // Start background streaming from where we left off
        var success = await BackgroundStreamingService.StartStreamingAsync(
            currentChat.Id,
            assistantMessage.Id,
            GetActualModelId(selectedModel),
            GetModelDisplayNameFromUniqueId(selectedModel),
            apiKey!,
            systemPrompt,
            chatMessages,
            isWebSearchEnabled,
            isWebSearchEnabled ? new WebSearchOptions() : null
        );

        if (success)
        {
            System.Diagnostics.Debug.WriteLine($"Successfully continued generation for message {assistantMessage.Id}");
            _lastMessageUpdate = DateTime.UtcNow;
            await InvokeAsync(StateHasChanged);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"Failed to continue generation for message {assistantMessage.Id}");
        }
    }

    public void Dispose()
    {
        System.Diagnostics.Debug.WriteLine("Component Disposing");
        
        isDisposed = true;
        
        // Cancel all pending operations
        disposalTokenSource?.Cancel();
        
        // Stop streaming update timer
        StopStreamingUpdateTimer();
        
        loadingSemaphore?.Dispose();
        disposalTokenSource?.Dispose();
        citationHandlerReference?.Dispose();
        
        System.Diagnostics.Debug.WriteLine("Component Disposed");
    }
}