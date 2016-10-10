namespace ServiceControl.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using Raven.Abstractions;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Extensions;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.Operations.Audit;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Operations.Error;

    public class ExpiredDocumentsCleaner : Feature
    {
        public ExpiredDocumentsCleaner()
        {
            EnableByDefault();
            Prerequisite(c => c.Settings.Get<Settings>("ServiceControl.Settings").ExpirationProcessTimerInSeconds > 0, "Expiration disabled");
            RegisterStartupTask<DeleteByIndexCleaner>();
            RegisterStartupTask<DeleteRavenAttachments>();
            RegisterStartupTask<MessageBodyStoreCleaner>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
        }

        private class DeleteRavenAttachments : FeatureStartupTask
        {
            private const string IDPrefix = "messagebodies/";
            private readonly IDocumentStore store;
            private readonly IMessageBodyStore messageBodyStore;

            private readonly TimeKeeper timeKeeper;
            private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            private readonly ILog logger = LogManager.GetLogger(typeof(DeleteRavenAttachments));
            private Timer timer;
            private AuditMessageBodyStoragePolicy auditMessageBodyStoragePolicy;
            private ErrorMessageBodyStoragePolicy errorMessageBodyStoragePolicy;

            public DeleteRavenAttachments(TimeKeeper timeKeeper, IDocumentStore store, IMessageBodyStore messageBodyStore, Settings settings)
            {
                this.timeKeeper = timeKeeper;
                this.store = store;
                this.messageBodyStore = messageBodyStore;

                auditMessageBodyStoragePolicy = new AuditMessageBodyStoragePolicy(settings);
                errorMessageBodyStoragePolicy = new ErrorMessageBodyStoragePolicy(settings);
            }

            protected override void OnStart()
            {
                var due = TimeSpan.FromHours(1);
                timer = timeKeeper.New(() =>
                {
                    logger.Info("Moving of legacy attachments to disk.");

                    try
                    {
                        var total = 0;
                        var stopWatch = Stopwatch.StartNew();

                        foreach (var head in store.DatabaseCommands.GetAttachmentHeadersStartingWith(IDPrefix, 0, 1024))
                        {
                            var attachment = store.DatabaseCommands.GetAttachment(head.Key);
                            var contentType = attachment.Metadata["ContentType"].Value<string>();
                            var bodySize = attachment.Metadata["ContentLength"].Value<int>();
                            var messageId = head.Key.Substring(IDPrefix.Length);
                            var messageBody = attachment.Data().ReadData();

                            try
                            {
                                bool saveToAudit;
                                using (var session = store.OpenSession())
                                {
                                    var saveToError = session.Query<MessagesViewIndex.SortAndFilterOptions, FailedMessageViewIndex>().Count(options => options.MessageId == messageId) > 0;

                                    if (!saveToError)
                                    {
                                        // Save attachment to audit using messageid
                                        messageBodyStore.Store(BodyStorageTags.Audit, messageBody, new MessageBodyMetadata(messageId, contentType, bodySize), auditMessageBodyStoragePolicy);
                                        store.DatabaseCommands.DeleteAttachment(head.Key, null);
                                        continue;
                                    }

                                    QueryHeaderInformation info;
                                    var indexQuery = new IndexQuery
                                    {
                                        Cutoff = SystemTime.UtcNow,
                                        DisableCaching = true,
                                        Query = $"MessageId:\"{messageId}\"",
                                        FieldsToFetch = new[]
                                        {
                                            "Id", // == UniqueMessageId
                                            "Status"
                                        },
                                        ResultsTransformer = new FailedMessageViewTransformer().TransformerName
                                    };
                                    using (var ie = store.DatabaseCommands.StreamQuery(new FailedMessageViewIndex().IndexName, indexQuery, out info))
                                    {
                                        while (ie.MoveNext())
                                        {
                                            var doc = ie.Current;
                                            var status = (FailedMessageStatus)doc.Value<int>("Status");
                                            if (status == FailedMessageStatus.Archived || status == FailedMessageStatus.Resolved)
                                            {
                                                // Save attachment to error transient using failure.UniqueMessageId
                                                messageBodyStore.Store(BodyStorageTags.ErrorTransient, messageBody, new MessageBodyMetadata(doc.Value<string>("Id"), contentType, bodySize), errorMessageBodyStoragePolicy);
                                            }
                                            else
                                            {
                                                // Save attachment to error permanent using failure.UniqueMessageId
                                                messageBodyStore.Store(BodyStorageTags.ErrorPersistent, messageBody, new MessageBodyMetadata(doc.Value<string>("Id"), contentType, bodySize), errorMessageBodyStoragePolicy);
                                            }
                                        }
                                    }

                                    saveToAudit = session.Query<MessagesViewIndex.SortAndFilterOptions, MessagesViewIndex>().Count(options => options.MessageId == messageId) > info.TotalResults;
                                }

                                if (saveToAudit)
                                {
                                    // Save attachment to audit using messageid
                                    messageBodyStore.Store(BodyStorageTags.Audit, messageBody, new MessageBodyMetadata(messageId, contentType, bodySize), auditMessageBodyStoragePolicy);
                                }

                                store.DatabaseCommands.DeleteAttachment(head.Key, null);
                            }
                            finally
                            {
                                total++;
                            }
                        }

                        logger.Info($"Moved {total} legacy attachments to disk in {stopWatch.ElapsedMilliseconds}ms.");
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Moving of legacy attachments to disk failed.", ex);
                    }
                }, due, due);
            }

            protected override void OnStop()
            {
                cancellationTokenSource.Cancel();

                timeKeeper.Release(timer);
            }
        }

        private class MessageBodyStoreCleaner : FeatureStartupTask
        {
            private readonly IMessageBodyStore messageBodyStore;
            private readonly Settings settings;
            private readonly TimeKeeper timeKeeper;
            private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            private readonly ILog logger = LogManager.GetLogger(typeof(MessageBodyStoreCleaner));
            private Timer timer;

            public MessageBodyStoreCleaner(TimeKeeper timeKeeper, Settings settings, IMessageBodyStore messageBodyStore)
            {
                this.timeKeeper = timeKeeper;
                this.settings = settings;
                this.messageBodyStore = messageBodyStore;
            }

            protected override void OnStart()
            {
                var deleteFrequencyInSeconds = settings.ExpirationProcessTimerInSeconds;

                var due = TimeSpan.FromSeconds(deleteFrequencyInSeconds);
                timer = timeKeeper.New(() =>
                {
                    logger.Info("Deleting expired message bodies from disk.");

                    var threshold = DateTime.UtcNow.Add(-settings.AuditRetentionPeriod);
                    var stopWatch = Stopwatch.StartNew();
                    int total;

                    try
                    {
                        total = messageBodyStore.PurgeExpired(BodyStorageTags.Audit, threshold);
                        logger.Info($"Deleted {total} expired audit message bodies from disk in {stopWatch.ElapsedMilliseconds}ms.");
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Deletion of audit expired message bodies failed.", ex);
                    }

                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        return;
                    }

                    try
                    {
                        stopWatch.Restart();
                        total = messageBodyStore.PurgeExpired(BodyStorageTags.ErrorTransient, threshold);
                        logger.Info($"Deleted {total} expired error message bodies from disk in {stopWatch.ElapsedMilliseconds}ms.");
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Deletion of error expired message bodies failed.", ex);
                    }
                }, due, due);
            }

            protected override void OnStop()
            {
                cancellationTokenSource.Cancel();

                timeKeeper.Release(timer);
            }
        }

        private class DeleteByIndexCleaner : FeatureStartupTask
        {
            private readonly Settings settings;
            private readonly IDocumentStore store;
            private readonly TimeKeeper timeKeeper;
            private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            private readonly ILog logger = LogManager.GetLogger(typeof(DeleteByIndexCleaner));
            private Timer timer;

            public DeleteByIndexCleaner(TimeKeeper timeKeeper, IDocumentStore store, Settings settings)
            {
                this.timeKeeper = timeKeeper;
                this.store = store;
                this.settings = settings;
            }

            protected override void OnStart()
            {
                var deleteFrequencyInSeconds = settings.ExpirationProcessTimerInSeconds;

                logger.Info($"Running deletion of expired documents every {deleteFrequencyInSeconds} seconds.");
                logger.Info($"Retention period for audits and sagahistory is {settings.AuditRetentionPeriod}.");
                logger.Info($"Retention period for errors is {settings.ErrorRetentionPeriod}");

                var due = TimeSpan.FromSeconds(deleteFrequencyInSeconds);
                timer = timeKeeper.New(() =>
                {
                    try
                    {
                        RunCleanup();
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Deletion of expired documents failed.", ex);
                    }
                }, due, due);
            }

            protected override void OnStop()
            {
                cancellationTokenSource.Cancel();

                timeKeeper.Release(timer);
            }

            private void RunCleanup()
            {
                var threshold = DateTime.UtcNow.Add(-settings.AuditRetentionPeriod);

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                logger.Debug($"Trying to find expired ProcessedMessage and SagaHistory documents to delete (with threshold {threshold.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture)})");
                try
                {
                    AuditMessageCleaner.Clean(store, threshold, cancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    logger.Error("Deletion of audit expired documents failed.", ex);
                }

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    SagaHistoryCleaner.Clean(store, threshold, cancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    logger.Error("Deletion of sagahistory expired documents failed.", ex);
                }

                threshold = SystemTime.UtcNow.Add(-settings.ErrorRetentionPeriod);

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                logger.Debug($"Trying to find expired FailedMessage documents to delete (with threshold {threshold.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture)})");
                try
                {
                    ErrorMessageCleaner.Clean(store, threshold, cancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    logger.Error("Deletion of error expired documents failed.", ex);
                }

                threshold = SystemTime.UtcNow.Add(-settings.EventsRetentionPeriod);

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }

                logger.Debug($"Trying to find expired EventLogItem documents to delete (with threshold {threshold.ToString(Default.DateTimeFormatsToWrite, CultureInfo.InvariantCulture)})");
                try
                {
                    EventLogItemsCleaner.Clean(store, threshold, cancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    logger.Error("Deletion of EventLogItem expired documents failed.", ex);
                }
            }
        }
    }
}