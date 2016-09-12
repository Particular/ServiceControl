namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Raven.Abstractions;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Database;

    public static class ErrorMessageCleaner
    {
        static NServiceBus.Logging.ILog logger = NServiceBus.Logging.LogManager.GetLogger(typeof(ErrorMessageCleaner));

        public static void Clean(int deletionBatchSize, DocumentDatabase database, DateTime expiryThreshold)
        {
            var stopwatch = Stopwatch.StartNew();
            var items = new List<ICommandData>(deletionBatchSize);
            var attachments = new List<string>(deletionBatchSize);
            try
            {
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
                        "ProcessingAttempts[0].MessageId"
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
                database.Query(indexName, query, database.WorkContext.CancellationToken,
                    null,
                    doc =>
                    {
                        var id = doc.Value<string>("__document_id");
                        if (string.IsNullOrEmpty(id))
                        {
                            return;
                        }

                        items.Add(new DeleteCommandData
                        {
                            Key = id
                        });
                        var bodyid = doc.Value<string>("ProcessingAttempts[0].MessageId");
                        attachments.Add(bodyid);
                    });
            }
            catch (OperationCanceledException)
            {
                //Ignore
            }

            var deletionCount = 0;

            Chunker.ExecuteInChunks(items.Count, (s, e) =>
            {
                logger.InfoFormat("Batching deletion of {0}-{1} error documents.", s, e);
                var results = database.Batch(items.GetRange(s, e - s + 1));
                logger.InfoFormat("Batching deletion of {0}-{1} error documents completed.", s, e);

                deletionCount += results.Count(x => x.Deleted == true);
            });

            Chunker.ExecuteInChunks(attachments.Count, (s, e) =>
            {
                database.TransactionalStorage.Batch(accessor =>
                {
                    logger.InfoFormat("Batching deletion of {0}-{1} attachment error documents.", s, e);
                    for (var idx = s; idx <= e; idx++)
                    {
                        accessor.Attachments.DeleteAttachment("messagebodies/" + attachments[idx], null);
                    }
                    logger.InfoFormat("Batching deletion of {0}-{1} attachment error documents completed.", s, e);
                });
            });

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