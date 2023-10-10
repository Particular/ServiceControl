namespace ServiceControl.Persistence.RavenDb5
{
    using System;
    using EventLog;
    using Raven.Client;
    using Raven.Client.Documents.Operations;
    using Raven.Client.Documents.Session;
    using FailedMessage = MessageFailures.FailedMessage;

    class ExpirationManager
    {
        public const string DeleteExpirationFieldScript = "; delete msg['@metadata']['@expires']";

        readonly TimeSpan errorRetentionPeriod;
        readonly TimeSpan eventsRetentionPeriod;

        public ExpirationManager(RavenDBPersisterSettings settings)
        {
            errorRetentionPeriod = settings.ErrorRetentionPeriod;
            eventsRetentionPeriod = settings.EventsRetentionPeriod;
        }

        public void CancelExpiration(IAsyncDocumentSession session, FailedMessage failedMessage)
        {
            session.Advanced.GetMetadataFor(failedMessage).Remove(Constants.Documents.Metadata.Expires);
        }

        public void EnableExpiration(IAsyncDocumentSession session, FailedMessage failedMessage)
        {
            var expiresAt = DateTime.UtcNow + errorRetentionPeriod;

            session.Advanced.GetMetadataFor(failedMessage)[Constants.Documents.Metadata.Expires] = expiresAt;
        }

        public void EnableExpiration(IAsyncDocumentSession session, EventLogItem eventLogItem)
        {
            var expiresAt = DateTime.UtcNow + eventsRetentionPeriod;

            session.Advanced.GetMetadataFor(eventLogItem)[Constants.Documents.Metadata.Expires] = expiresAt;
        }

        public void EnableExpiration(PatchRequest request)
        {
            var expiredAt = DateTime.UtcNow + errorRetentionPeriod;

            request.Script += "\nthis['@metadata']['@expires'] = args.Expires;";
            request.Values.Add("Expires", expiredAt);
        }

        public void CancelExpiration(PatchRequest request) => request.Script += "delete this['@metadata']['@expires']";
    }
}