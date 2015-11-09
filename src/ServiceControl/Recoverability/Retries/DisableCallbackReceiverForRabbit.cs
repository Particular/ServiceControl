namespace ServiceControl.Recoverability.Retries
{
    using System;
    using NServiceBus;
    using NServiceBus.Configuration.AdvanceExtensibility;

    public class DisableCallbackReceiverForRabbit : INeedInitialization
    {
        public void Customize(BusConfiguration configuration)
        {
            Type rabbitMqTransportFeature = Type.GetType("NServiceBus.Features.RabbitMqTransportFeature");

            if (rabbitMqTransportFeature != null)
            {
                var keyField = rabbitMqTransportFeature.GetField("UseCallbackReceiverSettingKey");
                if (keyField != null)
                {
                    string keyValue = keyField.GetValue(null) as string;
                    if (keyValue != null)
                    {
                        configuration.GetSettings().Set(keyValue, false);
                    }
                }
            }
        }
    }
}
