namespace ServiceControl.Config.UI.Shell
{
    using Newtonsoft.Json;
    using ServiceControlInstaller.Engine.Configuration.ServiceControl;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;

    public static class VersionCheckerHelper
    {
        public static async Task<Release> GetRecommendedRelease(string currentVersion)
        {
            Version current = new Version(currentVersion);
            List<Release> releases = await GetVersionInformation().ConfigureAwait(false);

            Release topversion = releases.Select(t => (t.Version, t)).Max().t;
            
            UpgradeInfo upgradeInfo = UpgradeControl.GetUpgradeInfoForTargetVersion(topversion.Version, current);
            
            Release recomendedVersion = releases.FirstOrDefault(t => t.Version == upgradeInfo.RecommendedUpgradeVersion);

            if (recomendedVersion != null && recomendedVersion.Version > current)
                return recomendedVersion;
            // we have no release available
            return new Release(current);
        }

        private static async Task<List<Release>> GetVersionInformation()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            using (var httpClient = new HttpClient(handler))
            {
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                
                // TODO: move this to some sort of configuration file/storage?
                var json = await httpClient.GetStringAsync("https://s3.us-east-1.amazonaws.com/platformupdate.particular.net/servicecontrol.txt").ConfigureAwait(false);

                return JsonConvert.DeserializeObject<List<Release>>(json);
            }
        }
    }

    // TODO: move it to a better place?
    public class Release
    {
        public Release()
        {
        }

        public Release(Version current)
        {
            Tag = current.ToString();
        }

        [JsonProperty("tag")]
        public string Tag { get; set; }

        [JsonProperty("release")]
        public Uri ReleaseUri { get; set; }

        [JsonProperty("published")]
        public DateTimeOffset Published { get; set; }

        [JsonProperty("assets")]
        public List<Asset> Assets { get; set; }

        [JsonIgnore]
        public Version Version => new Version(Tag);
    }

    public class Asset
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("download")]
        public Uri Download { get; set; }
    }
}