using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CsfStudio.Core.Translation
{
    public class MicrosoftTranslatorProvider : ITranslationProvider
    {
        public TranslationServiceConfig Config { get; private set; }
        private static readonly HttpClient HttpClient = new HttpClient();
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public MicrosoftTranslatorProvider(TranslationServiceConfig config)
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

            string apiKey = Config.ApiKey?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                result.Success = false;
                result.ErrorMessage = "Microsoft Translator requires an Azure API Key. Please enter your API Key in Settings (or use Google Translate for free web translation).";
                return result;
            }

            string source = TranslationLanguageHelper.Normalize(sourceLang);
            string target = TranslationLanguageHelper.Normalize(targetLang);
            if (string.IsNullOrWhiteSpace(target) || target == "auto")
            {
                result.Success = false;
                result.ErrorMessage = "A target language is required for Microsoft Translator.";
                return result;
            }

            string endpoint = !string.IsNullOrWhiteSpace(Config.UrlTemplate)
                ? Config.UrlTemplate
                : (!string.IsNullOrWhiteSpace(Config.Endpoint)
                    ? Config.Endpoint
                    : "https://api.cognitive.microsofttranslator.com/translate?api-version=3.0");

            endpoint = endpoint.Replace("{src}", Uri.EscapeDataString(source ?? ""))
                               .Replace("{tgt}", Uri.EscapeDataString(target ?? ""));

            if (!endpoint.Contains("api-version="))
            {
                string sep = endpoint.Contains("?") ? "&" : "?";
                endpoint += sep + "api-version=3.0";
            }

            if (!endpoint.Contains("to=") && !string.IsNullOrWhiteSpace(target))
            {
                string sep = endpoint.Contains("?") ? "&" : "?";
                endpoint += sep + "to=" + Uri.EscapeDataString(target);
            }

            if (!endpoint.Contains("from=") && !string.IsNullOrWhiteSpace(source) && source != "auto")
            {
                string sep = endpoint.Contains("?") ? "&" : "?";
                endpoint += sep + "from=" + Uri.EscapeDataString(source);
            }

            var requestItems = new List<Dictionary<string, string>>();
            foreach (var item in items)
            {
                requestItems.Add(new Dictionary<string, string> { { "Text", item.SourceText ?? string.Empty } });
            }

            using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
                if (Config.ExtraParams != null && Config.ExtraParams.TryGetValue("Region", out string region) && !string.IsNullOrWhiteSpace(region))
                {
                    request.Headers.Add("Ocp-Apim-Subscription-Region", region.Trim());
                }

                if (!string.IsNullOrWhiteSpace(Config.UserAgent))
                {
                    request.Headers.UserAgent.ParseAdd(Config.UserAgent);
                }

                request.Content = new StringContent(Serializer.Serialize(requestItems), Encoding.UTF8, "application/json");

                try
                {
                    var response = await HttpClient.SendAsync(request, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    string json = await response.Content.ReadAsStringAsync();
                    var parsed = Serializer.DeserializeObject(json) as ArrayList;

                    if (parsed != null)
                    {
                        for (int i = 0; i < Math.Min(items.Count, parsed.Count); i++)
                        {
                            var itemDict = parsed[i] as Dictionary<string, object>;
                            var translations = itemDict == null ? null : itemDict["translations"] as ArrayList;
                            var firstTranslation = translations != null && translations.Count > 0
                                ? translations[0] as Dictionary<string, object>
                                : null;
                            if (firstTranslation != null && firstTranslation.TryGetValue("text", out object translated))
                            {
                                items[i].TranslatedText = translated?.ToString();
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
            }

            return result;
        }
    }
}
