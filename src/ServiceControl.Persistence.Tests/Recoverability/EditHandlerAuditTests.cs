namespace ServiceControl.Persistence.Tests.Recoverability;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Contracts.Operations;
using MessageFailures;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Testing;
using NServiceBus.Transport;
using NUnit.Framework;
using ServiceControl.Infrastructure.Auth;
using ServiceControl.Recoverability;
using ServiceControl.Recoverability.Editing;

sealed class EditHandlerAuditTests : PersistenceTestBase
{
    EditHandler handler;
    readonly TestableUnicastDispatcher dispatcher = new();
    readonly ErrorQueueNameCache errorQueueNameCache = new()
    {
        ResolvedErrorAddress = "errorQueueName"
    };
    readonly RecordingMessageActionAuditLog audit = new();

    public EditHandlerAuditTests() =>
        RegisterServices = services => services
            .AddSingleton<IMessageDispatcher>(dispatcher)
            .AddSingleton(errorQueueNameCache)
            .AddSingleton<IMessageActionAuditLog>(audit)
            .AddTransient<EditHandler>();

    [SetUp]
    public void Setup() => handler = ServiceProvider.GetRequiredService<EditHandler>();

    [Test]
    public async Task Successful_edit_is_audited_with_the_initiating_user()
    {
        var user = new AuditUser("alice-sub", "Alice");
        var failedMessage = await CreateAndStoreFailedMessage();
        var message = CreateEditMessage(failedMessage.UniqueMessageId);

        var context = new TestableMessageHandlerContext { MessageHeaders = StampedHeaders(user, "op-edit") };
        await handler.Handle(message, context);

        var entry = audit.Messages.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.MessageId, Is.EqualTo(failedMessage.UniqueMessageId));
            Assert.That(entry.User, Is.EqualTo(user));
            Assert.That(entry.OperationId, Is.EqualTo("op-edit"));
            Assert.That(entry.Kind, Is.EqualTo(MessageActionKind.Edit));
            Assert.That(entry.Scope, Is.EqualTo(MessageActionScope.Single));
        }
    }

    static System.Collections.Generic.Dictionary<string, string> StampedHeaders(AuditUser user, string operationId) => new()
    {
        [AuditHeaders.SubjectId] = user.Id,
        [AuditHeaders.SubjectName] = user.Name,
        [AuditHeaders.OperationId] = operationId
    };

    static EditAndSend CreateEditMessage(string failedMessageId) =>
        new()
        {
            FailedMessageId = failedMessageId,
            NewBody = Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
            NewHeaders = []
        };

    async Task<FailedMessage> CreateAndStoreFailedMessage(string failedMessageId = null)
    {
        failedMessageId ??= Guid.NewGuid().ToString();

        var failedMessage = new FailedMessage
        {
            UniqueMessageId = failedMessageId,
            Id = FailedMessageIdGenerator.MakeDocumentId(failedMessageId),
            Status = FailedMessageStatus.Unresolved,
            ProcessingAttempts =
                [
                    new FailedMessage.ProcessingAttempt
                    {
                        MessageId = Guid.NewGuid().ToString(),
                        FailureDetails = new FailureDetails
                        {
                            AddressOfFailingEndpoint = "OriginalEndpointAddress"
                        }
                    }
                ]
        };
        await ErrorMessageDataStore.StoreFailedMessagesForTestsOnly(new[] { failedMessage });
        return failedMessage;
    }
}