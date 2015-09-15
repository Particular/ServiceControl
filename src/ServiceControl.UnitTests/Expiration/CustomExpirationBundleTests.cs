namespace ServiceControl.UnitTests.Expiration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using MessageAuditing;
    using MessageFailures;
    using NUnit.Framework;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.RavenDB.Expiration;
    using ServiceControl.Operations.BodyStorage.RavenAttachments;

    [TestFixture]
    public class CustomExpirationBundleTests
    {
        [Test]
        public void Processed_messages_are_being_expired()
        {
            using (var documentStore = GetDocumentStore())
            {
                var processedMessage = new ProcessedMessage
                {
                    Id = "1",
                    ProcessedAt = DateTime.UtcNow.AddHours(-(Settings.HoursToKeepMessagesBeforeExpiring*3)),
                };

                var processedMessage2 = new ProcessedMessage
                {
                    Id = "2",
                    ProcessedAt = DateTime.UtcNow.AddHours(-(Settings.HoursToKeepMessagesBeforeExpiring*2)),
                };
                processedMessage2.MessageMetadata["IsSystemMessage"] = true;

                using (var session = documentStore.OpenSession())
                {
                    session.Store(processedMessage);
                    session.Store(processedMessage2);
                    session.SaveChanges();
                }

                documentStore.WaitForIndexing();
                Thread.Sleep(Settings.ExpirationProcessTimerInSeconds*1000*2);

                using (var session = documentStore.OpenSession())
                {
                    var msg = session.Load<ProcessedMessage>(processedMessage.Id);
                    Assert.Null(msg);

                    msg = session.Load<ProcessedMessage>(processedMessage2.Id);
                    Assert.Null(msg);
                }
            }
        }

        [Test]
        public void Many_processed_messages_are_being_expired()
        {
            using (var documentStore = GetDocumentStore())
            {
                new ExpiryProcessedMessageIndex().Execute(documentStore);

                var processedMessage = new ProcessedMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    ProcessedAt = DateTime.UtcNow.AddMinutes(-DateTime.UtcNow.Millisecond%30).AddDays(-(Settings.HoursToKeepMessagesBeforeExpiring*3)),
                };

                var processedMessage2 = new ProcessedMessage
                {
                    Id = "2",
                    ProcessedAt = DateTime.UtcNow,
                };

                using (var session = documentStore.OpenSession())
                {
                    for (var i = 0; i < 100; i++)
                    {
                        processedMessage = new ProcessedMessage
                        {
                            Id = Guid.NewGuid().ToString(),
                            ProcessedAt = DateTime.UtcNow.AddMinutes(-DateTime.UtcNow.Millisecond%30).AddDays(-(Settings.HoursToKeepMessagesBeforeExpiring*3)),
                        };

                        session.Store(processedMessage);
                    }

                    session.Store(processedMessage2);
                    session.SaveChanges();
                }

                documentStore.WaitForIndexing();
                Thread.Sleep(Settings.ExpirationProcessTimerInSeconds*1000*10);
                using (var session = documentStore.OpenSession())
                {
                    var results = session.Query<ProcessedMessage, ExpiryProcessedMessageIndex>().Customize(x => x.WaitForNonStaleResults()).ToArray();
                    Assert.AreEqual(1, results.Length);

                    var msg = session.Load<ProcessedMessage>(processedMessage.Id);
                    Assert.Null(msg, "Message with datestamp {0} and ID {1} was found", processedMessage.ProcessedAt, processedMessage.Id);

                    msg = session.Load<ProcessedMessage>(processedMessage2.Id);
                    Assert.NotNull(msg);
                }
            }
        }

        [Test]
        public void Only_processed_messages_are_being_expired()
        {
            using (var documentStore = GetDocumentStore())
            {
                new ExpiryProcessedMessageIndex().Execute(documentStore);

                var processedMessage = new ProcessedMessage
                {
                    Id = "1",
                    ProcessedAt = DateTime.UtcNow.AddHours(-(Settings.HoursToKeepMessagesBeforeExpiring*3)),
                };

                var processedMessage2 = new ProcessedMessage
                {
                    Id = "2",
                    ProcessedAt = DateTime.UtcNow,
                };
                processedMessage2.MessageMetadata["IsSystemMessage"] = true;

                using (var session = documentStore.OpenSession())
                {
                    session.Store(processedMessage);
                    session.Store(processedMessage2);
                    session.SaveChanges();
                }

                documentStore.WaitForIndexing();
                Thread.Sleep(Settings.ExpirationProcessTimerInSeconds*1000*2);

                using (var session = documentStore.OpenSession())
                {
                    var msg = session.Load<ProcessedMessage>(processedMessage.Id);
                    Assert.Null(msg);

                    msg = session.Load<ProcessedMessage>(processedMessage2.Id);
                    Assert.NotNull(msg);
                }
            }
        }

        [Test]
        public void Stored_bodies_are_being_removed_when_message_expires()
        {
            using (var documentStore = GetDocumentStore())
            {
                // Store expired message with associated body
                var messageId = "21";
                var bodyStorage = new RavenAttachmentsBodyStorage
                {
                    DocumentStore = documentStore
                };

                var processedMessage = new ProcessedMessage
                {
                    Id = "1",
                    ProcessedAt = DateTime.UtcNow.AddHours(-(Settings.HoursToKeepMessagesBeforeExpiring*2))
                };

                processedMessage.MessageMetadata["MessageId"] = messageId;

                using (var session = documentStore.OpenSession())
                {
                    session.Store(processedMessage);
                    session.SaveChanges();
                }

                var body = new byte[]
                {
                    1,
                    2,
                    3,
                    4,
                    5
                };

                using (var stream = new MemoryStream(body))
                    bodyStorage.Store(messageId, "binary", 5, stream);

                // Wait for expiry
                documentStore.WaitForIndexing();
                Thread.Sleep(Settings.ExpirationProcessTimerInSeconds*1000*2);

                // Verify message expired
                using (var session = documentStore.OpenSession())
                {
                    var msg = session.Load<ProcessedMessage>(processedMessage.Id);
                    Assert.Null(msg, "Audit document should be deleted");
                }

                // Verify body expired
                Stream dummy;
                var bodyFound = bodyStorage.TryFetch(messageId, out dummy);
                Assert.False(bodyFound, "Audit document body should be deleted");
            }
        }

        [Test]
        public void Recent_processed_messages_are_not_being_expired()
        {
            using (var documentStore = GetDocumentStore())
            {
                new ExpiryProcessedMessageIndex().Execute(documentStore);

                var processedMessage = new ProcessedMessage
                {
                    Id = "1",
                    ProcessedAt = DateTime.UtcNow,
                };

                using (var session = documentStore.OpenSession())
                {
                    session.Store(processedMessage);
                    session.SaveChanges();
                }

                documentStore.WaitForIndexing();
                Thread.Sleep(Settings.ExpirationProcessTimerInSeconds*1000*2);

                using (var session = documentStore.OpenSession())
                {
                    var msg = session.Load<ProcessedMessage>(processedMessage.Id);
                    Assert.NotNull(msg);
                }
            }
        }

        [Test]
        public void Errors_are_not_being_expired()
        {
            using (var documentStore = GetDocumentStore())
            {
                var failedMsg = new FailedMessage
                {
                    Id = "1",
                    ProcessingAttempts = new List<FailedMessage.ProcessingAttempt>
                    {
                        new FailedMessage.ProcessingAttempt
                        {
                            AttemptedAt = DateTime.UtcNow.AddHours(-(Settings.HoursToKeepMessagesBeforeExpiring * 3))
                        },
                        new FailedMessage.ProcessingAttempt
                        {
                            AttemptedAt = DateTime.UtcNow.AddHours(-(Settings.HoursToKeepMessagesBeforeExpiring * 2)),
                        }
                    },
                    Status = FailedMessageStatus.Unresolved,
                };

                using (var session = documentStore.OpenSession())
                {
                    session.Store(failedMsg);
                    session.SaveChanges();
                }

                documentStore.WaitForIndexing();
                Thread.Sleep(Settings.ExpirationProcessTimerInSeconds * 1000 * 2);

                using (var session = documentStore.OpenSession())
                {
                    var msg = session.Load<FailedMessage>(failedMsg.Id);
                    Assert.NotNull(msg);
                }
            }
        }


        EmbeddableDocumentStore GetDocumentStore()
        {
            var documentStore = InMemoryStoreBuilder.GetInMemoryStore(withExpiration: true);

            var customIndex = new ExpiryProcessedMessageIndex();
            customIndex.Execute(documentStore);
            return documentStore;
        }

    }
}
