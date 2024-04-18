#nullable enable

namespace ServiceControl.Config.Framework
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading.Tasks;
    using Extensions;
    using Mindscape.Raygun4Net;

    public sealed class RaygunFeedback : IRaygunUserProvider
    {
        public RaygunFeedback()
        {
            trackingId = GetOrSetTrackingId();

            raygunClient = new RaygunClient(raygunSettings, this);

            init = Task.Run(async () =>
            {
                enabled = await TryInitializeRaygunClientWithCredentials() ||
                          await TryInitializeRaygunClientWithCredentials(CredentialCache.DefaultCredentials) ||
                          await TryInitializeRaygunClientWithCredentials(CredentialCache.DefaultNetworkCredentials);
                version = GetVersion();
            });
        }

        static Guid GetOrSetTrackingId()
        {
            var trackerLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Particular");
            if (!Directory.Exists(trackerLocation))
            {
                Directory.CreateDirectory(trackerLocation);
            }

            var trackingId = Guid.NewGuid();
            var trackingFile = new FileInfo(Path.Combine(trackerLocation, ".feedbackid"));
            if (!trackingFile.Exists || !Guid.TryParse(File.ReadAllText(trackingFile.FullName), out trackingId))
            {
                File.WriteAllText(trackingFile.FullName, trackingId.BareString());
            }
            return trackingId;
        }

        public Task SendFeedBack(string emailAddress, string message, bool includeSystemInfo)
        {
            var userInfo = ((IRaygunUserProvider)this).GetUser()!;
            userInfo.Email = emailAddress;

            var raygunMessage = RaygunMessageBuilder.New(new RaygunSettings())
                .SetUser(userInfo)
                .SetVersion(version)
                .SetExceptionDetails(new Feedback(message));

            if (includeSystemInfo)
            {
                raygunMessage.SetMachineName(Environment.MachineName);
                raygunMessage.SetEnvironmentDetails();
            }

            var m = raygunMessage.Build();
            return raygunClient.Send(m);
        }

        public Task SendException(Exception ex, bool includeSystemInfo)
        {
            var userInfo = ((IRaygunUserProvider)this).GetUser()!;

            var raygunMessage = RaygunMessageBuilder.New(new RaygunSettings())
                .SetUser(userInfo)
                .SetVersion(version)
                .SetExceptionDetails(ex);

            if (includeSystemInfo)
            {
                raygunMessage.SetMachineName(Environment.MachineName);
                raygunMessage.SetEnvironmentDetails();
            }

            var m = raygunMessage.Build();
            return raygunClient.Send(m);
        }

        RaygunIdentifierMessage IRaygunUserProvider.GetUser() => new(trackingId.BareString())
        {
            UUID = trackingId.BareString()
        };

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

        async Task<bool> TryInitializeRaygunClientWithCredentials(ICredentials? credentials = default)
        {
            HttpClient? http = null;
            try
            {
                http = new HttpClient(new HttpClientHandler { Credentials = credentials })
                {
                    Timeout = TimeSpan.FromSeconds(5)
                };
                using var response = await http.GetAsync(RaygunUrl);
                response.EnsureSuccessStatusCode();

                raygunClient = new RaygunClient(raygunSettings, http, this);
                return true;
            }
            catch
            {
                http?.Dispose();
                return false;
            }
        }

        static string? GetVersion()
        {
            var assemblyInfo = Assembly.GetExecutingAssembly().GetAttribute<AssemblyInformationalVersionAttribute>();
            var versionParts = assemblyInfo?.InformationalVersion.Split('+');
            return versionParts?[0];
        }

        bool enabled;
        string? version;
        RaygunClient raygunClient;
        readonly Task init;
        readonly Guid trackingId;
        readonly RaygunSettings raygunSettings = new() { ApiKey = "zdm49nndHCXZ3NVzM8Kzug==" };

        const string RaygunUrl = "https://raygun.io";

        sealed class Feedback(string message) : Exception(message);
    }
}