namespace Particular.Backend.Debugging.RavenDB
{
    using NServiceBus;
    using NServiceBus.Features;
    using Particular.Backend.Debugging.RavenDB.Storage;

    public class RavenDBDebuggingMessageStore : Feature
    {
        public override bool IsEnabledByDefault
        {
            get { return true; }
        }

        public override void Initialize()
        {
            Configure.Component<MessageSnapshotStore>(DependencyLifecycle.InstancePerCall);
        }
    }
}