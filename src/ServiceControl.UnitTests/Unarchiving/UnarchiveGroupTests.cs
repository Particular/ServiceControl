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
    public class UnarchiveGroupTests
    {
        [Test]
        public async Task UnarchiveGroup_skips_over_empty_batches_but_still_completes()
        {
            // Arrange
            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var groupId = "TestGroup";
                var previousUnarchiveBatchId = UnarchiveBatch.MakeId(groupId, ArchiveType.FailureGroup, 1);

                using (var session = documentStore.OpenAsyncSession())
                {
                    var previousUnarchiveBatch = new UnarchiveBatch { Id = previousUnarchiveBatchId };
                    await session.StoreAsync(previousUnarchiveBatch)
                        .ConfigureAwait(false);

                    var previousUnarchiveOperation = new UnarchiveOperation
                    {
                        Id = UnarchiveOperation.MakeId(groupId, ArchiveType.FailureGroup),
                        RequestId = groupId,
                        ArchiveType = ArchiveType.FailureGroup,
                        TotalNumberOfMessages = 2,
                        NumberOfMessagesUnarchived = 2,
                        Started = DateTime.Now,
                        GroupName = "Test Group",
                        NumberOfBatches = 3,
                        CurrentBatch = 0
                    };
                    await session.StoreAsync(previousUnarchiveOperation)
                        .ConfigureAwait(false);

                    await session.SaveChangesAsync()
                        .ConfigureAwait(false);
                }

                var domainEvents = new DomainEvents();
                var handler = new UnarchiveAllInGroupHandler(documentStore,
                    new FakeDomainEvents(),
                    new UnarchiveDocumentManager(),
                    new UnarchivingManager(domainEvents, new OperationsManager()),
                    new RetryingManager(domainEvents));

                var context = new TestableMessageHandlerContext();
                var message = new UnarchiveAllInGroup { GroupId = groupId };

                // Act
                await handler.Handle(message, context)
                    .ConfigureAwait(false);

                // Assert
                using (var session = documentStore.OpenSession())
                {
                    var loadedBatch = session.Load<UnarchiveBatch>(previousUnarchiveBatchId);
                    Assert.IsNull(loadedBatch);
                }
            }
        }

        [Test]
        public async Task UnarchiveGroup_GetGroupDetails_doesnt_fail_with_invalid_groupId()
        {
            // Arrange
            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var failureGroupsViewIndex = new ArchivedGroupsViewIndex();
                await failureGroupsViewIndex.ExecuteAsync(documentStore);

                var groupId = "TestGroup";
                var previousUnarchiveBatchId = UnarchiveBatch.MakeId(groupId, ArchiveType.FailureGroup, 1);

                using (var session = documentStore.OpenAsyncSession())
                {
                    var previousUnarchiveBatch = new UnarchiveBatch { Id = previousUnarchiveBatchId };
                    await session.StoreAsync(previousUnarchiveBatch)
                        .ConfigureAwait(false);

                    var previousUnarchiveOperation = new UnarchiveOperation
                    {
                        Id = UnarchiveOperation.MakeId(groupId, ArchiveType.FailureGroup),
                        RequestId = groupId,
                        ArchiveType = ArchiveType.FailureGroup,
                        TotalNumberOfMessages = 2,
                        NumberOfMessagesUnarchived = 0,
                        Started = DateTime.Now,
                        GroupName = "Test Group",
                        NumberOfBatches = 3,
                        CurrentBatch = 0
                    };
                    await session.StoreAsync(previousUnarchiveOperation)
                        .ConfigureAwait(false);

                    await session.SaveChangesAsync()
                        .ConfigureAwait(false);
                }

                var domainEvents = new DomainEvents();
                var handler = new UnarchiveAllInGroupHandler(documentStore,
                    new FakeDomainEvents(),
                    new UnarchiveDocumentManager(),
                    new UnarchivingManager(domainEvents, new OperationsManager()),
                    new RetryingManager(domainEvents));

                var context = new TestableMessageHandlerContext();
                var message = new UnarchiveAllInGroup { GroupId = groupId + "Invalid" };

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