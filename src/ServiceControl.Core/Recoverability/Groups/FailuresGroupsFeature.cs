﻿namespace ServiceControl.Recoverability.Groups
{
    using NServiceBus;
    using NServiceBus.Features;
    using ServiceControl.Recoverability.Groups.Groupers;

    public class FailuresGroupsFeature : Feature
    {
        public override bool IsEnabledByDefault
        {
            get { return true; }
        }

        public override void Initialize()
        {
            Configure.Component<ExceptionTypeAndStackTraceMessageGrouper>(DependencyLifecycle.SingleInstance);
            Configure.Component<MD5HashGroupIdGenerator>(DependencyLifecycle.SingleInstance);
        }
    }
}