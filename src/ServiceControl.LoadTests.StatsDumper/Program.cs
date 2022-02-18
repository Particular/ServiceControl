namespace ServiceControl.LoadTests.StatsDumper
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Dapper;

    class Program
    {
        static async Task Main()
        {
            var connectionString = Environment.GetEnvironmentVariable("PlatformSpike_AzureSQLConnectionString");

            while (true)
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    var result = await connection.QuerySingleAsync(DumpStats).ConfigureAwait(false);

                    Console.WriteLine($"{(DateTime)result.Time}, {(int)result.Count}");

                    await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                }
            }
        }

        const string DumpStats = @"
SELECT CURRENT_TIMESTAMP as [Time], count(*) as [Count]
FROM [dbo].[MessagesView]
";

    }
}
