//TODO: RAVEN5 Need rethinking
// namespace ServiceControl.Infrastructure.RavenDB.Expiration
// {
//     using System;
//     using System.Collections.Generic;
//     using System.Diagnostics;
//     using System.Linq;
//     using System.Threading;
//     using NServiceBus.Logging;
//     using Raven.Client.Documents.Commands.Batches;
//     using Raven.Client.Documents.Queries;
//     using Raven.Client.Util;
//     using ServiceControl.MessageFailures;
//     using ServiceControl.Recoverability;
//
//     static class ErrorMessageCleaner
//     {
//         public static void Clean(int deletionBatchSize, DocumentDatabase database, DateTime expiryThreshold, CancellationToken token)
//         {
//             var stopwatch = Stopwatch.StartNew();
//             var items = new List<ICommandData>(deletionBatchSize);
//             var attachments = new List<string>(deletionBatchSize);
//             var failedRetryItems = new List<ICommandData>(deletionBatchSize);
//
//             var itemsAndAttachements = new
//             {
//                 items,
//                 attachments,
//                 failedRetryItems
//             };
//
//             try
//             {
//                 var query = new IndexQuery
//                 {
//                     Start = 0,
//                     PageSize = deletionBatchSize,
//                     Cutoff = SystemTime.UtcNow,
//                     DisableCaching = true,
//                     Query = $"Status:[2 TO 4] AND LastModified:[* TO {expiryThreshold.Ticks}]",
//                     FieldsToFetch = new[]
//                     {
//                         "__document_id",
//                         "ProcessingAttempts[0].MessageId"
//                     },
//                     SortedFields = new[]
//                     {
//                         new SortedField("LastModified")
//                         {
//                             Field = "LastModified",
//                             Descending = false
//                         }
//                     }
//                 };
//
//                 var indexName = new ExpiryErrorMessageIndex().IndexName;
//                 database.Query(indexName, query, token,
//                     (doc, state) =>
//                     {
//                         var id = doc.Value<string>("__document_id");
//                         if (string.IsNullOrEmpty(id))
//                         {
//                             return;
//                         }
//
//                         var failedMessageRetryId = FailedMessageRetry.MakeDocumentId(FailedMessage.GetMessageIdFromDocumentId(id));
//                         state.failedRetryItems.Add(new DeleteCommandData
//                         {
//                             Key = failedMessageRetryId
//                         });
//
//                         state.items.Add(new DeleteCommandData
//                         {
//                             Key = id
//                         });
//                         var bodyid = doc.Value<string>("ProcessingAttempts[0].MessageId");
//                         state.attachments.Add(bodyid);
//                     }, itemsAndAttachements);
//             }
//             catch (OperationCanceledException)
//             {
//                 logger.Info("Cleanup operation cancelled");
//                 return;
//             }
//
//             if (token.IsCancellationRequested)
//             {
//                 return;
//             }
//
//             var deletedFailedMessageRetry = Chunker.ExecuteInChunks(failedRetryItems.Count, (itemsForBatch, db, s, e) =>
//             {
//                 if (logger.IsDebugEnabled)
//                 {
//                     logger.Debug($"Batching deletion of {s}-{e} FailedMessageRetry documents.");
//                 }
//
//                 var results = db.Batch(itemsForBatch.GetRange(s, e - s + 1), CancellationToken.None);
//                 if (logger.IsDebugEnabled)
//                 {
//                     logger.Debug($"Batching deletion of {s}-{e} FailedMessageRetry documents completed.");
//                 }
//
//                 return results.Count(x => x.Deleted == true);
//             }, failedRetryItems, database, token);
//
//             var deletedAttachments = Chunker.ExecuteInChunks(attachments.Count, (atts, db, s, e) =>
//             {
//                 var deleted = 0;
//                 if (logger.IsDebugEnabled)
//                 {
//                     logger.Debug($"Batching deletion of {s}-{e} attachment error documents.");
//                 }
//
//                 db.TransactionalStorage.Batch(accessor =>
//                 {
//                     for (var idx = s; idx <= e; idx++)
//                     {
//                         //We want to continue using attachments for now
// #pragma warning disable 618
//                         accessor.Attachments.DeleteAttachment("messagebodies/" + attachments[idx], null);
// #pragma warning restore 618
//                         deleted++;
//                     }
//                 });
//                 if (logger.IsDebugEnabled)
//                 {
//                     logger.Debug($"Batching deletion of {s}-{e} attachment error documents completed.");
//                 }
//
//                 return deleted;
//             }, attachments, database, token);
//
//             var deletedFailedMessage = Chunker.ExecuteInChunks(items.Count, (itemsForBatch, db, s, e) =>
//             {
//                 if (logger.IsDebugEnabled)
//                 {
//                     logger.Debug($"Batching deletion of {s}-{e} error documents.");
//                 }
//
//                 var results = db.Batch(itemsForBatch.GetRange(s, e - s + 1), CancellationToken.None);
//                 if (logger.IsDebugEnabled)
//                 {
//                     logger.Debug($"Batching deletion of {s}-{e} error documents completed.");
//                 }
//
//                 return results.Count(x => x.Deleted == true);
//             }, items, database, token);
//
//             if (deletedFailedMessage + deletedAttachments + deletedFailedMessageRetry == 0)
//             {
//                 logger.Info("No expired error documents found");
//             }
//             else
//             {
//                 logger.Info($"Deleted {deletedFailedMessage} expired error documents and {deletedAttachments} message body attachments. Batch execution took {stopwatch.ElapsedMilliseconds} ms");
//             }
//         }
//
//         static ILog logger = LogManager.GetLogger(typeof(ErrorMessageCleaner));
//     }
// }