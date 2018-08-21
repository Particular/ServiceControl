namespace ServiceBus.Management.Infrastructure.Settings
{
    using System;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Logging;

    class DeprecatedConfigurationCheckFeature : Feature
    {
        public DeprecatedConfigurationCheckFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context) => context.RegisterStartupTask(b => b.Build<DeprecatedConfigurationCheck>());

        class DeprecatedConfigurationCheck : FeatureStartupTask
        {
            protected override Task OnStart(IMessageSession session)
            {
                var config = XDocument.Load(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile);
                var sections = config?.Element("configuration");

                if (sections != null)
                {
                    LogIfAsbConfigSectionExists(sections);
                    LogIfAsqConfigSectionExists(sections);
                }

                return Task.CompletedTask;
            }

            void LogIfAsbConfigSectionExists(XElement configSections)
            {
                var element = configSections.Element("AzureServiceBusQueueConfig");
                if (element != null)
                {
                    logging.Warn("The use of AzureServiceBusQueueConfig within ServiceControl has been deprecated. A Transport Adapater (https://docs.particular.net/servicecontrol/transport-adapter/) can be used if there are transport defaults that are not suitable for this environment.");
                }
            }

            void LogIfAsqConfigSectionExists(XElement configSections)
            {
                var element = configSections.Element("AzureQueueConfig");
                if (element != null)
                {
                    logging.Warn("The use of AzureQueueConfig within ServiceControl has been deprecated. A Transport Adapater (https://docs.particular.net/servicecontrol/transport-adapter/) can be used if there are transport defaults that are not suitable for this environment.");
                }
            }

            protected override Task OnStop(IMessageSession session)
            {
                return Task.CompletedTask;
            }

            ILog logging = LogManager.GetLogger(typeof(DeprecatedConfigurationCheck));
        }
    }
}
