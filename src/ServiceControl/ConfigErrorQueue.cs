namespace Particular.ServiceControl
{
    using NServiceBus;
    using NServiceBus.Config;
    using NServiceBus.Config.ConfigurationSource;
    using NServiceBus.Settings;

    class ConfigErrorQueue : IProvideConfiguration<MessageForwardingInCaseOfFaultConfig>
    {
        string endpointName;

        public ConfigErrorQueue(ReadOnlySettings settings)
        {
            endpointName = settings.EndpointName();
        }

        public MessageForwardingInCaseOfFaultConfig GetConfiguration()
        {
            return new MessageForwardingInCaseOfFaultConfig
            {
                ErrorQueue = $"{endpointName}.Errors"
            };
        }
    }
}