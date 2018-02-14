﻿using NUnit.Framework;
using ServiceControl.Recoverability;
using ServiceControl.UnitTests.Recoverability;
using System;

namespace ServiceControl.UnitTests.Archiving
{
    [TestFixture]
    public class ArchiveGroupTests
    {
        [Test]
        public void ArchiveGroup_skips_over_empty_batches_but_still_completes()
        {
            // Arrange
            using (var documentStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var groupId = "TestGroup";
                var previousArchiveBatchId = ArchiveBatch.MakeId(groupId, ArchiveType.FailureGroup, 1);

                using (var session = documentStore.OpenSession())
                {
                    var previousAchiveBatch = new ArchiveBatch { Id = previousArchiveBatchId };
                    session.Store(previousAchiveBatch);

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
                    session.Store(previousArchiveOperation);

                    session.SaveChanges();
                }

                var testBus = new TestBus();
                var documentManager = new ArchiveDocumentManager();
                var archivingManager = new ArchivingManager();
                var retryingManager = new RetryingManager();
                var handler = new ArchiveAllInGroupHandler(testBus, documentStore, documentManager, archivingManager, retryingManager);

                var message = new ArchiveAllInGroup { GroupId = groupId };

                // Act
                handler.Handle(message);

                // Assert
                using (var session = documentStore.OpenSession())
                {
                    var loadedBatch = session.Load<ArchiveBatch>(previousArchiveBatchId);
                    Assert.IsNull(loadedBatch);
                }
            }
        }
    }
}