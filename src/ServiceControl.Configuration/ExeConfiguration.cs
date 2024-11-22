namespace ServiceControl.Configuration
{
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;

    public static class ExeConfiguration
    {
        // ConfigurationManager on .NET is looking for {assembly}.dll.config files, but all previous versions of ServiceControl will have {assembly}.exe.config instead.
        // This code reads in the exe.config files and adds all the values into the ConfigurationManager's collections.
        public static void PopulateAppSettings(Assembly assembly)
        {
            var location = Path.GetDirectoryName(assembly.Location);
            var assemblyName = Path.GetFileNameWithoutExtension(assembly.Location);
            var exeConfigPath = Path.Combine(location, $"{assemblyName}.exe.config");
            var fileMap = new ExeConfigurationFileMap { ExeConfigFilename = exeConfigPath };
            var configuration = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);

            foreach (var key in configuration.AppSettings.Settings.AllKeys)
            {
                ConfigurationManager.AppSettings.Set(key, configuration.AppSettings.Settings[key].Value);
            }

            // The connection strings collection has had its read only flag set, so we need to clear it before we can add items to it
            UnsetCollectionReadonly(ConfigurationManager.ConnectionStrings);

            foreach (var connectionStringSetting in configuration.ConnectionStrings.ConnectionStrings.Cast<ConnectionStringSettings>())
            {
                ConfigurationManager.ConnectionStrings.Add(connectionStringSetting);
            }

            // Put the collection back into its previous state after we're done adding items to it
            SetCollectionReadOnly(ConfigurationManager.ConnectionStrings);
        }

        static void UnsetCollectionReadonly(ConfigurationElementCollection collection)
        {
            ref bool field = ref GetReadOnlyFieldRef(collection);
            field = false;

            [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_readOnly")]
            static extern ref bool GetReadOnlyFieldRef(ConfigurationElementCollection collection);
        }

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SetReadOnly")]
        static extern void SetCollectionReadOnly(ConfigurationElementCollection collection);
    }
}