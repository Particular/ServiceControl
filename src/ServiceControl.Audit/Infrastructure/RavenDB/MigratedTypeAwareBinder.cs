namespace ServiceControl.Audit.Infrastructure.RavenDB
{
    using System;
    using Monitoring;
    using Raven.Imports.Newtonsoft.Json.Serialization;
    using ServiceControl.SagaAudit;

    class MigratedTypeAwareBinder : DefaultSerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
        {
            if (typeName == "ServiceControl.Contracts.Operations.EndpointDetails" && assemblyName == "ServiceControl")
            {
                return typeof(EndpointDetails);
            }

            if (typeName == "ServiceControl.SagaAudit.SagaInfo" && assemblyName == "ServiceControl.Audit")
            {
                return typeof(SagaInfo);
            }

            return base.BindToType(assemblyName, typeName);
        }
    }
}