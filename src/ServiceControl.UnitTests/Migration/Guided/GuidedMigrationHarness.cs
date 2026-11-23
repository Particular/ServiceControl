namespace ServiceControl.UnitTests.Migration.Guided;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Migration.Guided;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

/// <summary>
/// Runs the guided migration over in-memory fakes, with the answers given as typed lines.
/// It builds the run itself, so nothing in <c>MigrateCommand.Run</c> runs.
/// </summary>
sealed class GuidedMigrationHarness
{
    // Like the RavenDB source, the fake reads no optional category and always has a setting to show in step 1.
    public GuidedMigrationHarness()
    {
        Target = new InMemoryMigrationTarget(Store);
        Source.Unsupported.UnionWith([MigrationCategoryIds.EventLog, MigrationCategoryIds.ArchivedAndResolvedFailedMessages]);
        Source.Configuration = [new("Server", "https://raven:8080", "default")];
    }

    public InMemoryMigrationSource Source { get; } = new();

    public InMemoryMigrationCheckpointStore Store { get; } = new();

    public InMemoryMigrationTarget Target { get; }

    public FakeMigrationTargetReadiness Readiness { get; } = new();

    public FakeTimeProvider Clock { get; } = new();

    public Settings Settings { get; } = new(persisterType: "PostgreSQL");

    /// <summary>
    /// Everything the last run wrote, including what it wrote before it threw.
    /// </summary>
    public string Output { get; private set; } = "";

    public IReadOnlyList<string> StepHeadings =>
        [.. Output.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => Regex.IsMatch(line, @"^\d+\. "))];

    public async Task<int> Run(GuidedMigrationArguments arguments, params string[] answers)
    {
        var output = new StringWriter();
        var input = new StringReader(string.Join(Environment.NewLine, answers));
        var console = new MigrationConsole(input, output, inputRedirected: false, canRedraw: false);
        var run = new MigrationRun(Settings, Source, Target, Store, Readiness, Clock, NullLoggerFactory.Instance, arguments, console);

        try
        {
            return await GuidedMigration.Run(run);
        }
        finally
        {
            Output = output.ToString();
        }
    }

    public async Task SettleEveryRequiredCategory()
    {
        var now = Clock.GetUtcNow().UtcDateTime;

        foreach (var category in MigrationCategoryRegistry.All.Where(category => category.Kind == MigrationCategoryKind.Required))
        {
            await Store.Upsert(new MigrationCheckpoint(category.Id, MigrationCategoryState.Complete, null, 0, 0, null, null, now, now, now, null));
        }
    }

    public static MigrationRow Row(string sourceId) => new(sourceId, new object(), new Dictionary<string, object>());
}
