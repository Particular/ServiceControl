namespace ServiceControl.ExternalIntegrations.Config
{
    using System.Configuration;

    public class AdapterElement : ConfigurationElement
    {
        [ConfigurationProperty("Name", IsRequired = true)]
        public string Name
        {
            get { return (string)this["Name"]; }
            set { this["Name"] = value; }
        }
    }
}