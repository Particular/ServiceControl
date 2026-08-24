#nullable enable
namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using MessageFailures;
using Microsoft.Extensions.Hosting;

public interface IPersistenceTestsContext
{
    Task Setup(IHostApplicationBuilder hostBuilder);

    Task PostSetup(IHost host);

    Task TearDown();

    Task CompleteDatabaseOperation();

    /// <summary>
    /// Move the clock the persister stamps its own timestamps from
    /// </summary>
    void AdvanceClock(TimeSpan by);

    /// <summary>
    /// Reads the same clock <see cref="AdvanceClock" /> moves
    /// </summary>
    DateTime UtcNow { get; }

    PersistenceSettings PersistenceSettings { get; }

    string GenerateFailedMessageRecordId(string messageId);
    Task InsertFailedMessages(params FailedMessage[] messages);
}
