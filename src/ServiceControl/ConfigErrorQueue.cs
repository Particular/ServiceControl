namespace Particular.ServiceControl
{
    using NServiceBus.Config;
    using NServiceBus.Config.ConfigurationSource;

    class ConfigErrorQueue : IProvideConfiguration<MessageForwardingInCaseOfFaultConfig>
    {
        public MessageForwardingInCaseOfFaultConfig GetConfiguration()
        {
            return new MessageForwardingInCaseOfFaultConfig
            {
                ErrorQueue = "Particular.ServiceControl.Errors"
            };
        }
    }
}