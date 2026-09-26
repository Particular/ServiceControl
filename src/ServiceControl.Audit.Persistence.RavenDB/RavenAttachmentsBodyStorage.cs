namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using Raven.Client.Documents.BulkInsert;
    using ServiceControl.Infrastructure;

    class RavenAttachmentsBodyStorage(
        IRavenSessionProvider sessionProvider,
        BulkInsertOperation bulkInsert,
        int settingsMaxBodySizeToStore)
        : IBodyStorage
    {
        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream, CancellationToken cancellationToken = default)
        {
            if (bodySize > settingsMaxBodySizeToStore)
            {
                return Task.CompletedTask;
            }

            return bulkInsert.AttachmentsFor(bodyId)
                .StoreAsync("body", bodyStream, contentType, cancellationToken);
        }

        public async Task<MessageBodyView> TryFetch(string bodyId, CancellationToken cancellationToken = default)
        {
            using var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);
            var result = await session.Advanced.Attachments.GetAsync($"MessageBodies/{bodyId}", "body", cancellationToken);

            if (result == null)
            {
                return MessageBodyView.NotFound();
            }

            return MessageBodyView.FromStream(
                result.Stream,
                result.Details.ContentType,
                (int)result.Details.Size,
                DataVersion.FromToken(result.Details.ChangeVector));
        }
    }
}