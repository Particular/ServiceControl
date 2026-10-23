namespace ServiceControl.Migration.Checks;

using System;
using System.Threading;
using System.Threading.Tasks;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Reads the engine options out of the settings, which refuses an optional category window that is not a time
/// span before anything is copied rather than part way through the copy.
/// </summary>
class OptionalCategoryWindowsAreValidCheck(TimeSpan eventRetentionPeriod, TimeSpan errorRetentionPeriod) : IMigrationStartupCheck
{
    public string Name => "the optional category windows are valid";

    /// <summary>
    /// The options parsed from the settings, which is null until <see cref="Run"/> has returned.
    /// </summary>
    public MigrationEngineOptions Options { get; private set; }

    public Task Run(CancellationToken cancellationToken = default)
    {
        Options = MigrationEngineOptions.FromSettings(Settings.SettingsRootNamespace, eventRetentionPeriod, errorRetentionPeriod);
        return Task.CompletedTask;
    }
}
