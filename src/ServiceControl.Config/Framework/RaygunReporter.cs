namespace ServiceControl.Config.Framework
{
    using System;
    using System.Net;
    using System.Reflection;
    using Mindscape.Raygun4Net;
    using ServiceControl.Config.Extensions;

    public class RaygunReporter
    {
        protected const string raygunApiKey = "zdm49nndHCXZ3NVzM8Kzug==";
        const string raygunUrl = "https://raygun.io";
        RaygunClient raygunClient = new RaygunClient(raygunApiKey);

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
                    client.DownloadString(raygunUrl);
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

        public void SendReport(Exception ex)
        {
            if (!Enabled) return;

            var raygunMessage = RaygunMessageBuilder.New
                .SetUser(raygunClient.UserInfo)
                .SetVersion(GetVersion())
                .SetExceptionDetails(ex);

            raygunMessage.SetMachineName(Environment.MachineName);
            raygunMessage.SetEnvironmentDetails();

            var m = raygunMessage.Build();
            raygunClient.Send(m);
        }
    }
}