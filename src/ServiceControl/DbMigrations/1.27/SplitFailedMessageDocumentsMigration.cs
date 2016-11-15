namespace Particular.ServiceControl.DbMigrations
{
    using Raven.Client;

    public class SplitFailedMessageDocumentsMigration : IMigration
    {
        public string MigrationId { get; } = "Split Failed Message Documents";

        public void Apply(IDocumentSession session)
        {
            // Iterate through all FailedMessage documents
            // Calculate UniqueMessageId for each ProcessingAttempt using new algorithm
            //    If the Processing Attempt has a Retry Id, use that as the UniqueMessageId instead
            // If there is more than one UniqueMessageId, this record needs to be split.
        }
    }
}