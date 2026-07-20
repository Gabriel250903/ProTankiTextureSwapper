using Serilog;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TextureSwapper.Services.Interfaces;

namespace TextureSwapper.Services
{
    public sealed class AiTextureService : IAiTextureService
    {
        private static readonly HttpClient _httpClient = new(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        })
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        private const string ModelName = "black-forest-labs/FLUX.1-schnell";

        public async Task<byte[]?> GenerateTextureAsync(string prompt, string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("Hugging Face API token is required.");
            }

            string formattedPrompt = $"{prompt}, seamless 2D tileable game texture pattern, flat lighting, game asset, top-down view, highly detailed, 8k";
            string apiUrl = $"https://router.huggingface.co/hf-inference/models/{ModelName}";

            Log.Information($"Calling Hugging Face Inference API for model {ModelName} with prompt: {formattedPrompt}");

            using HttpRequestMessage request = new(HttpMethod.Post, apiUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
            request.Headers.Add("X-Use-Cache", "false");

            int randomSeed = Random.Shared.Next(1, 99999999);
            var payload = new
            {
                inputs = formattedPrompt,
                parameters = new { width = 256, height = 256, seed = randomSeed }
            };
            string jsonPayload = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                using HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    byte[] data = await response.Content.ReadAsByteArrayAsync();
                    Log.Information($"Successfully generated AI texture. Size: {data.Length} bytes.");
                    return data;
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Log.Warning($"Hugging Face API failed. Status: {response.StatusCode}, Error: {errorContent}");

                    if (errorContent.Contains("estimated_time"))
                    {
                        throw new InvalidOperationException("The AI model is currently loading on Hugging Face servers. Please wait a moment and try again.");
                    }

                    throw new HttpRequestException($"Hugging Face API error ({response.StatusCode}): {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to call Hugging Face generation API.");
                throw;
            }
        }
    }
}
