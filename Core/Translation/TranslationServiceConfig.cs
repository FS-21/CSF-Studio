using System;
using System.Collections.Generic;

namespace CsfStudio.Core.Translation
{
    public class TranslationServiceConfig
    {
        public string SectionName { get; set; }
        public string DisplayName { get; set; }
        public string ProviderType { get; set; } // "GoogleWeb", "OpenAICompatible", "DeepL"
        public string ApiKey { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string UrlTemplate { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = "GET";
        public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
        public int MaxTokens { get; set; } = 1000;
        public double Temperature { get; set; } = 0.3;
        public string SystemPrompt { get; set; } = string.Empty;
        public Dictionary<string, string> ExtraParams { get; set; } = new Dictionary<string, string>();

        public bool IsEnabled { get; set; } = true;

        public bool IsAiModel => ProviderType != null && ProviderType.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase);

        public override string ToString()
        {
            string display = string.IsNullOrWhiteSpace(DisplayName) ? SectionName : DisplayName;
            if (IsAiModel && !display.StartsWith("[AI]", StringComparison.OrdinalIgnoreCase))
            {
                display = "[AI] " + display;
            }
            if (!IsEnabled)
            {
                display += " 🚫 (Disabled)";
            }
            return display;
        }
    }

    public class GlobalTranslationSettings
    {
        public string DefaultSourceLanguage { get; set; } = "en";
        public string DefaultSystemPrompt { get; set; } = "You are an expert game localizer for Command & Conquer: Red Alert 2. Translate string table values accurately while preserving military tone, conciseness, and brevity. Keep standard gaming and technical acronyms (such as UI, HUD, GUI, HP, XP, AI, FPS) intact without expanding them into long words. NEVER alter or translate formatting tags like \\n or variables like {0}.";
        public int BatchSize { get; set; } = 25;
        public int DelayBetweenBatchesMs { get; set; } = 300;
        public string ActiveServices { get; set; } = string.Empty;
    }
}
