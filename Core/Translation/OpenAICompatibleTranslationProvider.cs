using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CsfStudio.Core.Translation
{
    public class OpenAICompatibleTranslationProvider : ITranslationProvider
    {
        public TranslationServiceConfig Config { get; }
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public OpenAICompatibleTranslationProvider(TranslationServiceConfig config)
        {
            Config = config;
        }

        public async Task<TranslationBatchResult> TranslateBatchAsync(List<TranslationItem> items, string sourceLang, string targetLang, CancellationToken cancellationToken)
        {
            var result = new TranslationBatchResult { Items = items };

            if (items == null || items.Count == 0)
            {
                result.Success = true;
                return result;
            }

            string systemPrompt = string.IsNullOrWhiteSpace(Config.SystemPrompt)
                ? TranslationConfigManager.GlobalSettings.DefaultSystemPrompt
                : Config.SystemPrompt;

            var userPayload = new Dictionary<string, string>();
            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item.SourceText))
                {
                    userPayload[item.Key ?? Guid.NewGuid().ToString()] = item.SourceText;
                }
            }

            if (userPayload.Count == 0)
            {
                result.Success = true;
                return result;
            }

            string promptText = $"Translate the following JSON object values from {sourceLang} to {targetLang}.\n" +
                               $"Maintain the exact JSON structure with the same keys. Do not alter formatting tags like \\n or variables like {{0}}.\n\n" +
                               _serializer.Serialize(userPayload);

            var requestBody = new
            {
                model = Config.Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = promptText }
                },
                temperature = Config.Temperature,
                max_tokens = Config.MaxTokens
            };

            string jsonBody = _serializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post, Config.Endpoint)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrWhiteSpace(Config.ApiKey))
            {
                if (Config.Endpoint != null && Config.Endpoint.Contains("anthropic.com"))
                {
                    request.Headers.Add("x-api-key", Config.ApiKey.Trim());
                    request.Headers.Add("anthropic-version", "2023-06-01");
                }
                else
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Config.ApiKey.Trim());
                }
            }

            try
            {
                var response = await _httpClient.SendAsync(request, cancellationToken);

                if (response.StatusCode == (HttpStatusCode)429 || response.StatusCode == HttpStatusCode.PaymentRequired || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    result.QuotaExceeded = true;
                    result.ErrorMessage = $"Quota limit reached or invalid API key on {Config.DisplayName} ({response.StatusCode}).";
                    result.Success = false;
                    return result;
                }

                response.EnsureSuccessStatusCode();
                string respJson = await response.Content.ReadAsStringAsync();

                var parsed = _serializer.Deserialize<Dictionary<string, object>>(respJson);
                if (parsed != null && parsed.TryGetValue("choices", out object choicesObj) && choicesObj is System.Collections.ArrayList choices && choices.Count > 0)
                {
                    if (choices[0] is Dictionary<string, object> choice && choice.TryGetValue("message", out object msgObj) && msgObj is Dictionary<string, object> message)
                    {
                        if (message.TryGetValue("content", out object contentObj) && contentObj != null)
                        {
                            string content = contentObj.ToString();
                            int jsonStart = content.IndexOf('{');
                            int jsonEnd = content.LastIndexOf('}');
                            if (jsonStart >= 0 && jsonEnd > jsonStart)
                            {
                                content = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                            }

                            var translatedDict = _serializer.Deserialize<Dictionary<string, string>>(content);
                            if (translatedDict != null)
                            {
                                foreach (var item in items)
                                {
                                    if (item.Key != null && translatedDict.TryGetValue(item.Key, out string trans))
                                    {
                                        item.TranslatedText = trans;
                                    }
                                }
                            }
                        }
                    }
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }
    }
}
