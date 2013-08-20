namespace ServiceControl.EndpointPlugin.Heartbeats
{
    using System;
    using System.Collections.Generic;

    public class EndpointHeartbeat
    {
        public Dictionary<string, string> Configuration
        {
            get
            {
                if (configuration == null)
                    configuration = new Dictionary<string, string>();

                return configuration;
            }
            set { configuration = value; }
        }

        public DateTime ExecutedAt { get; set; }

        Dictionary<string, string> configuration;
    }
}