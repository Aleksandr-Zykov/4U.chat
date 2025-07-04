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
    [Inject] private ILogger<Home> _logger { get; set; } = default!;
    [Inject] private MarkdownService MarkdownService { get; set; } = default!;
    [Inject] private BackgroundStreamingService BackgroundStreamingService { get; set; } = default!;
    [Inject] private BackgroundJobService BackgroundJobService { get; set; } = default!;
    [Inject] private NotificationService NotificationService { get; set; } = default!;
    [Inject] protected UIStateService UIStateService { get; set; } = default!;
    [Inject] private ChatService ChatService { get; set; } = default!;
    
    private User? currentUser;
    private _4U.chat.Components.Layout.ChatSidebar? chatSidebar;
    private int? cachedCurrentChatId;
    private bool isInitializing = true;
    private bool isDisposed = false;
    private bool isPrerendering = true;
    private readonly SemaphoreSlim loadingSemaphore = new(1, 1);
    private CancellationTokenSource disposalTokenSource = new();
    private Chat? currentChat;
    private List<Message>? messages;
    private Dictionary<int, string> processedMessageContent = new();
    private string selectedModel = "google/gemini-2.5-flash-lite-preview-06-17|Gemini 2.5 Flash Lite";
    private bool shouldHighlightCode = false;
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

    // Image modal state
    private bool showImageModal = false;
    private string? selectedImageUrl;
    private string? selectedImageFileName;

    // PDF modal state
    private bool showPdfModal = false;
    private string? selectedPdfUrl;
    private string? selectedPdfFileName;
    
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

    private string GetAttachmentIcon(AttachmentType type)
    {
        return type switch
        {
            AttachmentType.Image => """<svg version="1.2" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" width="16" height="16"><style>.s0 { fill: none;stroke: currentColor;stroke-linecap: round;stroke-linejoin: round;stroke-width: 2 } </style><g><path class="s0" d="m21 11.5v7.5q0 0.4-0.2 0.8-0.1 0.3-0.4 0.6-0.3 0.3-0.6 0.4-0.4 0.2-0.8 0.2h-14q-0.4 0-0.8-0.2-0.3-0.1-0.6-0.4-0.3-0.3-0.4-0.6-0.2-0.4-0.2-0.8v-14q0-0.4 0.2-0.8 0.1-0.3 0.4-0.6 0.3-0.3 0.6-0.4 0.4-0.2 0.8-0.2h7.5"/><path class="s0" d="m11.5 3h7.5q0.4 0 0.8 0.2 0.3 0.1 0.6 0.4 0.3 0.3 0.4 0.6 0.2 0.4 0.2 0.8v14q0 0.4-0.2 0.8-0.1 0.3-0.4 0.6-0.3 0.3-0.6 0.4-0.4 0.2-0.8 0.2h-14q-0.4 0-0.8-0.2-0.3-0.1-0.6-0.4-0.3-0.3-0.4-0.6-0.2-0.4-0.2-0.8v-7.5"/><path class="s0" d="m21 15l-3.1-3.1q-0.3-0.3-0.6-0.4-0.4-0.2-0.8-0.2-0.4 0-0.8 0.2-0.3 0.1-0.6 0.4l-9.1 9.1"/><path class="s0" d="m9 11c-1.1 0-2-0.9-2-2 0-1.1 0.9-2 2-2 1.1 0 2 0.9 2 2 0 1.1-0.9 2-2 2z"/></g></svg>""",
            AttachmentType.PDF => """<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z"></path><path d="M14 2v4a2 2 0 0 0 2 2h4"></path><path d="M10 9H8"></path><path d="M16 13H8"></path><path d="M16 17H8"></path></svg>""",
            _ => """<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="m21.44 11.05-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48"/></svg>"""
        };
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
        UIStateService.OnChange += StateHasChangedHandler;

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
            // This is now handled by the MessageInput component
            
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
                // Persisted settings like model and web search are now loaded in the MessageInput component.
                
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
                UIStateService.SetTheme(!hasLightClass);
            }
            catch
            {
                // If we can't read DOM, assume dark
                UIStateService.SetTheme(true);
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
        
        currentChat = await ChatService.GetChatWithMessagesAsync(chatId, currentUser!.Id);
            
        if (currentChat != null)
        {
            messages = currentChat.Messages;
            
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

    private void HandleModelLoaded()
    {
        isModelLoaded = true;
        StateHasChanged();
    }

    private async Task HandleMessageSent(_4U.chat.Components.Shared.MessageInput.MessageSentEventArgs args)
    {
        if ((string.IsNullOrWhiteSpace(args.Content) && !args.Attachments.Any()) || currentUser == null || IsCurrentChatGenerating) return;

        var content = args.Content;
        var attachments = args.Attachments;
        this.selectedModel = args.SelectedModel;
        this.isWebSearchEnabled = args.IsWebSearchEnabled;

        bool isNewChat = currentChat == null;
        Chat chat;
        Message userMessage;

        if (isNewChat)
        {
            chat = await ChatService.CreateNewChatAsync(currentUser.Id, "New Chat");
            currentChat = chat;
        }
        else
        {
            chat = currentChat!;
        }

        (userMessage, chat) = await ChatService.AddUserMessageAsync(chat.Id, content, attachments);
        messages?.Add(userMessage);

        if (isNewChat)
        {
            currentChat = await ChatService.GetChatWithMessagesAsync(chat.Id, currentUser.Id);
            messages = currentChat!.Messages;
            if (chatSidebar is not null) await chatSidebar.RefreshChats();
        }

        if (chat.Title == "New Chat")
        {
            _ = GenerateChatNameAsync(content, chat);
        }

        shouldHighlightCode = true;
        await ForceStateHasChanged();

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
        
        var assistantMessage = await ChatService.GenerateResponseAsync(currentChat!, selectedModel, isWebSearchEnabled, BuildSystemPrompt);
        if (assistantMessage != null)
        {
            messages?.Add(assistantMessage);
        }

        if (isNewChat && currentChat != null && !isPrerendering)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("history.replaceState", disposalTokenSource.Token, null, "", $"/chat/{currentChat.Id}");
                await JSRuntime.InvokeVoidAsync("setActiveChatItem", disposalTokenSource.Token, currentChat.Id);
            }
            catch (Exception)
            {
                // Ignore JS interop errors
            }
        }
        await ForceStateHasChanged();
    }

    private void OpenImageModal(MessageAttachment attachment)
    {
        if (attachment.Type == AttachmentType.Image)
        {
            selectedImageUrl = $"data:{attachment.ContentType};base64,{attachment.Base64Data}";
            selectedImageFileName = attachment.FileName;
            showImageModal = true;
            StateHasChanged();
        }
    }

    private void CloseImageModal()
    {
        showImageModal = false;
        selectedImageUrl = null;
        selectedImageFileName = null;
        StateHasChanged();
    }

    private void OpenPdfModal(MessageAttachment attachment)
    {
        if (attachment.Type == AttachmentType.PDF)
        {
            selectedPdfUrl = $"data:{attachment.ContentType};base64,{attachment.Base64Data}";
            selectedPdfFileName = attachment.FileName;
            showPdfModal = true;
            StateHasChanged();
        }
    }

    private void ClosePdfModal()
    {
        showPdfModal = false;
        selectedPdfUrl = null;
        selectedPdfFileName = null;
        StateHasChanged();
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

    private async Task SuggestMessage(string suggestion)
    {
        // This functionality will need to be re-wired to the new MessageInput component if needed.
        // For now, it will not function.
        await Task.CompletedTask;
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
    
        UIStateService.SetTheme(!UIStateService.IsDarkTheme);
    
        // Apply theme and save to localStorage atomically to prevent race conditions
        await ApplyThemeAndSave();
    }
    
    private async Task ApplyThemeAndSave()
    {
        if (isDisposed || isPrerendering) return;
    
        var themeValue = UIStateService.IsDarkTheme ? "dark" : "light";
        
        try
        {
            // Save to localStorage first, then apply to DOM to ensure consistency
            await JSRuntime.InvokeVoidAsync("localStorage.setItem", disposalTokenSource.Token, "theme", themeValue);
            
            // Apply theme to DOM immediately after saving
            if (UIStateService.IsDarkTheme)
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
            UIStateService.SetTheme(!UIStateService.IsDarkTheme);
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
            UIStateService.SetTheme(!shouldBeLight);
            
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
    
        await JSRuntime.InvokeVoidAsync("console.log", disposalTokenSource.Token, $"ApplyTheme called - isDarkTheme: {UIStateService.IsDarkTheme}");
    
        try
        {
            if (UIStateService.IsDarkTheme)
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
    
    private void ToggleSidebar()
    {
        UIStateService.ToggleSidebar();
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
    
    private async Task DeleteChat(int chatId)
    {
        if (currentUser == null) return;

        await ChatService.DeleteChatAsync(chatId, currentUser.Id);
        
        // If we deleted the current chat, navigate to home
        if (CurrentChatId == chatId)
        {
            processedMessageContent.Clear();
            await GoToHome();
        }
        
        // Refresh the chat list
        if (chatSidebar is not null) await chatSidebar.RefreshChats();
        StateHasChanged();
    }

    private async Task TogglePinChat(int chatId)
    {
        if (currentUser == null) return;
        
        var success = await ChatService.TogglePinChatAsync(chatId, currentUser.Id);
            
        if (success)
        {
            // Refresh the chat list
            if (chatSidebar is not null) await chatSidebar.RefreshChats();
            
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

    private async Task GenerateChatNameAsync(string userMessage, Chat chat)
    {
        try
        {
            var newName = await ChatService.GenerateChatNameAsync(userMessage, currentUser?.OpenRouterApiKey);

            if (chat != null && !string.IsNullOrEmpty(newName))
            {
                await AnimateChatName(newName, chat);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate chat name for chat {ChatId}", chat.Id);
        }
    }

    private async Task AnimateChatName(string newName, Chat chat)
    {
        if (isDisposed) return;
        
        // If prerendering, just update directly without animation
        if (isPrerendering)
        {
            await ChatService.UpdateChatTitleAsync(chat.Id, newName);
            
            // Also update the local currentChat object if it's the same chat
            if (currentChat?.Id == chat.Id)
            {
                currentChat.Title = newName;
            }
            if (chatSidebar is not null) await chatSidebar.RefreshChats();
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
            await ChatService.UpdateChatTitleAsync(chat.Id, newName);
            
            // Also update the local currentChat object if it's the same chat
            if (currentChat?.Id == chat.Id)
            {
                currentChat.Title = newName;
            }
            if (chatSidebar is not null) await chatSidebar.RefreshChats();
            
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
            await ChatService.UpdateChatTitleAsync(chat.Id, newName);

            // Also update the local currentChat object if it's the same chat
            if (currentChat?.Id == chat.Id)
            {
                currentChat.Title = newName;
            }
            if (chatSidebar is not null) await chatSidebar.RefreshChats();
            
            if (!isDisposed)
            {
                isChatNameAnimating = false;
                StateHasChanged();
            }
        }
    }
    
    private readonly Dictionary<int, bool> _wasStreaming = new();

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
        if (isDisposed || isPrerendering) return;

        try
        {
            await CheckAndDisplayNotifications();

            if (currentChat == null) return;

            var chatId = currentChat.Id;
            var isCurrentlyStreaming = BackgroundStreamingService.IsStreamingActive(chatId);

            if (isCurrentlyStreaming)
            {
                // Simply trigger a UI update to reflect the latest streaming state
                // from BackgroundStreamingService. No need to hit the database here.
                if (!isDisposed)
                {
                    await InvokeAsync(StateHasChanged);
                }
            }
            else
            {
                // Check for any "Generating images..." messages that might be complete
                var imageGenMessages = messages?.Where(m => m.Content == "*Generating images...*").ToList();
                if (imageGenMessages != null)
                {
                    foreach (var msg in imageGenMessages)
                    {
                        if (!BackgroundJobService.IsJobActive(msg.Id))
                        {
                            _logger.LogInformation($"Detected completed image job for message {msg.Id}. Reloading.");
                            using var dbContext = await DbContextFactory.CreateDbContextAsync();
                            await dbContext.Entry(msg).ReloadAsync();
                            processedMessageContent.Remove(msg.Id);
                            await InvokeAsync(StateHasChanged);
                        }
                    }
                }
            }

            if (_wasStreaming.TryGetValue(chatId, out var wasStreaming) && wasStreaming && !isCurrentlyStreaming)
            {
                _logger.LogInformation($"Forcing final UI update after streaming completion for chat {chatId}");

                var lastMessage = messages?.LastOrDefault(m => m.Role == "assistant");
                if (lastMessage != null)
                {
                    using var dbContext = await DbContextFactory.CreateDbContextAsync();
                    await dbContext.Entry(lastMessage).ReloadAsync();
                    processedMessageContent.Remove(lastMessage.Id);
                    
                    if (!isDisposed)
                    {
                        await InvokeAsync(StateHasChanged);
                    }
                }
                
                // Focus textarea after streaming is complete
                if (!isPrerendering)
                {
                    try
                    {
                        await JSRuntime.InvokeVoidAsync("focusElementById", "message-input-field");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to focus textarea after streaming.");
                    }
                }
            }

            _wasStreaming[chatId] = isCurrentlyStreaming;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CheckStreamingUpdates");
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

        await ChatService.RetryMessageAsync(currentChat.Id, userMessage.Id);

        // Update local messages list
        if (messages != null)
        {
            var messagesToRemove = messages.Where(m => m.CreatedAt > userMessage.CreatedAt).ToList();
            foreach (var msg in messagesToRemove)
            {
                messages.Remove(msg);
            }
        }
        
        // Clear processed content cache and update UI
        processedMessageContent.Clear();
        shouldHighlightCode = true;
        StateHasChanged();

        // Start new generation after retrying
        if (!IsCurrentChatGenerating)
        {
            await ChatService.GenerateResponseAsync(currentChat, selectedModel, isWebSearchEnabled, BuildSystemPrompt);
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

        var newContent = editingMessageContent.Trim();
        await ChatService.SaveEditedMessageAsync(userMessage.Id, newContent);

        // Update local message and remove subsequent messages
        userMessage.Content = newContent;
        if (messages != null)
        {
            var messagesToRemove = messages.Where(m => m.CreatedAt > userMessage.CreatedAt).ToList();
            foreach (var msg in messagesToRemove)
            {
                messages.Remove(msg);
            }
        }

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
            await ChatService.GenerateResponseAsync(currentChat, selectedModel, isWebSearchEnabled, BuildSystemPrompt);
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
        
        // This method will need to be updated to communicate with the MessageInput component.
        // For now, it won't work as messageInput is no longer part of this component.
        
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

        var branchedChat = await ChatService.BranchOffChatAsync(currentChat.Id, assistantMessage.Id, currentUser.Id);
        
        // Clear processed content cache and invalidate chat ID cache
        processedMessageContent.Clear();
        cachedCurrentChatId = null; // Reset cache tracking before navigation
        
        // Refresh chat list and navigate to the new branched chat
        if (chatSidebar is not null) await chatSidebar.RefreshChats();
        
        // Force a state update to ensure the sidebar renders with the new chat
        StateHasChanged();
        
        await SelectChat(branchedChat.Id, shouldNavigate: true);
        
        // Force immediate UI update to ensure streaming state is properly reflected
        await ForceStateHasChanged();
    }

    private async Task RetryAssistantMessage(Message assistantMessage)
    {
        if (currentChat == null || IsCurrentChatGenerating) return;

        await ChatService.RetryAssistantMessageAsync(assistantMessage.Id);

        // Update local messages list
        if (messages != null)
        {
            var messagesToRemove = messages.Where(m => m.CreatedAt >= assistantMessage.CreatedAt).ToList();
            foreach (var msg in messagesToRemove)
            {
                messages.Remove(msg);
            }
        }
        
        // Clear processed content cache and update UI
        processedMessageContent.Clear();
        shouldHighlightCode = true;
        StateHasChanged();

        // Start new generation from the last user message
        if (!IsCurrentChatGenerating)
        {
            await ChatService.GenerateResponseAsync(currentChat, selectedModel, isWebSearchEnabled, BuildSystemPrompt);
        }
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
        if (!string.IsNullOrEmpty(user.Name))
        {
            prompt.Add($"The user prefers to be called '{user.Name}'.");
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
    
    private string GetLoadingMessage()
    {
        if (isInitializing)
        {
            return "Initializing 4U Chat...";
        }
        else if (!isModelLoaded)
        {
            return "Loading AI Models...";
        }
        else if (!isChatLoaded)
        {
            return "Loading Chat...";
        }
        
        return "Loading...";
    }
    
    private string GetLoadingSubMessage()
    {
        if (isInitializing)
        {
            return "Setting up your personalized chat experience";
        }
        else if (!isModelLoaded)
        {
            return "Preparing available AI models";
        }
        else if (!isChatLoaded)
        {
            return "Retrieving your conversation history";
        }
        
        return "Please wait while we prepare your content";
    }

    public void Dispose()
    {
        System.Diagnostics.Debug.WriteLine("Component Disposing");
    
        isDisposed = true;
    
        // Cancel all pending operations
        disposalTokenSource?.Cancel();
    
        // Stop streaming update timer
        StopStreamingUpdateTimer();
    
        UIStateService.OnChange -= StateHasChangedHandler;

        loadingSemaphore?.Dispose();
        disposalTokenSource?.Dispose();
        citationHandlerReference?.Dispose();
    
        System.Diagnostics.Debug.WriteLine("Component Disposed");
    }

    private void StateHasChangedHandler()
    {
        if (!isDisposed)
        {
            InvokeAsync(StateHasChanged);
        }
    }
}
