namespace Particular.ServiceControl.DbMigrations
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using global::ServiceControl.MessageFailures;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;

    public class SplitFailedMessageDocumentsMigration : IMigration
    {
        private readonly LoggingSettings loggingSettings;

        public SplitFailedMessageDocumentsMigration(LoggingSettings loggingSettings)
        {
            this.loggingSettings = loggingSettings;
        }

        public string Apply(IDocumentStore store)
        {
            string report;
            var stats = new MigrationStats();
            var now = DateTime.Now;
            var fileName = Path.Combine(loggingSettings.LogPath, $"Issue842.Report.{now.ToString("yyyy.MM.dd-HH.mm", CultureInfo.InvariantCulture)}.txt");

            using (var file = File.OpenWrite(fileName))
            using (var writer = new StreamWriter(file))
            {
                var currentPage = 0;
                int retrievedResults;
                do
                {
                    using (var session = store.OpenSession())
                    {

                        var failedMessages = session.Advanced.LoadStartingWith<FailedMessage>(
                            FailedMessage.MakeDocumentId(string.Empty),
                            start: PageSize * currentPage,
                            pageSize: PageSize);

                        currentPage++;

                        retrievedResults = failedMessages.Length;

                        foreach (var failedMessage in failedMessages)
                        {
                            stats += Check(failedMessage, writer);
                        }

                        session.SaveChanges();
                    }
                } while (retrievedResults == PageSize);


                if (stats.FoundProblem > 0)
                {
                    report = $"Found {stats.FoundProblem} issue(s) in {stats.Checked} Failed Message document(s).";
                }
                else
                {
                    report = "No problems found";
                }

                writer.WriteLine($"\n{report}");
            }
            return $"{report} - Report written to {fileName}";
        }

        private MigrationStats Check(FailedMessage failedMessage, TextWriter writer)
        {
            var stats = new MigrationStats
            {
                Checked = 1
            };

            var processingAttempts = failedMessage.ProcessingAttempts
                .Select(x => new ProcessingAttemptRecord(x))
                .ToArray();

            var retries = processingAttempts.Where(x => x.IsRetry).ToArray();

            var nonRetries = processingAttempts.Except(retries).ToArray();

            if (nonRetries.Length <= 1)
            {
                return stats;
            }

            writer.WriteLine($"{failedMessage.UniqueMessageId} {failedMessage.Status}");
            stats.FoundProblem = 1;

            foreach (var attempt in processingAttempts)
            {
                var retryMarker = attempt.IsRetry ? "R" : "A";

                writer.WriteLine($"\t{retryMarker}: {attempt.MessageId} {attempt.MessageType} {attempt.FailedQ}");
            }

            writer.WriteLine();

            return stats;
        }

        class ProcessingAttemptRecord
        {
            public ProcessingAttemptRecord(FailedMessage.ProcessingAttempt attempt)
            {
                Attempt = attempt;

                string uniqueMessageId;
                if (attempt.Headers.TryGetValue("ServiceControl.Retry.UniqueMessageId", out uniqueMessageId))
                {
                    IsRetry = true;
                }

                object msgType;
                if (attempt.MessageMetadata.TryGetValue("MessageType", out msgType))
                {
                    MessageType = (string) msgType;
                }

                string failedQ;
                if (attempt.Headers.TryGetValue("NServiceBus.FailedQ", out failedQ))
                {
                    FailedQ = failedQ;
                }

            }

            public FailedMessage.ProcessingAttempt Attempt { get; }

            public bool IsRetry { get; }
            public string MessageType { get; } = "~";
            public string FailedQ { get; } = "~";
            public string MessageId => Attempt.MessageId;
        }

        struct MigrationStats
        {
            public int Checked { get; set; }
            public int FoundProblem { get; set; }

            public static MigrationStats operator +(MigrationStats left, MigrationStats right)
                => new MigrationStats
                {
                    Checked = left.Checked + right.Checked,
                    FoundProblem = left.FoundProblem + right.FoundProblem
                };
        }

        public string MigrationId { get; } = "Split Failed Message Documents";

        private const int PageSize = 1024;
    }
}