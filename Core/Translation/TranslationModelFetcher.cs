using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace CsfStudio.Core.Translation
{
    public static class TranslationModelFetcher
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public static async Task<List<string>> FetchModelsAsync(string endpoint, string apiKey, CancellationToken cancellationToken = default)
        {
            var modelList = new List<string>();

            if (string.IsNullOrWhiteSpace(endpoint)) return modelList;

            string modelsUrl = ResolveModelsEndpoint(endpoint);

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, modelsUrl);
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey.Trim());
                }

                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();
                var parsed = _serializer.Deserialize<Dictionary<string, object>>(json);

                if (parsed != null && parsed.TryGetValue("data", out object dataObj) && dataObj is System.Collections.ArrayList dataList)
                {
                    foreach (var itemObj in dataList)
                    {
                        if (itemObj is Dictionary<string, object> itemDict && itemDict.TryGetValue("id", out object idVal) && idVal != null)
                        {
                            string modelId = idVal.ToString();
                            if (!string.IsNullOrWhiteSpace(modelId) && !modelList.Contains(modelId))
                            {
                                modelList.Add(modelId);
                            }
                        }
                    }
                }
                else if (parsed != null && parsed.TryGetValue("models", out object modelsObj) && modelsObj is System.Collections.ArrayList mList)
                {
                    foreach (var itemObj in mList)
                    {
                        if (itemObj is Dictionary<string, object> itemDict && itemDict.TryGetValue("name", out object nameVal) && nameVal != null)
                        {
                            string modelId = nameVal.ToString();
                            if (!string.IsNullOrWhiteSpace(modelId) && !modelList.Contains(modelId))
                            {
                                modelList.Add(modelId);
                            }
                        }
                        else if (itemObj is string mStr && !string.IsNullOrWhiteSpace(mStr))
                        {
                            if (!modelList.Contains(mStr)) modelList.Add(mStr);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error fetching models: " + ex.Message);
                throw;
            }

            modelList.Sort();
            return modelList;
        }

        private static string ResolveModelsEndpoint(string endpoint)
        {
            string url = endpoint.Trim();
            if (url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                return url.Substring(0, url.Length - "/chat/completions".Length) + "/models";
            }
            if (url.EndsWith("/completions", StringComparison.OrdinalIgnoreCase))
            {
                return url.Substring(0, url.Length - "/completions".Length) + "/models";
            }
            if (url.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
            {
                return url.Substring(0, url.Length - "/responses".Length) + "/models";
            }
            if (!url.EndsWith("/models", StringComparison.OrdinalIgnoreCase))
            {
                if (url.EndsWith("/")) return url + "models";
                return url + "/models";
            }
            return url;
        }
    }
}
