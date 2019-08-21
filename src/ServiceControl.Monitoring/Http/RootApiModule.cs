namespace ServiceControl.Monitoring.Http
{
    using Nancy;
    public class RootApiModule : BaseModule
    {
        public RootApiModule()
        {
            Get["/"] = _ => Negotiate.WithModel(new
            {
                InstanceType = "monitoring",
                Version = VersionInfo.FileVersion,
            });
        }
    }
}
