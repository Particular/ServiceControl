namespace ServiceControl.MultiInstance.AcceptanceTests.Recoverability;

using System.Threading;

using System.Threading.Tasks;
using AcceptanceTesting;
using MessageFailures;
using MessageFailures.Api;
using TestSupport;

abstract class WhenRetrying : AcceptanceTest
{
    protected Task<SingleResult<FailedMessage>> GetFailedMessage(string uniqueMessageId, string instance, FailedMessageStatus expectedStatus, CancellationToken cancellationToken = default)
    {
        if (uniqueMessageId == null)
        {
            return Task.FromResult(SingleResult<FailedMessage>.Empty);
        }

        return this.TryGet<FailedMessage>($"/api/errors/{uniqueMessageId}", f => f.Status == expectedStatus, instance, cancellationToken);
    }

    protected Task<ManyResult<FailedMessageView>> GetAllFailedMessage(string instance, FailedMessageStatus expectedStatus, CancellationToken cancellationToken = default) => this.TryGetMany<FailedMessageView>("/api/errors", f => f.Status == expectedStatus, instance, cancellationToken);
}