namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Logging;
    using ServiceControl.LicenseManagement;

    class SetupCommand : AbstractCommand
    {
        public override Task Execute(Settings settings)
        {
            // Validate license:
            var license = LicenseManager.FindLicense();
            if (license.Details.HasLicenseExpired())
            {
                Logger.Error("License has expired.");
                return Task.CompletedTask;
            }

            if (license.Details.IsTrialLicense)
            {
                Logger.Error("Cannot run setup with a trial license.");
                return Task.CompletedTask;
            }

            var endpointConfig = new EndpointConfiguration(settings.EndpointName);

            new Bootstrapper(
                c => Environment.FailFast("NServiceBus Critical Error", c.Exception),
                settings,
                endpointConfig);

            endpointConfig.EnableInstallers(settings.Username);

            if (settings.SkipQueueCreation)
            {
                endpointConfig.DoNotCreateQueues();
            }

            return Endpoint.Create(endpointConfig);
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(SetupCommand));
    }
}