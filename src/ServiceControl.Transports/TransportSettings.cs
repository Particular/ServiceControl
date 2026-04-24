namespace ServiceControl.Transports
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Loader;
    using NServiceBus.Settings;

    public class TransportSettings : SettingsHolder
    {
        public Func<string, AssemblyLoadContext> AssemblyLoadContextResolver { get; set; }

        public string TransportType { get; set; }

        public string ConnectionString { get; set; }

        public string EndpointName { get; set; }

        public int? MaxConcurrency { get; set; }

        public bool RunCustomChecks { get; set; }

        public IReadOnlySet<Type> EventTypesPublished { get; init; } = new HashSet<Type>();

        public string ErrorQueue
        {
            set;
            get => string.IsNullOrEmpty(field) ? $"{EndpointName}.Errors" : field;
        }
    }
}