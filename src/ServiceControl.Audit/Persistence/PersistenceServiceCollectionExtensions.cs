namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Infrastructure.Settings;

    static class PersistenceServiceCollectionExtensions
    {
        public static void AddServiceControlAuditPersistence(this IServiceCollection serviceCollection, PersistenceSettings settings)
        {
            var persistenceCustomizationType = SettingsReader<string>.Read("ServiceControl.Audit", "PersistenceType", null);

            try
            {
                var customizationType = Type.GetType(persistenceCustomizationType, true);

                var persistenceConfig = (IPersistenceConfiguration)Activator.CreateInstance(customizationType);
                persistenceConfig.ConfigureServices(serviceCollection, settings);

            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {persistenceCustomizationType}.", e);
            }
        }
    }
}