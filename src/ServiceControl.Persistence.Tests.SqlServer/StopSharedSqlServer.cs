namespace ServiceControl;

using System.Threading.Tasks;
using NUnit.Framework;
using Persistence.Tests;

[SetUpFixture]
public class StopSharedSqlServer
{
    [OneTimeTearDown]
    public async Task Teardown() => await SqlServerSharedContainer.Stop();
}
