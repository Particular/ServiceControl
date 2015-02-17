namespace ServiceControl.SagaAudit
{
    using System;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Abstractions;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.RavenDB.Expiration;

    public class SagaHistoryMigration : IWantToRunWhenBusStartsAndStops,IDisposable
    {
        static ILog logger = LogManager.GetLogger(typeof(SagaHistoryMigration));
        
        public IDocumentStore Store { get; set; }
        int emptyRunCount;
        DateTime currentExpiryThresholdTime;
        PeriodicExecutor periodicExecutor;

        public SagaHistoryMigration()
            : this(TimeSpan.FromHours(Settings.HoursToKeepMessagesBeforeExpiring), TimeSpan.FromMinutes(5))
        {
        }

        public SagaHistoryMigration(TimeSpan timeToKeepMessagesBeforeExpiring, TimeSpan timerPeriod)
        {
            periodicExecutor = new PeriodicExecutor(Migrate, timerPeriod);
            currentExpiryThresholdTime = SystemTime.UtcNow.Add(-timeToKeepMessagesBeforeExpiring);
        }

        public void Start()
        {
            periodicExecutor.Start(true);
        }

        void Migrate()
        {
            bool wasCleanEmptyRun;
            Migrate(out wasCleanEmptyRun);
            if (wasCleanEmptyRun)
            {
                emptyRunCount++;
            }
            if (emptyRunCount == 5)
            {
                periodicExecutor.Stop();
            }
        }


        public void Migrate(out bool wasCleanEptyRun)
        {
            using (var querySession = Store.OpenSession())
            {
                QueryHeaderInformation information;
                var luceneQuery = querySession.Advanced.LuceneQuery<SagaHistory>("Raven/DocumentsByEntityName")
                    .WhereEquals("Tag", "SagaHistories")
                    .AddOrder("LastModified", true);

                using (var enumerator = querySession.Advanced.Stream(luceneQuery, out information))
                {
                    while (enumerator.MoveNext())
                    {
                        ProcessHistoryRecord(enumerator.Current);
                    }
                }
                wasCleanEptyRun = information.IsStable && information.TotalResults == 0;
            }
        }

        void ProcessHistoryRecord(StreamResult<SagaHistory> result)
        {
            var sagaHistory = result.Document;

            using (var updateSession = Store.OpenSession())
            {
                try
                {
                    foreach (var sagaStateChange in sagaHistory.Changes
                        .Where(x => x.FinishTime > currentExpiryThresholdTime))
                    {
                        updateSession.Store(ConvertToSnapshot(sagaHistory, sagaStateChange));
                    }
                    updateSession.Advanced.Defer(new DeleteCommandData
                                                 {
                                                     Key = result.Key,
                                                     Etag = result.Etag
                                                 });
                    updateSession.SaveChanges();
                }
                catch (ConcurrencyException)
                {
                    var message = string.Format("Could not migrate SagaHistory (SagaId={0}) to SagaSnapshot since it has been updated during the migration. It will be migrated on the next run.", sagaHistory.SagaId);
                    logger.Warn(message);
                }
            }
        }

        static SagaSnapshot ConvertToSnapshot(SagaHistory sagaHistory, SagaStateChange sagaStateChange)
        {
            return new SagaSnapshot
                   {
                       SagaId = sagaHistory.SagaId,
                       Endpoint = sagaStateChange.Endpoint,
                       FinishTime = sagaStateChange.FinishTime,
                       InitiatingMessage = sagaStateChange.InitiatingMessage,
                       OutgoingMessages = sagaStateChange.OutgoingMessages,
                       SagaType = sagaHistory.SagaType,
                       StartTime = sagaStateChange.StartTime,
                       StateAfterChange = sagaStateChange.StateAfterChange,
                       Status = sagaStateChange.Status,
                   };
        }

        public void Dispose()
        {
            if (periodicExecutor != null)
            {
                periodicExecutor.Stop();
            }
        }

        public void Stop()
        {
            Dispose();
        }
    }

}
