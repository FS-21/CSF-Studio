using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CsfStudio.Core.Translation
{
    public static class TranslationConfigManager
    {
        private static readonly HashSet<string> KnownAppSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AppSettings", "FilterSettings", "FindReplaceSettings", "BatchRenameSettings",
            "IniScannerSettings", "UndoSettings", "WindowSettings", "BackupSettings", "PathPreferences"
        };

        public static string ConfigFilePath
        {
            get
            {
                var appConfig = ConfigManager.LoadConfig();
                return ConfigManager.GetActiveIniPath(appConfig.SaveInAppData);
            }
        }

        public static GlobalTranslationSettings GlobalSettings { get; set; } = new GlobalTranslationSettings();
        public static List<TranslationServiceConfig> ConfiguredServices { get; set; } = new List<TranslationServiceConfig>();

        public static void LoadConfig()
        {
            ConfiguredServices.Clear();
            GlobalSettings = new GlobalTranslationSettings();

            string filePath = ConfigFilePath;
            if (!File.Exists(filePath))
            {
                CreateDefaultServices();
                SaveConfig();
                return;
            }

            try
            {
                ParseIniFile(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error loading translation config: " + ex.Message);
            }

            if (ConfiguredServices.Count == 0)
            {
                CreateDefaultServices();
                SaveConfig();
            }
            else
            {
                bool dirty = false;
                dirty |= EnsureOpenCodeServices();
                foreach (var s in ConfiguredServices)
                {
                    if (s.SectionName.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) && string.Equals(s.ProviderType, "GoogleWeb", StringComparison.OrdinalIgnoreCase))
                    {
                        s.ProviderType = "MicrosoftTranslator";
                        dirty = true;
                    }
                    if (s.DisplayName.Contains("1.5 Flash") || s.DisplayName.Contains("GPT-4o-mini") || s.DisplayName.Contains("Llama 3.1") || s.DisplayName.Contains("Free Tier") || s.DisplayName.Contains("V4 Flash") || s.DisplayName.Contains("(Official API)") || s.DisplayName.Contains("(Free)"))
                    {
                        if (s.SectionName.Equals("Google", StringComparison.OrdinalIgnoreCase)) { s.DisplayName = "Google Translate"; dirty = true; }
                        else if (s.SectionName.Equals("DeepSeek", StringComparison.OrdinalIgnoreCase)) { s.DisplayName = "DeepSeek API"; dirty = true; }
                        else if (s.SectionName.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)) { s.DisplayName = "OpenAI API"; dirty = true; }
                        else if (s.SectionName.Equals("Gemini", StringComparison.OrdinalIgnoreCase)) { s.DisplayName = "Google Gemini API"; dirty = true; }
                        else if (s.SectionName.Equals("Groq", StringComparison.OrdinalIgnoreCase)) { s.DisplayName = "Groq Cloud API"; dirty = true; }
                        else if (s.SectionName.Equals("OpenRouter", StringComparison.OrdinalIgnoreCase)) { s.DisplayName = "OpenRouter API"; dirty = true; }
                    }
                }
                if (dirty) SaveConfig();
            }
        }

        private static bool EnsureOpenCodeServices()
        {
            bool changed = false;
            if (!ConfiguredServices.Any(s => s.SectionName.Equals("OpenCodeGo", StringComparison.OrdinalIgnoreCase)))
            {
                ConfiguredServices.Add(new TranslationServiceConfig
                {
                    SectionName = "OpenCodeGo",
                    DisplayName = "OpenCode Go",
                    ProviderType = "OpenAICompatible",
                    Endpoint = "https://opencode.ai/zen/go/v1/chat/completions",
                    Model = string.Empty,
                    ApiKey = string.Empty
                });
                changed = true;
            }

            if (!ConfiguredServices.Any(s => s.SectionName.Equals("OpenCodeZen", StringComparison.OrdinalIgnoreCase)))
            {
                ConfiguredServices.Add(new TranslationServiceConfig
                {
                    SectionName = "OpenCodeZen",
                    DisplayName = "OpenCode Zen",
                    ProviderType = "OpenAICompatible",
                    Endpoint = "https://opencode.ai/zen/v1/chat/completions",
                    Model = string.Empty,
                    ApiKey = string.Empty
                });
                changed = true;
            }

            return changed;
        }

        private static void ParseIniFile(string filePath)
        {
            string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
            TranslationServiceConfig currentService = null;
            string currentSection = "";

            var rawServicesDict = new Dictionary<string, TranslationServiceConfig>(StringComparer.OrdinalIgnoreCase);
            var indexedServiceList = new List<string>();

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";") || line.StartsWith("#"))
                {
                    continue;
                }

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim();
                    if (!KnownAppSections.Contains(currentSection) &&
                        !currentSection.Equals("TranslationSettings", StringComparison.OrdinalIgnoreCase) &&
                        !currentSection.Equals("TranslationServices", StringComparison.OrdinalIgnoreCase) &&
                        !currentSection.Equals("Services", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!rawServicesDict.ContainsKey(currentSection))
                        {
                            currentService = new TranslationServiceConfig
                            {
                                SectionName = currentSection,
                                DisplayName = currentSection.StartsWith("Provider_", StringComparison.OrdinalIgnoreCase)
                                    ? currentSection.Substring(9)
                                    : currentSection
                            };
                            rawServicesDict[currentSection] = currentService;
                        }
                        else
                        {
                            currentService = rawServicesDict[currentSection];
                        }
                    }
                    else
                    {
                        currentService = null;
                    }
                    continue;
                }

                int idx = line.IndexOf('=');
                if (idx > 0)
                {
                    string key = line.Substring(0, idx).Trim();
                    string val = line.Substring(idx + 1).Trim();

                    if (currentSection.Equals("TranslationSettings", StringComparison.OrdinalIgnoreCase))
                    {
                        if (key.Equals("DefaultSourceLanguage", StringComparison.OrdinalIgnoreCase)) GlobalSettings.DefaultSourceLanguage = val;
                        else if (key.Equals("DefaultSystemPrompt", StringComparison.OrdinalIgnoreCase)) GlobalSettings.DefaultSystemPrompt = val.Replace("\\n", "\n");
                        else if (key.Equals("BatchSize", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out int bs)) GlobalSettings.BatchSize = bs;
                        else if (key.Equals("DelayBetweenBatchesMs", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out int db)) GlobalSettings.DelayBetweenBatchesMs = db;
                        else if (key.Equals("ActiveServices", StringComparison.OrdinalIgnoreCase)) GlobalSettings.ActiveServices = val;
                    }
                    else if (currentSection.Equals("TranslationServices", StringComparison.OrdinalIgnoreCase) ||
                             currentSection.Equals("Services", StringComparison.OrdinalIgnoreCase))
                    {
                        // Numeric index entry: 0=Google, 1=DeepSeek, 2=OpenAI
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            indexedServiceList.Add(val);
                        }
                    }
                    else if (currentService != null)
                    {
                        // Service Properties
                        if (key.Equals("DisplayName", StringComparison.OrdinalIgnoreCase)) currentService.DisplayName = val;
                        else if (key.Equals("Type", StringComparison.OrdinalIgnoreCase) || key.Equals("ProviderType", StringComparison.OrdinalIgnoreCase)) currentService.ProviderType = val;
                        else if (key.Equals("ApiKey", StringComparison.OrdinalIgnoreCase)) currentService.ApiKey = val;
                        else if (key.Equals("Endpoint", StringComparison.OrdinalIgnoreCase)) currentService.Endpoint = val;
                        else if (key.Equals("Model", StringComparison.OrdinalIgnoreCase)) currentService.Model = val;
                        else if (key.Equals("UrlTemplate", StringComparison.OrdinalIgnoreCase)) currentService.UrlTemplate = val;
                        else if (key.Equals("HttpMethod", StringComparison.OrdinalIgnoreCase)) currentService.HttpMethod = val;
                        else if (key.Equals("UserAgent", StringComparison.OrdinalIgnoreCase)) currentService.UserAgent = val;
                        else if (key.Equals("IsEnabled", StringComparison.OrdinalIgnoreCase) && bool.TryParse(val, out bool en)) currentService.IsEnabled = en;
                        else if (key.Equals("MaxTokens", StringComparison.OrdinalIgnoreCase) && int.TryParse(val, out int mt)) currentService.MaxTokens = mt;
                        else if (key.Equals("Temperature", StringComparison.OrdinalIgnoreCase) && double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double temp)) currentService.Temperature = temp;
                        else if (key.Equals("SystemPrompt", StringComparison.OrdinalIgnoreCase)) currentService.SystemPrompt = val.Replace("\\n", "\n");
                        else currentService.ExtraParams[key] = val;
                    }
                }
            }

            // Order/filter services by [TranslationServices] indexed list or ActiveServices key
            var orderedNames = new List<string>();
            if (indexedServiceList.Count > 0)
            {
                orderedNames.AddRange(indexedServiceList);
            }
            else if (!string.IsNullOrWhiteSpace(GlobalSettings.ActiveServices))
            {
                orderedNames.AddRange(GlobalSettings.ActiveServices.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
            }

            if (orderedNames.Count > 0)
            {
                foreach (var sName in orderedNames)
                {
                    var matchKey = rawServicesDict.Keys.FirstOrDefault(k => string.Equals(k, sName, StringComparison.OrdinalIgnoreCase) || string.Equals(k, "Provider_" + sName, StringComparison.OrdinalIgnoreCase));
                    if (matchKey != null && rawServicesDict.TryGetValue(matchKey, out var serviceObj))
                    {
                        if (!ConfiguredServices.Contains(serviceObj))
                        {
                            ConfiguredServices.Add(serviceObj);
                        }
                    }
                }

                // Add any remaining provider sections
                foreach (var kvp in rawServicesDict)
                {
                    if (!ConfiguredServices.Contains(kvp.Value))
                    {
                        ConfiguredServices.Add(kvp.Value);
                    }
                }
            }
            // Deduplicate services by unique SectionName if legacy duplicate INI sections exist
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < ConfiguredServices.Count; i++)
            {
                var sObj = ConfiguredServices[i];
                string nameKey = sObj.SectionName;
                if (seenNames.Contains(nameKey))
                {
                    ConfiguredServices.RemoveAt(i);
                    i--;
                }
                else
                {
                    seenNames.Add(nameKey);
                }
            }
        }

        public static string GetTranslationConfigIniString()
        {
            GlobalSettings.ActiveServices = string.Join(",", ConfiguredServices.Select(s => s.SectionName));

            var sb = new StringBuilder();
            sb.AppendLine("; ==============================================================================");
            sb.AppendLine("; CSF Studio - Translation & AI Services Configuration");
            sb.AppendLine("; ==============================================================================");
            sb.AppendLine("; The [TranslationServices] section lists active services in menu display order.");
            sb.AppendLine("; ==============================================================================");
            sb.AppendLine();

            sb.AppendLine("[TranslationSettings]");
            sb.AppendLine($"DefaultSourceLanguage={GlobalSettings.DefaultSourceLanguage}");
            if (!string.IsNullOrEmpty(GlobalSettings.DefaultSystemPrompt))
            {
                sb.AppendLine($"DefaultSystemPrompt={GlobalSettings.DefaultSystemPrompt.Replace("\n", "\\n").Replace("\r", "")}");
            }
            sb.AppendLine($"BatchSize={GlobalSettings.BatchSize}");
            sb.AppendLine($"DelayBetweenBatchesMs={GlobalSettings.DelayBetweenBatchesMs}");
            sb.AppendLine();

            sb.AppendLine("[TranslationServices]");
            for (int i = 0; i < ConfiguredServices.Count; i++)
            {
                sb.AppendLine($"{i}={ConfiguredServices[i].SectionName}");
            }
            sb.AppendLine();

            foreach (var s in ConfiguredServices)
            {
                sb.AppendLine($"[{s.SectionName}]");
                sb.AppendLine($"DisplayName={s.DisplayName}");
                sb.AppendLine($"Type={s.ProviderType}");
                sb.AppendLine($"IsEnabled={(s.IsEnabled ? "true" : "false")}");

                if (!string.IsNullOrEmpty(s.ApiKey)) sb.AppendLine($"ApiKey={s.ApiKey}");
                if (!string.IsNullOrEmpty(s.Endpoint)) sb.AppendLine($"Endpoint={s.Endpoint}");
                if (!string.IsNullOrEmpty(s.Model)) sb.AppendLine($"Model={s.Model}");
                if (!string.IsNullOrEmpty(s.UrlTemplate)) sb.AppendLine($"UrlTemplate={s.UrlTemplate}");
                if (!string.IsNullOrEmpty(s.HttpMethod)) sb.AppendLine($"HttpMethod={s.HttpMethod}");
                if (!string.IsNullOrEmpty(s.UserAgent)) sb.AppendLine($"UserAgent={s.UserAgent}");
                if (s.MaxTokens != 1000) sb.AppendLine($"MaxTokens={s.MaxTokens}");
                if (Math.Abs(s.Temperature - 0.3) > 0.01) sb.AppendLine($"Temperature={s.Temperature.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                if (!string.IsNullOrEmpty(s.SystemPrompt)) sb.AppendLine($"SystemPrompt={s.SystemPrompt.Replace("\n", "\\n").Replace("\r", "")}");

                foreach (var kvp in s.ExtraParams)
                {
                    sb.AppendLine($"{kvp.Key}={kvp.Value}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public static void SaveConfig()
        {
            string filePath = ConfigFilePath;
            var nonTranslationLines = new List<string>();

            if (File.Exists(filePath))
            {
                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
                bool inTranslationSection = false;

                foreach (var rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        string sName = line.Substring(1, line.Length - 2).Trim();
                        if (sName.Equals("TranslationSettings", StringComparison.OrdinalIgnoreCase) ||
                            sName.Equals("TranslationServices", StringComparison.OrdinalIgnoreCase) ||
                            sName.Equals("Services", StringComparison.OrdinalIgnoreCase) ||
                            !KnownAppSections.Contains(sName))
                        {
                            inTranslationSection = true;
                        }
                        else
                        {
                            inTranslationSection = false;
                        }
                    }

                    if (!inTranslationSection)
                    {
                        nonTranslationLines.Add(rawLine);
                    }
                }
            }

            var sb = new StringBuilder();
            foreach (var l in nonTranslationLines)
            {
                sb.AppendLine(l);
            }

            if (nonTranslationLines.Count > 0 && !nonTranslationLines[nonTranslationLines.Count - 1].StartsWith("["))
            {
                sb.AppendLine();
            }

            sb.Append(GetTranslationConfigIniString());

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static void CreateDefaultServices()
        {
            if (ConfiguredServices.Count > 0) return;

            ConfiguredServices.Add(new TranslationServiceConfig
            {
                SectionName = "Google",
                DisplayName = "Google Translate",
                ProviderType = "GoogleWeb",
                UrlTemplate = "https://translate.googleapis.com/translate_a/single?client=gtx&sl={src}&tl={tgt}&dt=t&q={text}",
                HttpMethod = "GET",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                IsEnabled = true
            });

            ConfiguredServices.Add(new TranslationServiceConfig
            {
                SectionName = "MyMemory",
                DisplayName = "MyMemory Translate (Free)",
                ProviderType = "GoogleWeb",
                UrlTemplate = "https://api.mymemory.translated.net/get?q={text}&langpair={src}|{tgt}",
                HttpMethod = "GET",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                IsEnabled = true
            });

            ConfiguredServices.Add(new TranslationServiceConfig
            {
                SectionName = "Lingva",
                DisplayName = "Lingva Translate (Free)",
                ProviderType = "GoogleWeb",
                UrlTemplate = "https://lingva.ml/api/v1/{src}/{tgt}/{text}",
                HttpMethod = "GET",
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
                IsEnabled = false
            });

            ConfiguredServices.Add(new TranslationServiceConfig
            {
                SectionName = "DeepL",
                DisplayName = "DeepL API",
                ProviderType = "DeepL",
                ApiKey = "",
                Endpoint = "https://api-free.deepl.com/v2/translate",
                IsEnabled = false
            });

            ConfiguredServices.Add(new TranslationServiceConfig
            {
                SectionName = "Microsoft",
                DisplayName = "Microsoft Translator",
                ProviderType = "MicrosoftTranslator",
                Endpoint = "https://api.cognitive.microsofttranslator.com/translate?api-version=3.0",
                IsEnabled = false
            });

            ConfiguredServices.Add(new TranslationServiceConfig
            {
                SectionName = "LibreTranslate",
                DisplayName = "LibreTranslate",
                ProviderType = "OpenAICompatible",
                Endpoint = "https://libretranslate.com/translate",
                IsEnabled = false
            });

            ConfiguredServices.Add(new TranslationServiceConfig
            {
                SectionName = "OpenCodeGo",
                DisplayName = "[AI] OpenCode Go",
                ProviderType = "OpenAICompatible",
                Endpoint = "https://opencode.ai/zen/go/v1/chat/completions",
                ApiKey = "",
                Model = "",
                IsEnabled = false
            });

            ConfiguredServices.Add(new TranslationServiceConfig
            {
                SectionName = "OpenCodeZen",
                DisplayName = "[AI] OpenCode Zen",
                ProviderType = "OpenAICompatible",
                Endpoint = "https://opencode.ai/zen/v1/chat/completions",
                ApiKey = "",
                Model = "",
                IsEnabled = false
            });

            ConfiguredServices.Add(new TranslationServiceConfig
            {
                SectionName = "DeepSeek",
                DisplayName = "[AI] DeepSeek API",
                ProviderType = "OpenAICompatible",
                ApiKey = "",
                Endpoint = "https://api.deepseek.com/v1/chat/completions",
                Model = "",
                IsEnabled = false
            });

            ConfiguredServices.Add(new TranslationServiceConfig
            {
                SectionName = "OpenAI",
                DisplayName = "[AI] OpenAI API",
                ProviderType = "OpenAICompatible",
                ApiKey = "",
                Endpoint = "https://api.openai.com/v1/chat/completions",
                Model = "",
                IsEnabled = false
            });

            ConfiguredServices.Add(new TranslationServiceConfig
            {
                SectionName = "Anthropic",
                DisplayName = "[AI] Anthropic Claude API",
                ProviderType = "OpenAICompatible",
                ApiKey = "",
                Endpoint = "https://api.anthropic.com/v1/messages",
                Model = "",
                IsEnabled = false
            });

            ConfiguredServices.Add(new TranslationServiceConfig
            {
                SectionName = "Gemini",
                DisplayName = "[AI] Google Gemini API",
                ProviderType = "OpenAICompatible",
                ApiKey = "",
                Endpoint = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
                Model = "",
                IsEnabled = false
            });

            ConfiguredServices.Add(new TranslationServiceConfig
            {
                SectionName = "Groq",
                DisplayName = "[AI] Groq Cloud API",
                ProviderType = "OpenAICompatible",
                ApiKey = "",
                Endpoint = "https://api.groq.com/openai/v1/chat/completions",
                Model = "",
                IsEnabled = false
            });

            ConfiguredServices.Add(new TranslationServiceConfig
            {
                SectionName = "OpenRouter",
                DisplayName = "[AI] OpenRouter API",
                ProviderType = "OpenAICompatible",
                ApiKey = "",
                Endpoint = "https://openrouter.ai/api/v1/chat/completions",
                Model = "",
                IsEnabled = false
            });

            ConfiguredServices.Add(new TranslationServiceConfig
            {
                SectionName = "Mistral",
                DisplayName = "[AI] Mistral AI API",
                ProviderType = "OpenAICompatible",
                ApiKey = "",
                Endpoint = "https://api.mistral.ai/v1/chat/completions",
                Model = "",
                IsEnabled = false
            });

            ConfiguredServices.Add(new TranslationServiceConfig
            {
                SectionName = "Together",
                DisplayName = "[AI] Together AI API",
                ProviderType = "OpenAICompatible",
                ApiKey = "",
                Endpoint = "https://api.together.xyz/v1/chat/completions",
                Model = "",
                IsEnabled = false
            });

            ConfiguredServices.Add(new TranslationServiceConfig
            {
                SectionName = "Ollama",
                DisplayName = "[AI] Ollama Local",
                ProviderType = "OpenAICompatible",
                ApiKey = "",
                Endpoint = "http://localhost:11434/v1/chat/completions",
                Model = "",
                IsEnabled = false
            });

            ConfiguredServices.Add(new TranslationServiceConfig
            {
                SectionName = "LMStudio",
                DisplayName = "[AI] LM Studio Local",
                ProviderType = "OpenAICompatible",
                ApiKey = "",
                Endpoint = "http://localhost:1234/v1/chat/completions",
                Model = "",
                IsEnabled = false
            });

            GlobalSettings.ActiveServices = string.Join(",", ConfiguredServices.Select(s => s.SectionName));
        }
    }
}
