using _4U.chat.Models;
using _4U.chat.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Identity;
using Microsoft.JSInterop;
using System.Text.Json;

namespace _4U.chat.Components.Shared
{
    public partial class MessageInput : ComponentBase, IDisposable
    {
        [Parameter] public bool IsSending { get; set; }
        [Parameter] public EventCallback<MessageSentEventArgs> OnMessageSent { get; set; }
        [Parameter] public EventCallback OnStopGeneration { get; set; }
        [Parameter] public User? CurrentUser { get; set; }
        [Parameter] public string InitialSelectedModel { get; set; } = "google/gemini-2.5-flash-lite-preview-06-17|Gemini 2.5 Flash Lite";
        [Parameter] public EventCallback OnModelLoaded { get; set; }

        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private UserManager<User> UserManager { get; set; } = default!;

        private string messageInput = string.Empty;
        private string selectedModel = string.Empty;
        private ElementReference messageTextArea;
        private bool showModelSelector = false;
        private string modelSearchQuery = string.Empty;
        private bool showAllModels = false;
        private bool isModelLoaded = false;
        private bool isWebSearchEnabled = false;
        private List<MessageAttachment> currentAttachments = new();
        private InputFile? fileInputComponent;

        private bool isDisposed = false;
        private bool isPrerendering = true;
        private CancellationTokenSource disposalTokenSource = new();

        protected override async Task OnInitializedAsync()
        {
            selectedModel = InitialSelectedModel;
            await LoadPersistedState();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                isPrerendering = false;
                await LoadPersistedState();
                StateHasChanged();
            }
        }
        
        private async Task LoadPersistedState()
        {
            if (isPrerendering) return;

            await LoadModelSelection();
            await LoadWebSearchState();
        }

        private async Task HandleSendMessage()
        {
            if ((string.IsNullOrWhiteSpace(messageInput) && !currentAttachments.Any()) || IsSending) return;

            var args = new MessageSentEventArgs
            {
                Content = messageInput.Trim(),
                Attachments = new List<MessageAttachment>(currentAttachments),
                SelectedModel = selectedModel,
                IsWebSearchEnabled = isWebSearchEnabled
            };

            messageInput = string.Empty;
            currentAttachments.Clear();

            await OnMessageSent.InvokeAsync(args);
            await AutoResizeTextarea();
        }

        private async Task HandleStopGeneration()
        {
            await OnStopGeneration.InvokeAsync();
        }

        private async Task AutoResizeTextarea()
        {
            if (isDisposed || isPrerendering) return;

            try
            {
                await JSRuntime.InvokeVoidAsync("autoResizeTextarea", disposalTokenSource.Token, messageTextArea);
            }
            catch (Exception) { /* Ignore JS errors */ }
        }

        public class MessageSentEventArgs
        {
            public string Content { get; set; } = string.Empty;
            public List<MessageAttachment> Attachments { get; set; } = new();
            public string SelectedModel { get; set; } = string.Empty;
            public bool IsWebSearchEnabled { get; set; }
        }

