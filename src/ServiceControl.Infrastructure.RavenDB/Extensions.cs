namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.Text;
    using System.Threading;
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

                text.AppendLine().AppendLine("See: https://docs.particular.net/search?q=servicecontrol+troubleshooting");
                throw new Exception(text.ToString());
            }
        }
    }
}