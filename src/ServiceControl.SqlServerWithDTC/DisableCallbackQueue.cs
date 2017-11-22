namespace ServiceControl.Transports.SqlServerWithDTC
{
    using NServiceBus;
    using NServiceBus.Configuration.AdvanceExtensibility;

    // ReSharper disable once UnusedMember.Global
    public class DisableCallbackQueue : INeedInitialization
    {
        public void Customize(BusConfiguration configuration)
        {
            configuration.GetSettings().Set("SqlServer.UseCallbackReceiver", false);
        }
    }
}