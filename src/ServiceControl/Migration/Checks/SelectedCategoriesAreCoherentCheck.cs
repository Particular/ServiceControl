namespace ServiceControl.Migration.Checks;

using System.Threading;
using System.Threading.Tasks;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Reads the engine options out of the settings, which refuses a typo in the optional category list before
/// anything is copied rather than part way through the copy.
/// </summary>
class SelectedCategoriesAreCoherentCheck : IMigrationStartupCheck
{
    public string Name => "the selected categories are coherent";

    /// <summary>
    /// The options parsed from the settings, which is null until <see cref="Run"/> has returned.
    /// </summary>
    public MigrationEngineOptions Options { get; private set; }

    public Task Run(CancellationToken cancellationToken = default)
    {
        Options = MigrationEngineOptions.FromSettings(Settings.SettingsRootNamespace);
        return Task.CompletedTask;
    }
}
