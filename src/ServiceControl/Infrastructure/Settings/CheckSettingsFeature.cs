namespace ServiceControl.Infrastructure.Settings
{
    using NServiceBus.Features;
    using NServiceBus.Logging;
    using ServiceBus.Management.Infrastructure.Settings;

    class CheckSettingsFeature : Feature
    {
        public CheckSettingsFeature()
        {
            EnableByDefault();
            RegisterStartupTask<VerifySettings>();
        }

        protected override void Setup(FeatureConfigurationContext context) { }

        class VerifySettings : FeatureStartupTask
        {
            protected override void OnStart()
            {
                if (!Settings.ForwardAuditMessages.HasValue)
                {
                    logger.Error("The setting ServiceControl/ForwardAuditMessages is not explicitly set. To suppress this error set ServiceControl/ForwardAuditMessages to true or false.");
                }
            }

            ILog logger = LogManager.GetLogger<VerifySettings>();
        }
    }
}
