namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using NUnit.Framework;
    using UnitOfWork;

    [TestFixture]
    abstract class PersistenceTestFixture
    {
        public Action<PersistenceSettings, IDictionary<string,string>> SetSettings = (_,_) => { };

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

        protected string GetManifestPath()
        {
            var currentFolder = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

            while (currentFolder != null)
            {
                var file = currentFolder.EnumerateFiles("*.sln", SearchOption.TopDirectoryOnly)
                    .SingleOrDefault();

                if (file != null)
                {
                    return Path.Combine(file.Directory.FullName, $"ServiceControl.Audit.Persistence.{PersisterName}", "persistence.manifest");
                }

                currentFolder = currentFolder.Parent;
            }

            throw new Exception($"Cannot find manifest folder for {PersisterName}");
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