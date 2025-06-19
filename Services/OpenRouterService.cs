using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.CompilerServices;
using System.Net;

namespace _4U.chat.Services
{
    public class ModelConfiguration
    {
        public string ModelId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string UniqueId => $"{ModelId}|{DisplayName}";
        public string Description { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string ProviderIcon { get; set; } = string.Empty;
        public bool HasVisionSupport { get; set; } = false;
        public bool HasPdfSupport { get; set; } = false;
        public bool HasReasoningSupport { get; set; } = false;
        public bool IsImageGeneration { get; set; } = false;
        public bool IsFavorite { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public string InactiveReason { get; set; } = string.Empty;
    }

    public class OpenRouterService
    {
        private readonly HttpClient _httpClient;

        public OpenRouterService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
        }



        // ===== CENTRALIZED MODEL CONFIGURATION =====
        // This is where you can configure all available models.
        // Modify this list to add/remove models or change their properties.
        public static List<ModelConfiguration> ModelConfigurations = new()
        {

            new ModelConfiguration
            {
                ModelId = "google/gemini-2.5-flash-lite-preview-06-17",
                DisplayName = "Gemini 2.5 Flash Lite",
                Description = "Google's fastest model",
                Provider = "Google",
                ProviderIcon = "<svg class=\"size-4 text-color-heading\" viewBox=\"0 0 16 16\" xmlns=\"http://www.w3.org/2000/svg\" fill=\"currentColor\"><title>Gemini</title><path d=\"M16 8.016A8.522 8.522 0 008.016 16h-.032A8.521 8.521 0 000 8.016v-.032A8.521 8.521 0 007.984 0h.032A8.522 8.522 0 0016 7.984v.032z\"></path></svg>",
                HasVisionSupport = true,
                HasPdfSupport = true,
                HasReasoningSupport = false,
                IsImageGeneration = false,
                IsFavorite = true,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "google/gemini-2.5-flash-lite-preview-06-17",
                DisplayName = "Gemini 2.5 Flash Lite Thinking",
                Description = "Google's fastest model",
                Provider = "Google",
                ProviderIcon = "<svg class=\"size-4 text-color-heading\" viewBox=\"0 0 16 16\" xmlns=\"http://www.w3.org/2000/svg\" fill=\"currentColor\"><title>Gemini</title><path d=\"M16 8.016A8.522 8.522 0 008.016 16h-.032A8.521 8.521 0 000 8.016v-.032A8.521 8.521 0 007.984 0h.032A8.522 8.522 0 0016 7.984v.032z\"></path></svg>",
                HasVisionSupport = true,
                HasPdfSupport = true,
                HasReasoningSupport = true,
                IsImageGeneration = false,
                IsFavorite = true,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "google/gemini-2.5-flash",
                DisplayName = "Gemini 2.5 Flash",
                Description = "Google's fast multimodal model",
                Provider = "Google",
                ProviderIcon = "<svg class=\"size-4 text-color-heading\" viewBox=\"0 0 16 16\" xmlns=\"http://www.w3.org/2000/svg\" fill=\"currentColor\"><title>Gemini</title><path d=\"M16 8.016A8.522 8.522 0 008.016 16h-.032A8.521 8.521 0 000 8.016v-.032A8.521 8.521 0 007.984 0h.032A8.522 8.522 0 0016 7.984v.032z\"></path></svg>",
                HasVisionSupport = true,
                HasPdfSupport = true,
                HasReasoningSupport = false,
                IsImageGeneration = false,
                IsFavorite = true,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "google/gemini-2.5-flash",
                DisplayName = "Gemini 2.5 Flash Thinking",
                Description = "Google's fast multimodal model",
                Provider = "Google",
                ProviderIcon = "<svg class=\"size-4 text-color-heading\" viewBox=\"0 0 16 16\" xmlns=\"http://www.w3.org/2000/svg\" fill=\"currentColor\"><title>Gemini</title><path d=\"M16 8.016A8.522 8.522 0 008.016 16h-.032A8.521 8.521 0 000 8.016v-.032A8.521 8.521 0 007.984 0h.032A8.522 8.522 0 0016 7.984v.032z\"></path></svg>",
                HasVisionSupport = true,
                HasPdfSupport = true,
                HasReasoningSupport = true,
                IsImageGeneration = false,
                IsFavorite = true,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "google/gemini-2.5-pro",
                DisplayName = "Gemini 2.5 Pro",
                Description = "Google's most capable model",
                Provider = "Google",
                ProviderIcon = "<svg class=\"size-4 text-color-heading\" viewBox=\"0 0 16 16\" xmlns=\"http://www.w3.org/2000/svg\" fill=\"currentColor\"><title>Gemini</title><path d=\"M16 8.016A8.522 8.522 0 008.016 16h-.032A8.521 8.521 0 000 8.016v-.032A8.521 8.521 0 007.984 0h.032A8.522 8.522 0 0016 7.984v.032z\"></path></svg>",
                HasVisionSupport = true,
                HasPdfSupport = true,
                HasReasoningSupport = true,
                IsImageGeneration = false,
                IsFavorite = true,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "anthropic/claude-sonnet-4",
                DisplayName = "Claude 4 Sonnet",
                Description = "Anthropic's best model",
                Provider = "Anthropic",
                ProviderIcon = "<svg class=\"size-4 text-color-heading\" viewBox=\"0 0 46 32\" xmlns=\"http://www.w3.org/2000/svg\" fill=\"currentColor\"><title>Anthropic</title><path d=\"M32.73 0h-6.945L38.45 32h6.945L32.73 0ZM12.665 0 0 32h7.082l2.59-6.72h13.25l2.59 6.72h7.082L19.929 0h-7.264Zm-.702 19.337 4.334-11.246 4.334 11.246h-8.668Z\"></path></svg>",
                HasVisionSupport = true,
                HasPdfSupport = true,
                HasReasoningSupport = false,
                IsImageGeneration = false,
                IsFavorite = true,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "anthropic/claude-sonnet-4",
                DisplayName = "Claude 4 Sonnet Thinking",
                Description = "Anthropic's best model",
                Provider = "Anthropic",
                ProviderIcon = "<svg class=\"size-4 text-color-heading\" viewBox=\"0 0 46 32\" xmlns=\"http://www.w3.org/2000/svg\" fill=\"currentColor\"><title>Anthropic</title><path d=\"M32.73 0h-6.945L38.45 32h6.945L32.73 0ZM12.665 0 0 32h7.082l2.59-6.72h13.25l2.59 6.72h7.082L19.929 0h-7.264Zm-.702 19.337 4.334-11.246 4.334 11.246h-8.668Z\"></path></svg>",
                HasVisionSupport = true,
                HasPdfSupport = true,
                HasReasoningSupport = true,
                IsImageGeneration = false,
                IsFavorite = true,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "anthropic/claude-opus-4",
                DisplayName = "Claude 4 Opus",
                Description = "Anthropic's most intelligent model",
                Provider = "Anthropic",
                ProviderIcon = "<svg class=\"size-4 text-color-heading\" viewBox=\"0 0 46 32\" xmlns=\"http://www.w3.org/2000/svg\" fill=\"currentColor\"><title>Anthropic</title><path d=\"M32.73 0h-6.945L38.45 32h6.945L32.73 0ZM12.665 0 0 32h7.082l2.59-6.72h13.25l2.59 6.72h7.082L19.929 0h-7.264Zm-.702 19.337 4.334-11.246 4.334 11.246h-8.668Z\"></path></svg>",
                HasVisionSupport = true,
                HasPdfSupport = true,
                HasReasoningSupport = false,
                IsImageGeneration = false,
                IsFavorite = false,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "anthropic/claude-opus-4",
                DisplayName = "Claude 4 Opus Thinking",
                Description = "Anthropic's most intelligent model",
                Provider = "Anthropic",
                ProviderIcon = "<svg class=\"size-4 text-color-heading\" viewBox=\"0 0 46 32\" xmlns=\"http://www.w3.org/2000/svg\" fill=\"currentColor\"><title>Anthropic</title><path d=\"M32.73 0h-6.945L38.45 32h6.945L32.73 0ZM12.665 0 0 32h7.082l2.59-6.72h13.25l2.59 6.72h7.082L19.929 0h-7.264Zm-.702 19.337 4.334-11.246 4.334 11.246h-8.668Z\"></path></svg>",
                HasVisionSupport = true,
                HasPdfSupport = true,
                HasReasoningSupport = true,
                IsImageGeneration = false,
                IsFavorite = false,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "perplexity/sonar",
                DisplayName = "Sonar",
                Description = "Perplexity's lightweight model",
                Provider = "Perplexity",
                ProviderIcon = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 512 509.64\" fill=\"currentColor\" class=\"size-4 text-color-heading\"> <path class=\"s0\" d=\"m268 148.1l136.3-115.2v128.2h43.5v193.7h-52.3v121.5l-127.5-117v113.4h-23v-113.4l-130.4 116.5v-121h-50.6v-193.7h52.3v-129.1l128.7 114.7v-112.3h23zm113.3-65.7l-93.1 78.7h93.1zm-152.7 101.7c-47.2 0-94.4 0-141.6 0v147.7h27.6v-36.6zm55.9 0l111 111.2v36.5h29.4v-147.7c-46.8 0-93.6 0-140.4 0zm-57.9-23l-87.3-77.9v77.9zm18.4 167.5v-128.5l-107.5 104.7v119.8zm23.3-128.3v128.1l104.3 95.6c0-39.8 0-79.6 0-119.4z\"/> </svg>",
                HasVisionSupport = false,
                HasPdfSupport = false,
                HasReasoningSupport = false,
                IsImageGeneration = false,
                IsFavorite = false,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "perplexity/sonar-reasoning",
                DisplayName = "Sonar Reasoning",
                Description = "Perplexity's reasoning model",
                Provider = "Perplexity",
                ProviderIcon = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 512 509.64\" fill=\"currentColor\" class=\"size-4 text-color-heading\"> <path class=\"s0\" d=\"m268 148.1l136.3-115.2v128.2h43.5v193.7h-52.3v121.5l-127.5-117v113.4h-23v-113.4l-130.4 116.5v-121h-50.6v-193.7h52.3v-129.1l128.7 114.7v-112.3h23zm113.3-65.7l-93.1 78.7h93.1zm-152.7 101.7c-47.2 0-94.4 0-141.6 0v147.7h27.6v-36.6zm55.9 0l111 111.2v36.5h29.4v-147.7c-46.8 0-93.6 0-140.4 0zm-57.9-23l-87.3-77.9v77.9zm18.4 167.5v-128.5l-107.5 104.7v119.8zm23.3-128.3v128.1l104.3 95.6c0-39.8 0-79.6 0-119.4z\"/> </svg>",
                HasVisionSupport = false,
                HasPdfSupport = false,
                HasReasoningSupport = true,
                IsImageGeneration = false,
                IsFavorite = false,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "perplexity/sonar-pro",
                DisplayName = "Sonar Pro",
                Description = "Perplexity's flagship model",
                Provider = "Perplexity",
                ProviderIcon = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 512 509.64\" fill=\"currentColor\" class=\"size-4 text-color-heading\"> <path class=\"s0\" d=\"m268 148.1l136.3-115.2v128.2h43.5v193.7h-52.3v121.5l-127.5-117v113.4h-23v-113.4l-130.4 116.5v-121h-50.6v-193.7h52.3v-129.1l128.7 114.7v-112.3h23zm113.3-65.7l-93.1 78.7h93.1zm-152.7 101.7c-47.2 0-94.4 0-141.6 0v147.7h27.6v-36.6zm55.9 0l111 111.2v36.5h29.4v-147.7c-46.8 0-93.6 0-140.4 0zm-57.9-23l-87.3-77.9v77.9zm18.4 167.5v-128.5l-107.5 104.7v119.8zm23.3-128.3v128.1l104.3 95.6c0-39.8 0-79.6 0-119.4z\"/> </svg>",
                HasVisionSupport = false,
                HasPdfSupport = false,
                HasReasoningSupport = false,
                IsImageGeneration = false,
                IsFavorite = false,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "perplexity/sonar-reasoning-pro",
                DisplayName = "Sonar Reasoning Pro",
                Description = "Perplexity's flagship reasoning model",
                Provider = "Perplexity",
                ProviderIcon = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 512 509.64\" fill=\"currentColor\" class=\"size-4 text-color-heading\"> <path class=\"s0\" d=\"m268 148.1l136.3-115.2v128.2h43.5v193.7h-52.3v121.5l-127.5-117v113.4h-23v-113.4l-130.4 116.5v-121h-50.6v-193.7h52.3v-129.1l128.7 114.7v-112.3h23zm113.3-65.7l-93.1 78.7h93.1zm-152.7 101.7c-47.2 0-94.4 0-141.6 0v147.7h27.6v-36.6zm55.9 0l111 111.2v36.5h29.4v-147.7c-46.8 0-93.6 0-140.4 0zm-57.9-23l-87.3-77.9v77.9zm18.4 167.5v-128.5l-107.5 104.7v119.8zm23.3-128.3v128.1l104.3 95.6c0-39.8 0-79.6 0-119.4z\"/> </svg>",
                HasVisionSupport = false,
                HasPdfSupport = false,
                HasReasoningSupport = true,
                IsImageGeneration = false,
                IsFavorite = false,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "perplexity/sonar-deep-research",
                DisplayName = "Sonar Deep Research",
                Description = "Perplexity's model for Deep Research",
                Provider = "Perplexity",
                ProviderIcon = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 512 509.64\" fill=\"currentColor\" class=\"size-4 text-color-heading\"> <path class=\"s0\" d=\"m268 148.1l136.3-115.2v128.2h43.5v193.7h-52.3v121.5l-127.5-117v113.4h-23v-113.4l-130.4 116.5v-121h-50.6v-193.7h52.3v-129.1l128.7 114.7v-112.3h23zm113.3-65.7l-93.1 78.7h93.1zm-152.7 101.7c-47.2 0-94.4 0-141.6 0v147.7h27.6v-36.6zm55.9 0l111 111.2v36.5h29.4v-147.7c-46.8 0-93.6 0-140.4 0zm-57.9-23l-87.3-77.9v77.9zm18.4 167.5v-128.5l-107.5 104.7v119.8zm23.3-128.3v128.1l104.3 95.6c0-39.8 0-79.6 0-119.4z\"/> </svg>",
                HasVisionSupport = false,
                HasPdfSupport = false,
                HasReasoningSupport = true,
                IsImageGeneration = false,
                IsFavorite = true,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "openai/gpt-image-gen",
                DisplayName = "GPT ImageGen",
                Description = "AI-powered image generation",
                Provider = "OpenAI",
                ProviderIcon = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"118 120 480 480\" fill=\"currentColor\" class=\"size-4 text-color-heading\"><path d=\"M304.246 295.411V249.828C304.246 245.989 305.687 243.109 309.044 241.191L400.692 188.412C413.167 181.215 428.042 177.858 443.394 177.858C500.971 177.858 537.44 222.482 537.44 269.982C537.44 273.34 537.44 277.179 536.959 281.018L441.954 225.358C436.197 222 430.437 222 424.68 225.358L304.246 295.411ZM518.245 472.945V364.024C518.245 357.304 515.364 352.507 509.608 349.149L389.174 279.096L428.519 256.543C431.877 254.626 434.757 254.626 438.115 256.543L529.762 309.323C556.154 324.679 573.905 357.304 573.905 388.971C573.905 425.436 552.315 459.024 518.245 472.941V472.945ZM275.937 376.982L236.592 353.952C233.235 352.034 231.794 349.154 231.794 345.315V239.756C231.794 188.416 271.139 149.548 324.4 149.548C344.555 149.548 363.264 156.268 379.102 168.262L284.578 222.964C278.822 226.321 275.942 231.119 275.942 237.838V376.986L275.937 376.982ZM360.626 425.922L304.246 394.255V327.083L360.626 295.416L417.002 327.083V394.255L360.626 425.922ZM396.852 571.789C376.698 571.789 357.989 565.07 342.151 553.075L436.674 498.374C442.431 495.017 445.311 490.219 445.311 483.499V344.352L485.138 367.382C488.495 369.299 489.936 372.179 489.936 376.018V481.577C489.936 532.917 450.109 571.785 396.852 571.785V571.789ZM283.134 464.79L191.486 412.01C165.094 396.654 147.343 364.029 147.343 332.362C147.343 295.416 169.415 262.309 203.48 248.393V357.791C203.48 364.51 206.361 369.308 212.117 372.665L332.074 442.237L292.729 464.79C289.372 466.707 286.491 466.707 283.134 464.79ZM277.859 543.48C223.639 543.48 183.813 502.695 183.813 452.314C183.813 448.475 184.294 444.636 184.771 440.797L279.295 495.498C285.051 498.856 290.812 498.856 296.568 495.498L417.002 425.927V471.509C417.002 475.349 415.562 478.229 412.204 480.146L320.557 532.926C308.081 540.122 293.206 543.48 277.854 543.48H277.859ZM396.852 600.576C454.911 600.576 503.37 559.313 514.41 504.612C568.149 490.696 602.696 440.315 602.696 388.976C602.696 355.387 588.303 322.762 562.392 299.25C564.791 289.173 566.231 279.096 566.231 269.024C566.231 200.411 510.571 149.067 446.274 149.067C433.322 149.067 420.846 150.984 408.37 155.305C386.775 134.192 357.026 120.758 324.4 120.758C266.342 120.758 217.883 162.02 206.843 216.721C153.104 230.637 118.557 281.018 118.557 332.357C118.557 365.946 132.95 398.571 158.861 422.083C156.462 432.16 155.022 442.237 155.022 452.309C155.022 520.922 210.682 572.266 274.978 572.266C287.931 572.266 300.407 570.349 312.883 566.028C334.473 587.141 364.222 600.576 396.852 600.576Z\"></path></svg>",
                HasVisionSupport = false,
                HasPdfSupport = false,
                HasReasoningSupport = false,
                IsImageGeneration = true,
                IsFavorite = true,
                IsActive = false,
                InactiveReason = "Can be implemented in future"
            },
            new ModelConfiguration
            {
                ModelId = "openai/o4-mini-high",
                DisplayName = "o4 Mini",
                Description = "OpenAI's cost-effective reasoning model",
                Provider = "OpenAI",
                ProviderIcon = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"118 120 480 480\" fill=\"currentColor\" class=\"size-4 text-color-heading\"><path d=\"M304.246 295.411V249.828C304.246 245.989 305.687 243.109 309.044 241.191L400.692 188.412C413.167 181.215 428.042 177.858 443.394 177.858C500.971 177.858 537.44 222.482 537.44 269.982C537.44 273.34 537.44 277.179 536.959 281.018L441.954 225.358C436.197 222 430.437 222 424.68 225.358L304.246 295.411ZM518.245 472.945V364.024C518.245 357.304 515.364 352.507 509.608 349.149L389.174 279.096L428.519 256.543C431.877 254.626 434.757 254.626 438.115 256.543L529.762 309.323C556.154 324.679 573.905 357.304 573.905 388.971C573.905 425.436 552.315 459.024 518.245 472.941V472.945ZM275.937 376.982L236.592 353.952C233.235 352.034 231.794 349.154 231.794 345.315V239.756C231.794 188.416 271.139 149.548 324.4 149.548C344.555 149.548 363.264 156.268 379.102 168.262L284.578 222.964C278.822 226.321 275.942 231.119 275.942 237.838V376.986L275.937 376.982ZM360.626 425.922L304.246 394.255V327.083L360.626 295.416L417.002 327.083V394.255L360.626 425.922ZM396.852 571.789C376.698 571.789 357.989 565.07 342.151 553.075L436.674 498.374C442.431 495.017 445.311 490.219 445.311 483.499V344.352L485.138 367.382C488.495 369.299 489.936 372.179 489.936 376.018V481.577C489.936 532.917 450.109 571.785 396.852 571.785V571.789ZM283.134 464.79L191.486 412.01C165.094 396.654 147.343 364.029 147.343 332.362C147.343 295.416 169.415 262.309 203.48 248.393V357.791C203.48 364.51 206.361 369.308 212.117 372.665L332.074 442.237L292.729 464.79C289.372 466.707 286.491 466.707 283.134 464.79ZM277.859 543.48C223.639 543.48 183.813 502.695 183.813 452.314C183.813 448.475 184.294 444.636 184.771 440.797L279.295 495.498C285.051 498.856 290.812 498.856 296.568 495.498L417.002 425.927V471.509C417.002 475.349 415.562 478.229 412.204 480.146L320.557 532.926C308.081 540.122 293.206 543.48 277.854 543.48H277.859ZM396.852 600.576C454.911 600.576 503.37 559.313 514.41 504.612C568.149 490.696 602.696 440.315 602.696 388.976C602.696 355.387 588.303 322.762 562.392 299.25C564.791 289.173 566.231 279.096 566.231 269.024C566.231 200.411 510.571 149.067 446.274 149.067C433.322 149.067 420.846 150.984 408.37 155.305C386.775 134.192 357.026 120.758 324.4 120.758C266.342 120.758 217.883 162.02 206.843 216.721C153.104 230.637 118.557 281.018 118.557 332.357C118.557 365.946 132.95 398.571 158.861 422.083C156.462 432.16 155.022 442.237 155.022 452.309C155.022 520.922 210.682 572.266 274.978 572.266C287.931 572.266 300.407 570.349 312.883 566.028C334.473 587.141 364.222 600.576 396.852 600.576Z\"></path></svg>",
                HasVisionSupport = true,
                HasPdfSupport = false,
                HasReasoningSupport = true,
                IsImageGeneration = false,
                IsFavorite = true,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "openai/gpt-4o",
                DisplayName = "GPT-4o",
                Description = "OpenAI's model for simple tasks",
                Provider = "OpenAI",
                ProviderIcon = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"118 120 480 480\" fill=\"currentColor\" class=\"size-4 text-color-heading\"><path d=\"M304.246 295.411V249.828C304.246 245.989 305.687 243.109 309.044 241.191L400.692 188.412C413.167 181.215 428.042 177.858 443.394 177.858C500.971 177.858 537.44 222.482 537.44 269.982C537.44 273.34 537.44 277.179 536.959 281.018L441.954 225.358C436.197 222 430.437 222 424.68 225.358L304.246 295.411ZM518.245 472.945V364.024C518.245 357.304 515.364 352.507 509.608 349.149L389.174 279.096L428.519 256.543C431.877 254.626 434.757 254.626 438.115 256.543L529.762 309.323C556.154 324.679 573.905 357.304 573.905 388.971C573.905 425.436 552.315 459.024 518.245 472.941V472.945ZM275.937 376.982L236.592 353.952C233.235 352.034 231.794 349.154 231.794 345.315V239.756C231.794 188.416 271.139 149.548 324.4 149.548C344.555 149.548 363.264 156.268 379.102 168.262L284.578 222.964C278.822 226.321 275.942 231.119 275.942 237.838V376.986L275.937 376.982ZM360.626 425.922L304.246 394.255V327.083L360.626 295.416L417.002 327.083V394.255L360.626 425.922ZM396.852 571.789C376.698 571.789 357.989 565.07 342.151 553.075L436.674 498.374C442.431 495.017 445.311 490.219 445.311 483.499V344.352L485.138 367.382C488.495 369.299 489.936 372.179 489.936 376.018V481.577C489.936 532.917 450.109 571.785 396.852 571.785V571.789ZM283.134 464.79L191.486 412.01C165.094 396.654 147.343 364.029 147.343 332.362C147.343 295.416 169.415 262.309 203.48 248.393V357.791C203.48 364.51 206.361 369.308 212.117 372.665L332.074 442.237L292.729 464.79C289.372 466.707 286.491 466.707 283.134 464.79ZM277.859 543.48C223.639 543.48 183.813 502.695 183.813 452.314C183.813 448.475 184.294 444.636 184.771 440.797L279.295 495.498C285.051 498.856 290.812 498.856 296.568 495.498L417.002 425.927V471.509C417.002 475.349 415.562 478.229 412.204 480.146L320.557 532.926C308.081 540.122 293.206 543.48 277.854 543.48H277.859ZM396.852 600.576C454.911 600.576 503.37 559.313 514.41 504.612C568.149 490.696 602.696 440.315 602.696 388.976C602.696 355.387 588.303 322.762 562.392 299.25C564.791 289.173 566.231 279.096 566.231 269.024C566.231 200.411 510.571 149.067 446.274 149.067C433.322 149.067 420.846 150.984 408.37 155.305C386.775 134.192 357.026 120.758 324.4 120.758C266.342 120.758 217.883 162.02 206.843 216.721C153.104 230.637 118.557 281.018 118.557 332.357C118.557 365.946 132.95 398.571 158.861 422.083C156.462 432.16 155.022 442.237 155.022 452.309C155.022 520.922 210.682 572.266 274.978 572.266C287.931 572.266 300.407 570.349 312.883 566.028C334.473 587.141 364.222 600.576 396.852 600.576Z\"></path></svg>",
                HasVisionSupport = true,
                HasPdfSupport = false,
                HasReasoningSupport = false,
                IsImageGeneration = false,
                IsFavorite = false,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "openai/gpt-4o-mini",
                DisplayName = "GPT-4o mini",
                Description = "OpenAI's efficient model",
                Provider = "OpenAI",
                ProviderIcon = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"118 120 480 480\" fill=\"currentColor\" class=\"size-4 text-color-heading\"><path d=\"M304.246 295.411V249.828C304.246 245.989 305.687 243.109 309.044 241.191L400.692 188.412C413.167 181.215 428.042 177.858 443.394 177.858C500.971 177.858 537.44 222.482 537.44 269.982C537.44 273.34 537.44 277.179 536.959 281.018L441.954 225.358C436.197 222 430.437 222 424.68 225.358L304.246 295.411ZM518.245 472.945V364.024C518.245 357.304 515.364 352.507 509.608 349.149L389.174 279.096L428.519 256.543C431.877 254.626 434.757 254.626 438.115 256.543L529.762 309.323C556.154 324.679 573.905 357.304 573.905 388.971C573.905 425.436 552.315 459.024 518.245 472.941V472.945ZM275.937 376.982L236.592 353.952C233.235 352.034 231.794 349.154 231.794 345.315V239.756C231.794 188.416 271.139 149.548 324.4 149.548C344.555 149.548 363.264 156.268 379.102 168.262L284.578 222.964C278.822 226.321 275.942 231.119 275.942 237.838V376.986L275.937 376.982ZM360.626 425.922L304.246 394.255V327.083L360.626 295.416L417.002 327.083V394.255L360.626 425.922ZM396.852 571.789C376.698 571.789 357.989 565.07 342.151 553.075L436.674 498.374C442.431 495.017 445.311 490.219 445.311 483.499V344.352L485.138 367.382C488.495 369.299 489.936 372.179 489.936 376.018V481.577C489.936 532.917 450.109 571.785 396.852 571.785V571.789ZM283.134 464.79L191.486 412.01C165.094 396.654 147.343 364.029 147.343 332.362C147.343 295.416 169.415 262.309 203.48 248.393V357.791C203.48 364.51 206.361 369.308 212.117 372.665L332.074 442.237L292.729 464.79C289.372 466.707 286.491 466.707 283.134 464.79ZM277.859 543.48C223.639 543.48 183.813 502.695 183.813 452.314C183.813 448.475 184.294 444.636 184.771 440.797L279.295 495.498C285.051 498.856 290.812 498.856 296.568 495.498L417.002 425.927V471.509C417.002 475.349 415.562 478.229 412.204 480.146L320.557 532.926C308.081 540.122 293.206 543.48 277.854 543.48H277.859ZM396.852 600.576C454.911 600.576 503.37 559.313 514.41 504.612C568.149 490.696 602.696 440.315 602.696 388.976C602.696 355.387 588.303 322.762 562.392 299.25C564.791 289.173 566.231 279.096 566.231 269.024C566.231 200.411 510.571 149.067 446.274 149.067C433.322 149.067 420.846 150.984 408.37 155.305C386.775 134.192 357.026 120.758 324.4 120.758C266.342 120.758 217.883 162.02 206.843 216.721C153.104 230.637 118.557 281.018 118.557 332.357C118.557 365.946 132.95 398.571 158.861 422.083C156.462 432.16 155.022 442.237 155.022 452.309C155.022 520.922 210.682 572.266 274.978 572.266C287.931 572.266 300.407 570.349 312.883 566.028C334.473 587.141 364.222 600.576 396.852 600.576Z\"></path></svg>",
                HasVisionSupport = true,
                HasPdfSupport = false,
                HasReasoningSupport = false,
                IsImageGeneration = false,
                IsFavorite = false,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "openai/gpt-4.1",
                DisplayName = "GPT-4.1",
                Description = "OpenAI's flagship model",
                Provider = "OpenAI",
                ProviderIcon = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"118 120 480 480\" fill=\"currentColor\" class=\"size-4 text-color-heading\"><path d=\"M304.246 295.411V249.828C304.246 245.989 305.687 243.109 309.044 241.191L400.692 188.412C413.167 181.215 428.042 177.858 443.394 177.858C500.971 177.858 537.44 222.482 537.44 269.982C537.44 273.34 537.44 277.179 536.959 281.018L441.954 225.358C436.197 222 430.437 222 424.68 225.358L304.246 295.411ZM518.245 472.945V364.024C518.245 357.304 515.364 352.507 509.608 349.149L389.174 279.096L428.519 256.543C431.877 254.626 434.757 254.626 438.115 256.543L529.762 309.323C556.154 324.679 573.905 357.304 573.905 388.971C573.905 425.436 552.315 459.024 518.245 472.941V472.945ZM275.937 376.982L236.592 353.952C233.235 352.034 231.794 349.154 231.794 345.315V239.756C231.794 188.416 271.139 149.548 324.4 149.548C344.555 149.548 363.264 156.268 379.102 168.262L284.578 222.964C278.822 226.321 275.942 231.119 275.942 237.838V376.986L275.937 376.982ZM360.626 425.922L304.246 394.255V327.083L360.626 295.416L417.002 327.083V394.255L360.626 425.922ZM396.852 571.789C376.698 571.789 357.989 565.07 342.151 553.075L436.674 498.374C442.431 495.017 445.311 490.219 445.311 483.499V344.352L485.138 367.382C488.495 369.299 489.936 372.179 489.936 376.018V481.577C489.936 532.917 450.109 571.785 396.852 571.785V571.789ZM283.134 464.79L191.486 412.01C165.094 396.654 147.343 364.029 147.343 332.362C147.343 295.416 169.415 262.309 203.48 248.393V357.791C203.48 364.51 206.361 369.308 212.117 372.665L332.074 442.237L292.729 464.79C289.372 466.707 286.491 466.707 283.134 464.79ZM277.859 543.48C223.639 543.48 183.813 502.695 183.813 452.314C183.813 448.475 184.294 444.636 184.771 440.797L279.295 495.498C285.051 498.856 290.812 498.856 296.568 495.498L417.002 425.927V471.509C417.002 475.349 415.562 478.229 412.204 480.146L320.557 532.926C308.081 540.122 293.206 543.48 277.854 543.48H277.859ZM396.852 600.576C454.911 600.576 503.37 559.313 514.41 504.612C568.149 490.696 602.696 440.315 602.696 388.976C602.696 355.387 588.303 322.762 562.392 299.25C564.791 289.173 566.231 279.096 566.231 269.024C566.231 200.411 510.571 149.067 446.274 149.067C433.322 149.067 420.846 150.984 408.37 155.305C386.775 134.192 357.026 120.758 324.4 120.758C266.342 120.758 217.883 162.02 206.843 216.721C153.104 230.637 118.557 281.018 118.557 332.357C118.557 365.946 132.95 398.571 158.861 422.083C156.462 432.16 155.022 442.237 155.022 452.309C155.022 520.922 210.682 572.266 274.978 572.266C287.931 572.266 300.407 570.349 312.883 566.028C334.473 587.141 364.222 600.576 396.852 600.576Z\"></path></svg>",
                HasVisionSupport = true,
                HasPdfSupport = false,
                HasReasoningSupport = false,
                IsImageGeneration = false,
                IsFavorite = false,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "openai/gpt-4.1-mini",
                DisplayName = "GPT-4.1 mini",
                Description = "OpenAI's small model",
                Provider = "OpenAI",
                ProviderIcon = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"118 120 480 480\" fill=\"currentColor\" class=\"size-4 text-color-heading\"><path d=\"M304.246 295.411V249.828C304.246 245.989 305.687 243.109 309.044 241.191L400.692 188.412C413.167 181.215 428.042 177.858 443.394 177.858C500.971 177.858 537.44 222.482 537.44 269.982C537.44 273.34 537.44 277.179 536.959 281.018L441.954 225.358C436.197 222 430.437 222 424.68 225.358L304.246 295.411ZM518.245 472.945V364.024C518.245 357.304 515.364 352.507 509.608 349.149L389.174 279.096L428.519 256.543C431.877 254.626 434.757 254.626 438.115 256.543L529.762 309.323C556.154 324.679 573.905 357.304 573.905 388.971C573.905 425.436 552.315 459.024 518.245 472.941V472.945ZM275.937 376.982L236.592 353.952C233.235 352.034 231.794 349.154 231.794 345.315V239.756C231.794 188.416 271.139 149.548 324.4 149.548C344.555 149.548 363.264 156.268 379.102 168.262L284.578 222.964C278.822 226.321 275.942 231.119 275.942 237.838V376.986L275.937 376.982ZM360.626 425.922L304.246 394.255V327.083L360.626 295.416L417.002 327.083V394.255L360.626 425.922ZM396.852 571.789C376.698 571.789 357.989 565.07 342.151 553.075L436.674 498.374C442.431 495.017 445.311 490.219 445.311 483.499V344.352L485.138 367.382C488.495 369.299 489.936 372.179 489.936 376.018V481.577C489.936 532.917 450.109 571.785 396.852 571.785V571.789ZM283.134 464.79L191.486 412.01C165.094 396.654 147.343 364.029 147.343 332.362C147.343 295.416 169.415 262.309 203.48 248.393V357.791C203.48 364.51 206.361 369.308 212.117 372.665L332.074 442.237L292.729 464.79C289.372 466.707 286.491 466.707 283.134 464.79ZM277.859 543.48C223.639 543.48 183.813 502.695 183.813 452.314C183.813 448.475 184.294 444.636 184.771 440.797L279.295 495.498C285.051 498.856 290.812 498.856 296.568 495.498L417.002 425.927V471.509C417.002 475.349 415.562 478.229 412.204 480.146L320.557 532.926C308.081 540.122 293.206 543.48 277.854 543.48H277.859ZM396.852 600.576C454.911 600.576 503.37 559.313 514.41 504.612C568.149 490.696 602.696 440.315 602.696 388.976C602.696 355.387 588.303 322.762 562.392 299.25C564.791 289.173 566.231 279.096 566.231 269.024C566.231 200.411 510.571 149.067 446.274 149.067C433.322 149.067 420.846 150.984 408.37 155.305C386.775 134.192 357.026 120.758 324.4 120.758C266.342 120.758 217.883 162.02 206.843 216.721C153.104 230.637 118.557 281.018 118.557 332.357C118.557 365.946 132.95 398.571 158.861 422.083C156.462 432.16 155.022 442.237 155.022 452.309C155.022 520.922 210.682 572.266 274.978 572.266C287.931 572.266 300.407 570.349 312.883 566.028C334.473 587.141 364.222 600.576 396.852 600.576Z\"></path></svg>",
                HasVisionSupport = true,
                HasPdfSupport = false,
                HasReasoningSupport = false,
                IsImageGeneration = false,
                IsFavorite = false,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "openai/gpt-4.1-nano",
                DisplayName = "GPT-4.1 nano",
                Description = "OpenAI's fastest model",
                Provider = "OpenAI",
                ProviderIcon = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"118 120 480 480\" fill=\"currentColor\" class=\"size-4 text-color-heading\"><path d=\"M304.246 295.411V249.828C304.246 245.989 305.687 243.109 309.044 241.191L400.692 188.412C413.167 181.215 428.042 177.858 443.394 177.858C500.971 177.858 537.44 222.482 537.44 269.982C537.44 273.34 537.44 277.179 536.959 281.018L441.954 225.358C436.197 222 430.437 222 424.68 225.358L304.246 295.411ZM518.245 472.945V364.024C518.245 357.304 515.364 352.507 509.608 349.149L389.174 279.096L428.519 256.543C431.877 254.626 434.757 254.626 438.115 256.543L529.762 309.323C556.154 324.679 573.905 357.304 573.905 388.971C573.905 425.436 552.315 459.024 518.245 472.941V472.945ZM275.937 376.982L236.592 353.952C233.235 352.034 231.794 349.154 231.794 345.315V239.756C231.794 188.416 271.139 149.548 324.4 149.548C344.555 149.548 363.264 156.268 379.102 168.262L284.578 222.964C278.822 226.321 275.942 231.119 275.942 237.838V376.986L275.937 376.982ZM360.626 425.922L304.246 394.255V327.083L360.626 295.416L417.002 327.083V394.255L360.626 425.922ZM396.852 571.789C376.698 571.789 357.989 565.07 342.151 553.075L436.674 498.374C442.431 495.017 445.311 490.219 445.311 483.499V344.352L485.138 367.382C488.495 369.299 489.936 372.179 489.936 376.018V481.577C489.936 532.917 450.109 571.785 396.852 571.785V571.789ZM283.134 464.79L191.486 412.01C165.094 396.654 147.343 364.029 147.343 332.362C147.343 295.416 169.415 262.309 203.48 248.393V357.791C203.48 364.51 206.361 369.308 212.117 372.665L332.074 442.237L292.729 464.79C289.372 466.707 286.491 466.707 283.134 464.79ZM277.859 543.48C223.639 543.48 183.813 502.695 183.813 452.314C183.813 448.475 184.294 444.636 184.771 440.797L279.295 495.498C285.051 498.856 290.812 498.856 296.568 495.498L417.002 425.927V471.509C417.002 475.349 415.562 478.229 412.204 480.146L320.557 532.926C308.081 540.122 293.206 543.48 277.854 543.48H277.859ZM396.852 600.576C454.911 600.576 503.37 559.313 514.41 504.612C568.149 490.696 602.696 440.315 602.696 388.976C602.696 355.387 588.303 322.762 562.392 299.25C564.791 289.173 566.231 279.096 566.231 269.024C566.231 200.411 510.571 149.067 446.274 149.067C433.322 149.067 420.846 150.984 408.37 155.305C386.775 134.192 357.026 120.758 324.4 120.758C266.342 120.758 217.883 162.02 206.843 216.721C153.104 230.637 118.557 281.018 118.557 332.357C118.557 365.946 132.95 398.571 158.861 422.083C156.462 432.16 155.022 442.237 155.022 452.309C155.022 520.922 210.682 572.266 274.978 572.266C287.931 572.266 300.407 570.349 312.883 566.028C334.473 587.141 364.222 600.576 396.852 600.576Z\"></path></svg>",
                HasVisionSupport = true,
                HasPdfSupport = false,
                HasReasoningSupport = false,
                IsImageGeneration = false,
                IsFavorite = false,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "deepseek/deepseek-r1-0528",
                DisplayName = "DeepSeek R1 0528",
                Description = "DeepSeek's latest reasoning model",
                Provider = "DeepSeek",
                ProviderIcon = "<svg class=\"size-4 text-color-heading\" viewBox=\"0 0 24 24\" xmlns=\"http://www.w3.org/2000/svg\" fill=\"currentColor\"><title>DeepSeek</title><path d=\"M23.748 4.482c-.254-.124-.364.113-.512.234-.051.039-.094.09-.137.136-.372.397-.806.657-1.373.626-.829-.046-1.537.214-2.163.848-.133-.782-.575-1.248-1.247-1.548-.352-.156-.708-.311-.955-.65-.172-.241-.219-.51-.305-.774-.055-.16-.11-.323-.293-.35-.2-.031-.278.136-.356.276-.313.572-.434 1.202-.422 1.84.027 1.436.633 2.58 1.838 3.393.137.093.172.187.129.323-.082.28-.18.552-.266.833-.055.179-.137.217-.329.14a5.526 5.526 0 01-1.736-1.18c-.857-.828-1.631-1.742-2.597-2.458a11.365 11.365 0 00-.689-.471c-.985-.957.13-1.743.388-1.836.27-.098.093-.432-.779-.428-.872.004-1.67.295-2.687.684a3.055 3.055 0 01-.465.137 9.597 9.597 0 00-2.883-.102c-1.885.21-3.39 1.102-4.497 2.623C.082 8.606-.231 10.684.152 12.85c.403 2.284 1.569 4.175 3.36 5.653 1.858 1.533 3.997 2.284 6.438 2.14 1.482-.085 3.133-.284 4.994-1.86.47.234.962.327 1.78.397.63.059 1.236-.03 1.705-.128.735-.156.684-.837.419-.961-2.155-1.004-1.682-.595-2.113-.926 1.096-1.296 2.746-2.642 3.392-7.003.05-.347.007-.565 0-.845-.004-.17.035-.237.23-.256a4.173 4.173 0 001.545-.475c1.396-.763 1.96-2.015 2.093-3.517.02-.23-.004-.467-.247-.588zM11.581 18c-2.089-1.642-3.102-2.183-3.52-2.16-.392.024-.321.471-.235.763.09.288.207.486.371.739.114.167.192.416-.113.603-.673.416-1.842-.14-1.897-.167-1.361-.802-2.5-1.86-3.301-3.307-.774-1.393-1.224-2.887-1.298-4.482-.02-.386.093-.522.477-.592a4.696 4.696 0 011.529-.039c2.132.312 3.946 1.265 5.468 2.774.868.86 1.525 1.887 2.202 2.891.72 1.066 1.494 2.082 2.48 2.914.348.292.625.514.891.677-.802.09-2.14.11-3.054-.614zm1-6.44a.306.306 0 01.415-.287.302.302 0 01.2.288.306.306 0 01-.31.307.303.303 0 01-.304-.308zm3.11 1.596c-.2.081-.399.151-.59.16a1.245 1.245 0 01-.798-.254c-.274-.23-.47-.358-.552-.758a1.73 1.73 0 01.016-.588c.07-.327-.008-.537-.239-.727-.187-.156-.426-.199-.688-.199a.559.559 0 01-.254-.078c-.11-.054-.2-.19-.114-.358.028-.054.16-.186.192-.21.356-.202.767-.136 1.146.016.352.144.618.408 1.001.782.391.451.462.576.685.914.176.265.336.537.445.848.067.195-.019.354-.25.452z\"></path></svg>",
                HasVisionSupport = false,
                HasPdfSupport = false,
                HasReasoningSupport = true,
                IsImageGeneration = false,
                IsFavorite = false,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "deepseek/deepseek-r1-distill-llama-70b",
                DisplayName = "DeepSeek R1 (Llama distilled)",
                Description = "DeepSeek's distilled reasoning model",
                Provider = "DeepSeek",
                ProviderIcon = "<svg class=\"size-4 text-color-heading\" viewBox=\"0 0 24 24\" xmlns=\"http://www.w3.org/2000/svg\" fill=\"currentColor\"><title>DeepSeek</title><path d=\"M23.748 4.482c-.254-.124-.364.113-.512.234-.051.039-.094.09-.137.136-.372.397-.806.657-1.373.626-.829-.046-1.537.214-2.163.848-.133-.782-.575-1.248-1.247-1.548-.352-.156-.708-.311-.955-.65-.172-.241-.219-.51-.305-.774-.055-.16-.11-.323-.293-.35-.2-.031-.278.136-.356.276-.313.572-.434 1.202-.422 1.84.027 1.436.633 2.58 1.838 3.393.137.093.172.187.129.323-.082.28-.18.552-.266.833-.055.179-.137.217-.329.14a5.526 5.526 0 01-1.736-1.18c-.857-.828-1.631-1.742-2.597-2.458a11.365 11.365 0 00-.689-.471c-.985-.957.13-1.743.388-1.836.27-.098.093-.432-.779-.428-.872.004-1.67.295-2.687.684a3.055 3.055 0 01-.465.137 9.597 9.597 0 00-2.883-.102c-1.885.21-3.39 1.102-4.497 2.623C.082 8.606-.231 10.684.152 12.85c.403 2.284 1.569 4.175 3.36 5.653 1.858 1.533 3.997 2.284 6.438 2.14 1.482-.085 3.133-.284 4.994-1.86.47.234.962.327 1.78.397.63.059 1.236-.03 1.705-.128.735-.156.684-.837.419-.961-2.155-1.004-1.682-.595-2.113-.926 1.096-1.296 2.746-2.642 3.392-7.003.05-.347.007-.565 0-.845-.004-.17.035-.237.23-.256a4.173 4.173 0 001.545-.475c1.396-.763 1.96-2.015 2.093-3.517.02-.23-.004-.467-.247-.588zM11.581 18c-2.089-1.642-3.102-2.183-3.52-2.16-.392.024-.321.471-.235.763.09.288.207.486.371.739.114.167.192.416-.113.603-.673.416-1.842-.14-1.897-.167-1.361-.802-2.5-1.86-3.301-3.307-.774-1.393-1.224-2.887-1.298-4.482-.02-.386.093-.522.477-.592a4.696 4.696 0 011.529-.039c2.132.312 3.946 1.265 5.468 2.774.868.86 1.525 1.887 2.202 2.891.72 1.066 1.494 2.082 2.48 2.914.348.292.625.514.891.677-.802.09-2.14.11-3.054-.614zm1-6.44a.306.306 0 01.415-.287.302.302 0 01.2.288.306.306 0 01-.31.307.303.303 0 01-.304-.308zm3.11 1.596c-.2.081-.399.151-.59.16a1.245 1.245 0 01-.798-.254c-.274-.23-.47-.358-.552-.758a1.73 1.73 0 01.016-.588c.07-.327-.008-.537-.239-.727-.187-.156-.426-.199-.688-.199a.559.559 0 01-.254-.078c-.11-.054-.2-.19-.114-.358.028-.054.16-.186.192-.21.356-.202.767-.136 1.146.016.352.144.618.408 1.001.782.391.451.462.576.685.914.176.265.336.537.445.848.067.195-.019.354-.25.452z\"></path></svg>",
                HasVisionSupport = false,
                HasPdfSupport = false,
                HasReasoningSupport = true,
                IsImageGeneration = false,
                IsFavorite = true,
                IsActive = true
            },
            new ModelConfiguration
            {
                ModelId = "deepseek/deepseek-chat-v3-0324",
                DisplayName = " ",
                Description = "DeepSeek's latest model",
                Provider = "DeepSeek",
                ProviderIcon = "<svg class=\"size-4 text-color-heading\" viewBox=\"0 0 24 24\" xmlns=\"http://www.w3.org/2000/svg\" fill=\"currentColor\"><title>DeepSeek</title><path d=\"M23.748 4.482c-.254-.124-.364.113-.512.234-.051.039-.094.09-.137.136-.372.397-.806.657-1.373.626-.829-.046-1.537.214-2.163.848-.133-.782-.575-1.248-1.247-1.548-.352-.156-.708-.311-.955-.65-.172-.241-.219-.51-.305-.774-.055-.16-.11-.323-.293-.35-.2-.031-.278.136-.356.276-.313.572-.434 1.202-.422 1.84.027 1.436.633 2.58 1.838 3.393.137.093.172.187.129.323-.082.28-.18.552-.266.833-.055.179-.137.217-.329.14a5.526 5.526 0 01-1.736-1.18c-.857-.828-1.631-1.742-2.597-2.458a11.365 11.365 0 00-.689-.471c-.985-.957.13-1.743.388-1.836.27-.098.093-.432-.779-.428-.872.004-1.67.295-2.687.684a3.055 3.055 0 01-.465.137 9.597 9.597 0 00-2.883-.102c-1.885.21-3.39 1.102-4.497 2.623C.082 8.606-.231 10.684.152 12.85c.403 2.284 1.569 4.175 3.36 5.653 1.858 1.533 3.997 2.284 6.438 2.14 1.482-.085 3.133-.284 4.994-1.86.47.234.962.327 1.78.397.63.059 1.236-.03 1.705-.128.735-.156.684-.837.419-.961-2.155-1.004-1.682-.595-2.113-.926 1.096-1.296 2.746-2.642 3.392-7.003.05-.347.007-.565 0-.845-.004-.17.035-.237.23-.256a4.173 4.173 0 001.545-.475c1.396-.763 1.96-2.015 2.093-3.517.02-.23-.004-.467-.247-.588zM11.581 18c-2.089-1.642-3.102-2.183-3.52-2.16-.392.024-.321.471-.235.763.09.288.207.486.371.739.114.167.192.416-.113.603-.673.416-1.842-.14-1.897-.167-1.361-.802-2.5-1.86-3.301-3.307-.774-1.393-1.224-2.887-1.298-4.482-.02-.386.093-.522.477-.592a4.696 4.696 0 011.529-.039c2.132.312 3.946 1.265 5.468 2.774.868.86 1.525 1.887 2.202 2.891.72 1.066 1.494 2.082 2.48 2.914.348.292.625.514.891.677-.802.09-2.14.11-3.054-.614zm1-6.44a.306.306 0 01.415-.287.302.302 0 01.2.288.306.306 0 01-.31.307.303.303 0 01-.304-.308zm3.11 1.596c-.2.081-.399.151-.59.16a1.245 1.245 0 01-.798-.254c-.274-.23-.47-.358-.552-.758a1.73 1.73 0 01.016-.588c.07-.327-.008-.537-.239-.727-.187-.156-.426-.199-.688-.199a.559.559 0 01-.254-.078c-.11-.054-.2-.19-.114-.358.028-.054.16-.186.192-.21.356-.202.767-.136 1.146.016.352.144.618.408 1.001.782.391.451.462.576.685.914.176.265.336.537.445.848.067.195-.019.354-.25.452z\"></path></svg>",
                HasVisionSupport = false,
                HasPdfSupport = false,
                HasReasoningSupport = false,
                IsImageGeneration = false,
                IsFavorite = false,
                IsActive = true
            },
        };

        // Helper methods to access model configuration
        public static ModelConfiguration? GetModelConfiguration(string modelId)
        {
            return ModelConfigurations.FirstOrDefault(m => m.ModelId == modelId);
        }

        public static ModelConfiguration? GetModelConfigurationByUniqueId(string uniqueId)
        {
            return ModelConfigurations.FirstOrDefault(m => m.UniqueId == uniqueId);
        }

        public static List<ModelConfiguration> GetFavoriteModels()
        {
            return ModelConfigurations.Where(m => m.IsFavorite).ToList();
        }

        public static List<ModelConfiguration> GetActiveModels()
        {
            return ModelConfigurations.Where(m => m.IsActive).ToList();
        }

        public static List<ModelConfiguration> GetModelsWithVision()
        {
            return ModelConfigurations.Where(m => m.HasVisionSupport).ToList();
        }

        public static List<ModelConfiguration> GetModelsWithPdf()
        {
            return ModelConfigurations.Where(m => m.HasPdfSupport).ToList();
        }

        public static List<ModelConfiguration> GetModelsWithReasoning()
        {
            return ModelConfigurations.Where(m => m.HasReasoningSupport).ToList();
        }

        public static List<ModelConfiguration> GetImageGenerationModels()
        {
            return ModelConfigurations.Where(m => m.IsImageGeneration).ToList();
        }

        // User-specific favorite model methods
        public static List<string> GetDefaultFavoriteModelIds()
        {
            return ModelConfigurations.Where(m => m.IsFavorite).Select(m => m.UniqueId).ToList();
        }

        public static List<ModelConfiguration> GetUserFavoriteModels(string? userFavoritesJson)
        {
            if (string.IsNullOrEmpty(userFavoritesJson))
            {
                // Return default favorites if user hasn't set any
                return ModelConfigurations.Where(m => m.IsFavorite).ToList();
            }

            try
            {
                var favoriteIds = JsonSerializer.Deserialize<List<string>>(userFavoritesJson) ?? new List<string>();
                return ModelConfigurations.Where(m => favoriteIds.Contains(m.UniqueId)).ToList();
            }
            catch
            {
                // Return default favorites if JSON parsing fails
                return ModelConfigurations.Where(m => m.IsFavorite).ToList();
            }
        }

        public static bool IsUserFavoriteModel(string uniqueId, string? userFavoritesJson)
        {
            if (string.IsNullOrEmpty(userFavoritesJson))
            {
                // Use default favorites if user hasn't set any
                var config = GetModelConfigurationByUniqueId(uniqueId);
                return config?.IsFavorite ?? false;
            }

            try
            {
                var favoriteIds = JsonSerializer.Deserialize<List<string>>(userFavoritesJson) ?? new List<string>();
                return favoriteIds.Contains(uniqueId);
            }
            catch
            {
                // Use default favorites if JSON parsing fails
                var config = GetModelConfigurationByUniqueId(uniqueId);
                return config?.IsFavorite ?? false;
            }
        }

        public static string SerializeFavoriteModels(List<string> favoriteIds)
        {
            try
            {
                return JsonSerializer.Serialize(favoriteIds);
            }
            catch
            {
                return "[]";
            }
        }


        public async Task<T> SendChatCompletionWithStructuredOutputAsync<T>(string model, List<ChatMessage> messages, object jsonSchema, string? apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentException("API key is required for OpenRouter service");
            }

            var request = new
            {
                model = model,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                response_format = new
                {
                    type = "json_schema",
                    json_schema = jsonSchema
                }
            };

            var json = JsonSerializer.Serialize(request);
            System.Diagnostics.Debug.WriteLine($"Sending structured output request: {json}");
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = content
            };
            requestMessage.Headers.Add("Authorization", $"Bearer {apiKey}");

