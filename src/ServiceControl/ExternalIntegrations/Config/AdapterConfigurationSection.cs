namespace ServiceControl.ExternalIntegrations.Config
{
    using System.Configuration;

    public class AdapterConfigurationSection : ConfigurationSection
    {
        [ConfigurationProperty("", Options = ConfigurationPropertyOptions.IsDefaultCollection)]
        public AdapterCollection Adapters
        {
            get { return this[""] as AdapterCollection; }
            set { this[""] = value; }
        }

        public static AdapterConfigurationSection GetAdapters()
        {
            return ConfigurationManager.GetSection("Adapters") as AdapterConfigurationSection ?? new AdapterConfigurationSection();
        }
    }
}