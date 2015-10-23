namespace ServiceControl.Config.Framework
{
    using System.Net;
    using System.Reflection;
    using Mindscape.Raygun4Net;
    using ServiceControl.Config.Extensions;

    public class RaygunReporter
    {
        protected const string RaygunApiKey = "zdm49nndHCXZ3NVzM8Kzug==";
        const string RaygunUrl = "https://raygun.io";
        RaygunClient raygunClient = new RaygunClient(RaygunApiKey);

        public bool Enabled { get; protected set; }

        public RaygunReporter()
        {
            Enabled = TestAndSetCreds(null) ||
                      TestAndSetCreds(CredentialCache.DefaultCredentials) ||
                      TestAndSetCreds(CredentialCache.DefaultNetworkCredentials);
        }

        protected bool TestAndSetCreds(ICredentials credentials)
        {
            using (var client = new WebClient())
            {
                try
                {
                    client.Proxy.Credentials = credentials;
                    client.DownloadString(RaygunUrl);
                    raygunClient.ProxyCredentials = credentials;
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        protected string GetVersion()
        {
            var assemblyInfo = Assembly.GetExecutingAssembly().GetAttribute<AssemblyInformationalVersionAttribute>();
            if (assemblyInfo == null)
            {
                return null;
            }
            var versionParts = assemblyInfo.InformationalVersion.Split('+');
            return versionParts[0];
        }
    }
}