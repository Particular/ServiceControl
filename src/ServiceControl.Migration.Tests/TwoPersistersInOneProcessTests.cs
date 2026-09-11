namespace ServiceControl.Migration.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Infrastructure;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
[NonParallelizable]
class TwoPersistersInOneProcessTests
{
    Settings settings;
    string bodyStoragePath;

    [SetUp]
    public void SetUp()
    {
        bodyStoragePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Bodies", Guid.NewGuid().ToString("n"));

        // Both persistence configurations read ErrorRetentionPeriod through the settings reader rather
        // than off the Settings object, so the constructor argument below does not satisfy either of them.
        SetVariable("SERVICECONTROL_ERRORRETENTIONPERIOD", "10.00:00:00");
        // CI's install-sql-server-action publishes only this variable and creates the catalog
        // ServiceControl; a developer machine has neither, so the local default is the fallback.
        SetVariable("SERVICECONTROL_DATABASE_CONNECTIONSTRING",
            Environment.GetEnvironmentVariable("ServiceControl_Persistence_SqlServer_ConnectionString")
            ?? "Server=localhost;Database=ServiceControl;Trusted_Connection=True;TrustServerCertificate=True");
        SetVariable("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "FileSystem");
        SetVariable("SERVICECONTROL_MESSAGEBODY_FILESYSTEM_STORAGEPATH", bodyStoragePath);
        SetVariable("SERVICECONTROL_RAVENDB_CONNECTIONSTRING", MigrationSourceServer.ServerUrl);
        SetVariable("SERVICECONTROL_RAVENDB_DATABASENAME", MigrationSourceServer.PrimaryDatabase);
        SetVariable("LICENSINGCOMPONENT_RAVENDB_THROUGHPUTDATABASENAME", MigrationSourceServer.ThroughputDatabase);

        settings = new Settings(persisterType: "SQLServer", forwardErrorMessages: false, errorRetentionPeriod: TimeSpan.FromDays(10));
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var name in variables)
        {
            Environment.SetEnvironmentVariable(name, null);
        }

        variables.Clear();
    }

    [Test]
    public async Task A_source_and_a_target_load_side_by_side_and_share_one_type_identity()
    {
        Assert.That(settings.AssemblyLoadContextResolver(typeof(Settings).Assembly.Location), Is.InstanceOf<PluginAssemblyLoadContext>(),
            "This test is meaningless if the resolver is pinned to AssemblyLoadContext.Default the way every acceptance test pins it.");

        var services = new ServiceCollection();
        services.AddPersistence(settings);
        var targetSettings = settings.PersisterSpecificSettings;

        await using var source = await PersistenceFactory.OpenMigrationSource(settings);

        var sourceContext = AssemblyLoadContext.GetLoadContext(source.GetType().Assembly);
        var targetContext = AssemblyLoadContext.GetLoadContext(settings.PersisterSpecificSettings.GetType().Assembly);

        Assert.Multiple(() =>
        {
            Assert.That(sourceContext, Is.Not.SameAs(AssemblyLoadContext.Default),
                "The RavenDB persister must load into its own plugin context, or this proves nothing.");
            Assert.That(targetContext, Is.Not.SameAs(AssemblyLoadContext.Default),
                "The target persister must be isolated too, otherwise only one half of the pairing is being tested.");
            Assert.That(sourceContext, Is.Not.SameAs(targetContext),
                "Source and target must land in separate contexts. That separation is the whole feature, and nothing else here asserts it.");
            Assert.That(source, Is.InstanceOf<IMigrationSource>(),
                "A plugin-context type must still cast to the host's copy of the interface.");
            Assert.That(settings.PersisterSpecificSettings, Is.SameAs(targetSettings),
                "Opening a source must leave the target's settings object exactly where it was.");
        });

        var description = await source.Describe();

        Assert.Multiple(() =>
        {
            Assert.That(AssemblyLoadContext.GetLoadContext(description.GetType().Assembly), Is.SameAs(AssemblyLoadContext.Default),
                "A shared type produced inside the plugin context must arrive as the host's own type.");
            Assert.That(description.PrimaryDatabase, Is.EqualTo(MigrationSourceServer.PrimaryDatabase),
                "The source read its own RavenDB settings rather than the target's.");
        });
    }

    [Test]
    public void Asking_a_SQL_persister_for_a_source_names_the_setting_to_change()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_SOURCEPERSISTENCETYPE", "SQLServer");

        try
        {
            var refusal = Assert.ThrowsAsync<Exception>(async () => await PersistenceFactory.OpenMigrationSource(new Settings(persisterType: "SQLServer", forwardErrorMessages: false, errorRetentionPeriod: TimeSpan.FromDays(10))));

            Assert.That(refusal.Message, Does.Contain("cannot be read as a migration source").And.Contain("ServiceControl/Migration/SourcePersistenceType"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SERVICECONTROL_MIGRATION_SOURCEPERSISTENCETYPE", null);
        }
    }

    [Test]
    public void Asking_a_SQL_persister_for_maintenance_mode_is_still_refused()
    {
        var refusal = Assert.Throws<Exception>(() => PersistenceFactory.Create(settings, maintenanceMode: true));

        Assert.That(refusal.Message, Does.Contain("Maintenance mode is not supported").And.Contain("SQLServer"),
            "PersistenceFactory's SupportsMaintenanceMode guard is what turns a persister mismatch into a sentence instead of a cast exception. A restructure that drops it compiles and passes every other test.");
    }

    void SetVariable(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        variables.Add(name);
    }

    readonly List<string> variables = [];
}