            var response = await _httpClient.SendAsync(requestMessage);
            var responseContent = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"API Response: {responseContent}");

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"OpenRouter API error: {responseContent}");
            }

            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
            var contentString = result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
            System.Diagnostics.Debug.WriteLine($"Content string: {contentString}");
            
            var deserializedResult = JsonSerializer.Deserialize<T>(contentString);
            System.Diagnostics.Debug.WriteLine($"Deserialized result: {deserializedResult}");
            
            return deserializedResult ?? throw new Exception("Failed to deserialize structured output");
        }


        public async Task<ChatNameResult> GenerateChatNameAsync(string userMessage, string? apiKey)
        {
            var messages = new List<ChatMessage>
            {
                new ChatMessage 
                { 
                    Role = "user", 
                    Content = $"Generate a short, descriptive name for a chat based on this user message: \"{userMessage}\". The name should be maximum 3 words and capture the main topic or intent." 
                }
            };

            var schema = new
            {
                name = "chat_name",
                strict = true,
                schema = new
                {
                    type = "object",
                    properties = new
                    {
                        name = new
                        {
                            type = "string",
                            description = "A short, descriptive name for the chat (maximum 3 words)"
                        }
                    },
                    required = new[] { "name" },
                    additionalProperties = false
                }
            };

            return await SendChatCompletionWithStructuredOutputAsync<ChatNameResult>("google/gemini-2.0-flash-lite-001", messages, schema, apiKey);
        }
        
        private object? GetProviderRouting(string model)
        {
            // Configure provider routing for specific models
            return model switch
            {
                "deepseek/deepseek-r1-0528" => new { only = new[] { "lambda/fp8" } },
                "deepseek/deepseek-r1-distill-llama-70b" => new { only = new[] { "lambda/fp8" } },
                "deepseek/deepseek-chat-v3-0324" => new { only = new[] { "lambda/fp8" } },
                _ => null
            };
        }

        private static object? GetReasoningConfig(string displayName)
        {
            // Configure reasoning parameters for models that support it based on display name
            return displayName switch
            {
                "Gemini 2.5 Flash Lite Thinking" => new { max_tokens = 10000 },
                "Gemini 2.5 Flash Thinking" => new { max_tokens = 10000 },
                "Gemini 2.5 Pro" => new { max_tokens = 10000 },
                "Claude 4 Sonnet Thinking" => new { max_tokens = 10000 },
                "Claude 4 Opus Thinking" => new { max_tokens = 10000 },
                _ => null // No reasoning config for non-reasoning models
            };
        }


        public async IAsyncEnumerable<string> SendChatCompletionStreamAsync(string model, List<ChatMessage> messages, string? apiKey, [EnumeratorCancellation] CancellationToken cancellationToken = default, bool enableWebSearch = false, WebSearchOptions? webSearchOptions = null, string? systemPrompt = null, string? modelDisplayName = null)
        {
            await foreach (var chunk in SendChatCompletionStreamWithReasoningAsync(model, messages, apiKey, cancellationToken, enableWebSearch, webSearchOptions, systemPrompt, modelDisplayName))
            {
                if (chunk.IsContent)
                {
                    yield return chunk.Content;
                }
            }
        }


        public async IAsyncEnumerable<StreamingChunk> SendChatCompletionStreamWithReasoningAsync(string model, List<ChatMessage> messages, string? apiKey, [EnumeratorCancellation] CancellationToken cancellationToken = default, bool enableWebSearch = false, WebSearchOptions? webSearchOptions = null, string? systemPrompt = null, string? modelDisplayName = null)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentException("API key is required for OpenRouter service");
            }

            // Ensure system prompt is first message if provided
            var processedMessages = PrepareMessagesWithSystemPrompt(messages, systemPrompt);

            var request = new
            {
                model = enableWebSearch ? GetWebSearchModel(model) : model,
                messages = processedMessages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                stream = true,
                transforms = new[] { "middle-out" },
                provider = GetProviderRouting(model),
                reasoning = !string.IsNullOrEmpty(modelDisplayName) ? GetReasoningConfig(modelDisplayName) : null,
                plugins = GetPlugins(model, enableWebSearch, webSearchOptions),
                web_search_options = webSearchOptions?.ToApiObject()
            };

            var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = content
            };
            requestMessage.Headers.Add("Authorization", $"Bearer {apiKey}");

            var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = OpenRouterErrorHandler.ProcessOpenRouterError(response, errorContent);
                throw new HttpRequestException(errorMessage, null, response.StatusCode);
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (line.StartsWith("data: "))
                {
                    var data = line.Substring(6);
                    if (data == "[DONE]") break;

                    var chunk = ParseStreamingChunk(data);
                    if (chunk != null && (chunk.IsReasoning || chunk.IsContent))
                    {
                        yield return chunk;
                    }
                }
            }
        }

        private StreamingChunk? ParseStreamingChunk(string data)
        {
            try
            {
                var responseJson = JsonSerializer.Deserialize<JsonElement>(data);
                
                // Check for errors in the stream
                if (responseJson.TryGetProperty("error", out var errorProp))
                {
                    var errorCode = errorProp.TryGetProperty("code", out var codeProp) ? codeProp.GetInt32() : 0;
                    var errorMessage = errorProp.TryGetProperty("message", out var messageProp) ? messageProp.GetString() : "Unknown error";
                    
                    var error = new OpenRouterErrorHandler.OpenRouterError
                    {
                        Code = errorCode,
                        Message = errorMessage ?? "Unknown error"
                    };
                    
                    if (errorProp.TryGetProperty("metadata", out var metadataProp))
                    {
                        error.Metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metadataProp.GetRawText());
                    }
                    
                    var formattedError = OpenRouterErrorHandler.ProcessOpenRouterError(new HttpResponseMessage((HttpStatusCode)errorCode), null);
                    throw new HttpRequestException(formattedError, null, (HttpStatusCode)errorCode);
                }
                
                var delta = responseJson.GetProperty("choices")[0].GetProperty("delta");
                
                var chunk = new StreamingChunk();
                
                if (delta.TryGetProperty("reasoning", out var reasoningProp))
                {
                    chunk.Reasoning = reasoningProp.GetString() ?? string.Empty;
                }
                
                if (delta.TryGetProperty("content", out var contentProp))
                {
                    chunk.Content = contentProp.GetString() ?? string.Empty;
                }

                return chunk;
            }
            catch (JsonException)
            {
                // Skip malformed JSON
                return null;
            }
        }
        
        private string GetWebSearchModel(string model)
        {
            // Use :online suffix for simple web search enablement
            return $"{model}:online";
        }
        
        private object[]? GetWebSearchPlugins(WebSearchOptions? options)
        {
            if (options == null)
            {
                return new[] { new { id = "web" } };
            }
            
            return new[]
            {
                new
                {
                    id = "web",
                    max_results = options.MaxResults,
                    search_prompt = options.SearchPrompt
                }
            };
        }
        
        private object[]? GetPlugins(string model, bool enableWebSearch = false, WebSearchOptions? webSearchOptions = null)
        {
            var plugins = new List<object>();
            
            // Add web search plugin if enabled
            if (enableWebSearch)
            {
                var webPlugins = GetWebSearchPlugins(webSearchOptions);
                if (webPlugins != null)
                {
                    plugins.AddRange(webPlugins);
                }
            }
            
            // Add file parser plugin for PDF support on non-native models
            if (!ModelSupportsNativeFiles(model))
            {
                plugins.Add(new
                {
                    id = "file-parser",
                    pdf = new
                    {
                        engine = "mistral-ocr" // Will automatically fallback for non-native models
                    }
                });
            }
            
            return plugins.Any() ? plugins.ToArray() : null;
        }
        
        public static bool ModelSupportsFiles(string uniqueId)
        {
            var config = GetModelConfigurationByUniqueId(uniqueId);
            return config?.HasVisionSupport == true || config?.HasPdfSupport == true;
        }
        
        public static bool ModelSupportsImages(string uniqueId)
        {
            var config = GetModelConfigurationByUniqueId(uniqueId);
            return config?.HasVisionSupport ?? false;
        }

        public static bool ModelSupportsPdf(string uniqueId)
        {
            var config = GetModelConfigurationByUniqueId(uniqueId);
            return config?.HasPdfSupport ?? false;
        }

        public static bool ModelSupportsReasoning(string uniqueId)
        {
            var config = GetModelConfigurationByUniqueId(uniqueId);
            return config?.HasReasoningSupport ?? false;
        }

        public static bool IsImageGenerationModel(string uniqueId)
        {
            var config = GetModelConfigurationByUniqueId(uniqueId);
            return config?.IsImageGeneration ?? false;
        }
        
        public static bool ModelSupportsNativeFiles(string uniqueId)
        {
            var config = GetModelConfigurationByUniqueId(uniqueId);
            return config?.HasPdfSupport == true;
        }
        
        private List<Annotation> ParseAnnotations(JsonElement annotationsElement)
        {
            var annotations = new List<Annotation>();
            
            if (annotationsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var annotationElement in annotationsElement.EnumerateArray())
                {
                    if (annotationElement.TryGetProperty("type", out var typeElement))
                    {
                        var annotation = new Annotation
                        {
                            Type = typeElement.GetString() ?? ""
                        };
                        
                        if (annotation.Type == "url_citation" && 
                            annotationElement.TryGetProperty("url_citation", out var urlCitationElement))
                        {
                            annotation.UrlCitation = new UrlCitation
                            {
                                Url = urlCitationElement.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? "" : "",
                                Title = urlCitationElement.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "",
                                Content = urlCitationElement.TryGetProperty("content", out var contentProp) ? contentProp.GetString() ?? "" : "",
                                StartIndex = urlCitationElement.TryGetProperty("start_index", out var startProp) ? startProp.GetInt32() : 0,
                                EndIndex = urlCitationElement.TryGetProperty("end_index", out var endProp) ? endProp.GetInt32() : 0
                            };
                        }
                        
                        annotations.Add(annotation);
                    }
                }
            }
            
            return annotations;
        }

        private List<ChatMessage> PrepareMessagesWithSystemPrompt(List<ChatMessage> messages, string? systemPrompt)
        {
            var processedMessages = new List<ChatMessage>();
            
            // Add system prompt as first message if provided
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                processedMessages.Add(ChatMessage.CreateTextMessage("system", systemPrompt));
            }
            
            // Add all non-system messages (in case there are existing system messages, remove them to avoid duplicates)
            processedMessages.AddRange(messages.Where(m => m.Role != "system"));
            
            return processedMessages;
        }
    }

    public class ChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public object Content { get; set; } = string.Empty;
        
        // Helper method to create a simple text message
        public static ChatMessage CreateTextMessage(string role, string content)
        {
            return new ChatMessage { Role = role, Content = content };
        }
        
        // Helper method to create a message with attachments
        public static ChatMessage CreateMessageWithAttachments(string role, string text, List<ChatAttachment> attachments)
        {
            var contentItems = new List<object>();
            
            // Add text content first
            if (!string.IsNullOrEmpty(text))
            {
                contentItems.Add(new { type = "text", text = text });
            }
            
            // Add file attachments
            foreach (var attachment in attachments)
            {
                if (attachment.Type == ChatAttachmentType.Image)
                {
                    contentItems.Add(new
                    {
                        type = "image_url",
                        image_url = new
                        {
                            url = $"data:{attachment.ContentType};base64,{attachment.Base64Data}"
                        }
                    });
                }
                else if (attachment.Type == ChatAttachmentType.PDF)
                {
                    contentItems.Add(new
                    {
                        type = "file",
                        file = new
                        {
                            filename = attachment.FileName,
                            file_data = $"data:{attachment.ContentType};base64,{attachment.Base64Data}"
                        }
                    });
                }
            }
            
            return new ChatMessage 
            { 
                Role = role, 
                Content = contentItems.Count == 1 && contentItems[0] is string 
                    ? contentItems[0] 
                    : contentItems.ToArray()
            };
        }
    }
    
    public class ChatAttachment
    {
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string Base64Data { get; set; } = string.Empty;
        public ChatAttachmentType Type { get; set; }
    }
    
    public enum ChatAttachmentType
    {
        Image,
        PDF
    }

    public class ChatNameResult
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class StreamingChunk
    {
        public string Content { get; set; } = string.Empty;
        public string Reasoning { get; set; } = string.Empty;
        public bool IsReasoning => !string.IsNullOrEmpty(Reasoning);
        public bool IsContent => !string.IsNullOrEmpty(Content);
    }
    
    public class ChatCompletionResult
    {
        public string Content { get; set; } = string.Empty;
        public List<Annotation> Annotations { get; set; } = new();
    }
    
    public class Annotation
    {
        public string Type { get; set; } = string.Empty;
        public UrlCitation? UrlCitation { get; set; }
    }
    
    public class UrlCitation
    {
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
    }
    
    public class WebSearchOptions
    {
        public int MaxResults { get; set; } = 10;
        public string SearchPrompt { get; set; } = "A web search was conducted. Incorporate the following web search results into your response.\n\nIMPORTANT: Cite them using markdown links named using the domain of the source.\nExample: [nytimes.com](https://nytimes.com/some-page).";
        public string SearchContextSize { get; set; } = "medium"; // low, medium, high
        
        public object ToApiObject()
        {
            return new
            {
                search_context_size = SearchContextSize
            };
        }
    }
}