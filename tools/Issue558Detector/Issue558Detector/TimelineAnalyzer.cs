using System;
using System.Collections.Generic;
using System.Linq;

namespace Issue558Detector
{
    public static class TimelineAnalyzer
    {
        public static IEnumerable<ClassifiedTimelineEntry> AnalyzeTimeline(TimelineEntry[] entries)
        {
            // Until we see the first event it is OK to Retry
            var canRetry = true;
            var status = EventClassification.Ok;
            var timelinePoisoned = false;

            foreach (var entry in entries.OrderBy(e => e.When))
            {
                if (!timelinePoisoned)
                {
                    switch (entry.Event)
                    {
                        case "MessageFailed":
                            canRetry = true;
                            break;
                        case "MessagesSubmittedForRetry":
                            if (canRetry == false)
                            {
                                status = EventClassification.NotOk;
                                timelinePoisoned = true;
                            }
                            canRetry = false;
                            break;

                        // If a message is retried successfully with audit ingestion off then there won't be a resolved message. 
                        // But if a message fails a retry with audit ingestion off then there should be another Message Failed event.
                        case "MessageSubmittedForRetry": // Event for Retries before SC 1.6
                        case "MessageFailureResolvedByRetry": // Only if audit ingestion is on
                        case "FailedMessageArchived": // Single message archived
                        case "FailedMessageGroupArchived": // SC1.6 group archived
                            canRetry = false;
                            break;
                        default: // An event we didn't account for. A Retry following this is suspect
                            status = EventClassification.Unknown;
                            timelinePoisoned = true;
                            Console.WriteLine("Unexpected event for message: {0}", entry.Event);
                            break;
                    }
                }

                yield return new ClassifiedTimelineEntry
                {
                    Entry = entry,
                    Classification = status
                };

                if (timelinePoisoned)
                {
                    status = EventClassification.Unknown;
                }
            }
        }
    }
}