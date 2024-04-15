namespace ServiceControl.Config.Framework
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading.Tasks;
    using Extensions;
    using Mindscape.Raygun4Net;

    public abstract class RaygunReporter
    {
        protected RaygunReporter()
        {
            init = Task.Run(async () =>
            {
                enabled = (await TestAndSetCreds(null)) ||
                          (await TestAndSetCreds(CredentialCache.DefaultCredentials)) ||
                          (await TestAndSetCreds(CredentialCache.DefaultNetworkCredentials));
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

        async Task<bool> TestAndSetCreds(ICredentials credentials)
        {
            HttpClient http = null;
            try
            {
                http = new HttpClient(new HttpClientHandler { Credentials = credentials })
                {
                    Timeout = TimeSpan.FromSeconds(5)
                };
                using var response = await http.GetAsync(RaygunUrl);
                response.EnsureSuccessStatusCode();

                raygunClient = new RaygunClientWithCredentials(http);
                return true;
            }
            catch
            {
                http?.Dispose();
                return false;
            }
        }

        class RaygunClientWithCredentials : RaygunClient
        {
            public RaygunClientWithCredentials(HttpClient http)
                : base(new RaygunSettings { ApiKey = RaygunApiKey }, http)
            {
            }
        }

        static string GetVersion()
        {
            var assemblyInfo = Assembly.GetExecutingAssembly().GetAttribute<AssemblyInformationalVersionAttribute>();
            var versionParts = assemblyInfo?.InformationalVersion.Split('+');
            return versionParts?[0];
        }

        protected RaygunClient raygunClient = new(new RaygunSettings { ApiKey = RaygunApiKey });
        Task init;
        bool enabled;
        protected const string RaygunApiKey = "zdm49nndHCXZ3NVzM8Kzug==";
        const string RaygunUrl = "https://raygun.io";
    }
}