using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SkillRadar.Console.Services
{
    public class TranslationService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _openAiApiKey;

        public TranslationService(HttpClient httpClient, string? openAiApiKey = null)
        {
            _httpClient = httpClient;
            _openAiApiKey = openAiApiKey;
        }

        public async Task<string?> TranslateAsync(string text, string targetLanguage)
        {
            if (string.IsNullOrEmpty(_openAiApiKey) || string.IsNullOrEmpty(text) || targetLanguage.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                var languageName = GetLanguageName(targetLanguage);
                if (string.IsNullOrEmpty(languageName))
                {
                    return null;
                }

                var prompt = $@"Translate the following English text to {languageName}. Keep technical terms and proper nouns in their original form when appropriate. Provide only the translation without any additional text:

{text}";

                var requestBody = new
                {
                    model = "gpt-3.5-turbo",
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 500,
                    temperature = 0.3
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _openAiApiKey);

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseJson = JsonSerializer.Deserialize<JsonElement>(responseContent);

                if (responseJson.TryGetProperty("choices", out var choices) && 
                    choices.GetArrayLength() > 0 &&
                    choices[0].TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var translatedContent))
                {
                    return translatedContent.GetString()?.Trim();
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Translation error: {ex.Message}");
                return null;
            }
        }

        private string GetLanguageName(string languageCode)
        {
            return languageCode.ToUpperInvariant() switch
            {
                "JA" => "Japanese",
                "ES" => "Spanish", 
                "FR" => "French",
                "DE" => "German",
                "IT" => "Italian",
                "PT" => "Portuguese",
                "RU" => "Russian",
                "KO" => "Korean",
                "ZH" => "Chinese",
                "AR" => "Arabic",
                _ => null
            };
        }

        public async Task<(string? titleTranslation, string? summaryTranslation)> TranslateArticleAsync(string title, string summary, string targetLanguage)
        {
            if (targetLanguage.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                return (null, null);
            }

            var titleTask = TranslateAsync(title, targetLanguage);
            var summaryTask = string.IsNullOrEmpty(summary) ? Task.FromResult<string?>(null) : TranslateAsync(summary, targetLanguage);

            await Task.WhenAll(titleTask, summaryTask);

            return (await titleTask, await summaryTask);
        }
    }
}