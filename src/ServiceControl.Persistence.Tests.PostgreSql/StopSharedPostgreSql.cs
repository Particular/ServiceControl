namespace ServiceControl;

using System.Threading.Tasks;
using NUnit.Framework;
using Persistence.Tests;

[SetUpFixture]
public class StopSharedPostgreSql
{
    [OneTimeTearDown]
    public async Task Teardown() => await PostgreSqlSharedContainer.Stop();
}
