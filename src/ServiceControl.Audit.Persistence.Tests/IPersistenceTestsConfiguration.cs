namespace ServiceControl.Audit.Persistence.Tests;

using System;
using System.Threading.Tasks;
using Auditing.BodyStorage;
using UnitOfWork;

interface IPersistenceTestsConfiguration
{
    string Name { get; }

    IAuditMessagesViewDataStore MessagesViewStore { get; }

    ISagaHistoryDataStore SagaHistoryStore { get; }

    IFailedAuditStorage FailedAuditStorage { get; }

    IBodyStorage BodyStorage { get; }

    IAuditIngestionUnitOfWorkFactory AuditIngestionUnitOfWorkFactory { get; }

    IServiceProvider ServiceProvider { get; }

    Task Configure(Action<PersistenceSettings> setSettings);

    Task CompleteDBOperation();

    Task Cleanup();
}
