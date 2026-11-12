namespace ServiceControl.Migration.AcceptanceTests;

using System.Threading.Tasks;
using NUnit.Framework;

[SetUpFixture]
public class StopMigrationSourceServer
{
    [OneTimeTearDown]
    public Task Teardown() => MigrationSourceServer.Stop();
}
