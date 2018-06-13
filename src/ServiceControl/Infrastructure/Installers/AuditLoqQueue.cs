namespace ServiceBus.Management.Infrastructure.Installers
{
    using NServiceBus.Features;
    using NServiceBus.Transport;
    using ServiceBus.Management.Infrastructure.Settings;

    public class AuditLoqQueue : Feature
    {
        public AuditLoqQueue()
        {
            Prerequisite(c =>
            {
                var settings = c.Settings.Get<Settings>("ServiceControl.Settings");
                return settings.ForwardAuditMessages && settings.AuditLogQueue != null;
            }, "Audit Log queue not enabled.");
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var settings = context.Settings.Get<Settings>("ServiceControl.Settings");
            var queueBindings = context.Settings.Get<QueueBindings>();
            queueBindings.BindSending(settings.AuditLogQueue);
        }
    }
}