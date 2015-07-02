namespace ServiceControl.Migrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Raven.Client;

    public class Runner
    {
        public static async Task Run(IDocumentStore documentStore, MigrationOptions options = null)
        {
            if (options == null)
                options = new MigrationOptions();

            if (!options.Assemblies.Any())
                options.Assemblies.Add(Assembly.GetCallingAssembly());

            var migrations = FindAllMigrationsWithOptions(options);

            foreach (var migrationsThatCanRunInParallel in migrations)
            {
                var migrationTasks = migrationsThatCanRunInParallel.Select(migration1 => Task.Run(async () =>
                {
                    var m = migration1.Migration();
                    m.Setup(documentStore);

                    // todo: possible issue here with sharding
                    var migrationId = m.GetMigrationIdFromName(documentStore.Conventions.IdentityPartsSeparator[0]);

                    using (var session = documentStore.OpenAsyncSession())
                    {
                        var migrationDoc = await session.LoadAsync<MigrationDocument>(migrationId);

                        // we already ran it
                        if (migrationDoc != null)
                        {
                            return;
                        }

                        await m.UpAsync();
                        await session.StoreAsync(new MigrationDocument
                        {
                            Id = migrationId
                        });

                        await session.SaveChangesAsync();
                    }
                })).ToList();

                await Task.WhenAll(migrationTasks);
            }
        }

        private static IEnumerable<List<MigrationWithAttribute>> FindAllMigrationsWithOptions(MigrationOptions options)
        {
            var migrationsToRun =
                from assembly in options.Assemblies
                from t in assembly.GetLoadableTypes()
                where typeof(Migration).IsAssignableFrom(t)
                      && !t.IsAbstract
                      && t.GetConstructor(Type.EmptyTypes) != null
                select new MigrationWithAttribute
                {
                    Migration = () => options.MigrationResolver.Resolve(t),
                    Attribute = t.GetMigrationAttribute()
                } into migration
                orderby migration.Attribute.ExecutionOrder
                group migration by migration.Attribute.ExecutionOrder into g
                select g.ToList();

            return migrationsToRun;
        }
    }
}