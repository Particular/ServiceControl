namespace ServiceControl.Persistence.Tests.RavenDB.Unarchiving
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.Testing;
    using NUnit.Framework;
    using ServiceControl.Recoverability;

    [TestFixture]
    class UnarchiveGroupTests : RavenPersistenceTestBase
    {
        public UnarchiveGroupTests() =>
            RegisterServices = services =>
            {
                services.AddSingleton<UnarchiveAllInGroupHandler>();
                services.AddSingleton<RetryingManager>();
            };

        [Test]
        public async Task UnarchiveGroup_skips_over_empty_batches_but_still_completes()
        {
            // Arrange
            var groupId = "TestGroup";
            var previousUnarchiveBatchId = UnarchiveBatch.MakeId(groupId, ArchiveType.FailureGroup, 1);

            using (var session = DocumentStore.OpenAsyncSession())
            {
                var previousUnarchiveBatch = new UnarchiveBatch { Id = previousUnarchiveBatchId };
                await session.StoreAsync(previousUnarchiveBatch);

                var previousUnarchiveOperation = new UnarchiveOperation
                {
                    Id = UnarchiveOperation.MakeId(groupId, ArchiveType.FailureGroup),
                    RequestId = groupId,
                    ArchiveType = ArchiveType.FailureGroup,
                    TotalNumberOfMessages = 2,
                    NumberOfMessagesUnarchived = 2,
                    Started = DateTime.UtcNow,
                    GroupName = "Test Group",
                    NumberOfBatches = 3,
                    CurrentBatch = 0
                };
                await session.StoreAsync(previousUnarchiveOperation);

                await session.SaveChangesAsync();
            }

            var handler = ServiceProvider.GetRequiredService<UnarchiveAllInGroupHandler>(); // See this.CreateHostBuilder

            var context = new TestableMessageHandlerContext();
            var message = new UnarchiveAllInGroup { GroupId = groupId };

            // Act
            await handler.Handle(message, context);

            // Assert
            using (var session = DocumentStore.OpenSession())
            {
                var loadedBatch = session.Load<UnarchiveBatch>(previousUnarchiveBatchId);
                Assert.IsNull(loadedBatch);
            }
        }

        [Test]
        public async Task UnarchiveGroup_GetGroupDetails_doesnt_fail_with_invalid_groupId()
        {
            // Arrange
            var failureGroupsViewIndex = new ArchivedGroupsViewIndex();
            await failureGroupsViewIndex.ExecuteAsync(DocumentStore);

            var groupId = "TestGroup";
            var previousUnarchiveBatchId = UnarchiveBatch.MakeId(groupId, ArchiveType.FailureGroup, 1);

            using (var session = DocumentStore.OpenAsyncSession())
            {
                var previousUnarchiveBatch = new UnarchiveBatch { Id = previousUnarchiveBatchId };
                await session.StoreAsync(previousUnarchiveBatch);

                var previousUnarchiveOperation = new UnarchiveOperation
                {
                    Id = UnarchiveOperation.MakeId(groupId, ArchiveType.FailureGroup),
                    RequestId = groupId,
                    ArchiveType = ArchiveType.FailureGroup,
                    TotalNumberOfMessages = 2,
                    NumberOfMessagesUnarchived = 0,
                    Started = DateTime.UtcNow,
                    GroupName = "Test Group",
                    NumberOfBatches = 3,
                    CurrentBatch = 0
                };
                await session.StoreAsync(previousUnarchiveOperation);

                await session.SaveChangesAsync();
            }

            var handler = ServiceProvider.GetRequiredService<UnarchiveAllInGroupHandler>(); // See this.CreateHostBuilder

            var context = new TestableMessageHandlerContext();
            var message = new UnarchiveAllInGroup { GroupId = groupId + "Invalid" };

            // Act
            // Assert
            Assert.DoesNotThrowAsync(async () => await handler.Handle(message, context));
        }
    }
}