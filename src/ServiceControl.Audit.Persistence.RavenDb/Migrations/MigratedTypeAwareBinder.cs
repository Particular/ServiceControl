namespace ServiceControl.Audit.Infrastructure.Migration
{
    using System;
    using System.Linq;
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

            var className = GetClassName(typeName);
            return className switch
            {
                nameof(EndpointDetails) => typeof(EndpointDetails),
                nameof(SagaInfo) => typeof(SagaInfo),
                _ => base.BindToType(assemblyName, typeName),
            };
        }

        string GetClassName(string typeName)
        {
            return typeName.Split('.').Last();
        }
    }
}