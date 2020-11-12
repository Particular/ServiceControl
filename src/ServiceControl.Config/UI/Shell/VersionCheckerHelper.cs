namespace ServiceControl.Config.UI.Shell
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;

    public static class VersionCheckerHelper
    {
        public static async Task<Release> GetLatestRelease(string currentVersion)
        {
            Version current = new Version(currentVersion);
            List<Release> releases = await GetVersionInformation().ConfigureAwait(false);

            if (releases != null)
            {
                Release topversion = releases.Select(t => (t.Version, t)).Max().t;

                if (topversion.Version > current)
                    return topversion;
            }

            // we have no release available
            return new Release(current);
        }

        private static async Task<List<Release>> GetVersionInformation()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            try
            {
                using (var httpClient = new HttpClient(handler))
                {
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    // TODO: move this to some sort of configuration file/storage?
                    var json = await httpClient.GetStringAsync("https://s3.us-east-1.amazonaws.com/platformupdate.particular.net/servicecontrol.txt").ConfigureAwait(false);

                    return JsonConvert.DeserializeObject<List<Release>>(json);
                }
            }
            catch
            {
                return null;
            }
        }

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
}