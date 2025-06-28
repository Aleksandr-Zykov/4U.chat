using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace _4U.chat.Services
{
    public class GoogleImageRequest
    {
        [JsonPropertyName("instances")]
        public List<Instance> Instances { get; set; } = new();

        [JsonPropertyName("parameters")]
        public Parameters Parameters { get; set; } = new();
    }

    public class Instance
    {
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;
    }

    public class Parameters
    {
        [JsonPropertyName("outputMimeType")]
        public string OutputMimeType { get; set; } = "image/jpeg";

        [JsonPropertyName("sampleCount")]
        public int SampleCount { get; set; } = 1;

        [JsonPropertyName("personGeneration")]
        public string PersonGeneration { get; set; } = "ALLOW_ADULT";

        [JsonPropertyName("aspectRatio")]
        public string AspectRatio { get; set; } = "1:1";
    }

    public class GoogleService
    {
        private readonly HttpClient _httpClient;

        public GoogleService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
        }

        public async Task<List<string>> GenerateImageAsync(string model, string prompt, string? apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentException("API key is required for Google Image service");
            }

            var request = new GoogleImageRequest
            {
                Instances = new List<Instance>
                {
                    new Instance { Prompt = prompt }
                },
                Parameters = new Parameters
                {
                    OutputMimeType = "image/jpeg",
                    SampleCount = 2,
                    PersonGeneration = "ALLOW_ADULT",
                    AspectRatio = "1:1"
                }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"models/{model}:predict?key={apiKey}")
            {
                Content = content
            };

            var response = await _httpClient.SendAsync(requestMessage);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Google Image API error: {response.StatusCode}. Response: {responseContent}. Sent JSON: {json}");
            }

            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
            var predictions = result.GetProperty("predictions");
            var imageBase64List = new List<string>();

            foreach (var prediction in predictions.EnumerateArray())
            {
                if (prediction.TryGetProperty("bytesBase64Encoded", out var bytesBase64Encoded))
                {
                    imageBase64List.Add(bytesBase64Encoded.GetString() ?? "");
                }
            }

            return imageBase64List;
        }
    }
}
