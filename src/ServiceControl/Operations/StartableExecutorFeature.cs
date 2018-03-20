namespace ServiceControl.EndpointControl.Handlers
{
    using System.Collections.Generic;
    using NServiceBus.Features;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.DomainEvents;

    public class StartableExecutorFeature : Feature
    {
        public StartableExecutorFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            RegisterStartupTask<StartableExecutor>();
        }

        class StartableExecutor : FeatureStartupTask
        {
            IEnumerable<IStartable> startables;
            ITimeKeeper timeKeeper;

            public StartableExecutor(IEnumerable<IStartable> startables, ITimeKeeper timeKeeper)
            {
                this.startables = startables;
                this.timeKeeper = timeKeeper;
            }

            protected override void OnStart()
            {
                foreach (var startable in startables)
                {
                    startable.Start(timeKeeper).GetAwaiter().GetResult();
                }
            }

            protected override void OnStop()
            {
                foreach (var startable in startables)
                {
                    startable.Stop(timeKeeper).GetAwaiter().GetResult();
                }
            }
        }
    }
}