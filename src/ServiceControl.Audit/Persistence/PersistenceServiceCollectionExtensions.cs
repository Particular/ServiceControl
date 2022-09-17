namespace ServiceControl.Audit.Persistence
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Extensions.DependencyInjection;
    using ServiceControl.Audit.Infrastructure.Settings;

    static class PersistenceServiceCollectionExtensions
    {
#pragma warning disable IDE0060 // Remove unused parameter
        public static void AddServiceControlAuditPersistence(this IServiceCollection serviceCollection, Settings settings, bool maintenanceMode = false, bool isSetup = false)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            var persistenceCustomizationType = SettingsReader<string>.Read("ServiceControl.Audit", "PersistenceType", null);

            try
            {
                var customizationType = Type.GetType(persistenceCustomizationType, true);

                var persistenceConfig = (IPersistenceConfiguration)Activator.CreateInstance(customizationType);
                persistenceConfig.ConfigureServices(serviceCollection, new Dictionary<string, string>(), maintenanceMode, isSetup);

            }
            catch (Exception e)
            {
                throw new Exception($"Could not load persistence customization type {persistenceCustomizationType}.", e);
            }
        }
    }
}