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
                LanguageManager.GetString("LangName.En", "English (US) [en]"),
                LanguageManager.GetString("LangName.Fr", "French [fr]"),
                LanguageManager.GetString("LangName.De", "German [de]"),
                LanguageManager.GetString("LangName.Es", "Spanish [es]"),
                LanguageManager.GetString("LangName.It", "Italian [it]"),
                LanguageManager.GetString("LangName.Ru", "Russian [ru]"),
                LanguageManager.GetString("LangName.Pl", "Polish [pl]"),
                LanguageManager.GetString("LangName.Ja", "Japanese [ja]"),
                LanguageManager.GetString("LangName.Ko", "Korean [ko]"),
                LanguageManager.GetString("LangName.ZhHant", "Traditional Chinese [zh-Hant]"),
                LanguageManager.GetString("LangName.ZhHans", "Simplified Chinese [zh-Hans]")
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
            else
            {
                start = value.IndexOf('(');
                end = value.IndexOf(')');
                if (start >= 0 && end > start)
                {
                    string inside = value.Substring(start + 1, end - start - 1).Trim();
                    if (inside.Length == 2 || inside.Length == 5)
                    {
                        value = inside;
                    }
                }
            }

            string lower = value.ToLowerInvariant().Replace('_', '-');
            if (lower == "auto" || lower.Contains("auto-detect") || lower.Contains("autodetect")) return "auto";
            if (lower == "en" || lower == "eng" || lower.StartsWith("english") || lower.StartsWith("ingl") || lower.StartsWith("engl") || lower.StartsWith("anglais")) return "en";
            if (lower == "fr" || lower == "fre" || lower == "fra" || lower.StartsWith("french") || lower.StartsWith("franc") || lower.StartsWith("franz")) return "fr";
            if (lower == "de" || lower == "ger" || lower == "deu" || lower.StartsWith("german") || lower.StartsWith("deutsch") || lower.StartsWith("alem") || lower.StartsWith("allem")) return "de";
            if (lower == "es" || lower == "spa" || lower == "esp" || lower.StartsWith("spanish") || lower.StartsWith("espa") || lower.StartsWith("spanis")) return "es";
            if (lower == "it" || lower == "ita" || lower.StartsWith("italian") || lower.StartsWith("italien")) return "it";
            if (lower == "ru" || lower == "rus" || lower.StartsWith("russian") || lower.StartsWith("rus") || lower.StartsWith("russ")) return "ru";
            if (lower == "pl" || lower == "pol" || lower.StartsWith("polish") || lower.StartsWith("polac") || lower.StartsWith("poln")) return "pl";
            if (lower == "ja" || lower == "jap" || lower == "jpn" || lower.StartsWith("japan") || lower.StartsWith("japon")) return "ja";
            if (lower == "ko" || lower == "kor" || lower.StartsWith("korean") || lower.StartsWith("corean") || lower.StartsWith("korean")) return "ko";
            if (lower.Contains("zh-hant") || lower.Contains("traditional") || lower.Contains("tradicional")) return "zh-Hant";
            if (lower.Contains("zh-hans") || lower.Contains("simplified") || lower.Contains("simplificado")) return "zh-Hans";
            if (lower == "zh" || lower == "chi" || lower == "chn" || lower.StartsWith("chin")) return "zh-CN";

            return value;
        }

        public static string GetDisplayName(string isoCode)
        {
            string normalized = Normalize(isoCode);
            switch (normalized.ToLowerInvariant())
            {
                case "en": return LanguageManager.GetString("LangName.En", "English (US) [en]");
                case "fr": return LanguageManager.GetString("LangName.Fr", "French [fr]");
                case "de": return LanguageManager.GetString("LangName.De", "German [de]");
                case "es": return LanguageManager.GetString("LangName.Es", "Spanish [es]");
                case "it": return LanguageManager.GetString("LangName.It", "Italian [it]");
                case "ru": return LanguageManager.GetString("LangName.Ru", "Russian [ru]");
                case "pl": return LanguageManager.GetString("LangName.Pl", "Polish [pl]");
                case "ja": return LanguageManager.GetString("LangName.Ja", "Japanese [ja]");
                case "ko": return LanguageManager.GetString("LangName.Ko", "Korean [ko]");
                case "zh-hant": return LanguageManager.GetString("LangName.ZhHant", "Traditional Chinese [zh-Hant]");
                case "zh-hans": return LanguageManager.GetString("LangName.ZhHans", "Simplified Chinese [zh-Hans]");
                case "zh-cn": return LanguageManager.GetString("LangName.ZhHans", "Simplified Chinese [zh-Hans]");
                case "auto": return LanguageManager.GetString("LangName.AutoDetect", "Auto-detect [auto]");
                default: return string.IsNullOrEmpty(normalized) ? string.Empty : normalized;
            }
        }
    }
}
