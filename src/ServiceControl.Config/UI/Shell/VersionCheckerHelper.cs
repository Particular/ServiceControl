namespace ServiceControl.Config.UI.Shell
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using NuGet.Versioning;

    public static class VersionCheckerHelper
    {
        public static async Task<Release> GetLatestRelease(SemanticVersion currentVersion)
        {
            List<Release> releases = await GetVersionInformation();

            if (releases != null)
            {
                Release topversion = releases.Select(t => (t.Version, t)).Max().t;

                if (topversion.Version > currentVersion)
                {
                    return topversion;
                }
            }

            // we have no release available
            return new Release(currentVersion);
        }

        static async Task<List<Release>> GetVersionInformation()
        {
            try
            {
                return await httpClient.GetFromJsonAsync<List<Release>>("https://s3.us-east-1.amazonaws.com/platformupdate.particular.net/servicecontrol.txt");
            }
            catch
            {
                return null;
            }
        }

        static readonly HttpClient httpClient = new HttpClient(new HttpClientHandler
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

            public Release(SemanticVersion current) => Tag = current.ToNormalizedString();

            [JsonPropertyName("tag")]
            public string Tag { get; set; }

            [JsonPropertyName("release")]
            public Uri ReleaseUri { get; set; }

            [JsonPropertyName("published")]
            public DateTimeOffset Published { get; set; }

            [JsonPropertyName("assets")]
            public List<Asset> Assets { get; set; }

            [JsonIgnore]
            public SemanticVersion Version => SemanticVersion.Parse(Tag);
        }

        public class Asset
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("size")]
            public long Size { get; set; }

            [JsonPropertyName("download")]
            public Uri Download { get; set; }
        }
    }
}