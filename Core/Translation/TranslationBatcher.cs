using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CsfStudio.Core.Translation
{
    public static class TranslationBatcher
    {
        public static async Task<TranslationBatchResult> ExecuteBatchTranslationAsync(
            ITranslationProvider provider,
            List<TranslationItem> allItems,
            string sourceLang,
            string targetLang,
            Action<int, int> progressCallback,
            CancellationToken cancellationToken)
        {
            var grandResult = new TranslationBatchResult { Success = true, Items = allItems };

            if (allItems == null || allItems.Count == 0) return grandResult;

            int batchSize = TranslationConfigManager.GlobalSettings.BatchSize;
            if (batchSize <= 0) batchSize = 25;
            int delayMs = TranslationConfigManager.GlobalSettings.DelayBetweenBatchesMs;

            int processedCount = 0;
            int totalCount = allItems.Count;

            for (int i = 0; i < totalCount; i += batchSize)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    grandResult.Success = false;
                    grandResult.ErrorMessage = "Translation cancelled by user.";
                    break;
                }

                int count = Math.Min(batchSize, totalCount - i);
                var chunk = allItems.GetRange(i, count);

                var chunkResult = await provider.TranslateBatchAsync(chunk, sourceLang, targetLang, cancellationToken);

                if (!chunkResult.Success)
                {
                    grandResult.Success = false;
                    grandResult.ErrorMessage = chunkResult.ErrorMessage;
                    if (chunkResult.QuotaExceeded)
                    {
                        grandResult.QuotaExceeded = true;
                    }
                    break;
                }

                processedCount += count;
                progressCallback?.Invoke(processedCount, totalCount);

                if (delayMs > 0 && i + batchSize < totalCount)
                {
                    await Task.Delay(delayMs, cancellationToken);
                }
            }

            return grandResult;
        }
    }
}
