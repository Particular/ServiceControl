namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using NUnit.Framework;
    using UnitOfWork;

    [TestFixture]
    abstract class PersistenceTestFixture
    {
        public Action<PersistenceSettings> SetSettings = _ => { };

        [SetUp]
        public virtual Task Setup()
        {
            configuration = new PersistenceTestsConfiguration();

            testCancellationTokenSource = Debugger.IsAttached ? new CancellationTokenSource() : new CancellationTokenSource(TestTimeout);

            return configuration.Configure(SetSettings);
        }

        [TearDown]
        public virtual Task Cleanup()
        {
            testCancellationTokenSource?.Dispose();
            return configuration?.Cleanup();
        }

        protected string PersisterName => configuration.Name;

        protected IAuditDataStore DataStore => configuration.AuditDataStore;

        protected IFailedAuditStorage FailedAuditStorage => configuration.FailedAuditStorage;

        protected IBodyStorage BodyStorage => configuration.BodyStorage;

        protected IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory =>
            configuration.AuditIngestionUnitOfWorkFactory;

        protected ValueTask<IAuditIngestionUnitOfWork> StartAuditUnitOfWork(int batchSize) =>
            AuditIngestionUnitOfWorkFactory.StartNew(batchSize, TestContext.CurrentContext.CancellationToken);

        protected IServiceProvider ServiceProvider => configuration.ServiceProvider;

        protected PersistenceTestsConfiguration configuration;

        protected CancellationToken TestTimeoutCancellationToken => testCancellationTokenSource.Token;

        CancellationTokenSource testCancellationTokenSource;

        static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);
    }
}