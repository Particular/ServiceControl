namespace ServiceBus.Management.Infrastructure.Installers
{
    using NServiceBus.Features;
    using NServiceBus.Transport;
    using Settings;

    public class ErrorLoqQueue : Feature
    {
        public ErrorLoqQueue()
        {
            Prerequisite(c =>
            {
                var settings = c.Settings.Get<Settings>("ServiceControl.Settings");
                return settings.ForwardErrorMessages && settings.ErrorLogQueue != null;
            }, "Error Log queue not enabled.");
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var settings = context.Settings.Get<Settings>("ServiceControl.Settings");
            var queueBindings = context.Settings.Get<QueueBindings>();
            queueBindings.BindSending(settings.ErrorLogQueue);
        }
    }
}