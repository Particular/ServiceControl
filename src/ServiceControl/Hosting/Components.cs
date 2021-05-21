namespace Particular.ServiceControl.Hosting
{
    using global::ServiceControl.CustomChecks;
    using global::ServiceControl.Heartbeats;
    using global::ServiceControl.Hosting;

    public static class Components
    {
        public static readonly ServiceControlComponent[] All =
        {
            new HeartbeatsServiceControlComponent(),
            new CustomChecksServiceControlComponent()
        };
    }
}