using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Issue558Detector.Tests
{
    [TestClass]
    public class ScenarioTests
    {
        static string Failed = "MessageFailed";
        static string NewRetry = "MessagesSubmittedForRetry";
        static string OldRetry = "MessageSubmittedForRetry";
        static string Resolved = "MessageFailureResolvedByRetry";
        static string Archived = "FailedMessageArchived";
        static string ArchivedAsPartOfAGroup = "FailedMessageGroupArchived";

        [TestMethod]
        public void NewRetryDirectlyAfterFailureIsOk()
        {
            var timeline = CreateTimeline(Failed, NewRetry).ToArray();

            var results = TimelineAnalyzer.AnalyzeTimeline(timeline.ToArray()).ToArray();

            Assert.AreEqual(timeline.Length, results.Length, "Analyzed Timeline should have the same number of events as the Ingested Timeline");
            Assert.IsTrue(results.All(x => x.Classification == EventClassification.Ok), "All events should be OK");
        }

        [TestMethod]
        public void NewRetryAfterArchivedIsNotOk()
        {
            var timeline = CreateTimeline(Failed, Archived, NewRetry).ToArray();

            var results = TimelineAnalyzer.AnalyzeTimeline(timeline).ToArray();

            Assert.AreEqual(timeline.Length, results.Length, "Analyzed Timeline should have the same number of events as the Ingested Timeline");

            Assert.AreEqual(EventClassification.Ok, results[0].Classification, "Initial Failed message should be OK");
            Assert.AreEqual(EventClassification.Ok, results[1].Classification, "Archiving should be OK");
            Assert.AreEqual(EventClassification.NotOk, results[2].Classification, "Retrying an Archived message is Not OK");
        }

        [TestMethod]
        public void NewRetryAfterArchivedAsGroupNotOk()
        {
            var timeline = CreateTimeline(Failed, ArchivedAsPartOfAGroup, NewRetry).ToArray();

            var results = TimelineAnalyzer.AnalyzeTimeline(timeline).ToArray();

            Assert.AreEqual(timeline.Length, results.Length, "Analyzed Timeline should have the same number of events as the Ingested Timeline");

            Assert.AreEqual(EventClassification.Ok, results[0].Classification, "Initial Failed message should be OK");
            Assert.AreEqual(EventClassification.Ok, results[1].Classification, "Archiving failed message should be OK");
            Assert.AreEqual(EventClassification.NotOk, results[2].Classification, "Retrying an Archived message is Not OK");
        }

        [TestMethod]
        public void NewRetryAfterResolvedByOldRetryIsNotOk()
        {
            var timeline = CreateTimeline(Failed, OldRetry, Resolved, NewRetry).ToArray();

            var results = TimelineAnalyzer.AnalyzeTimeline(timeline).ToArray();

            Assert.AreEqual(timeline.Length, results.Length, "Analyzed Timeline should have the same number of events as the Ingested Timeline");

            Assert.AreEqual(EventClassification.Ok, results[0].Classification, "Initial Failed message should be OK");
            Assert.AreEqual(EventClassification.Ok, results[1].Classification, "Retrying failed message should be OK");
            Assert.AreEqual(EventClassification.Ok, results[2].Classification, "Resolved message is OK");
            Assert.AreEqual(EventClassification.NotOk, results[3].Classification, "Retrying an resolved message is Not OK");
        }


        [TestMethod]
        public void NewRetryAfterOldRetryIssuedNotOk()
        {
            // This will happen for one of two reasons. Both are not OK
            // a) Audit Ingestion is switched off and the message was successful.
            // b) The Retry is still in flight at the time the new new retry is attempted.
            var timeline = CreateTimeline(Failed, OldRetry, NewRetry).ToArray();

            var results = TimelineAnalyzer.AnalyzeTimeline(timeline).ToArray();

            Assert.AreEqual(timeline.Length, results.Length, "Analyzed Timeline should have the same number of events as the Ingested Timeline");

            Assert.AreEqual(EventClassification.Ok, results[0].Classification, "Initial Failed message should be OK");
            Assert.AreEqual(EventClassification.Ok, results[1].Classification, "Retrying failed message should be OK");
            Assert.AreEqual(EventClassification.NotOk, results[2].Classification, "Retrying a message which is already being retried is Not OK");
        }

        [TestMethod]
        public void NewRetryAfterOldRetryFailedOk()
        {
            var timeline = CreateTimeline(Failed, OldRetry, Failed, NewRetry).ToArray();

            var results = TimelineAnalyzer.AnalyzeTimeline(timeline).ToArray();

            Assert.AreEqual(timeline.Length, results.Length, "Analyzed Timeline should have the same number of events as the Ingested Timeline");

            Assert.AreEqual(EventClassification.Ok, results[0].Classification, "Initial Failed message should be OK");
            Assert.AreEqual(EventClassification.Ok, results[1].Classification, "Retrying failed message should be OK");
            Assert.AreEqual(EventClassification.Ok, results[2].Classification, "Failure of Retried Message should be OK");
            Assert.AreEqual(EventClassification.Ok, results[3].Classification, "Retrying a message which has failed a previous retry should be OK");
        }

        [TestMethod]
        public void NewRetryAfterNewRetryIsNotOk()
        {
            var timeline = CreateTimeline(Failed, NewRetry, NewRetry).ToArray();

            var results = TimelineAnalyzer.AnalyzeTimeline(timeline).ToArray();
            
            Assert.AreEqual(timeline.Length, results.Length, "Analyzed Timeline should have the same number of events as the Ingested Timeline");

            Assert.AreEqual(EventClassification.Ok, results[0].Classification, "Initial Failed message should be OK");
            Assert.AreEqual(EventClassification.Ok, results[1].Classification, "Retrying failed message should be OK");
            Assert.AreEqual(EventClassification.NotOk, results[2].Classification, "Retrying a message which is already being retried is Not OK");
        }

        [TestMethod]
        public void NewRetryAfterNewRetryFailsIsOk()
        {
            var timeline = CreateTimeline(Failed, NewRetry, Failed, NewRetry).ToArray();

            var results = TimelineAnalyzer.AnalyzeTimeline(timeline).ToArray();

            Assert.AreEqual(timeline.Length, results.Length, "Analyzed Timeline should have the same number of events as the Ingested Timeline");

            Assert.AreEqual(EventClassification.Ok, results[0].Classification, "Initial Failed message should be OK");
            Assert.AreEqual(EventClassification.Ok, results[1].Classification, "Retrying failed message should be OK");
            Assert.AreEqual(EventClassification.Ok, results[2].Classification, "Message failing retry is OK");
            Assert.AreEqual(EventClassification.Ok, results[3].Classification, "Retrying a message which failed a previous retry is OK");
        }

        [TestMethod]
        public void NewRetryAfterNewRetrySucceedsIsNotOk()
        {
            var timeline = CreateTimeline(Failed, NewRetry, Resolved, NewRetry).ToArray();

            var results = TimelineAnalyzer.AnalyzeTimeline(timeline).ToArray();

            Assert.AreEqual(timeline.Length, results.Length, "Analyzed Timeline should have the same number of events as the Ingested Timeline");

            Assert.AreEqual(EventClassification.Ok, results[0].Classification, "Initial Failed message should be OK");
            Assert.AreEqual(EventClassification.Ok, results[1].Classification, "Retrying failed message should be OK");
            Assert.AreEqual(EventClassification.Ok, results[2].Classification, "Message resolved by retry is OK");
            Assert.AreEqual(EventClassification.NotOk, results[3].Classification, "Retrying a message which was resolved by a previous retry is Not OK");
        }

        [TestMethod]
        public void OnceTimelineIsPoisonedAllFurtherEventsAreUnknown()
        {
            var timeline = CreateTimeline(Failed, Archived, NewRetry, Resolved).ToArray();

            var results = TimelineAnalyzer.AnalyzeTimeline(timeline).ToArray();

            Assert.AreEqual(timeline.Length, results.Length, "Analyzed Timeline should have the same number of events as the Ingested Timeline");

            Assert.AreEqual(EventClassification.Ok, results[0].Classification, "Initial Failed message should be OK");
            Assert.AreEqual(EventClassification.Ok, results[1].Classification, "Archiving failed message should be OK");
            Assert.AreEqual(EventClassification.NotOk, results[2].Classification, "Retrying a message which is archived is Not OK");
            Assert.AreEqual(EventClassification.Unknown, results[3].Classification, "A message resolving after an invalid retry is Unknown");
        }

        [TestMethod]
        public void AnInvalidRetryWhenTimelineIsAlreadyPoisonedIsUnknown()
        {
            var timeline = CreateTimeline(Failed, Archived, NewRetry, Resolved, NewRetry).ToArray();

            var results = TimelineAnalyzer.AnalyzeTimeline(timeline).ToArray();

            Assert.AreEqual(timeline.Length, results.Length, "Analyzed Timeline should have the same number of events as the Ingested Timeline");

            Assert.AreEqual(EventClassification.Ok, results[0].Classification, "Initial Failed message should be OK");
            Assert.AreEqual(EventClassification.Ok, results[1].Classification, "Archiving failed message should be OK");
            Assert.AreEqual(EventClassification.NotOk, results[2].Classification, "Retrying a message which is archived is Not OK");
            Assert.AreEqual(EventClassification.Unknown, results[3].Classification, "A message resolving after an invalid retry should be Unknown");
            Assert.AreEqual(EventClassification.Unknown, results[4].Classification, "An invalid retry on a poisoned timeline is still Unknown");
        }

        [TestMethod]
        public void IfANewRetryIsTheFirstThingInAMessageTimelineThenTheStatusIsOk()
        {
            var timeline = CreateTimeline(NewRetry).ToArray();

            var results = TimelineAnalyzer.AnalyzeTimeline(timeline).ToArray();

            Assert.AreEqual(timeline.Length, results.Length, "Analyzed Timeline should have the same number of events as the Ingested Timeline");

            Assert.AreEqual(EventClassification.Ok, results[0].Classification, "Retry as first timeline entry is Ok");
        }

        [TestMethod]
        public void AnUnexpectedEventCannotBeClassified()
        {
            var timeline = CreateTimeline(Failed, "Unexpected Event").ToArray();

            var results = TimelineAnalyzer.AnalyzeTimeline(timeline).ToArray();

            Assert.AreEqual(timeline.Length, results.Length, "Analyzed Timeline should have the same number of events as the Ingested Timeline");

            Assert.AreEqual(EventClassification.Ok, results[0].Classification, "Initial Failed Message should be OK");
            Assert.AreEqual(EventClassification.Unknown, results[1].Classification, "Unexpected Event should be Unknown");
        }

        [TestMethod]
        public void AfterAnUnexpectedEventTheTimelineIsPosioned()
        {
            var timeline = CreateTimeline(Failed, "Unexpected Event", NewRetry).ToArray();

            var results = TimelineAnalyzer.AnalyzeTimeline(timeline).ToArray();

            Assert.AreEqual(timeline.Length, results.Length, "Analyzed Timeline should have the same number of events as the Ingested Timeline");

            Assert.AreEqual(EventClassification.Ok, results[0].Classification, "Initial Failed Message should be OK");
            Assert.AreEqual(EventClassification.Unknown, results[1].Classification, "Unknown Event should be Unknown");
            Assert.AreEqual(EventClassification.Unknown, results[2].Classification, "Status of Retry directly after Unknown Event is Unknown");
        }


        private static IEnumerable<TimelineEntry> CreateTimeline(params string[] events)
        {
            var yesterday = DateTime.UtcNow.AddDays(-1);

            var time = yesterday;

            foreach (var evt in events)
            {
                yield return new TimelineEntry {Event = evt, When = time};
                time = time.AddMinutes(5);
            }
        }
    }
}
