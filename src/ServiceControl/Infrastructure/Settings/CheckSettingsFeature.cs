namespace ServiceControl.Infrastructure.Settings
{
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

    class CheckSettingsFeature : Feature
    {
        public CheckSettingsFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask(builder => builder.Build<VerifySettings>());
        }

        class VerifySettings : FeatureStartupTask
        {
            protected override Task OnStart(IBusSession session)
            {
                if (!Settings.ForwardAuditMessages.HasValue)
                {
                    logger.Error("The setting ServiceControl/ForwardAuditMessages is not explicitly set. To suppress this error set ServiceControl/ForwardAuditMessages to true or false.");
                }

                return Task.FromResult(0);

            }

            protected override Task OnStop(IBusSession session)
            {
                return Task.FromResult(0);
            }

            ILog logger = LogManager.GetLogger<VerifySettings>();
        }
    }
}
