namespace ServiceControl.Audit.Persistence.Tests
{
    using System.IO;
    using System;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using NUnit.Framework;
    using UnitOfWork;
    using System.Linq;

    [TestFixture]
    class PersistenceTestFixture
    {
        [SetUp]
        public Task Setup()
        {
            configuration = new PersistenceTestsConfiguration();

            return configuration.Configure();
        }

        [TearDown]
        public Task Cleanup()
        {
            return configuration.Cleanup();
        }

        protected DirectoryInfo GetZipFolder()
        {
            var currentFolder = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

            while (currentFolder != null)
            {
                var file = currentFolder.EnumerateFiles("*.sln", SearchOption.TopDirectoryOnly)
                    .SingleOrDefault();

                if (file != null)
                {
                    return new DirectoryInfo(Path.Combine(file.Directory.Parent.FullName, "zip"));
                }

                currentFolder = currentFolder.Parent;
            }

            throw new Exception("Cannot find zip folder");
        }

        protected string ZipName => configuration.ZipName;

        protected IAuditDataStore DataStore => configuration.AuditDataStore;

        protected IFailedAuditStorage FailedAuditStorage => configuration.FailedAuditStorage;

        protected IBodyStorage BodyStorage => configuration.BodyStorage;

        protected IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory =>
            configuration.AuditIngestionUnitOfWorkFactory;

        protected IAuditIngestionUnitOfWork StartAuditUnitOfWork(int batchSize) =>
            AuditIngestionUnitOfWorkFactory.StartNew(batchSize);

        protected PersistenceTestsConfiguration configuration;
    }
}