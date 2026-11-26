namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;

    // SQL Server compiles the endpoint filter for the first endpoint asked for, then reuses that plan for every other endpoint.
    class EndpointFilterPlanReuseTests : EFPersistenceTestFixture
    {
        [TestCase(BusyEndpoint, QuietEndpoint)]
        [TestCase(QuietEndpoint, BusyEndpoint)]
        public async Task The_page_for_an_endpoint_stays_cheap_on_a_plan_compiled_for_another_endpoint(string compiledFor, string listed)
        {
            await Seed();

            await ListFirstPage(compiledFor);
            await ListFirstPage(listed);

            var pageReads = await PageQueryReads();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(pageReads, Is.Not.Empty, "The page query was not found in the plan cache.");
                Assert.That(pageReads, Has.All.LessThan(MaxReads));
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

        async Task<List<long>> PageQueryReads()
        {
            await using var connection = new SqlConnection(configuration.ConnectionString);
            await connection.OpenAsync(TestTimeoutCancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT qs.last_logical_reads
                FROM sys.dm_exec_query_stats AS qs
                CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) AS st
                WHERE CHARINDEX(@table, st.text) > 0 AND CHARINDEX('@endpointName', st.text) > 0 AND CHARINDEX('ORDER BY', st.text) > 0
                """;
            command.Parameters.AddWithValue("@table", $"[{configuration.Schema}].[AuditMessages]");

            var reads = new List<long>();
            await using var reader = await command.ExecuteReaderAsync(TestTimeoutCancellationToken);
            while (await reader.ReadAsync(TestTimeoutCancellationToken))
            {
                reads.Add(reader.GetInt64(0));
            }

            return reads;
        }

        const string BusyEndpoint = "Sales";
        const string OtherEndpoint = "Billing";
        const string QuietEndpoint = "Rare";

        // Without an index that serves both the filter and the sort, each order reads 90,000 or more.
        const long MaxReads = 1_000;
    }
}
