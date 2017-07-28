namespace Particular.ServiceControl.DbMigrations
{
    using global::ServiceControl.Recoverability;
    using Raven.Client;
    using System.Collections.Generic;

    public class RerunClassifiersMigration_1_41_3 : IMigration
    {
        readonly IEnumerable<IFailureClassifier> classifiers;

        public RerunClassifiersMigration_1_41_3(IEnumerable<IFailureClassifier> classifiers)
        {
            this.classifiers = classifiers;
        }

        public string Apply(IDocumentStore store)
        {
            var reclassifier = new Reclassifier(null);

            var failedMessagesReclassified = reclassifier.ReclassifyFailedMessages(store, true, classifiers);

            return "Add Endpoint details classifiers";
        }

        public string MigrationId { get; } = "Add Endpoint details classifiers";
    }
}