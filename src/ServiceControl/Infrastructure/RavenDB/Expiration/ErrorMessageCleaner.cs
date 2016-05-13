namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using System.Diagnostics;
    using System.Threading;
    using Raven.Abstractions;
    using Raven.Abstractions.Data;
    using Raven.Client;

    public static class ErrorMessageCleaner
    {
        static NServiceBus.Logging.ILog logger = NServiceBus.Logging.LogManager.GetLogger(typeof(ErrorMessageCleaner));

        public static void Clean(int deletionBatchSize, IDocumentStore store, DateTime expiryThreshold, CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();
            var query = new IndexQuery
            {
                Start = 0,
                PageSize = deletionBatchSize,
                Cutoff = SystemTime.UtcNow,
                DisableCaching = true,
                Query = $"Status:[2 TO 4] AND LastModified:[* TO {expiryThreshold.Ticks}]",
                FieldsToFetch = new[]
                {
                    "__document_id",
                    "MessageId"
                },
                SortedFields = new[]
                {
                    new SortedField("LastModified")
                    {
                        Field = "LastModified",
                        Descending = false
                    }
                }
            };
            var indexName = new ExpiryErrorMessageIndex().IndexName;
            QueryHeaderInformation _;
            var deletionCount = 0;

            logger.Info("Batching deletion of error documents.");

            using (var ie = store.DatabaseCommands.StreamQuery(indexName, query, out _))
            {
                while (ie.MoveNext())
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    var doc = ie.Current;
                    var id = doc.Value<string>("__document_id");
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    store.DatabaseCommands.Delete(id, null);
                    deletionCount++;
                    try
                    {
                        store.DatabaseCommands.DeleteAttachment("messagebodies/" + doc.Value<string>("MessageId"), null);
                    }
                    catch (Exception ex)
                    {
                        logger.Warn("Deletion of attachment failed.", ex);
                    }

                    if (deletionCount >= deletionBatchSize)
                    {
                        break;
                    }
                }
            }

            logger.Info("Batching deletion of error documents completed.");

            if (deletionCount == 0)
            {
                logger.Info("No expired error documents found");
            }
            else
            {
                logger.InfoFormat("Deleted {0} expired error documents. Batch execution took {1}ms", deletionCount, stopwatch.ElapsedMilliseconds);
            }
        }
    }
}