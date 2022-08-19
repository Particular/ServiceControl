namespace ServiceControl.Audit.UnitTests.Infrastructure.RavenDB.Expiration
{
    using System;
    using System.Threading.Tasks;
    using Monitoring;
    using NUnit.Framework;
    using Raven.Client;
    using Raven.Client.Document;
    using ServiceControl.Audit.Persistence.RavenDB.Expiration;

    [TestFixture]
    public class KnownEndpointCleanerTests
    {
        [Test]
        public async Task Should_clean_expired_endpoints()
        {
            using (var store = InMemoryStoreBuilder.GetInMemoryStore())
            {
                new ExpiryKnownEndpointsIndex().Execute(store.DatabaseCommands, new DocumentConvention());

                using (var bulkInsert = store.BulkInsert())
                {
                    for (var i = 0; i < 50; i++)
                    {
                        bulkInsert.Store(new KnownEndpoint
                        {
                            LastSeen = DateTime.UtcNow
                        });
                    }

                    for (var i = 0; i < 50; i++)
                    {
                        bulkInsert.Store(new KnownEndpoint
                        {
                            LastSeen = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1))
                        });
                    }

                    await bulkInsert.DisposeAsync();
                }

                store.WaitForIndexing();
                KnownEndpointsCleaner.Clean(25, store.DocumentDatabase, DateTime.UtcNow.Subtract(TimeSpan.FromHours(5)));
                store.WaitForIndexing();
                KnownEndpointsCleaner.Clean(25, store.DocumentDatabase, DateTime.UtcNow.Subtract(TimeSpan.FromHours(5)));

                store.WaitForIndexing();

                using (var session = store.OpenAsyncSession())
                {
                    var foundEndpoints = await session.Query<KnownEndpoint>().CountAsync();

                    Assert.AreEqual(50, foundEndpoints);
                }
            }
        }
    }
}