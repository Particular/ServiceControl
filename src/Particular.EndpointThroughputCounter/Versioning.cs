using System.Reflection;
using System.Text.RegularExpressions;
using NuGet.Versioning;

static class Versioning
{
    public static string InformationalVersion { get; }
    public static string FullSha { get; }
    public static string ShortSha { get; }
    public static string NuGetVersion { get; }
    public static string PreReleaseLabel { get; }

    static Versioning()
    {
        var assembly = Assembly.GetEntryAssembly();
        var infoVersionAtt = assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().FirstOrDefault();
        InformationalVersion = infoVersionAtt?.InformationalVersion ?? "Unknown";

        var versionRegex = new Regex(@"^(?<CoreVersion>\d+\.\d+\.\d+)(-(?<PrereleaseLabel>[a-z0-9-]+)\.(?<PrereleaseNumber>\d+)\.(?<Height>\d+))?\+(?<FullSha>[0-9a-f]{40})$");

        var match = versionRegex.Match(InformationalVersion);
        if (match.Success)
        {
            FullSha = match.Groups["FullSha"].Value;
            ShortSha = FullSha[..7];

            var coreVersion = match.Groups["CoreVersion"].Value;
            PreReleaseLabel = match.Groups["PrereleaseLabel"].Value;
            if (string.IsNullOrEmpty(PreReleaseLabel))
            {
                NuGetVersion = coreVersion;
            }
            else
            {
                var prereleaseNumber = match.Groups["PrereleaseNumber"].Value;
                var height = match.Groups["Height"];
                NuGetVersion = $"{coreVersion}-{PreReleaseLabel}.{prereleaseNumber}+{height}";
            }
        }

    }

    public static async Task<bool> EvaluateVersion(bool skipVersionCheck, CancellationToken cancellationToken = default)
    {
        Out.WriteLine($"Particular.EndpointThroughputCounter {NuGetVersion} (Sha:{ShortSha})");

        if (skipVersionCheck)
        {
            Out.WriteWarn("Skipping current version check. Please ensure you are not using an outdated version.");
            return true;
        }

        const string checkUrl = "https://s3.amazonaws.com/particular.downloads/EndpointThroughputCounter/version.txt";

        try
        {
            Out.WriteLine("Checking for latest version...");
            NuGetVersion latest = null;

            using (var tokenSource = new CancellationTokenSource(10_000))
            using (var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(tokenSource.Token, cancellationToken))
            {
                try
                {
                    using var http = new HttpClient();
                    var versionString = await http.GetStringAsync(checkUrl, combinedTokenSource.Token);
                    latest = new NuGetVersion(versionString.Trim());
                }
                catch (OperationCanceledException) when (combinedTokenSource.Token.IsCancellationRequested)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        // Shutting down
                        throw;
                    }
                    // 10s timeout
                    Out.WriteWarn("WARNING: Unable to check current version within 10s timeout. The tool will still run, but only the most recent version of the tool should be used.");
                    return true;
                }
            }

            var current = new NuGetVersion(NuGetVersion);

            if (latest != null && latest > current)
            {
                Out.WriteLine();
                Out.WriteLine($"** New version detected: {latest.ToNormalizedString()}");
#if EXE
                Out.WriteLine("** Download the latest version here: https://s3.amazonaws.com/particular.downloads/EndpointThroughputCounter/Particular.EndpointThroughputCounter.zip");
#else
                Out.WriteLine("** To install, execute the following command:");
                Out.WriteLine(" > dotnet tool update -g Particular.EndpointThroughputCounter --add-source=https://f.feedz.io/particular-software/packages/nuget/index.json");
#endif
                Out.WriteLine();
                return false;
            }
        }
        catch (HttpRequestException)
        {
            Out.WriteWarn("WARNING: Unable to validate the latest version of the tool. The tool will still run, but only the most recent version of the tool should be used.");
        }

        return true;
    }
}