using NServiceBus;
using NServiceBus.Features;
using ServiceControl.Monitoring;
using System.Threading.Tasks;

namespace ServiceControl.Audit.Monitoring
{
    class WarmupKnownEndpoints : Feature
    {
        public WarmupKnownEndpoints()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask(b =>
            {
                return b.Build<Startup>();
            });
        }


        class Startup : FeatureStartupTask
        {
            public Startup(EndpointInstanceMonitoring monitoring)
            {
                this.monitoring = monitoring;
            }

            protected override Task OnStart(IMessageSession session)
            {
                return monitoring.Warmup();
            }

            protected override Task OnStop(IMessageSession session)
            {
                return Task.CompletedTask;
            }

            private readonly EndpointInstanceMonitoring monitoring;
        }
    }
}
