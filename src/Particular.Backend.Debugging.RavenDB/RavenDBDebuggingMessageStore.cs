namespace Particular.Backend.Debugging.RavenDB
{
    using NServiceBus;
    using NServiceBus.Features;
    using Particular.Backend.Debugging.RavenDB.Data;

    public class RavenDBDebuggingMessageStore : Feature
    {
        public override bool IsEnabledByDefault
        {
            get { return true; }
        }

        public override void Initialize()
        {
            Configure.Component<AuditImporter>(DependencyLifecycle.InstancePerCall);
        }
    }
}