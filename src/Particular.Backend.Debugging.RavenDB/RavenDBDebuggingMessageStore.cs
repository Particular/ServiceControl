namespace Particular.Backend.Debugging.RavenDB
{
    using NServiceBus;
    using NServiceBus.Features;
    using Particular.Backend.Debugging.RavenDB.Migration;
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

            Configure.Component<SagaHistoryMigration>(DependencyLifecycle.InstancePerCall);

            Configure.Component<FailedMessageConverter>(DependencyLifecycle.InstancePerCall);
            Configure.Component<FailedMessageMigration>(DependencyLifecycle.InstancePerCall);
        }
    }
}