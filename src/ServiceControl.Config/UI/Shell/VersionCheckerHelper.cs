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
            var currentRelease = new Release(new Version(currentVersion));

            List<Release> allReleasesIncludingCurrent = await GetVersionInformation(currentRelease)
                .ConfigureAwait(false);

            return allReleasesIncludingCurrent.Select(t => (t.Version, t)).Max().t;
        }

        private static async Task<List<Release>> GetVersionInformation(Release current)
        {
            try
            {
                // TODO: move this to some sort of configuration file/storage?
                var json = await httpClient.GetStringAsync("https://s3.us-east-1.amazonaws.com/platformupdate.particular.net/servicecontrol.txt").ConfigureAwait(false);

                var releases = JsonConvert.DeserializeObject<List<Release>>(json) ?? new List<Release>();
                releases.Add(current);
                return releases;
            }
            catch
            {
                return new List<Release> { current };
            }
        }

        private static readonly HttpClient httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        })
        {
            DefaultRequestHeaders =
            {
                Accept =
                {
                    new MediaTypeWithQualityHeaderValue("application/json"),
                }
            }
        };

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