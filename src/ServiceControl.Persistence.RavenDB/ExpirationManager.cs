namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using EventLog;
    using Raven.Client;
    using Raven.Client.Documents.Operations;
    using Raven.Client.Documents.Session;
    using FailedMessage = MessageFailures.FailedMessage;

    class ExpirationManager
    {
        public const string DeleteExpirationFieldExpression = "delete msg['@metadata']['@expires']";

        readonly TimeSpan errorRetentionPeriod;
        readonly TimeSpan eventsRetentionPeriod;

        public ExpirationManager(RavenPersisterSettings settings)
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

        public void EnableExpiration(PatchRequest request) => request.Script += "\n" + EnableExpirationScript(request);

        // Registers the value and hands back the statement, for scripts that only expire the
        // document down one branch and so cannot have it appended to the end.
        public string EnableExpirationScript(PatchRequest request)
        {
            var expiredAt = DateTime.UtcNow + errorRetentionPeriod;

            request.Values.Add("Expires", expiredAt);

            return "this['@metadata']['@expires'] = args.Expires;";
        }

        public void CancelExpiration(PatchRequest request) => request.Script += CancelExpirationScript;

        public const string CancelExpirationScript = "delete this['@metadata']['@expires'];";
    }
}