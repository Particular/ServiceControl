namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;

    // SQL Server compiles the endpoint filter for the first endpoint asked for, then reuses that plan for every other endpoint.
    class EndpointFilterPlanReuseTests : EFPersistenceTestFixture
    {
        [TestCase(BusyEndpoint, QuietEndpoint)]
        [TestCase(QuietEndpoint, BusyEndpoint)]
        public async Task Listing_an_endpoint_stays_cheap_on_a_plan_compiled_for_another_endpoint(string compiledFor, string listed)
        {
            await Seed();

            await ListFirstPage(compiledFor);
            await ListFirstPage(listed);

            var statements = await StatementReads();
            var report = string.Join("\n", statements.Select(s => $"{s.LastReads} reads: {s.Text}"));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(statements, Has.Some.Matches<(string Text, long LastReads)>(s => s.Text.Contains("COUNT_BIG")), report);
                Assert.That(statements, Has.Some.Matches<(string Text, long LastReads)>(s => s.Text.Contains("ORDER BY")), report);
                Assert.That(statements.Select(s => s.LastReads), Has.All.LessThan(MaxReads), report);
            }
        }

        async Task Seed()
        {
            await using var connection = new SqlConnection(configuration.ConnectionString);
            await connection.OpenAsync(TestTimeoutCancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = $$"""
                WITH n AS (SELECT TOP (50000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS i FROM sys.all_objects a CROSS JOIN sys.all_objects b)
                INSERT [{{configuration.Schema}}].[AuditMessages] (CreatedOn, UniqueMessageId, TimeSent, ProcessedAt, IsSystemMessage, Status, ReceivingEndpointName, HeadersJson, BodyState, BodySize)
                SELECT DATEADD(hour, -(i % 168), @hour), NEWID(), DATEADD(second, i % 3600, DATEADD(hour, -(i % 168), @hour)), DATEADD(hour, -(i % 168), @hour), 0, 1,
                       CASE WHEN i % 2500 = 0 THEN @quiet WHEN i % 10 < 6 THEN @busy ELSE @other END, '{}', 0, 0
                FROM n;
                UPDATE STATISTICS [{{configuration.Schema}}].[AuditMessages] WITH FULLSCAN;
                """;
            command.Parameters.AddWithValue("@hour", TimeSent);
            command.Parameters.AddWithValue("@quiet", QuietEndpoint);
            command.Parameters.AddWithValue("@busy", BusyEndpoint);
            command.Parameters.AddWithValue("@other", OtherEndpoint);
            await command.ExecuteNonQueryAsync(TestTimeoutCancellationToken);
        }

        Task ListFirstPage(string endpoint) =>
            MessagesViewStore.QueryMessagesByReceivingEndpoint(false, endpoint, new PagingInfo(pageSize: 100), new SortInfo("time_sent", "desc"), cancellationToken: TestTimeoutCancellationToken);

        async Task<List<(string Text, long LastReads)>> StatementReads()
        {
            await using var connection = new SqlConnection(configuration.ConnectionString);
            await connection.OpenAsync(TestTimeoutCancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT st.text, qs.last_logical_reads
                FROM sys.dm_exec_query_stats AS qs
                CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS st
                WHERE CHARINDEX(@table, st.text) > 0 AND CHARINDEX('@endpointName', st.text) > 0
                """;
            command.Parameters.AddWithValue("@table", $"[{configuration.Schema}].[AuditMessages]");

            var statements = new List<(string Text, long LastReads)>();
            await using var reader = await command.ExecuteReaderAsync(TestTimeoutCancellationToken);
            while (await reader.ReadAsync(TestTimeoutCancellationToken))
            {
                statements.Add((reader.GetString(0), reader.GetInt64(1)));
            }

            return statements;
        }

        const string BusyEndpoint = "Sales";
        const string OtherEndpoint = "Billing";
        const string QuietEndpoint = "Rare";

        // Without an index that serves both the filter and the sort, each order reads 90,000 or more.
        const long MaxReads = 1_000;
    }
}
