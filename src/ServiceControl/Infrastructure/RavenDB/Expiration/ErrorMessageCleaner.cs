namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Raven.Abstractions;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Client;

    public static class ErrorMessageCleaner
    {
        static NServiceBus.Logging.ILog logger = NServiceBus.Logging.LogManager.GetLogger(typeof(ErrorMessageCleaner));

        public static void Clean(int deletionBatchSize, IDocumentStore store, DateTime expiryThreshold)
        {
            var stopwatch = Stopwatch.StartNew();
            var items = new List<ICommandData>(deletionBatchSize);
            var attachments = new List<string>(deletionBatchSize);
            var query = new IndexQuery
            {
                Start = 0,
                PageSize = deletionBatchSize,
                Cutoff = SystemTime.UtcNow,
                DisableCaching = true,
                Query = string.Format("Status:[2 TO 4] AND LastModified:[* TO {0}]", expiryThreshold.Ticks),
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
            using (var ie = store.DatabaseCommands.StreamQuery(indexName, query, out _))
            {
                while (ie.MoveNext())
                {
                    var doc = ie.Current;
                    var id = doc.Value<string>("__document_id");
                    if (string.IsNullOrEmpty(id))
                    {
                        continue;
                    }

                    items.Add(new DeleteCommandData
                    {
                        Key = id
                    });

                    attachments.Add(doc.Value<string>("MessageId"));
                }
            }

            var deletionCount = 0;

            Chunker.ExecuteInChunks(items.Count, (s, e) =>
            {
                logger.InfoFormat("Batching deletion of {0}-{1} error documents.", s, e);
                var results = store.DatabaseCommands.Batch(items.GetRange(s, e - s + 1));
                logger.InfoFormat("Batching deletion of {0}-{1} error documents completed.", s, e);

                deletionCount += results.Count(x => x.Deleted == true);
            });

            logger.InfoFormat("Deletion of {0}-{1} attachment error documents.", 0, attachments.Count);
            try
            {
                Parallel.ForEach(attachments, attach =>
                {
                    store.DatabaseCommands.DeleteAttachment("messagebodies/" + attach, null);
                });
            }
            catch (AggregateException ex)
            {
                logger.Warn("Deletion of attachments failed", ex);
            }
            logger.InfoFormat("Deletion of {0}-{1} attachment error documents completed.", 0, attachments.Count);

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