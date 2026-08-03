namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Operations;

class FailedMessageSummaryTests : PersistenceTestBase
{
    [Test]
    public async Task Summarises_by_endpoint_host_and_message_type()
    {
        var sales = new IngestedFailure();
        var alsoSales = new IngestedFailure();
        var billing = new IngestedFailure
        {
            MessageType = "MyCompany.Billing.InvoiceRaised",
            ReceivingEndpoint = new EndpointDetails { Name = "Billing", Host = "BillingHost", HostId = Guid.NewGuid() }
        };

        await Insert(sales, alsoSales, billing);

        var summary = await FailedMessageQueryStore.GetFailedMessagesSummary();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Counts(summary, "Endpoints"), Is.EqualTo(new Dictionary<string, int> { ["Sales"] = 2, ["Billing"] = 1 }));
            Assert.That(Counts(summary, "Hosts"), Is.EqualTo(new Dictionary<string, int> { ["ReceiverHost"] = 2, ["BillingHost"] = 1 }));
            Assert.That(Counts(summary, "Message types"), Is.EqualTo(new Dictionary<string, int>
            {
                ["MyCompany.Sales.OrderPlaced"] = 2,
                ["MyCompany.Billing.InvoiceRaised"] = 1
            }));
        }
    }

    [Test]
    public async Task Counts_only_unresolved_messages()
    {
        await Insert(
            new IngestedFailure().ToFailedMessage(),
            new IngestedFailure().ToFailedMessage(FailedMessageStatus.Archived),
            new IngestedFailure().ToFailedMessage(FailedMessageStatus.Resolved));

        var summary = await FailedMessageQueryStore.GetFailedMessagesSummary();

        Assert.That(Counts(summary, "Endpoints"), Is.EqualTo(new Dictionary<string, int> { ["Sales"] = 1 }));
    }

    static Dictionary<string, int> Counts(IDictionary<string, object> summary, string key)
    {
        Assert.That(summary, Contains.Key(key));

        return ((IDictionary<string, object>)summary[key])
            .ToDictionary(entry => entry.Key, entry => Convert.ToInt32(entry.Value));
    }

    Task Insert(params IngestedFailure[] failures) =>
        Insert([.. failures.Select(failure => failure.ToFailedMessage())]);

    async Task Insert(params FailedMessage[] messages)
    {
        foreach (var message in messages)
        {
            message.Id = PersistenceTestsContext.GenerateFailedMessageRecordId(message.UniqueMessageId);
        }

        await PersistenceTestsContext.InsertFailedMessages(messages);
        await CompleteDatabaseOperation();
    }
}
