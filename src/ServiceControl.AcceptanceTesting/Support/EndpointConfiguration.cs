namespace NServiceBus.AcceptanceTesting.Support
{
    using System;
    using System.Collections.Generic;

    public class EndpointConfiguration
    {
        public EndpointConfiguration()
        {
            UserDefinedConfigSections = new Dictionary<Type, object>();
            TypesToInclude = new List<Type>();
            GetBus = () => null;
            StopBus = null;
        }

        public IDictionary<Type, Type> EndpointMappings { get; set; }

        public List<Type> TypesToInclude { get; set; }

        public Func<RunDescriptor, IDictionary<Type, string>, BusConfiguration> GetConfiguration { get; set; }

        internal Func<IStartableBus> GetBus { get; set; }

        internal Action StopBus { get; set; }

        public string EndpointName { get; set; }

        public Type BuilderType { get; set; }

        public Address AddressOfAuditQueue { get; set; }
        public Address AddressOfErrorQueue { get; set; }

        public IDictionary<Type, object> UserDefinedConfigSections { get; }
    }
}