namespace ServiceControl.UnitTests.Archiving
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus.Testing;
    using NUnit.Framework;
    using Operations;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Recoverability;

    [TestFixture]
    public class ArchiveGroupTests
    {
        [Test]
        public async Task ArchiveGroup_skips_over_empty_batches_but_still_completes()
        {
            // Arrange
            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var groupId = "TestGroup";
                var previousArchiveBatchId = ArchiveBatch.MakeId(groupId, ArchiveType.FailureGroup, 1);

                using (var session = documentStore.OpenAsyncSession())
                {
                    var previousArchiveBatch = new ArchiveBatch { Id = previousArchiveBatchId };
                    await session.StoreAsync(previousArchiveBatch)
                        .ConfigureAwait(false);

                    var previousArchiveOperation = new ArchiveOperation
                    {
                        Id = ArchiveOperation.MakeId(groupId, ArchiveType.FailureGroup),
                        RequestId = groupId,
                        ArchiveType = ArchiveType.FailureGroup,
                        TotalNumberOfMessages = 2,
                        NumberOfMessagesArchived = 0,
                        Started = DateTime.Now,
                        GroupName = "Test Group",
                        NumberOfBatches = 3,
                        CurrentBatch = 0
                    };
                    await session.StoreAsync(previousArchiveOperation)
                        .ConfigureAwait(false);

                    await session.SaveChangesAsync()
                        .ConfigureAwait(false);
                }

                var domainEvents = new DomainEvents();
                var handler = new ArchiveAllInGroupHandler(documentStore,
                    new FakeDomainEvents(),
                    new ArchiveDocumentManager(),
                    new ArchivingManager(domainEvents),
                    new RetryingManager(domainEvents));

                var context = new TestableMessageHandlerContext();
                var message = new ArchiveAllInGroup { GroupId = groupId };

                // Act
                await handler.Handle(message, context)
                    .ConfigureAwait(false);

                // Assert
                using (var session = documentStore.OpenSession())
                {
                    var loadedBatch = session.Load<ArchiveBatch>(previousArchiveBatchId);
                    Assert.IsNull(loadedBatch);
                }
            }
        }

        [Test]
        public async Task ArchiveGroup_GetGroupDetails_doesnt_fail_with_invalid_groupId()
        {
            // Arrange
            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var failureGroupsViewIndex = new FailureGroupsViewIndex();
                await failureGroupsViewIndex.ExecuteAsync(documentStore);

                var groupId = "TestGroup";
                var previousArchiveBatchId = ArchiveBatch.MakeId(groupId, ArchiveType.FailureGroup, 1);

                using (var session = documentStore.OpenAsyncSession())
                {
                    var previousArchiveBatch = new ArchiveBatch { Id = previousArchiveBatchId };
                    await session.StoreAsync(previousArchiveBatch)
                        .ConfigureAwait(false);

                    var previousArchiveOperation = new ArchiveOperation
                    {
                        Id = ArchiveOperation.MakeId(groupId, ArchiveType.FailureGroup),
                        RequestId = groupId,
                        ArchiveType = ArchiveType.FailureGroup,
                        TotalNumberOfMessages = 2,
                        NumberOfMessagesArchived = 0,
                        Started = DateTime.Now,
                        GroupName = "Test Group",
                        NumberOfBatches = 3,
                        CurrentBatch = 0
                    };
                    await session.StoreAsync(previousArchiveOperation)
                        .ConfigureAwait(false);

                    await session.SaveChangesAsync()
                        .ConfigureAwait(false);
                }

                var domainEvents = new DomainEvents();
                var handler = new ArchiveAllInGroupHandler(documentStore,
                    new FakeDomainEvents(),
                    new ArchiveDocumentManager(),
                    new ArchivingManager(domainEvents),
                    new RetryingManager(domainEvents));

                var context = new TestableMessageHandlerContext();
                var message = new ArchiveAllInGroup { GroupId = groupId + "Invalid" };

                // Act
                // Assert
                Assert.DoesNotThrowAsync(async () =>
                {
                    // Act
                    await handler.Handle(message, context).ConfigureAwait(false);
                });
            }
        }
    }
}