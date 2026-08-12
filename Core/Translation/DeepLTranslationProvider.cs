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
    public class DeepLTranslationProvider : ITranslationProvider
    {
        public TranslationServiceConfig Config { get; }
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public DeepLTranslationProvider(TranslationServiceConfig config)
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

            if (string.IsNullOrWhiteSpace(Config.ApiKey))
            {
                result.Success = false;
                result.ErrorMessage = "DeepL API Key is not configured in settings.ini.";
                return result;
            }

            string tgt = MapDeepLLangCode(targetLang);

            var requestBody = new
            {
                text = items.ConvertAll(i => i.SourceText ?? string.Empty),
                target_lang = tgt
            };

            string jsonBody = _serializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post, string.IsNullOrWhiteSpace(Config.Endpoint) ? "https://api-free.deepl.com/v2/translate" : Config.Endpoint)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("DeepL-Auth-Key", Config.ApiKey.Trim());

            try
            {
                var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.StatusCode == (HttpStatusCode)429 || response.StatusCode == (HttpStatusCode)456 || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    result.QuotaExceeded = true;
                    result.ErrorMessage = $"Quota or character limit reached on DeepL ({response.StatusCode}).";
                    result.Success = false;
                    return result;
                }

                response.EnsureSuccessStatusCode();
                string respJson = await response.Content.ReadAsStringAsync();

                var parsed = _serializer.Deserialize<Dictionary<string, object>>(respJson);
                if (parsed != null && parsed.TryGetValue("translations", out object transObj) && transObj is System.Collections.ArrayList translations)
                {
                    for (int i = 0; i < Math.Min(items.Count, translations.Count); i++)
                    {
                        if (translations[i] is Dictionary<string, object> itemDict && itemDict.TryGetValue("text", out object textVal))
                        {
                            items[i].TranslatedText = textVal?.ToString();
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

        private string MapDeepLLangCode(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang)) return "EN-US";
            string l = lang.Trim().ToUpperInvariant();
            if (l.Contains("ES")) return "ES";
            if (l.Contains("FR")) return "FR";
            if (l.Contains("DE")) return "DE";
            if (l.Contains("IT")) return "IT";
            if (l.Contains("RU")) return "RU";
            if (l.Contains("PL")) return "PL";
            if (l.Contains("JA")) return "JA";
            if (l.Contains("KO")) return "KO";
            if (l.Contains("ZH")) return "ZH";
            return "EN-US";
        }
    }
}
