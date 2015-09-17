namespace ServiceControl.Infrastructure.RavenDB.Expiration
{

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using Raven.Abstractions;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Logging;
    using Raven.Database;
    using Raven.Database.Impl;
    using ServiceBus.Management.Infrastructure.Settings;


    public class ExpiredDocumentsCleaner
    {
        static ILog logger = LogManager.GetLogger(typeof(ExpiredDocumentsCleaner));
        static string indexName = new ExpiryProcessedMessageIndex().IndexName;

        public static void RunCleanup(int deletionBatchSize, DocumentDatabase database)
        {
            var hoursToKeep = Settings.HoursToKeepMessagesBeforeExpiring;
            var expiryThreshold = SystemTime.UtcNow.AddHours(-hoursToKeep);

            logger.Debug("Trying to find expired documents to delete (with threshold {0})", expiryThreshold.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture));
           
            try
            {
                InnerCleanup(deletionBatchSize, database, expiryThreshold);
            }
            catch (Exception e)
            {
                logger.ErrorException("Error when trying to find expired documents", e);
            }
        }

        public static void InnerCleanup(int deletionBatchSize, DocumentDatabase database, DateTime expiryThreshold)
        {
            var query = new IndexQuery
            {
                Start = 0,
                PageSize = deletionBatchSize,
                Cutoff = DateTime.UtcNow,
                FieldsToFetch = new[]
                {
                    "__document_id",
                    "LastModified",
                    "MessageMetadata"
                },
                SortedFields = new[]
                {
                    new SortedField("LastModified")
                    {
                        Field = "LastModified",
                        Descending = false
                    }
                },
            };

            var docsToExpire = 0;
            // we may be receiving a LOT of documents to delete, so we are going to skip
            // the cache for that, to avoid filling it up very quickly
            var stopwatch = Stopwatch.StartNew();
            int deletionCount;
            using (DocumentCacher.SkipSettingDocumentsInDocumentCache())
            using (database.DisableAllTriggersForCurrentThread())
            using (var cts = new CancellationTokenSource())
            {
                var documentWithCurrentThresholdTimeReached = false;
                var items = new List<ICommandData>(deletionBatchSize);
                var attachments = new List<string>(deletionBatchSize);
                try
                {
                    database.Query(indexName, query, CancellationTokenSource.CreateLinkedTokenSource(database.WorkContext.CancellationToken, cts.Token).Token,
                        null,
                        doc =>
                        {
                            if (documentWithCurrentThresholdTimeReached)
                            {
                                return;
                            }

                            if (doc.Value<DateTime>("LastModified") >= expiryThreshold)
                            {
                                documentWithCurrentThresholdTimeReached = true;
                                cts.Cancel();
                                return;
                            }

                            var id = doc.Value<string>("__document_id");
                            if (string.IsNullOrEmpty(id))
                            {
                                return;
                            }
                            items.Add(new DeleteCommandData
                            {
                                Key = id
                            });

                            var bodyNotStored = doc.SelectToken("MessageMetadata.BodyNotStored", errorWhenNoMatch: false);
                            if (bodyNotStored == null || bodyNotStored.Value<bool>() == false)
                            {
                                var msgId = doc.SelectToken("MessageMetadata.MessageId", errorWhenNoMatch: false);
                                if (msgId != null)
                                {
                                    var attachmentId = "messagebodies/" + msgId.Value<string>();
                                    attachments.Add(attachmentId);
                                }
                            }
                        });
                }
                catch (OperationCanceledException)
                {
                    //Ignore
                }

                logger.Debug("Batching deletion of {0} documents.", items.Count);

                docsToExpire += items.Count;
                var results = database.Batch(items.ToArray());
                database.TransactionalStorage.Batch(accessor =>
                {
                    foreach (var attach in attachments)
                    {
                        accessor.Attachments.DeleteAttachment(attach, null);
                    }
                });
                deletionCount = results.Count(x => x.Deleted == true);
                items.Clear();
            }

            if (docsToExpire == 0)
            {
                logger.Debug("No expired documents found");
            }
            else
            {
                logger.Debug("Deleted {0} out of {1} expired documents batch - Execution time:{2}ms", deletionCount, docsToExpire, stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
