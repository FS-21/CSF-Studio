using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CsfStudio.Core.Translation
{
    public class TranslationItem
    {
        public string Key { get; set; }
        public string SourceText { get; set; }
        public string ExtraWav { get; set; }
        public string TranslatedText { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class TranslationBatchResult
    {
        public bool Success { get; set; }
        public bool QuotaExceeded { get; set; }
        public string ErrorMessage { get; set; }
        public List<TranslationItem> Items { get; set; } = new List<TranslationItem>();
    }

    public interface ITranslationProvider
    {
        TranslationServiceConfig Config { get; }
        Task<TranslationBatchResult> TranslateBatchAsync(List<TranslationItem> items, string sourceLang, string targetLang, CancellationToken cancellationToken);
    }
}
