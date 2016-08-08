namespace ServiceControl.Config.Framework
{
    using System.Net;
    using System.Reflection;
    using System.Threading.Tasks;
    using Mindscape.Raygun4Net;
    using ServiceControl.Config.Extensions;

    public class RaygunReporter
    {
        protected const string RaygunApiKey = "zdm49nndHCXZ3NVzM8Kzug==";
        const string RaygunUrl = "https://raygun.io";
        RaygunClient raygunClient = new RaygunClient(RaygunApiKey);
        private Task init;
        private bool enabled;

        public bool Enabled
        {
            get
            {
                if (!init.IsCompleted)
                {
                    init.Wait();
                }

                return enabled;
            }
        }

        protected string Version { get; private set; }

        protected RaygunReporter()
        {
            init = Task.Run(() =>
            {
                enabled = TestAndSetCreds(null) ||
                      TestAndSetCreds(CredentialCache.DefaultCredentials) ||
                      TestAndSetCreds(CredentialCache.DefaultNetworkCredentials);
                Version = GetVersion();
            });
            
        }

        bool TestAndSetCreds(ICredentials credentials)
        {
            var client = WebRequest.Create(RaygunUrl);
            try
            {
                client.Timeout = 5000;
                client.Proxy.Credentials = credentials;
                using (client.GetResponse())
                {
                    raygunClient.ProxyCredentials = credentials;
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        static string GetVersion()
        {
            var assemblyInfo = Assembly.GetExecutingAssembly().GetAttribute<AssemblyInformationalVersionAttribute>();
            var versionParts = assemblyInfo?.InformationalVersion.Split('+');
            return versionParts?[0];
        }
    }
}