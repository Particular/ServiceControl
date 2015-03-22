namespace ServiceControl.Migrations
{
    using System;
    using NServiceBus;
    using NServiceBus.Logging;
    using Raven.Abstractions.Commands;
    using Raven.Abstractions.Data;
    using Raven.Abstractions.Exceptions;
    using Raven.Client;
    using ServiceControl.Shell.Api;

    public abstract class Migration<T> : IWantToRunWhenBusStartsAndStops, IDisposable
    {
        readonly ILog logger;
        int emptyRunCount;
        PeriodicExecutor executor;

        readonly IDocumentStore Store;

        protected Migration(IDocumentStore store)
        {
            Store = store;
            logger = LogManager.GetLogger(GetType());
            executor = new PeriodicExecutor(Migrate, TimeSpan.FromMinutes(5));
        }

        protected abstract string EntityName { get; }
        public abstract void Migrate(T document, IDocumentSession updateSession, Func<bool> shouldCancel);

        public void Start()
        {
            executor.Start(true);
        }

        void Migrate(PeriodicExecutor e)
        {
            var wasCleanEmptyRun = Migrate(() => e.IsCancellationRequested);
            if (wasCleanEmptyRun)
            {
                emptyRunCount++;
            }
            if (emptyRunCount == 5)
            {
                e.Stop();
            }
        }


        public bool Migrate(Func<bool> shouldCancel)
        {
            using (var querySession = Store.OpenSession())
            {
                QueryHeaderInformation information;
                var luceneQuery = querySession.Advanced.LuceneQuery<T>("Raven/DocumentsByEntityName")
                    .WhereEquals("Tag", EntityName)
                    .AddOrder("LastModified", true);
                var processedRecord = false;

                using (var enumerator = querySession.Advanced.Stream(luceneQuery, out information))
                {
                    while (enumerator.MoveNext())
                    {
                        processedRecord = true;
                        Migrate(enumerator.Current, shouldCancel);
                    }
                }
                if (shouldCancel())
                {
                    return false;
                }
                return information.IsStable && !processedRecord;
            }
        }

        void Migrate(StreamResult<T> result, Func<bool> shouldCancel)
        {
            var document = result.Document;

            using (var updateSession = Store.OpenSession())
            {
                try
                {
                    Migrate(document, updateSession, shouldCancel);

                    updateSession.Advanced.Defer(new DeleteCommandData
                    {
                        Key = result.Key,
                        Etag = result.Etag
                    });
                    updateSession.SaveChanges();
                }
                catch (ConcurrencyException)
                {
                    var message = string.Format("Could not migrate document {0}/{1} since it has been updated during the migration. It will be migrated on the next run.", EntityName, result.Key);
                    logger.Warn(message);
                }
            }
        }


        public void Dispose()
        {
            executor.Stop();
        }

        public void Stop()
        {
            Dispose();
        }
    }
}