namespace ServiceControl.Monitoring.QueueLength
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.Features;
    using Transports;

    public class QueueLength : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask(b => new QueueLengthProviderTask(b.GetRequiredService<IProvideQueueLength>()));
        }
    }

    public class QueueLengthProviderTask : FeatureStartupTask
    {
        public QueueLengthProviderTask(IProvideQueueLength queueLengthProvider)
        {
            this.queueLengthProvider = queueLengthProvider;
        }

        protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
        {
            return queueLengthProvider.Start();
        }

        protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default)
        {
            return queueLengthProvider.Stop();
        }

        IProvideQueueLength queueLengthProvider;
    }
}