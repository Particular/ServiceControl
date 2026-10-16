namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EFCore.SqlServer;
using Microsoft.Data.SqlClient;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.Entities;

// SQL Server reuses a cached plan only when the parameter declarations match too, so these prove
// that the ingestion statements declare a string by its column and not by the value it carries.
class IngestionPlanReuseTests : ErrorIngestionTestBase
{
    [Test]
    public async Task Failed_messages_with_different_value_lengths_and_nulls_share_one_plan()
    {
        await Upsert(FailedMessage(length: 1));
        await Upsert(FailedMessage(length: 450));
        await Upsert(FailedMessageWithoutOptionalStrings());
        await Upsert(FailedMessageWithoutOptionalValues());

        var plans = await CachedPlans("FailedMessages");

        Assert.That(plans, Has.Count.EqualTo(1));
        Assert.That(plans[0].UseCount, Is.EqualTo(4));
        Assert.That(plans[0].Text, Does.Contain("@p8 nvarchar(max)").And.Contain("@p9 nvarchar(450)").And.Contain("@p10 datetime2(7)").And.Contain("@p14 uniqueidentifier"));
    }

    [Test]
    public async Task Groups_with_different_string_lengths_share_one_plan()
    {
        var message = FailedMessage(length: 10);
        await Upsert(message);

        await InsertGroup(message.UniqueMessageId, groupId: "a", title: "t", type: "T");
        await InsertGroup(message.UniqueMessageId, groupId: new string('b', 64), title: new string('t', 5000), type: new string('T', 255));

        var plans = await CachedPlans("FailedMessageGroups");

        Assert.That(plans, Has.Count.EqualTo(1));
        Assert.That(plans[0].UseCount, Is.EqualTo(2));
        Assert.That(plans[0].Text, Does.Contain("@p1 nvarchar(64)").And.Contain("@p2 nvarchar(max)").And.Contain("@p3 nvarchar(255)"));
    }

    [Test]
    public void A_string_longer_than_its_column_is_refused_rather_than_cut_short()
    {
        var message = FailedMessage(length: 10);
        message.MessageType = new string('x', 451);

        Assert.That(() => Upsert(message), Throws.InstanceOf<SqlException>().With.Message.Contains("truncated").And.Message.Contains("MessageType"));
    }

    Task Upsert(FailedMessageEntity row) =>
        InDialectTransaction((dialect, dbContext, ct) => dialect.UpsertFailedMessages(dbContext, [row], ct));

    Task InsertGroup(Guid uniqueMessageId, string groupId, string title, string type) =>
        InDialectTransaction((dialect, dbContext, ct) => dialect.InsertGroups(
            dbContext,
            [new FailedMessageGroupEntity { FailedMessageUniqueId = uniqueMessageId, GroupId = groupId, Title = title, Type = type }],
            ct));

    async Task<List<(string Text, int UseCount)>> CachedPlans(string table)
    {
        var settings = (SqlServerPersisterSettings)PersistenceSettings;

        await using var connection = new SqlConnection(settings.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT st.text, cp.usecounts
            FROM sys.dm_exec_cached_plans AS cp
            CROSS APPLY sys.dm_exec_sql_text(cp.plan_handle) AS st
            WHERE CHARINDEX(@statement, st.text) > 0
            """;
        command.Parameters.AddWithValue("@statement", $"MERGE [{settings.Schema}].[{table}] WITH (HOLDLOCK)");

        var plans = new List<(string Text, int UseCount)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            plans.Add((reader.GetString(0), reader.GetInt32(1)));
        }

        return plans;
    }

    // The nvarchar(max) columns get ten times the length, so a long one crosses 4,000 characters.
    static FailedMessageEntity FailedMessage(int length)
    {
        var text = new string('x', length);
        var longText = new string('y', length * 10);

        var message = FailedMessageWithoutOptionalStrings();
        message.MessageId = longText;
        message.MessageType = text;
        message.ConversationId = text;
        message.SendingEndpointName = text;
        message.SendingEndpointHost = text;
        message.ReceivingEndpointName = text;
        message.ReceivingEndpointHost = text;
        message.ExceptionType = longText;
        message.ExceptionMessage = longText;
        message.HeadersJson = longText;
        message.BodyText = longText;
        message.BodyContentType = text;
        message.FailingEndpointAddress = text;

        return message;
    }

    static FailedMessageEntity FailedMessageWithoutOptionalValues()
    {
        var message = FailedMessageWithoutOptionalStrings();
        message.TimeSent = null;
        message.SendingEndpointHostId = null;
        message.ReceivingEndpointHostId = null;

        return message;
    }

    static FailedMessageEntity FailedMessageWithoutOptionalStrings()
    {
        var now = new DateTime(2026, 8, 3, 9, 30, 0, DateTimeKind.Utc);

        return new FailedMessageEntity
        {
            UniqueMessageId = Guid.NewGuid(),
            Status = FailedMessageStatus.Unresolved,
            StatusChangedAt = now,
            LastModified = now,
            NumberOfProcessingAttempts = 1,
            FirstTimeOfFailure = now,
            LastTimeOfFailure = now,
            LastAttemptedAt = now,
            TimeSent = now,
            SendingEndpointHostId = Guid.NewGuid(),
            ReceivingEndpointHostId = Guid.NewGuid(),
            HeadersJson = "{}",
            FailingEndpointAddress = "Sales@MACHINE"
        };
    }
}
