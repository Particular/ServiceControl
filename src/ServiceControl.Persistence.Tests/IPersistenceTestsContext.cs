#nullable enable
namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using MessageFailures;
using Microsoft.Extensions.Hosting;

public interface IPersistenceTestsContext
{
    Task Setup(IHostApplicationBuilder hostBuilder);

    /// <summary>
    /// Puts the schema in place on the built host, before it is started, the way <c>--setup</c> does in
    /// production. The EF Core persisters refuse to start against a database whose schema predates the
    /// build, and that check runs before any hosted service, so migrating after the start is too late.
    /// </summary>
    Task InstallSchema(IHost host);

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
