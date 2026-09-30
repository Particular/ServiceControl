namespace ServiceControl.Persistence.Tests;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;

class HealthEnvironmentDataTests : PersistenceTestBase
{
    [Test]
    public async Task An_empty_schema_is_healthy()
    {
        var provider = ServiceProvider.GetRequiredService<IEnvironmentDataProvider>();
        var data = new Dictionary<string, string>();

        foreach (var datum in provider.GetData())
        {
            data[datum.Key] = await datum.ReadValue(CancellationToken.None);
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data, Does.ContainKey("Health.Error.FailedImports").WithValue("0"));
            Assert.That(data, Does.ContainKey("Health.Error.RetentionBehindHours").WithValue("0"));
            Assert.That(data, Does.ContainKey("Storage.UnresolvedFailedMessages").WithValue("0"));
        }
    }
}
