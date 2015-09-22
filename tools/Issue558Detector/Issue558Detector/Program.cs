using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Raven.Client.Document;

namespace Issue558Detector
{
    class Program
    {
        static void Main(string[] args)
        {
            var store = new DocumentStore
            {
                Url = "http://localhost:33333/storage",
            };

            store.Initialize();

            Console.Write("Creating Temp Index (this may take some time)...");

            var index = new MessageHistories();

            try
            {
                store.ExecuteIndex(index);

                while (store.DatabaseCommands.GetStatistics().StaleIndexes.Length != 0)
                {
                    Thread.Sleep(10);
                }

                Console.WriteLine(" DONE");

                Console.WriteLine("\nSearching for messages which may have been affected by issue https://github.com/Particular/ServiceControl/pull/558\n");

                var dangerLevel = CheckForMessagesAffectedByIssue(store);

                switch (dangerLevel)
                {
                    case 0: // Not affected
                        Console.WriteLine("You are NOT affected by issue https://github.com/Particular/ServiceControl/pull/558");
                        Console.WriteLine("However, we ask all users of ServiceControl using 1.6.x to update to 1.6.3 immediately.");
                        break;
                    case 1: // Maybe affected
                        Console.WriteLine("You may be affected by issue https://github.com/Particular/ServiceControl/pull/558");
                        Console.WriteLine("Please upgrade ServiceControl to 1.6.3 immediately.");
                        Console.WriteLine("Contact us via support (http://particular.net/support) and we will work with you to get your situation sorted out.");
                        break;
                    case 2: // Definitely affected
                        Console.WriteLine("You are affected by issue https://github.com/Particular/ServiceControl/pull/558");
                        Console.WriteLine("Please upgrade ServiceControl to 1.6.3 immediately.");
                        Console.WriteLine("Contact us via support (http://particular.net/support) and we will work with you to get your situation sorted out.");
                        break;
                }
            }
            finally
            {
                Console.WriteLine("Removing Temp Index");
                store.DatabaseCommands.DeleteIndex(index.IndexName);
            }
        }

        private static int CheckForMessagesAffectedByIssue(DocumentStore store)
        {
            var dangerLevel = 0;

            using (var session = store.OpenSession())
            {
                var query = session.Query<MessageHistories.Result, MessageHistories>();

                using (var stream = session.Advanced.Stream(query))
                {
                    while (stream.MoveNext())
                    {
                        var classifiedTimeline = AnalyseTimeline(stream.Current.Document.Events).ToArray();
                        if (classifiedTimeline.Any(x => x.Classification != EventClassification.Ok))
                        {
                            var failedMessageId = stream.Current.Document.MessageId.Replace("/message/", "FailedMessages/");

                            var failedMessage = session.Load<FailedMessage>(failedMessageId);

                            var attempt = failedMessage.ProcessingAttempts.Last();

                            Console.WriteLine("Message Id:         {0}", attempt.MessageId);
                            Console.WriteLine("Receiving Endpoint: {0}", attempt.FailureDetails.AddressOfFailingEndpoint);
                            Console.WriteLine("Message Type:       {0}", attempt.MessageMetadata.MessageType);
                            Console.WriteLine("Timeline: ");

                            foreach (var item in classifiedTimeline)
                            {
                                Console.WriteLine("\t{0:dd-MMM-yy HH:mm:ss K}: [{2,7}] {1}", item.Entry.When, item.Entry.Event, item.Classification);
                                if (item.Classification == EventClassification.NotOk)
                                {
                                    dangerLevel = 2;
                                }
                                else if (item.Classification == EventClassification.Unknown)
                                {
                                    dangerLevel = Math.Max(dangerLevel, 1);
                                }
                            }
                            Console.WriteLine();
                            Console.WriteLine();
                        }
                    }
                }
            }

            return dangerLevel;
        }

        private static IEnumerable<ClassifiedTimelineEntry> AnalyseTimeline(TimelineEntry[] entries)
        {
            bool? canRetry = null;

            foreach (var entry in entries.OrderBy(e => e.When))
            {
                var status = EventClassification.Ok;

                switch (entry.Event)
                {
                    case "MessageFailed":
                        canRetry = true;
                        break;
                    case "MessagesSubmittedForRetry":
                        if (canRetry.HasValue)
                        {
                            if (canRetry.Value == false)
                            {
                                status = EventClassification.NotOk;
                            }
                        }
                        else
                        {
                            status = EventClassification.Unknown;
                        }
                        canRetry = false;
                        break;
                    case "MessageSubmittedForRetry": // Event for Retries before SC 1.6
                    case "MessageFailureResolvedByRetry": // Only is audit ingestion is on
                    case "FailedMessageArchived": // Single message archived
                    case "FailedMessageGroupArchived": // SC1.6 group archived
                        canRetry = false;
                        break;
                    default: // An event we didn't account for. A Retry following this is suspect
                        canRetry = null;
                        Console.WriteLine("Unexpected event for message: {0}", entry.Event);
                        break;
                }

                yield return new ClassifiedTimelineEntry
                {
                    Entry = entry,
                    Classification = status
                };

            }
        }
    }
}
