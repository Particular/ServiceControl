namespace ServiceControl.Persistence
{
    using System;
    using ServiceBus.Management.Infrastructure.Settings;

    static class PersistenceFactory
    {
        public static IPersistence Create(Settings settings)
        {
            var persistenceConfiguration = CreatePersistenceConfiguration(settings.PersistenceType);

            //HINT: This is false when executed from acceptance tests
            if (settings.PersisterSpecificSettings == null)
            {
                (bool, object) TryRead(string name, Type type)
                {
                    var exists = SettingsReader.TryRead(name, type: type, out object value);
                    return (exists, value);
                };

                settings.PersisterSpecificSettings = persistenceConfiguration.CreateSettings(TryRead);
            }

            var persistence = persistenceConfiguration.Create(settings.PersisterSpecificSettings);
            return persistence;
        }

        static IPersistenceConfiguration CreatePersistenceConfiguration(string persistenceType)
        {
            try
            {
                var foundPersistenceType = PersistenceManifestLibrary.Find(persistenceType);
                var customizationType = Type.GetType(foundPersistenceType, true);
                var persistenceConfiguration = (IPersistenceConfiguration)Activator.CreateInstance(customizationType);
                return persistenceConfiguration;
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {persistenceType}.", e);
            }
        }
    }
}