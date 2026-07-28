namespace ServiceControl.Persistence.Tests;

using System.Threading.Tasks;
using MessageFailures;
using Microsoft.Extensions.Hosting;

public interface IPersistenceTestsContext
{
    Task Setup(IHostApplicationBuilder hostBuilder);

    Task PostSetup(IHost host);

    Task TearDown();

    Task CompleteDatabaseOperation();

    PersistenceSettings PersistenceSettings { get; }

    string GenerateFailedMessageRecordId(string messageId);
    Task InsertFailedMessages(params FailedMessage[] messages);
}