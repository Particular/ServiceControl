namespace ServiceControl.Persistence
{
    using System;
    using ServiceBus.Management.Infrastructure.Settings;

    static class PersistenceFactory
    {
        public static IPersistence Create(string persistenceType, Func<string, Type, object> readSetting = default)
        {
            try
            {
                var foundPersistenceType = PersistenceManifestLibrary.Find(persistenceType);
                var customizationType = Type.GetType(foundPersistenceType, true);
                var persistenceConfiguration = (IPersistenceConfiguration)Activator.CreateInstance(customizationType);
                return persistenceConfiguration.Create(readSetting ?? SettingsReader.Read);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {persistenceType}.", e);
            }
        }
    }
}