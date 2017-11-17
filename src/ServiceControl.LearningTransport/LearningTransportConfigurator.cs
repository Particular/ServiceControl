namespace NServiceBus
{
    using Features;
    using ServiceControl.LearningTransport;
    using Transports;

    class LearningTransportConfigurator : ConfigureTransport
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ConfigureTransport"/>.
        /// </summary>
        protected override void Configure(FeatureConfigurationContext context, string connectionString)
        {
            var endpointName = context.Settings.EndpointName();
            var localAddress = context.Settings.LocalAddress().Queue;

            PathChecker.ThrowForBadPath(connectionString, "ConnectionString");
            PathChecker.ThrowForBadPath(endpointName, "EndpointName");
            PathChecker.ThrowForBadPath(localAddress, "LocalAddress");

            context.Container.ConfigureComponent(() => new PathCalculator(connectionString), DependencyLifecycle.SingleInstance);

            context.Container.ConfigureComponent<MessageDispatcher>(DependencyLifecycle.InstancePerCall);

            context.Container.ConfigureComponent<LearningTransportUnitOfWork>(DependencyLifecycle.SingleInstance);

            if (!context.Settings.GetOrDefault<bool>("Endpoint.SendOnly"))
            {
                context.Container.ConfigureComponent<LearningDequeueStrategy>(DependencyLifecycle.InstancePerCall);
            }

            context.Container.ConfigureComponent<LearningMessageSender>(DependencyLifecycle.InstancePerCall);
            context.Container.ConfigureComponent<LearningMessageDeferrer>(DependencyLifecycle.InstancePerCall);
            context.Container.ConfigureComponent<LearningMessagePublisher>(DependencyLifecycle.InstancePerCall);

            context.Container.ConfigureComponent(() => new LearningTransportSubscriptionManager(connectionString, endpointName, localAddress),  DependencyLifecycle.InstancePerCall);

            context.Container.ConfigureComponent<LearningQueueCreator>(DependencyLifecycle.SingleInstance);
        }

        protected override string ExampleConnectionStringForErrorMessage { get; } = @"C:\.learningtransport";

        protected override bool RequiresConnectionString { get; } = true;
    }
}
