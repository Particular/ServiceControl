namespace ServiceControl.Persistence.Tests.RavenDB.ConnectedApplications;

using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Operations;

[TestFixture]
public class ConnectedApplicationsEnricherTests
{
    [Test]
    public void Only_newly_detected_applications_are_stored()
    {
        var dataStore = new FakeConnectedApplicationsDataStore();
        var enricher = new ConnectedApplicationsEnricher(dataStore);

        var applicationId = "ServiceControl.Connector.MassTransit";

        var context = new ErrorEnricherContext(new Dictionary<string, string>()
        {
            {ConnectedApplicationsEnricher.ConnectedAppHeaderName, applicationId}
        }, new Dictionary<string, object>());

        enricher.Enrich(context);
        enricher.Enrich(context);

        Assert.That(new[] {applicationId}, Is.EquivalentTo(dataStore.ConnectedApplications));

    }
}

public class FakeConnectedApplicationsDataStore : IConnectedApplicationsDataStore
{
    public List<string> ConnectedApplications = new ();
    public async Task AddIfNotExists(string connectedApplication) => ConnectedApplications.Add(connectedApplication);
    public Task<IList<string>> GetConnectedApplications() => Task.FromResult<IList<string>>(ConnectedApplications);
}