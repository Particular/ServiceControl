namespace ServiceControl.Migration.Guided;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Prints every value the migration will use and where each came from, then asks whether it is correct.
/// It opens nothing, so a no leaves the source and the target untouched.
/// </summary>
sealed class ConfigurationStep : IMigrationStep
{
    public string Title => "Configuration";

    public bool AppliesTo(MigrationRun run) => true;

    public Task<int?> Run(MigrationRun run, CancellationToken cancellationToken = default)
    {
        var blocks = new List<(string Name, string Kind, IReadOnlyList<MigrationSettingValue> Values)>
        {
            ("Source", PersistenceFactory.MigrationSourcePersistenceType, run.Source.Configuration),
            ("Target", run.Settings.PersistenceType, run.Readiness.Configuration)
        };

        var labelWidth = blocks.SelectMany(block => block.Values).Max(value => value.Label.Length) + 2;

        foreach (var (name, kind, values) in blocks)
        {
            run.Console.WriteLine($"   {name,-11}{kind}".TrimEnd());

            foreach (var value in values)
            {
                run.Console.WriteLine($"   {"",-11}{value.Label.PadRight(labelWidth)}{value.Value}  ({value.Origin})");
            }
        }

        if (run.Console.Confirm("Is this correct?", run.Arguments.NonInteractive, cancellationToken))
        {
            return Task.FromResult<int?>(null);
        }

        run.Console.WriteLine("Nothing has been opened.");
        return Task.FromResult<int?>(1);
    }
}
