namespace ServiceControl.Audit.UnitTests.KnownEndpointsMigration
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Audit.Auditing;
    using NUnit.Framework;
    using Raven.Abstractions.Data;
    using Raven.Client.Indexes;
    using ServiceControl.Audit.Infrastructure.RavenDB;
    using ServiceControl.Audit.Monitoring;

    [TestFixture]
    public class EndpointMigrationTests
    {
        [Test]
        public async Task Should_disable_index_after_first_migration()
        {
            Guid sendHostId = Guid.NewGuid();
            Guid receiverHostId = Guid.NewGuid();

            using (var store = InMemoryStoreBuilder.GetInMemoryStore())
            {
                store.ExecuteIndex(new EndpointsIndex());

                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new ProcessedMessage
                    {
                        Id = "1",
                        ProcessedAt = DateTime.Now,
                        UniqueMessageId = "xyz",
                        MessageMetadata = new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "SendingEndpoint", new EndpointDetails { Host = "SendHost", HostId = sendHostId, Name = "SendHostName" } },
                            { "ReceivingEndpoint", new EndpointDetails { Host = "ReceivingHost", HostId = receiverHostId, Name = "ReceivingHostName" } },
                        }
                    });

                    await session.SaveChangesAsync();
                }

                store.WaitForIndexing();

                var migrator = new MigrateKnownEndpoints();
                migrator.Store = store;

                await migrator.MigrateEndpoints().ConfigureAwait(false);

                var dbStatistics = await store.AsyncDatabaseCommands.GetStatisticsAsync().ConfigureAwait(false);
                var indexStats = dbStatistics.Indexes.First(index => index.Name == "EndpointsIndex");
                Assert.AreEqual(IndexingPriority.Disabled, indexStats.Priority);
            }
        }

        [Test]
        public async Task Should_delete_index_after_second_migration()
        {
            Guid sendHostId = Guid.NewGuid();
            Guid receiverHostId = Guid.NewGuid();

            using (var store = InMemoryStoreBuilder.GetInMemoryStore())
            {
                store.ExecuteIndex(new EndpointsIndex());

                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new ProcessedMessage
                    {
                        Id = "1",
                        ProcessedAt = DateTime.Now,
                        UniqueMessageId = "xyz",
                        MessageMetadata = new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "SendingEndpoint", new EndpointDetails { Host = "SendHost", HostId = sendHostId, Name = "SendHostName" } },
                            { "ReceivingEndpoint", new EndpointDetails { Host = "ReceivingHost", HostId = receiverHostId, Name = "ReceivingHostName" } },
                        }
                    });

                    await session.SaveChangesAsync();
                }

                store.WaitForIndexing();

                var migrator = new MigrateKnownEndpoints();
                migrator.Store = store;

                await migrator.MigrateEndpoints().ConfigureAwait(false);
                await migrator.MigrateEndpoints().ConfigureAwait(false);

                var knownEndpointsIndex = await store.AsyncDatabaseCommands.GetIndexAsync("EndpointsIndex").ConfigureAwait(false);
                Assert.IsNull(knownEndpointsIndex);
            }
        }

        [Test]
        public async Task Should_migrate_endpoints_to_document_collection()
        {
            Guid sendHostId = Guid.NewGuid();
            Guid receiverHostId = Guid.NewGuid();

            using (var store = InMemoryStoreBuilder.GetInMemoryStore())
            {
                store.ExecuteIndex(new EndpointsIndex());

                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new ProcessedMessage
                    {
                        Id = "1",
                        ProcessedAt = DateTime.Now,
                        UniqueMessageId = "xyz",
                        MessageMetadata = new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "SendingEndpoint", new EndpointDetails { Host = "SendHost", HostId = sendHostId, Name = "SendHostName" } },
                            { "ReceivingEndpoint", new EndpointDetails { Host = "ReceivingHost", HostId = receiverHostId, Name = "ReceivingHostName" } },
                        }
                    });

                    await session.SaveChangesAsync();
                }

                store.WaitForIndexing();

                var migrator = new MigrateKnownEndpoints();
                migrator.Store = store;

                await migrator.MigrateEndpoints().ConfigureAwait(false);

                using (var session = store.OpenAsyncSession())
                {
                    var loadedSenderEndpoint = await session.LoadAsync<KnownEndpoint>(KnownEndpoint.MakeDocumentId("SendHostName", sendHostId)).ConfigureAwait(false);
                    var loadedReceiverEndpoint = await session.LoadAsync<KnownEndpoint>(KnownEndpoint.MakeDocumentId("ReceivingHostName", receiverHostId)).ConfigureAwait(false);

                    Assert.NotNull(loadedReceiverEndpoint);
                    Assert.NotNull(loadedSenderEndpoint);
                }
            }
        }

        [Test]
        public async Task Should_migrate_idempotently()
        {
            Guid sendHostId = Guid.NewGuid();
            Guid receiverHostId = Guid.NewGuid();

            using (var store = InMemoryStoreBuilder.GetInMemoryStore())
            {
                store.ExecuteIndex(new EndpointsIndex());

                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new ProcessedMessage
                    {
                        Id = "1",
                        ProcessedAt = DateTime.Now,
                        UniqueMessageId = "xyz",
                        MessageMetadata = new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "SendingEndpoint", new EndpointDetails { Host = "SendHost", HostId = sendHostId, Name = "SendHostName" } },
                            { "ReceivingEndpoint", new EndpointDetails { Host = "ReceivingHost", HostId = receiverHostId, Name = "ReceivingHostName" } },
                        }
                    });

                    await session.SaveChangesAsync();
                }

                store.WaitForIndexing();

                var migrator = new MigrateKnownEndpoints();
                migrator.Store = store;

                await migrator.MigrateEndpoints().ConfigureAwait(false);

                store.ExecuteIndex(new EndpointsIndex());
                store.WaitForIndexing();

                await migrator.MigrateEndpoints().ConfigureAwait(false);

                using (var session = store.OpenAsyncSession())
                {
                    var loadedSenderEndpoint = await session.LoadAsync<KnownEndpoint>(KnownEndpoint.MakeDocumentId("SendHostName", sendHostId)).ConfigureAwait(false);
                    var loadedReceiverEndpoint = await session.LoadAsync<KnownEndpoint>(KnownEndpoint.MakeDocumentId("ReceivingHostName", receiverHostId)).ConfigureAwait(false);

                    Assert.NotNull(loadedReceiverEndpoint);
                    Assert.NotNull(loadedSenderEndpoint);
                }
            }
        }

        [Test]
        public async Task Should_page_endpoints_migration()
        {
            Guid sendHostId = Guid.NewGuid();
            Guid receiverHostId = Guid.NewGuid();

            using (var store = InMemoryStoreBuilder.GetInMemoryStore())
            {
                store.ExecuteIndex(new EndpointsIndex());

                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new ProcessedMessage
                    {
                        Id = "1",
                        ProcessedAt = DateTime.Now,
                        UniqueMessageId = "xyz",
                        MessageMetadata = new System.Collections.Generic.Dictionary<string, object>
                        {
                            { "SendingEndpoint", new EndpointDetails { Host = "SendHost", HostId = sendHostId, Name = "SendHostName" } },
                            { "ReceivingEndpoint", new EndpointDetails { Host = "ReceivingHost", HostId = receiverHostId, Name = "ReceivingHostName" } },
                        }
                    });

                    await session.SaveChangesAsync();
                }

                store.WaitForIndexing();

                var migrator = new MigrateKnownEndpoints();
                migrator.Store = store;

                await migrator.MigrateEndpoints(pageSize: 1).ConfigureAwait(false);

                using (var session = store.OpenAsyncSession())
                {
                    var loadedSenderEndpoint = await session.LoadAsync<KnownEndpoint>(KnownEndpoint.MakeDocumentId("SendHostName", sendHostId)).ConfigureAwait(false);
                    var loadedReceiverEndpoint = await session.LoadAsync<KnownEndpoint>(KnownEndpoint.MakeDocumentId("ReceivingHostName", receiverHostId)).ConfigureAwait(false);

                    Assert.NotNull(loadedReceiverEndpoint);
                    Assert.NotNull(loadedSenderEndpoint);
                }
            }
        }

        class EndpointsIndex : AbstractIndexCreationTask<ProcessedMessage, EndpointDetails>
        {
            public EndpointsIndex()
            {
                Map = messages => from message in messages
                                  let sending = (EndpointDetails)message.MessageMetadata["SendingEndpoint"]
                                  let receiving = (EndpointDetails)message.MessageMetadata["ReceivingEndpoint"]
                                  from endpoint in new[] { sending, receiving }
                                  where endpoint != null
                                  select new EndpointDetails
                                  {
                                      Host = endpoint.Host,
                                      HostId = endpoint.HostId,
                                      Name = endpoint.Name
                                  };

                Reduce = results => from result in results
                                    group result by new { result.Name, result.HostId }
                    into grouped
                                    let first = grouped.First()
                                    select new EndpointDetails
                                    {
                                        Host = first.Host,
                                        HostId = first.HostId,
                                        Name = first.Name
                                    };
            }
        }
    }
}