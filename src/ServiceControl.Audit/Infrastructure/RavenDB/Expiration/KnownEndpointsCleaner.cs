// TODO: RAVEN5 - Not dealing with Expiration yet
//namespace ServiceControl.Audit.Infrastructure.RavenDB.Expiration
//{
//    using System;
//    using System.Collections.Generic;
//    using System.Diagnostics;
//    using System.Linq;
//    using System.Threading;
//    using NServiceBus.Logging;
//    using Raven.Abstractions;
//    using Raven.Abstractions.Commands;
//    using Raven.Abstractions.Data;
//    using Raven.Client.Documents.Commands.Batches;
//    using Raven.Client.Documents.Queries;
//    using Raven.Client.Util;
//    using Raven.Database;

//    static class KnownEndpointsCleaner
//    {
//        public static void Clean(int deletionBatchSize, DocumentDatabase database, DateTime expiryThreshold, CancellationToken token)
//        {
//            var stopwatch = Stopwatch.StartNew();
//            var items = new List<ICommandData>(deletionBatchSize);

//            try
//            {
//                var query = new IndexQuery
//                {
//                    Start = 0,
//                    PageSize = deletionBatchSize,
//                    Cutoff = SystemTime.UtcNow,
//                    DisableCaching = true,
//                    Query = $"LastSeen:[* TO {expiryThreshold.Ticks}]",
//                    FieldsToFetch = new[]
//                    {
//                        "__document_id"
//                    },
//                    SortedFields = new[]
//                    {
//                        new SortedField("LastSeen")
//                        {
//                            Field = "LastSeen",
//                            Descending = false
//                        }
//                    }
//                };

//                var indexName = new ExpiryKnownEndpointsIndex().IndexName;
//                database.Query(indexName, query, token,
//                    (doc, state) =>
//                    {
//                        var id = doc.Value<string>("__document_id");
//                        if (string.IsNullOrEmpty(id))
//                        {
//                            return;
//                        }

//                        state.Add(new DeleteCommandData
//                        {
//                            Key = id
//                        });
//                    }, items);
//            }
//            catch (OperationCanceledException)
//            {
//                logger.Info("KnownEndpoints Cleanup operation cancelled");
//                return;
//            }

//            if (token.IsCancellationRequested)
//            {
//                return;
//            }

//            var deleteKnownEndpointDocuments = Chunker.ExecuteInChunks(items.Count, (itemsForBatch, db, s, e) =>
//            {
//                if (logger.IsDebugEnabled)
//                {
//                    logger.Debug($"Batching deletion of {s}-{e} known endpoint documents.");
//                }

//                var results = db.Batch(itemsForBatch.GetRange(s, e - s + 1), CancellationToken.None);
//                if (logger.IsDebugEnabled)
//                {
//                    logger.Debug($"Batching deletion of {s}-{e} known endpoint documents completed.");
//                }

//                return results.Count(x => x.Deleted == true);
//            }, items, database, token);


//            if (deleteKnownEndpointDocuments == 0)
//            {
//                logger.Info("No expired known endpoints documents found");
//            }
//            else
//            {
//                logger.Info($"Deleted {deleteKnownEndpointDocuments} expired known endpoint documents. Batch execution took {stopwatch.ElapsedMilliseconds} ms");
//            }
//        }

//        static ILog logger = LogManager.GetLogger(typeof(KnownEndpointsCleaner));
//    }
//}