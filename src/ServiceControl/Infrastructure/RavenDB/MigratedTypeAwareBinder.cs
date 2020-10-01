namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using Raven.Imports.Newtonsoft.Json.Serialization;
    using SagaAudit;

    class MigratedTypeAwareBinder : DefaultSerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
        {
            if (typeName == "ServiceControl.SagaAudit.SagaInfo" && assemblyName == "ServiceControl")
            {
                return typeof(SagaInfo);
            }

            return base.BindToType(assemblyName, typeName);
        }
    }
}