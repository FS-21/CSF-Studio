using System;

namespace CsfStudio.Core.Translation
{
    public static class TranslationProviderFactory
    {
        public static ITranslationProvider CreateProvider(TranslationServiceConfig config)
        {
            if (config == null) return null;

            string type = config.ProviderType ?? "";
            if (type.Equals("GoogleWeb", StringComparison.OrdinalIgnoreCase))
            {
                return new GoogleWebTranslationProvider(config);
            }
            else if (type.Equals("DeepL", StringComparison.OrdinalIgnoreCase))
            {
                return new DeepLTranslationProvider(config);
            }
            else if (type.Equals("MicrosoftTranslator", StringComparison.OrdinalIgnoreCase))
            {
                return new MicrosoftTranslatorProvider(config);
            }
            else
            {
                // Default to OpenAICompatible for AI models and custom LLMs
                return new OpenAICompatibleTranslationProvider(config);
            }
        }
    }
}
