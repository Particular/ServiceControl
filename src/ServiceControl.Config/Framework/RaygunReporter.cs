namespace ServiceControl.Config.Framework
{
    using System.Net;
    using System.Reflection;
    using System.Threading.Tasks;
    using Extensions;
    using Mindscape.Raygun4Net;

    public class RaygunReporter
    {
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

        RaygunClient raygunClient = new RaygunClient(RaygunApiKey);
        Task init;
        bool enabled;
        protected const string RaygunApiKey = "zdm49nndHCXZ3NVzM8Kzug==";
        const string RaygunUrl = "https://raygun.io";
    }
}