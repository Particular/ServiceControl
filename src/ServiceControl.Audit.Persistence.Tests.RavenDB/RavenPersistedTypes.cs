namespace ServiceControl.UnitTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Particular.Approvals;
    using Raven.Abstractions.Data;
    using Raven.Client.Indexes;
    using ServiceControl.Audit.Persistence.Infrastructure;
    using ServiceControl.Audit.Persistence.Monitoring;
    using ServiceControl.Audit.Persistence.RavenDb;
    using ServiceControl.Audit.Persistence.Tests;

    class RavenPersistedTypes : PersistenceTestFixture
    {
        [Test]
        public void Verify()
        {
            var ravenPersistenceType = typeof(RavenDbPersistenceConfiguration);

            var ravenPersistenceTypes = ravenPersistenceType.Assembly.GetTypes()
                .Where(type => typeof(AbstractIndexCreationTask).IsAssignableFrom(type))
                .SelectMany(indexType => indexType.BaseType?.GenericTypeArguments)
                .Distinct();

            var documentTypeNames = string.Join(Environment.NewLine, ravenPersistenceTypes.Select(t => t.AssemblyQualifiedName));

            Approver.Verify(documentTypeNames);
        }

        [Test]
        public async Task CanLoadLegacyTypes()
        {
            var hostId = Guid.NewGuid();
            var endpointName = "Sales";
            var endpointId = KnownEndpoint.MakeDocumentId(endpointName, hostId);
            var knownEndpoint = new KnownEndpoint()
            {
                Id = endpointId,
                Host = "Sales.Host",
                HostId = hostId,
                LastSeen = DateTime.Now,
                Name = endpointName
            };

            using (var session = configuration.DocumentStore.OpenAsyncSession())
            {
                await session.StoreAsync(knownEndpoint);
                await session.SaveChangesAsync();
            }

            await configuration.CompleteDBOperation();

            _ = await configuration.DocumentStore.AsyncDatabaseCommands.PatchAsync(endpointId, new ScriptedPatchRequest()
            {
                Script =
@"
            var metadata = this['@metadata'];
            metadata['Raven-Clr-Type'] = 'This.Is.All.Wrong.SagaHistory, WrongAssembly'
            "
            });

            await configuration.CompleteDBOperation();

            var endpoints = await DataStore.QueryKnownEndpoints();
            var results = endpoints.Results.ToList();

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Id, Is.EqualTo(DeterministicGuid.MakeId(endpointName, hostId.ToString())));
        }
    }
}