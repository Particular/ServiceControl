namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using NServiceBus.Logging;
    using Raven.Abstractions;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Database;

    static class ErrorMessageCleaner
    {
        public static void Clean(int deletionBatchSize, DocumentDatabase database, DateTime expiryThreshold, CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();
            var items = new List<ICommandData>(deletionBatchSize);
            var attachments = new List<string>(deletionBatchSize);
            var itemsAndAttachements = Tuple.Create(items, attachments);

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
                database.Query(indexName, query, token,
                    (doc, state) =>
                    {
                        var id = doc.Value<string>("__document_id");
                        if (string.IsNullOrEmpty(id))
                        {
                            return;
                        }

                        state.Item1.Add(new DeleteCommandData
                        {
                            Key = id
                        });
                        var bodyid = doc.Value<string>("ProcessingAttempts[0].MessageId");
                        state.Item2.Add(bodyid);
                    }, itemsAndAttachements);
            }
            catch (OperationCanceledException)
            {
                logger.Info("Cleanup operation cancelled");
                return;
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            var deletedFailedMessage = Chunker.ExecuteInChunks(items.Count, (itemsForBatch, db, s, e) =>
            {
                if (logger.IsDebugEnabled)
                {
                    logger.Debug($"Batching deletion of {s}-{e} error documents.");
                }

                var results = db.Batch(itemsForBatch.GetRange(s, e - s + 1), CancellationToken.None);
                if (logger.IsDebugEnabled)
                {
                    logger.Debug($"Batching deletion of {s}-{e} error documents completed.");
                }

                return results.Count(x => x.Deleted == true);
            }, items, database, token);

            var deletedAttachments = Chunker.ExecuteInChunks(attachments.Count, (atts, db, s, e) =>
            {
                if (logger.IsDebugEnabled)
                {
                    logger.Debug($"Batching deletion of {s}-{e} attachment error documents.");
                }

                db.TransactionalStorage.Batch(accessor =>
                {
                    for (var idx = s; idx <= e; idx++)
                    {
                        //We want to continue using attachments for now
#pragma warning disable 618
                        accessor.Attachments.DeleteAttachment("messagebodies/" + attachments[idx], null);
#pragma warning restore 618
                    }
                });
                if (logger.IsDebugEnabled)
                {
                    logger.Debug($"Batching deletion of {s}-{e} attachment error documents completed.");
                }

                return 0;
            }, attachments, database, token);

            if (deletedFailedMessage + deletedAttachments == 0)
            {
                logger.Info("No expired error documents found");
            }
            else
            {
                logger.Info($"Deleted {deletedFailedMessage} expired error documents and {deletedAttachments} message body attachments. Batch execution took {stopwatch.ElapsedMilliseconds} ms");
            }
        }

        static ILog logger = LogManager.GetLogger(typeof(ErrorMessageCleaner));
    }
}