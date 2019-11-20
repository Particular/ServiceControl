namespace ServiceControl.Monitoring.QueueLength
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using Transports;

    public class QueueLength : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask(b => new QueueLengthProviderTask(b.Build<IProvideQueueLength>()));
        }
    }

    public class QueueLengthProviderTask : FeatureStartupTask
    {
        public QueueLengthProviderTask(IProvideQueueLength queueLengthProvider)
        {
            this.queueLengthProvider = queueLengthProvider;
        }

        protected override Task OnStart(IMessageSession session)
        {
            return queueLengthProvider.Start();
        }

        protected override Task OnStop(IMessageSession session)
        {
            return queueLengthProvider.Stop();
        }

        IProvideQueueLength queueLengthProvider;
    }
}