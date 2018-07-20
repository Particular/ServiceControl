namespace Particular.ServiceControl.DbMigrations
{
    using System.Collections.Generic;
    using global::ServiceControl.Recoverability;
    using Raven.Client;

    public class RerunClassifiersMigration_1_41_3 : IMigration
    {
        public RerunClassifiersMigration_1_41_3(IEnumerable<IFailureClassifier> classifiers)
        {
            this.classifiers = classifiers;
        }

        public string Apply(IDocumentStore store)
        {
            var reclassifier = new Reclassifier(null);

            var failedMessagesReclassified = reclassifier.ReclassifyFailedMessages(store, true, classifiers).GetAwaiter().GetResult();

            return $"Reclassified {failedMessagesReclassified} messages";
        }

        public string MigrationId { get; } = "Add Endpoint details classifiers";
        readonly IEnumerable<IFailureClassifier> classifiers;
    }
}