namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using CustomChecks;
    using EventLog;
    using Notifications;
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

            if (typeName == "ServiceControl.Notifications.NotificationsSettings" && assemblyName == "ServiceControl")
            {
                return typeof(NotificationsSettings);
            }

            if (typeName == "ServiceControl.CustomChecks.CustomCheck" && assemblyName == "ServiceControl")
            {
                return typeof(CustomCheck);
            }

            if (typeName == "ServiceControl.EventLog.EventLogItem" && assemblyName == "ServiceControl")
            {
                return typeof(EventLogItem);
            }

            return base.BindToType(assemblyName, typeName);
        }
    }
}