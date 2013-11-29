namespace ServiceControl.Plugin.Operations.Configuration.Messages
{
    using System.Collections.Generic;

    public class EndpointConfiguration
    {
        Dictionary<string, string> configuration;
        public Dictionary<string, string> Configuration
        {
            get
            {
                if (configuration == null)
                {
                    configuration = new Dictionary<string, string>();
                }

                return configuration;
            }
            set { configuration = value; }
        }

    }
}
