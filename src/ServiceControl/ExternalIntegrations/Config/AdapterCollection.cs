namespace ServiceControl.ExternalIntegrations.Config
{
    using System.Configuration;

    public class AdapterCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new AdapterElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            var e = (AdapterElement) element;
            return e.Name;
        }
    }
}