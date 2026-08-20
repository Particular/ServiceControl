namespace ServiceControl.Persistence.Tests.AuditCapable
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Operations.BodyStorage;
    using ServiceControl.Persistence.Infrastructure;

    // The third step of the arbitration order IBodyStorage states: a failed message body wins, and the
    // audit copy answers only when no failed message holds one.
    class AuditCapableBodyStorage(IBodyStorage inner, InMemoryAuditStore auditStore) : IBodyStorage
    {
        public async Task<MessageBodyResult> TryFetch(string bodyId, CancellationToken cancellationToken = default)
        {
            var failedMessageBody = await inner.TryFetch(bodyId, cancellationToken);

            if (failedMessageBody.State != MessageBodyState.NotFound)
            {
                return failedMessageBody;
            }

            var body = auditStore.BodyFor(bodyId);

            if (body == null)
            {
                return MessageBodyResult.NotFound();
            }

            return body.Length == 0
                ? MessageBodyResult.Empty()
                : MessageBodyResult.Available(new MessageBodyStreamContent(new MemoryStream(body, writable: false), "application/json", body.Length, DataVersion.None));
        }
    }
}
