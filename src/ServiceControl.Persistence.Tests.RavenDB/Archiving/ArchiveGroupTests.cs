namespace ServiceControl.UnitTests.Archiving
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.Testing;
    using NUnit.Framework;
    using PersistenceTests;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Recoverability;

    [TestFixture]
    class ArchiveGroupTests : PersistenceTestBase
    {
        public ArchiveGroupTests() =>
            RegisterServices = services =>
            {
                services.AddSingleton<ArchiveAllInGroupHandler>();
                services.AddSingleton<RetryingManager>();
            };

        [Test]
        public async Task ArchiveGroup_skips_over_empty_batches_but_still_completes()
        {
            // Arrange
            var groupId = "TestGroup";
            var previousArchiveBatchId = ArchiveBatch.MakeId(groupId, ArchiveType.FailureGroup, 1);

            using (var session = DocumentStore.OpenAsyncSession())
            {
                var previousArchiveBatch = new ArchiveBatch { Id = previousArchiveBatchId };
                await session.StoreAsync(previousArchiveBatch);

                var previousArchiveOperation = new ArchiveOperation
                {
                    Id = ArchiveOperation.MakeId(groupId, ArchiveType.FailureGroup),
                    RequestId = groupId,
                    ArchiveType = ArchiveType.FailureGroup,
                    TotalNumberOfMessages = 2,
                    NumberOfMessagesArchived = 0,
                    Started = DateTime.UtcNow,
                    GroupName = "Test Group",
                    NumberOfBatches = 3,
                    CurrentBatch = 0
                };
                await session.StoreAsync(previousArchiveOperation);

                await session.SaveChangesAsync();
            }

            var handler = GetRequiredService<ArchiveAllInGroupHandler>(); // See this.CreateHostBuilder

            var context = new TestableMessageHandlerContext();
            var message = new ArchiveAllInGroup { GroupId = groupId };

            // Act
            await handler.Handle(message, context);

            // Assert
            using (var session = DocumentStore.OpenSession())
            {
                var loadedBatch = session.Load<ArchiveBatch>(previousArchiveBatchId);
                Assert.IsNull(loadedBatch);
            }
        }

        [Test]
        public async Task ArchiveGroup_GetGroupDetails_doesnt_fail_with_invalid_groupId()
        {
            // Arrange
            var failureGroupsViewIndex = new FailureGroupsViewIndex();
            await failureGroupsViewIndex.ExecuteAsync(DocumentStore);

            var groupId = "TestGroup";
            var previousArchiveBatchId = ArchiveBatch.MakeId(groupId, ArchiveType.FailureGroup, 1);

            using (var session = DocumentStore.OpenAsyncSession())
            {
                var previousArchiveBatch = new ArchiveBatch { Id = previousArchiveBatchId };
                await session.StoreAsync(previousArchiveBatch);

                var previousArchiveOperation = new ArchiveOperation
                {
                    Id = ArchiveOperation.MakeId(groupId, ArchiveType.FailureGroup),
                    RequestId = groupId,
                    ArchiveType = ArchiveType.FailureGroup,
                    TotalNumberOfMessages = 2,
                    NumberOfMessagesArchived = 0,
                    Started = DateTime.UtcNow,
                    GroupName = "Test Group",
                    NumberOfBatches = 3,
                    CurrentBatch = 0
                };
                await session.StoreAsync(previousArchiveOperation);

                await session.SaveChangesAsync();
            }

            var handler = GetRequiredService<ArchiveAllInGroupHandler>(); // See this.CreateHostBuilder

            var context = new TestableMessageHandlerContext();
            var message = new ArchiveAllInGroup { GroupId = groupId + "Invalid" };

            // Act
            // Assert
            Assert.DoesNotThrowAsync(async () =>
            {
                // Act
                await handler.Handle(message, context);
            });
        }
    }
}