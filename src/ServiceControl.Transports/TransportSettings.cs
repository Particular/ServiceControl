namespace ServiceControl.Transports
{
    using System;
    using System.Runtime.Loader;
    using NServiceBus.Settings;

    public class TransportSettings : SettingsHolder
    {
        [Obsolete("",true)]
        public Func<string, AssemblyLoadContext> AssemblyLoadContextResolver { get; set; }

        public string TransportType { get; set; }

        public string ConnectionString { get; set; }

        public string EndpointName { get; set; }

        public int? MaxConcurrency { get; set; }

        public bool RunCustomChecks { get; set; }

        public string ErrorQueue
        {
            set => customErrorQueue = value;
            get => string.IsNullOrEmpty(customErrorQueue) ? $"{EndpointName}.Errors" : customErrorQueue;
        }

        string customErrorQueue;
    }
}