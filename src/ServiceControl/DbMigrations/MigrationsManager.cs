namespace Particular.ServiceControl.DbMigrations
{
    using System;
    using System.Linq;
    using NServiceBus.Logging;
    using Raven.Client;

    public class MigrationsManager
    {
        private IDocumentStore store;
        private IMigration[] migrations;

        public MigrationsManager(IDocumentStore store, IMigration[] migrations)
        {
            this.store = store;
            this.migrations = migrations;
        }

        public void ApplyMigrations()
        {
            var migrationsRecord = LoadMigrationsRecord();

            var appliedMigrations = migrationsRecord.AppliedMigrations.Select(m => m.MigrationId).ToList();
            var migrationsToApply = migrations.Where(m => appliedMigrations.Contains(m.MigrationId) == false).ToArray();

            foreach (var migration in migrationsToApply)
            {
                try
                {
                    log.Info($"Applying migration [{migration.MigrationId}]");
                    using (var session = store.OpenSession())
                    {
                        migration.Apply(session);

                        session.Load<Migrations>(Migrations.DocumentId).Add(migration.MigrationId);

                        session.SaveChanges();
                    }
                    log.Info($"Applying migration [{migration.MigrationId}] complete");
                }
                catch (Exception ex)
                {
                    log.Fatal($"Error while applying migration [{migration.MigrationId}]", ex);
                    throw;
                }
            }
        }

        private Migrations LoadMigrationsRecord()
        {
            using (var session = store.OpenSession())
            {
                var document = session.Load<Migrations>(Migrations.DocumentId);
                if (document == null)
                {
                    document = new Migrations();
                    session.Store(document, Migrations.DocumentId);
                    session.SaveChanges();
                }
                return document;
            }
        }

        private static ILog log = LogManager.GetLogger<MigrationsManager>();
    }
}