        public class ModelInfo
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Provider { get; set; } = string.Empty;
            public bool IsActive { get; set; } = true;
            public string InactiveReason { get; set; } = string.Empty;
        }

        private List<ModelInfo> availableModels
        {
            get
            {
                return OpenRouterService.ModelConfigurations
                    .Where(config => showAllModels || IsUserFavoriteModel(config.UniqueId))
                    .Select(config => new ModelInfo
                    {
                        Id = config.UniqueId,
                        Name = config.DisplayName,
                        Description = config.Description,
                        Provider = config.Provider,
                        IsActive = config.IsActive,
                        InactiveReason = config.InactiveReason
                    })
                    .ToList();
            }
        }

        private void ToggleModelSelector()
        {
            showModelSelector = !showModelSelector;
        }

        private async Task SelectModel(string uniqueId)
        {
            var modelInfo = availableModels.FirstOrDefault(m => m.Id == uniqueId);
            if (modelInfo != null && !modelInfo.IsActive)
            {
                return;
            }

            selectedModel = uniqueId;
            showModelSelector = false;
            await SaveModelSelection();
        }

        private string GetModelDisplayName(string uniqueId)
        {
            var config = OpenRouterService.GetModelConfigurationByUniqueId(uniqueId);
            return config?.DisplayName ?? "Unknown Model";
        }

        private List<ModelInfo> GetFilteredModels()
        {
            var models = availableModels.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(modelSearchQuery))
            {
                var query = modelSearchQuery.Trim().ToLowerInvariant();
                models = models.Where(m =>
                    m.Name.ToLowerInvariant().Contains(query) ||
                    m.Description.ToLowerInvariant().Contains(query) ||
                    m.Provider.ToLowerInvariant().Contains(query)
                );
            }

            return models.ToList();
        }

        private void FilterModels()
        {
        }

        private void ToggleShowAllModels()
        {
            showAllModels = !showAllModels;
        }

        private async Task ToggleWebSearch()
        {
            isWebSearchEnabled = !isWebSearchEnabled;
            await SaveWebSearchState();
        }

        private async Task SaveModelSelection()
        {
            if (isDisposed || isPrerendering) return;
            try
            {
                await JSRuntime.InvokeVoidAsync("localStorage.setItem", disposalTokenSource.Token, "selectedModel", selectedModel);
            }
            catch (Exception) { /* Ignore */ }
        }

        private async Task LoadModelSelection()
        {
            if (isDisposed || isPrerendering) return;

            try
            {
                var savedModel = await JSRuntime.InvokeAsync<string>("localStorage.getItem", disposalTokenSource.Token, "selectedModel");
                if (!string.IsNullOrEmpty(savedModel))
                {
                    var modelConfig = OpenRouterService.ModelConfigurations.FirstOrDefault(c => c.UniqueId == savedModel);
                    if (modelConfig != null)
                    {
                        selectedModel = savedModel;
                    }
                    else
                    {
                        var actualModelId = savedModel.Split('|')[0];
                        var backwardCompatibleConfig = OpenRouterService.ModelConfigurations.FirstOrDefault(c => c.ModelId == actualModelId);
                        if (backwardCompatibleConfig != null)
                        {
                            selectedModel = backwardCompatibleConfig.UniqueId;
                            await SaveModelSelection();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadModelSelection error: {ex.Message}");
            }
            finally
            {
                isModelLoaded = true;
                await OnModelLoaded.InvokeAsync();
            }
        }

        private async Task SaveWebSearchState()
        {
            if (isDisposed || isPrerendering) return;
            try
            {
                await JSRuntime.InvokeVoidAsync("localStorage.setItem", disposalTokenSource.Token, "webSearchEnabled", isWebSearchEnabled.ToString().ToLower());
            }
            catch (Exception) { /* Ignore */ }
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadWebSearchState error: {ex.Message}");
            }
        }

        private async Task HandleFileUpload()
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("triggerFileInput");
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
                foreach (var file in e.GetMultipleFiles(10))
                {
                    if (!IsValidFileType(file.ContentType)) continue;
                    var maxFileSizeBytes = 10 * 1024 * 1024;
                    if (file.Size > maxFileSizeBytes) continue;

                    var attachmentType = GetAttachmentType(file.ContentType);
                    if (attachmentType == AttachmentType.Image && !OpenRouterService.ModelSupportsImages(selectedModel))
                    {
                        continue;
                    }

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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnFileChange error: {ex.Message}");
            }
        }

        private bool IsValidFileType(string contentType)
        {
            var validTypes = new[] { "image/png", "image/jpeg", "image/jpg", "image/webp", "application/pdf" };
            return validTypes.Contains(contentType.ToLower());
        }

        private AttachmentType GetAttachmentType(string contentType)
        {
            return contentType.ToLower() switch
            {
                "application/pdf" => AttachmentType.PDF,
                _ => AttachmentType.Image
            };
        }

        private void RemoveAttachment(int index)
        {
            if (index >= 0 && index < currentAttachments.Count)
            {
                currentAttachments.RemoveAt(index);
            }
        }

        private string GetAttachmentDisplayName(MessageAttachment attachment)
        {
            return attachment.FileName.Length > 30 ? attachment.FileName[..27] + "..." : attachment.FileName;
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

        private string GetAcceptedFileTypes()
        {
            var accepts = new List<string> { ".pdf" };
            if (OpenRouterService.ModelSupportsImages(selectedModel))
            {
                accepts.AddRange(new[] { ".png", ".jpg", ".jpeg", ".webp" });
            }
            return string.Join(",", accepts);
        }

        private bool IsImageGenerationModel(string uniqueId) => OpenRouterService.IsImageGenerationModel(uniqueId);
        
        private async Task ToggleFavorite(string uniqueId)
        {
            if (CurrentUser == null) return;

            var currentFavorites = GetUserFavoriteModelIds();
            if (currentFavorites.Contains(uniqueId))
            {
                currentFavorites.Remove(uniqueId);
            }
            else
            {
                currentFavorites.Add(uniqueId);
            }

            CurrentUser.FavoriteModels = OpenRouterService.SerializeFavoriteModels(currentFavorites);
            await UserManager.UpdateAsync(CurrentUser);
        }

        private List<string> GetUserFavoriteModelIds()
        {
            if (CurrentUser?.FavoriteModels == null)
            {
                return OpenRouterService.GetDefaultFavoriteModelIds();
            }
            try
            {
                return JsonSerializer.Deserialize<List<string>>(CurrentUser.FavoriteModels) ?? new List<string>();
            }
            catch
            {
                return OpenRouterService.GetDefaultFavoriteModelIds();
            }
        }

        private bool IsUserFavoriteModel(string uniqueId)
        {
            return OpenRouterService.IsUserFavoriteModel(uniqueId, CurrentUser?.FavoriteModels);
        }

        public void Dispose()
        {
            isDisposed = true;
            disposalTokenSource?.Cancel();
            disposalTokenSource?.Dispose();
        }
    }
}
