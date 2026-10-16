namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using NUnit.Framework;
    using ServiceControl.SagaAudit;

    // SQL Server reuses a cached plan only when the parameter declarations match too, so these prove
    // that an unbounded text column is always declared nvarchar(max), whether its value is null, short or long.
    class IngestionPlanReuseTests : EFPersistenceTestFixture
    {
        [Test]
        public async Task Messages_with_no_body_a_short_body_and_a_long_body_share_one_plan()
        {
            await Ingest(MakeMessage(contentType: "text/plain"));
            await Ingest(MakeMessage(contentType: "text/plain"), Encoding.UTF8.GetBytes(new string('x', 10)));
            await Ingest(MakeMessage(contentType: "text/plain"), Encoding.UTF8.GetBytes(new string('x', 4001)));

            var plans = await CachedPlans("AuditMessages");

            Assert.That(plans, Has.Count.EqualTo(1));
            Assert.That(plans[0].UseCount, Is.EqualTo(3));
            Assert.That(plans[0].Text, Does.Contain("nvarchar(max)").And.Not.Contain("nvarchar(4000)"));
        }

        [Test]
        public async Task Saga_snapshots_with_no_state_a_short_state_and_a_long_state_share_one_plan()
        {
            await RecordSnapshot(state: null);
            await RecordSnapshot(state: new string('x', 10));
            await RecordSnapshot(state: new string('x', 4001));

            var plans = await CachedPlans("SagaSnapshots");

            Assert.That(plans, Has.Count.EqualTo(1));
            Assert.That(plans[0].UseCount, Is.EqualTo(3));
            Assert.That(plans[0].Text, Does.Contain("nvarchar(max)").And.Not.Contain("nvarchar(4000)"));
        }

        async Task RecordSnapshot(string state)
        {
            await using var unitOfWork = await StartAuditUnitOfWork(1);
            await unitOfWork.RecordSagaSnapshot(new SagaSnapshot
            {
                SagaId = Guid.NewGuid(),
                SagaType = "Shipping.ShippingPolicy",
                Status = SagaStateChangeStatus.Updated,
                StartTime = TimeSent,
                FinishTime = TimeSent,
                StateAfterChange = state
            });
            await unitOfWork.Complete();
        }

        async Task<List<(string Text, int UseCount)>> CachedPlans(string table)
        {
            await using var connection = new SqlConnection(configuration.ConnectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT st.text, cp.usecounts
                FROM sys.dm_exec_cached_plans AS cp
                CROSS APPLY sys.dm_exec_sql_text(cp.plan_handle) AS st
                WHERE CHARINDEX(@statement, st.text) > 0
                """;
            command.Parameters.AddWithValue("@statement", $"INSERT INTO [{configuration.Schema}].[{table}]");

            var plans = new List<(string Text, int UseCount)>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                plans.Add((reader.GetString(0), reader.GetInt32(1)));
            }

            return plans;
        }
    }
}
