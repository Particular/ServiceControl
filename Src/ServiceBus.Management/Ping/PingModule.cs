using Nancy;

namespace ServiceBus.Management.Ping
{
    public class PingModule : NancyModule
    {
        public PingModule()
        {
            Get["/ping"] = _ =>
                {
                    var assemblyName = this.GetType().Assembly.GetName();
                    return Negotiate.WithModel(new VersionInfo
                        {
                            Name = assemblyName.Name,
                            Version = assemblyName.Version.ToString()
                        });
                };
        }
    }
}