namespace Particular.ThroughputCollector;

using System.Threading.Tasks;

public static class ThroughputCollectorInstaller
{
    public static async Task Install(string persistenceType)
    {
        var persistenceConfiguration = PersistenceConfigurationFactory.LoadPersistenceConfiguration(persistenceType);
        var persistenceSettings = persistenceConfiguration.BuildPersistenceSettings();
        var persistence = persistenceConfiguration.Create(persistenceSettings);
        var installer = persistence.CreateInstaller();

        await installer.Install().ConfigureAwait(false);
    }
}
