namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.CustomChecks;
using NUnit.Framework;
using ServiceControl.Persistence.RavenDB.DataMigration.Readers;

class RavenStorageCheckIdsTests : RavenPersistenceTestBase
{
    [Test]
    public void Lists_exactly_the_checks_the_RavenDB_persister_registers_with_their_categories() =>
        Assert.That(ServiceProvider.GetServices<ICustomCheck>().Select(check => (check.Id, check.Category)), Is.EquivalentTo(RavenStorageCheckIds.All),
            "the custom checks reader flags exactly these pairs, so a check the persister adds, renames or moves to another category would be copied to SQL, where nothing reports it again");
}
