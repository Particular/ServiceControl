namespace ServiceControl.Contracts.Operations
{
    using System.Collections.Generic;
    using NServiceBus;

    public class EndpointConfigurationReceived:IEvent
    {
        public string Endpoint { get; set; }

        public Dictionary<string, string> SettingsReceived
        {
            get
            {
                if (setting == null)
                {
                    setting = new Dictionary<string, string>();
                }

                return setting;
            }
            set { setting = value; }
        }

        Dictionary<string, string> setting;
    }
}