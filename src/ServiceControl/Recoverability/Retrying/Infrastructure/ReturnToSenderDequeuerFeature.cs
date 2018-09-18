namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using Infrastructure.DomainEvents;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Transport;
    using Raven.Client;

    public class ReturnToSenderDequeuerFeature : Feature
    {
        public ReturnToSenderDequeuerFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var inputAddress = $"{context.Settings.EndpointName()}.staging";

            var queueBindings = context.Settings.Get<QueueBindings>();
            queueBindings.BindReceiving(inputAddress);

            context.Container.ConfigureComponent(
                b => new ReturnToSenderDequeuer(
                    context.Settings.Get<TransportDefinition>(),
                    b.Build<ReturnToSender>(),
                    b.Build<IDocumentStore>(),
                    b.Build<IDomainEvents>(),
                    inputAddress,
                    b.Build<RawEndpointFactory>()),
                DependencyLifecycle.SingleInstance);

            context.RegisterStartupTask(b => new StartupTask(b.Build<ReturnToSenderDequeuer>()));
        }

        class StartupTask : FeatureStartupTask
        {
            public StartupTask(ReturnToSenderDequeuer returnToSenderDequeuer)
            {
                this.returnToSenderDequeuer = returnToSenderDequeuer;
            }

            protected override Task OnStart(IMessageSession session)
            {
                return Task.CompletedTask;
            }

            protected override Task OnStop(IMessageSession session)
            {
                return returnToSenderDequeuer.Stop();
            }

            ReturnToSenderDequeuer returnToSenderDequeuer;
        }
    }
}