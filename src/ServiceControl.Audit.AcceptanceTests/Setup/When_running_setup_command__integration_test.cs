namespace ServiceControl.Audit.AcceptanceTests.Setup
{
    using NUnit.Framework;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using AcceptanceTesting;
    using Infrastructure;

    [TestFixture]
    public class When_running_setup_command_integration_test
    {
        [Test]
        public void VerifySetupCommandBehavior()
        {
            //GIVEN
            using (var configuredAuditBinaries = AuditBinaries.CopyAndConfigureForSqlT())
            {
                //WHEN
                configuredAuditBinaries.ExecuteSetupCommand();

                //THEN
                AssertEventLogSourceGotCreated();
                AssertQueuesGotCreated(configuredAuditBinaries);
                AssertRavenDbDatabaseGotCreated(configuredAuditBinaries.WorkingDirectory);
            }
        }

        static void AssertEventLogSourceGotCreated() =>
            Assert.IsTrue(EventLog.SourceExists(EventSource.SourceName),
                $"Expected Windows event viewer event source '{EventSource.SourceName}' to have been created");

        static void AssertQueuesGotCreated(AuditBinaries auditBinaries)
        {
            Assert.IsTrue(auditBinaries.SqlTDb.HasTableWithName("SubscriptionRouting"),
                "Expected subscription routing to have been setup");
            Assert.IsTrue(auditBinaries.SqlTDb.HasTableWithName(auditBinaries.ErrorQueue),
                "Expected errors queue to have been created");
            Assert.IsTrue(auditBinaries.SqlTDb.HasTableWithName(auditBinaries.AuditQueueName),
                "Expected audit queue to have been created");
        }

        void AssertRavenDbDatabaseGotCreated(string workingDirectory)
        {
            var dbPath = Path.Combine(workingDirectory, "Database");
            Assert.IsTrue(File.Exists($"{dbPath}/IndexDefinitions/indexes.txt"), "Unable to find RavenDB database.");
            var indexesFileContent = File.ReadAllLines($"{dbPath}/IndexDefinitions/indexes.txt");

            Assert.IsTrue(indexesFileContent.Any(i => i.Contains("SagaDetailsIndex")), "RavenDB database does not contain expected index SagaDetailsIndex");
            Assert.IsTrue(indexesFileContent.Any(i => i.Contains("MessagesViewIndex")), "RavenDB database does not contain expected index MessagesViewIndex");
        }

        [SetUp]
        public void Setup()
        {
            var transportIntegration = (ITransportIntegration)TestSuiteConstraints.Current.CreateTransportConfiguration();
#if !DEBUG
            if (transportIntegration.Name.Equals("SQL Server", System.StringComparison.InvariantCultureIgnoreCase) == false)
            {
                Assert.Inconclusive($"This test is meant to run for SQL Server transport only therefore skipping this test when running tests for '{transportIntegration.Name}'");
                return;
            }
#endif
            DeleteEventViewerEventSource();
        }

        static void DeleteEventViewerEventSource()
        {
            try
            {
                if (EventLog.SourceExists(EventSource.SourceName))
                {
                    EventLog.DeleteEventSource(EventSource.SourceName);
                }
            }
            catch
            {
                // ignored
            }

            Assert.IsTrue(EventLog.SourceExists(EventSource.SourceName) == false,
                $"Running this test requires elevated privileges. The test setup couldn't confirm windows event log source {EventSource.SourceName} doesn't exists before starting the test execution");
        }
    }
}
