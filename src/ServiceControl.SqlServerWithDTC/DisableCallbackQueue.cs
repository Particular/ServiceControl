namespace ServiceControl.Transports.SqlServerWithDTC
{
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;

    // ReSharper disable once UnusedMember.Global
    public class DisableCallbackQueue : INeedInitialization
    {
        public void Customize(EndpointConfiguration configuration)
        {
            configuration.GetSettings().Set("SqlServer.UseCallbackReceiver", false);
        }
    }
}