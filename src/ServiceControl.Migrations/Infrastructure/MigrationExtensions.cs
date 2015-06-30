namespace ServiceControl.Migrations
{
    using System.Threading.Tasks;
    using Raven.Abstractions.Data;
    using Raven.Client;

    static class MigrationExtensions
    {
        /// <summary>
        /// Will wait until the store has completed indexing.
        /// </summary>
        /// <remarks>Taken from Matt Warren's example here http://stackoverflow.com/q/10316721/2608 </remarks>
        public static async Task WaitForIndexingAsync(this IDocumentStore store)
        {
            DatabaseStatistics stats;
            do
            {
                stats = await store.AsyncDatabaseCommands.GetStatisticsAsync();
                await Task.Delay(10);
            } while (stats.StaleIndexes.Length != 0);
        }
    }
}