namespace ServiceControl.EndpointControl.Handlers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
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
                var tasks = startables.Select(s => s.Start(timeKeeper));
                var waitTask = Task.WhenAll(tasks);
                waitTask.GetAwaiter().GetResult();
            }

            protected override void OnStop()
            {
                var tasks = startables.Select(s => s.Stop(timeKeeper));
                var waitTask = Task.WhenAll(tasks);
                waitTask.GetAwaiter().GetResult();
            }
        }
    }
}