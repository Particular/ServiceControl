namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Infrastructure.Settings;

    static class PersistenceServiceCollectionExtensions
    {
        public static void AddServiceControlAuditPersistence(this IServiceCollection serviceCollection, PersistenceSettings settings)
        {
            var persistenceCustomizationType = SettingsReader<string>.Read("ServiceControl.Audit", "PersistenceType", null);

            IPersistenceConfiguration persistenceConfig;
            try
            {
                var customizationType = Type.GetType(persistenceCustomizationType, true);

                persistenceConfig = (IPersistenceConfiguration)Activator.CreateInstance(customizationType);
            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {persistenceCustomizationType}.", e);
            }

            //hardcode for now
            foreach (var key in ConfigurationManager.AppSettings.AllKeys)
            {
                settings.PersisterSpecificSettings[key] = ConfigurationManager.AppSettings[key];
            }

            persistenceConfig.ConfigureServices(serviceCollection, settings);
        }
    }
}