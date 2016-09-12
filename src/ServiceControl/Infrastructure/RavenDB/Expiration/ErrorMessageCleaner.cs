namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using Raven.Abstractions;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Client;
    using ServiceControl.Operations.BodyStorage;
    using System.Linq;

    public static class ErrorMessageCleaner
    {
        static NServiceBus.Logging.ILog logger = NServiceBus.Logging.LogManager.GetLogger(typeof(ErrorMessageCleaner));

        public static void Clean(int deletionBatchSize, IDocumentStore store, DateTime expiryThreshold, CancellationToken token, IMessageBodyStore messageBodyStore)
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

            var stopwatch = Stopwatch.StartNew();
            var items = new List<ICommandData>(deletionBatchSize);
            var attachments = new List<string>(deletionBatchSize);
            var indexName = new ExpiryErrorMessageIndex().IndexName;

            logger.Info("Starting clean-up of expired error documents.");

            var qResults = store.DatabaseCommands.Query(indexName, query);

            foreach (var doc in qResults.Results)
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

                attachments.Add(doc.Value<string>("ProcessingAttempts[0].MessageId"));
            }
            logger.Info($"Query for expired error documents took {stopwatch.ElapsedMilliseconds}ms.");

            if (attachments.Count > 0)
            {
                stopwatch.Restart();

                foreach (var bodyId in attachments)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        messageBodyStore.Delete(bodyId);
                    }
                    catch (Exception ex)
                    {
                        logger.Warn("Deletion of attachment failed.", ex);
                    }
                }

                logger.Info($"Deleted {attachments.Count} attachments for expired error documents in {stopwatch.ElapsedMilliseconds}ms.");
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            stopwatch.Restart();

            var deletionCount = 0;

            Chunker.ExecuteInChunks(items.Count, (s, e, t) =>
            {
                var results = store.DatabaseCommands.Batch(items.GetRange(s, e - s + 1));
                logger.Info($"Batching deletion of {t}/{items.Count} error documents completed.");

                deletionCount += results.Count(x => x.Deleted == true);
            }, token);

            if (deletionCount == 0)
            {
                logger.Info("No expired error documents found");
            }
            else
            {
                logger.Info($"Deleted {deletionCount} expired error documents in {stopwatch.ElapsedMilliseconds}ms.");
            }
        }
    }
}