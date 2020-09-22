namespace ServiceControl.Audit.Infrastructure.RavenDB
{
    using System;
    using Monitoring;
    using Newtonsoft.Json.Serialization;

    class MigratedTypeAwareBinder : DefaultSerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
        {
            if (typeName == "ServiceControl.Contracts.Operations.EndpointDetails" && assemblyName == "ServiceControl")
            {
                return typeof(EndpointDetails);
            }

            return base.BindToType(assemblyName, typeName);
        }
    }
}