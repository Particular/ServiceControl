namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using ServiceControl.Infrastructure;

    class RavenBodyStorage(IRavenSessionProvider sessionProvider) : IBodyStorage
    {
        public Task Store(string bodyId, string contentType, int bodySize, System.IO.Stream bodyStream, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("RavenBodyStorage is fetch-only; use RavenAttachmentsBodyStorage for storing.");

        public async Task<MessageBodyView> TryFetch(string bodyId, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var result = await session.Advanced.Attachments.GetAsync(bodyId, "body", cancellationToken);

            return result == null
                ? MessageBodyView.NoContent()
                : MessageBodyView.FromStream(result.Stream, result.Details.ContentType, (int)result.Details.Size, DataVersion.FromToken(result.Details.ChangeVector));
        }
    }
}