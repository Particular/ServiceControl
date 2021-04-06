namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.Text;
    using System.Threading;
    using NServiceBus.Logging;
    using Raven.Abstractions.Data;
    using Raven.Client.Embedded;
    using Raven.Database;
    using Raven.Json.Linq;

    public static class Extensions
    {
        public static void Query<TState>(this DocumentDatabase db, string index, IndexQuery query, Action<RavenJObject, TState> onItem, TState state, CancellationToken cancellationToken)
        {
            var results = db.Queries.Query(index, query, cancellationToken);
            foreach (var doc in results.Results)
            {
                onItem(doc, state);
            }
        }

        public static void ThrowWhenIndexErrors(this EmbeddableDocumentStore documentStore)
        {
            var statistics = documentStore.DatabaseCommands.GetStatistics();

            if (statistics.Errors.Length > 0)
            {
                var text = new StringBuilder();
                text.AppendLine("Detected RavenDB index errors, please start maintenance mode and resolve the following issues:");
                foreach (var indexError in statistics.Errors)
                {
                    text.AppendLine($"- Index [{indexError.IndexName}] error: {indexError.Error} (Action: {indexError.Action},  Doc: {indexError.Document}, At: {indexError.Timestamp})");
                }
                throw new Exception(text.ToString());
            }
        }

        public static void WaitUntilNoStaleIndexes(this EmbeddableDocumentStore documentStore)
        {
            var interval = TimeSpan.FromMinutes(1);
            var next = DateTime.MinValue;
            string[] staleIndexes;

            // Check for the number of stale indexes every second, but report only an update only every 1 minutes
            while ((staleIndexes = documentStore.DatabaseCommands.GetStatistics().StaleIndexes).Length > 0)
            {
                var now = DateTime.UtcNow;
                if (next < now)
                {
                    var text = new StringBuilder();
                    text.AppendLine("Stale indexes detected, delaying start until all indexes are non-stale. DO NOT KILL THIS PROCESS! Operation can run for a very long time!");
                    foreach (var staleIndex in staleIndexes)
                    {
                        text.AppendLine($"- {staleIndex}");
                    }
                    Log.Warn(text.ToString());
                    next = now + interval;
                }
                Thread.Sleep(1000);
            }
        }

        static ILog Log = LogManager.GetLogger(typeof(Extensions).Namespace);
    }
}