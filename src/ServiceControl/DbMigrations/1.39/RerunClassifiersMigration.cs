﻿namespace Particular.ServiceControl.DbMigrations
{
    using global::ServiceControl.Recoverability;
    using Raven.Client;

    public class RerunClassifiersMigration : IMigration
    {
        public string Apply(IDocumentStore store)
        {
            using (var session = store.OpenSession())
            {
                var reclassifySettings = session.Load<ReclassifyErrorSettings>(ReclassifyErrorSettings.IdentifierCase);

                if (reclassifySettings != null)
                {
                    reclassifySettings.ReclassificationDone = false;
                }

                session.SaveChanges();
            }

            return "Reclassification settings updated.";
        }

        public string MigrationId { get; } = "Rerun error classifiers";
    }
}