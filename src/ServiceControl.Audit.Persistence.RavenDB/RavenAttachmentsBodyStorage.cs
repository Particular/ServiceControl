namespace ServiceControl.Audit.Persistence.RavenDB
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing.BodyStorage;
    using Raven.Client.Documents.BulkInsert;

    class RavenAttachmentsBodyStorage(
        IRavenSessionProvider sessionProvider,
        BulkInsertOperation bulkInsert,
        int settingsMaxBodySizeToStore)
        : IBodyStorage
    {
        public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream, CancellationToken cancellationToken)
        {
            if (bodySize > settingsMaxBodySizeToStore)
            {
                return Task.CompletedTask;
            }

            return bulkInsert.AttachmentsFor(bodyId)
                .StoreAsync("body", bodyStream, contentType, cancellationToken);
        }

        public async Task<StreamResult> TryFetch(string bodyId)
        {
            using var session = await sessionProvider.OpenSession();
            var result = await session.Advanced.Attachments.GetAsync($"MessageBodies/{bodyId}", "body");

            if (result == null)
            {
                return new StreamResult { HasResult = false };
            }

            return new StreamResult
            {
                HasResult = true,
                Stream = result.Stream,
                BodySize = (int)result.Details.Size,
                ContentType = result.Details.ContentType,
                Etag = result.Details.ChangeVector
            };
        }
    }
}