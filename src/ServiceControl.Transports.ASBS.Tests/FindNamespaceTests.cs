namespace ServiceControl.Transport.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.ResourceManager.ServiceBus;
using Azure.ResourceManager.ServiceBus.Models;
using NUnit.Framework;
using Transports.ASBS;

[TestFixture]
class FindNamespaceTests
{
    [TestCase("contoso")]
    [TestCase("CONTOSO")]
    public async Task Matches_a_namespace_by_name_without_reading_aliases(string name)
    {
        var contoso = Namespace("contoso", NotToBeRead);

        var found = await AzureQuery.FindNamespace(Listed(Namespace("fabrikam", NotToBeRead), contoso), name);

        Assert.That(found, Is.SameAs(contoso));
    }

    [Test]
    public async Task Resolves_a_geo_recovery_alias_to_its_primary_namespace()
    {
        var primary = Namespace("contoso-west", Alias("contoso", ServiceBusDisasterRecoveryRole.Primary));

        var found = await AzureQuery.FindNamespace(Listed(Namespace("contoso-east", Alias("contoso", ServiceBusDisasterRecoveryRole.Secondary)), primary), "CONTOSO");

        Assert.That(found, Is.SameAs(primary));
    }

    [Test]
    public async Task Resolves_a_geo_recovery_alias_after_failover()
    {
        var newPrimary = Namespace("contoso-east", Alias("contoso", ServiceBusDisasterRecoveryRole.PrimaryNotReplicating));

        var found = await AzureQuery.FindNamespace(Listed(Namespace("contoso-west"), newPrimary), "contoso");

        Assert.That(found, Is.SameAs(newPrimary));
    }

    [Test]
    public async Task Skips_namespaces_whose_aliases_cannot_be_read()
    {
        var primary = Namespace("contoso-west", Alias("contoso", ServiceBusDisasterRecoveryRole.Primary));

        var found = await AzureQuery.FindNamespace(Listed(Namespace("fabrikam", Forbidden), primary), "contoso");

        Assert.That(found, Is.SameAs(primary));
    }

    [Test]
    public void Does_not_resolve_an_alias_to_its_secondary_namespace()
    {
        var exception = Assert.ThrowsAsync<Exception>(() =>
            AzureQuery.FindNamespace(Listed(Namespace("contoso-east", Alias("contoso", ServiceBusDisasterRecoveryRole.Secondary))), "contoso"));

        Assert.That(exception.Message, Is.EqualTo("Could not find an Azure Service Bus namespace or Geo-Recovery alias named \"contoso\""));
    }

    [Test]
    public void Names_the_missing_permission_when_aliases_cannot_be_read()
    {
        var exception = Assert.ThrowsAsync<Exception>(() => AzureQuery.FindNamespace(Listed(Namespace("contoso-west", Forbidden)), "contoso"));

        Assert.That(exception.Message, Is.EqualTo("Could not find an Azure Service Bus namespace named \"contoso\". If \"contoso\" is a Geo-Recovery alias, resolving it requires the Microsoft.ServiceBus/namespaces/disasterRecoveryConfigs/read permission."));
    }

    static IAsyncEnumerable<ServiceBusNamespaceResource> Listed(params ServiceBusNamespaceResource[] namespaces) => namespaces.ToAsyncEnumerable();

    static ServiceBusNamespaceResource Namespace(string name, params ServiceBusDisasterRecoveryData[] aliases) =>
        new FakeNamespace(name, () => aliases);

    static ServiceBusNamespaceResource Namespace(string name, Func<IReadOnlyList<ServiceBusDisasterRecoveryData>> aliases) =>
        new FakeNamespace(name, aliases);

    static ServiceBusDisasterRecoveryData Alias(string name, ServiceBusDisasterRecoveryRole role) =>
        ArmServiceBusModelFactory.ServiceBusDisasterRecoveryData(name: name, role: role);

    static readonly Func<IReadOnlyList<ServiceBusDisasterRecoveryData>> NotToBeRead =
        () => throw new AssertionException("Aliases were read although a namespace matched by name");

    static readonly Func<IReadOnlyList<ServiceBusDisasterRecoveryData>> Forbidden =
        () => throw new RequestFailedException(403, "The client does not have authorization to perform action 'Microsoft.ServiceBus/namespaces/disasterRecoveryConfigs/read'");

    class FakeNamespace(string name, Func<IReadOnlyList<ServiceBusDisasterRecoveryData>> aliases) : ServiceBusNamespaceResource
    {
        public override bool HasData => true;

        public override ServiceBusNamespaceData Data { get; } = ArmServiceBusModelFactory.ServiceBusNamespaceData(name: name, geoDataReplication: null);

        public override ServiceBusDisasterRecoveryCollection GetServiceBusDisasterRecoveries() => new FakeAliases(aliases);
    }

    class FakeAliases(Func<IReadOnlyList<ServiceBusDisasterRecoveryData>> aliases) : ServiceBusDisasterRecoveryCollection
    {
        public override AsyncPageable<ServiceBusDisasterRecoveryResource> GetAllAsync(CancellationToken cancellationToken = default) =>
            AsyncPageable<ServiceBusDisasterRecoveryResource>.FromPages(Pages());

        IEnumerable<Page<ServiceBusDisasterRecoveryResource>> Pages()
        {
            yield return Page<ServiceBusDisasterRecoveryResource>.FromValues([.. aliases().Select(data => new FakeAlias(data))], null, null);
        }
    }

    class FakeAlias(ServiceBusDisasterRecoveryData data) : ServiceBusDisasterRecoveryResource
    {
        public override bool HasData => true;

        public override ServiceBusDisasterRecoveryData Data => data;
    }
}
