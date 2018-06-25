namespace ServiceControl.Recoverability
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using Raven.Client;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Operations.BodyStorage;

    public class ReturnToSenderDequeuerFeature : Feature
    {
        public ReturnToSenderDequeuerFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent(
                b => new ReturnToSenderDequeuer(
                    b.Build<ReturnToSender>(),
                    b.Build<IDocumentStore>(),
                    b.Build<IDomainEvents>(),
                    context.Settings.EndpointName(),
                    b.Build<RawEndpointFactory>()),
                DependencyLifecycle.SingleInstance);

            context.RegisterStartupTask(b => new StartupTask(b.Build<ReturnToSenderDequeuer>()));
        }

        class StartupTask : FeatureStartupTask
        {
            ReturnToSenderDequeuer returnToSenderDequeuer;

            public StartupTask(ReturnToSenderDequeuer returnToSenderDequeuer)
            {
                this.returnToSenderDequeuer = returnToSenderDequeuer;
            }

            protected override Task OnStart(IMessageSession session)
            {
                return returnToSenderDequeuer.CreateQueue();
            }

            protected override Task OnStop(IMessageSession session)
            {
                returnToSenderDequeuer.Stop();
                return Task.FromResult(0);
            }
        }
    }
}