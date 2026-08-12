using System;
using System.Collections.Generic;

namespace CsfStudio.Core.Translation
{
    public static class TranslationLanguageHelper
    {
        public static List<string> GetLanguageOptions()
        {
            return new List<string>
            {
                "English (US) [en]",
                "French [fr]",
                "German [de]",
                "Spanish [es]",
                "Italian [it]",
                "Russian [ru]",
                "Polish [pl]",
                "Japanese [ja]",
                "Korean [ko]",
                "Traditional Chinese [zh-Hant]",
                "Simplified Chinese [zh-Hans]"
            };
        }

        public static string GetDefaultSourceLanguage()
        {
            string value = TranslationConfigManager.GlobalSettings?.DefaultSourceLanguage;
            string normalized = Normalize(value);
            return string.IsNullOrEmpty(normalized) ? "en" : normalized;
        }

        public static string GetIsoCode(CsfStudio.Core.CsfLanguage? language)
        {
            if (!language.HasValue) return string.Empty;

            switch (language.Value)
            {
                case CsfStudio.Core.CsfLanguage.EnglishUS:
                case CsfStudio.Core.CsfLanguage.EnglishUK:
                    return "en";
                case CsfStudio.Core.CsfLanguage.French:
                    return "fr";
                case CsfStudio.Core.CsfLanguage.German:
                    return "de";
                case CsfStudio.Core.CsfLanguage.Spanish:
                    return "es";
                case CsfStudio.Core.CsfLanguage.Italian:
                    return "it";
                case CsfStudio.Core.CsfLanguage.Japanese:
                    return "ja";
                case CsfStudio.Core.CsfLanguage.Korean:
                    return "ko";
                case CsfStudio.Core.CsfLanguage.Chinese:
                    return "zh-CN";
                default:
                    return string.Empty;
            }
        }

        public static string Normalize(string rawLanguage)
        {
            if (string.IsNullOrWhiteSpace(rawLanguage)) return string.Empty;

            string value = rawLanguage.Trim();
            int start = value.IndexOf('[');
            int end = value.IndexOf(']');
            if (start >= 0 && end > start)
            {
                value = value.Substring(start + 1, end - start - 1).Trim();
            }

            string lower = value.ToLowerInvariant().Replace('_', '-');
            if (lower == "auto" || lower.Contains("auto-detect") || lower.Contains("autodetect")) return "auto";
            if (lower == "en" || lower == "eng" || lower.StartsWith("english")) return "en";
            if (lower == "fr" || lower == "fre" || lower == "fra" || lower.StartsWith("french")) return "fr";
            if (lower == "de" || lower == "ger" || lower == "deu" || lower.StartsWith("german")) return "de";
            if (lower == "es" || lower == "spa" || lower == "esp" || lower.StartsWith("spanish")) return "es";
            if (lower == "it" || lower == "ita" || lower.StartsWith("italian")) return "it";
            if (lower == "ru" || lower == "rus" || lower.StartsWith("russian")) return "ru";
            if (lower == "pl" || lower == "pol" || lower.StartsWith("polish")) return "pl";
            if (lower == "ja" || lower == "jap" || lower == "jpn" || lower.StartsWith("japanese")) return "ja";
            if (lower == "ko" || lower == "kor" || lower.StartsWith("korean")) return "ko";
            if (lower.Contains("zh-hant") || lower.Contains("traditional")) return "zh-Hant";
            if (lower.Contains("zh-hans") || lower.Contains("simplified")) return "zh-Hans";
            if (lower == "zh" || lower == "chi" || lower == "chn") return "zh-CN";

            return value;
        }

        public static string GetDisplayName(string isoCode)
        {
            string normalized = Normalize(isoCode);
            switch (normalized.ToLowerInvariant())
            {
                case "en": return "English (US) [en]";
                case "fr": return "French [fr]";
                case "de": return "German [de]";
                case "es": return "Spanish [es]";
                case "it": return "Italian [it]";
                case "ru": return "Russian [ru]";
                case "pl": return "Polish [pl]";
                case "ja": return "Japanese [ja]";
                case "ko": return "Korean [ko]";
                case "zh-hant": return "Traditional Chinese [zh-Hant]";
                case "zh-hans": return "Simplified Chinese [zh-Hans]";
                case "zh-cn": return "Simplified Chinese [zh-Hans]";
                case "auto": return "Auto-detect [auto]";
                default: return string.IsNullOrEmpty(normalized) ? string.Empty : normalized;
            }
        }
    }
}
