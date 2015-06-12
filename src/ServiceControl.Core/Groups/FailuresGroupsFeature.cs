namespace ServiceControl.Groups
{
    using NServiceBus;
    using NServiceBus.Features;
    using ServiceControl.Groups.Groupers;

    public class FailuresGroupsFeature : Feature
    {
        public override bool IsEnabledByDefault
        {
            get { return true; }
        }

        public override void Initialize()
        {
            Configure.Component<ExceptionMessageAndExceptionTypeMessageGrouper>(DependencyLifecycle.SingleInstance);
            Configure.Component<ExceptionTypeAndStackTraceMessageGrouper>(DependencyLifecycle.SingleInstance);
            Configure.Component<ExceptionTypeMessageGrouper>(DependencyLifecycle.SingleInstance);
            Configure.Component<MD5HashGroupIdGenerator>(DependencyLifecycle.SingleInstance);
        }
    }
}