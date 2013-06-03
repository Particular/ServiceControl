namespace ServiceBus.Management.Modules
{
    using Nancy;

    public class VersionModule : NancyModule
    {
        public VersionModule()
        {
            Get["/version"] = _ =>
                {
                    var version = GetType().Assembly.GetName().Version;
                    return Negotiate.WithModel(new
                                    {
                                        Version = string.Format("{0}.{1}.{2}", version.Major, version.Major, version.Build)
                                    });
                };
        }
    }
}