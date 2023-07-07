namespace ServiceControl.Audit.Persistence.Tests.Raven
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using global::Raven.Abstractions.Data;
    using global::Raven.Client.Indexes;
    using NUnit.Framework;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Infrastructure.Migration;
    using ServiceControl.Audit.Monitoring;
    using ServiceControl.Audit.Persistence.Monitoring;

    class EndpointMigrationTests : PersistenceTestFixture
    {
        [Test]
        public async Task Should_disable_index_after_first_migration()
        {
            var sendHostId = Guid.NewGuid();
            var receiverHostId = Guid.NewGuid();

            configuration.DocumentStore.ExecuteIndex(new EndpointsIndex());

            using (var session = configuration.DocumentStore.OpenAsyncSession())
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

            configuration.DocumentStore.WaitForIndexing();

            var migrator = new MigrateKnownEndpoints(configuration.DocumentStore);

            await migrator.Migrate();

            var dbStatistics = await configuration.DocumentStore.AsyncDatabaseCommands.GetStatisticsAsync();
            var indexStats = dbStatistics.Indexes.First(index => index.Name == "EndpointsIndex");
            Assert.AreEqual(IndexingPriority.Disabled, indexStats.Priority);
        }

        [Test]
        public async Task Should_delete_index_after_second_migration()
        {
            var sendHostId = Guid.NewGuid();
            var receiverHostId = Guid.NewGuid();

            configuration.DocumentStore.ExecuteIndex(new EndpointsIndex());

            using (var session = configuration.DocumentStore.OpenAsyncSession())
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

            configuration.DocumentStore.WaitForIndexing();

            var migrator = new MigrateKnownEndpoints(configuration.DocumentStore);

            await migrator.Migrate();
            await migrator.Migrate();

            var knownEndpointsIndex = await configuration.DocumentStore.AsyncDatabaseCommands.GetIndexAsync("EndpointsIndex");
            Assert.IsNull(knownEndpointsIndex);
        }

        [Test]
        public async Task Should_migrate_endpoints_to_document_collection()
        {
            var sendHostId = Guid.NewGuid();
            var receiverHostId = Guid.NewGuid();

            configuration.DocumentStore.ExecuteIndex(new EndpointsIndex());

            using (var session = configuration.DocumentStore.OpenAsyncSession())
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

            configuration.DocumentStore.WaitForIndexing();

            var migrator = new MigrateKnownEndpoints(configuration.DocumentStore);

            await migrator.Migrate();

            using (var session = configuration.DocumentStore.OpenAsyncSession())
            {
                var loadedSenderEndpoint = await session.LoadAsync<KnownEndpoint>(KnownEndpoint.MakeDocumentId("SendHostName", sendHostId));
                var loadedReceiverEndpoint = await session.LoadAsync<KnownEndpoint>(KnownEndpoint.MakeDocumentId("ReceivingHostName", receiverHostId));

                Assert.NotNull(loadedReceiverEndpoint);
                Assert.NotNull(loadedSenderEndpoint);
            }
        }

        [Test]
        public async Task Should_migrate_idempotently()
        {
            var sendHostId = Guid.NewGuid();
            var receiverHostId = Guid.NewGuid();

            configuration.DocumentStore.ExecuteIndex(new EndpointsIndex());

            using (var session = configuration.DocumentStore.OpenAsyncSession())
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

            configuration.DocumentStore.WaitForIndexing();

            var migrator = new MigrateKnownEndpoints(configuration.DocumentStore);

            await migrator.Migrate();

            configuration.DocumentStore.ExecuteIndex(new EndpointsIndex());
            configuration.DocumentStore.WaitForIndexing();

            await migrator.Migrate();
            await migrator.Migrate();
            await migrator.Migrate();
            await migrator.Migrate();
            await migrator.Migrate();

            using (var session = configuration.DocumentStore.OpenAsyncSession())
            {
                var loadedSenderEndpoint = await session.LoadAsync<KnownEndpoint>(KnownEndpoint.MakeDocumentId("SendHostName", sendHostId));
                var loadedReceiverEndpoint = await session.LoadAsync<KnownEndpoint>(KnownEndpoint.MakeDocumentId("ReceivingHostName", receiverHostId));

                Assert.NotNull(loadedReceiverEndpoint);
                Assert.NotNull(loadedSenderEndpoint);
            }
        }

        [Test]
        public async Task Should_page_endpoints_migration()
        {
            var sendHostId = Guid.NewGuid();
            var receiverHostId = Guid.NewGuid();

            configuration.DocumentStore.ExecuteIndex(new EndpointsIndex());

            using (var session = configuration.DocumentStore.OpenAsyncSession())
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

            configuration.DocumentStore.WaitForIndexing();

            var migrator = new MigrateKnownEndpoints(configuration.DocumentStore);

            await migrator.Migrate(pageSize: 1);

            using (var session = configuration.DocumentStore.OpenAsyncSession())
            {
                var loadedSenderEndpoint = await session.LoadAsync<KnownEndpoint>(KnownEndpoint.MakeDocumentId("SendHostName", sendHostId));
                var loadedReceiverEndpoint = await session.LoadAsync<KnownEndpoint>(KnownEndpoint.MakeDocumentId("ReceivingHostName", receiverHostId));

                Assert.NotNull(loadedReceiverEndpoint);
                Assert.NotNull(loadedSenderEndpoint);
            }
        }

        [Test]
        public async Task Should_noop_migration_if_no_index()
        {
            var sendHostId = Guid.NewGuid();
            var receiverHostId = Guid.NewGuid();

            using (var session = configuration.DocumentStore.OpenAsyncSession())
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

            configuration.DocumentStore.WaitForIndexing();

            var migrator = new MigrateKnownEndpoints(configuration.DocumentStore);

            await migrator.Migrate(pageSize: 1);

            using (var session = configuration.DocumentStore.OpenAsyncSession())
            {
                var loadedSenderEndpoint = await session.LoadAsync<KnownEndpoint>(KnownEndpoint.MakeDocumentId("SendHostName", sendHostId));

                Assert.Null(loadedSenderEndpoint);
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