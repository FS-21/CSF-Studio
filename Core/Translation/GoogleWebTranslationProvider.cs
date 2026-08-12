using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;

namespace CsfStudio.Core.Translation
{
    public class GoogleWebTranslationProvider : ITranslationProvider
    {
        public TranslationServiceConfig Config { get; }
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public GoogleWebTranslationProvider(TranslationServiceConfig config)
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

            string src = MapLangCode(sourceLang);
            string tgt = MapLangCode(targetLang);

            foreach (var item in items)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (string.IsNullOrEmpty(item.SourceText))
                {
                    item.TranslatedText = string.Empty;
                    continue;
                }

                try
                {
                    var transRes = await TranslateSingleTextPreservingFormatAsync(item.SourceText, src, tgt, cancellationToken);
                    if (transRes.QuotaExceeded)
                    {
                        result.QuotaExceeded = true;
                        result.ErrorMessage = transRes.ErrorMessage;
                        result.Success = false;
                        return result;
                    }

                    if (!string.IsNullOrEmpty(transRes.Text))
                    {
                        item.TranslatedText = transRes.Text;
                    }
                    else
                    {
                        item.ErrorMessage = transRes.ErrorMessage ?? "Google Translate returned an empty response or unexpected format.";
                    }
                }
                catch (Exception ex)
                {
                    item.ErrorMessage = ex.Message;
                }
            }

            result.Success = true;
            return result;
        }

        private class SingleTranslationResult
        {
            public string Text { get; set; }
            public bool QuotaExceeded { get; set; }
            public string ErrorMessage { get; set; }
        }

        private async Task<SingleTranslationResult> TranslateSingleTextPreservingFormatAsync(string text, string src, string tgt, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(text)) return new SingleTranslationResult { Text = text };

            string lineBreak = text.Contains("\r\n") ? "\r\n" : "\n";
            string[] rawLines = text.Replace("\r\n", "\n").Split('\n');
            var translatedLines = new List<string>();

            foreach (var rawLine in rawLines)
            {
                if (string.IsNullOrEmpty(rawLine))
                {
                    translatedLines.Add(string.Empty);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(rawLine))
                {
                    translatedLines.Add(rawLine);
                    continue;
                }

                int leadLen = 0;
                while (leadLen < rawLine.Length && char.IsWhiteSpace(rawLine[leadLen])) leadLen++;

                int trailLen = 0;
                while (trailLen < (rawLine.Length - leadLen) && char.IsWhiteSpace(rawLine[rawLine.Length - 1 - trailLen])) trailLen++;

                string leadWs = leadLen > 0 ? rawLine.Substring(0, leadLen) : "";
                string trailWs = trailLen > 0 ? rawLine.Substring(rawLine.Length - trailLen) : "";
                string core = rawLine.Substring(leadLen, rawLine.Length - leadLen - trailLen);

                if (string.IsNullOrEmpty(core))
                {
                    translatedLines.Add(rawLine);
                    continue;
                }

                string encodedText = Uri.EscapeDataString(core);
                string url = Config.UrlTemplate
                    .Replace("{src}", src)
                    .Replace("{tgt}", tgt)
                    .Replace("{text}", encodedText);

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrWhiteSpace(Config.UserAgent))
                {
                    request.Headers.UserAgent.ParseAdd(Config.UserAgent);
                }

                var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.StatusCode == (HttpStatusCode)429 || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    return new SingleTranslationResult
                    {
                        QuotaExceeded = true,
                        ErrorMessage = $"Rate limit or quota reached on Google Translate ({response.StatusCode})."
                    };
                }

                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                string translatedCore = ExtractGoogleTranslateText(json);

                if (string.IsNullOrEmpty(translatedCore))
                {
                    translatedCore = core;
                }

                translatedLines.Add(leadWs + translatedCore + trailWs);
            }

            return new SingleTranslationResult { Text = string.Join(lineBreak, translatedLines) };
        }

        private string ExtractGoogleTranslateText(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return string.Empty;

            try
            {
                var parsed = _serializer.DeserializeObject(json);

                // 1. Check MyMemory & Lingva JSON dictionary responses
                if (parsed is Dictionary<string, object> dict)
                {
                    // MyMemory format: {"responseData": {"translatedText": "..."}}
                    if (dict.TryGetValue("responseData", out var respObj) && respObj is Dictionary<string, object> respDict)
                    {
                        if (respDict.TryGetValue("translatedText", out var textVal) && textVal != null)
                        {
                            return textVal.ToString();
                        }
                    }
                    // Lingva format: {"translation": "..."}
                    if (dict.TryGetValue("translation", out var transVal) && transVal != null)
                    {
                        return transVal.ToString();
                    }
                }

                // 2. Check Google Translate array response: [[["translatedText", "sourceText"...]]]
                if (parsed is System.Collections.IEnumerable topList)
                {
                    var sb = new StringBuilder();
                    foreach (var item in topList)
                    {
                        if (item is System.Collections.IEnumerable sentenceList)
                        {
                            foreach (var sObj in sentenceList)
                            {
                                if (sObj is System.Collections.IEnumerable pair)
                                {
                                    var enumerator = pair.GetEnumerator();
                                    if (enumerator.MoveNext() && enumerator.Current != null)
                                    {
                                        sb.Append(enumerator.Current.ToString());
                                    }
                                }
                            }
                        }
                        break;
                    }
                    if (sb.Length > 0) return sb.ToString();
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private string MapLangCode(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang) || lang.Equals("auto", StringComparison.OrdinalIgnoreCase)) return "auto";
            string l = lang.Trim().ToLowerInvariant();
            if (l.Contains("["))
            {
                int s = l.IndexOf('[');
                int e = l.IndexOf(']');
                if (e > s) l = l.Substring(s + 1, e - s - 1);
            }

            if (l.StartsWith("en")) return "en";
            if (l.StartsWith("es")) return "es";
            if (l.StartsWith("fr")) return "fr";
            if (l.StartsWith("de")) return "de";
            if (l.StartsWith("it")) return "it";
            if (l.StartsWith("ru")) return "ru";
            if (l.StartsWith("pl")) return "pl";
            if (l.StartsWith("ja") || l.StartsWith("jp")) return "ja";
            if (l.StartsWith("ko")) return "ko";
            if (l.Contains("zh-tw") || l.Contains("zh_tw") || l.Contains("zh-hant") || l.Contains("zh_hant") || l.Contains("traditional")) return "zh-TW";
            if (l.Contains("zh-cn") || l.Contains("zh_cn") || l.Contains("zh-hans") || l.Contains("zh_hans") || l.StartsWith("zh")) return "zh-CN";
            return l;
        }
    }
}
