namespace ServiceControl.Migration.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
[NonParallelizable]
class MigrationCategoryCoverageTests
{
    Settings settings;

    [SetUp]
    public void SetUp()
    {
        // Both persistence configurations read ErrorRetentionPeriod through the settings reader rather
        // than off the Settings object, so the constructor argument below does not satisfy either of them.
        SetVariable("SERVICECONTROL_ERRORRETENTIONPERIOD", "10.00:00:00");
        // Registering the SQL Server persistence never connects, so any connection string satisfies it.
        SetVariable("SERVICECONTROL_DATABASE_CONNECTIONSTRING", "Server=localhost;Database=ServiceControl;Trusted_Connection=True;TrustServerCertificate=True");
        SetVariable("SERVICECONTROL_MESSAGEBODY_STORAGETYPE", "FileSystem");
        SetVariable("SERVICECONTROL_MESSAGEBODY_FILESYSTEM_STORAGEPATH",
            Path.Combine(TestContext.CurrentContext.WorkDirectory, "Bodies", Guid.NewGuid().ToString("n")));

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

    // A reader with no writer, or the reverse, is a category the intersection would otherwise drop in silence.
    [Test]
    public async Task Every_reader_has_a_writer_and_the_reverse()
    {
        await using var source = PersistenceFactory.CreateMigrationSource(settings);
        var sourceCategoryIds = source.SupportedCategoryIds;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(settings);
        await using var provider = services.BuildServiceProvider();
        var target = provider.GetRequiredService<IMigrationTarget>();

        var readerOnly = sourceCategoryIds.Except(target.SupportedCategoryIds).ToArray();
        var writerOnly = target.SupportedCategoryIds.Except(sourceCategoryIds).ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(readerOnly, Is.Empty, $"the reader claims a category the writer does not: {string.Join(", ", readerOnly)}");
            Assert.That(writerOnly, Is.Empty, $"the writer claims a category the reader does not: {string.Join(", ", writerOnly)}");
        }
    }

    void SetVariable(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        variables.Add(name);
    }

    readonly List<string> variables = [];
}
