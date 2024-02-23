namespace Particular.ThroughputCollector.Infrastructure
{
    using System;
    using System.Text.RegularExpressions;

    class SemVerVersion
    {
        public Version Version { get; }
        public string? PrereleaseLabel { get; }

        SemVerVersion(Version version, string? prereleaseLabel)
        {
            Version = version;
            PrereleaseLabel = prereleaseLabel;
        }

        public static bool TryParse(string? value, out SemVerVersion? version)
        {
            version = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var match = SemVerRegex.Match(value);
            if (!match.Success)
            {
                return false;
            }

            if (!Version.TryParse(match.Groups["MainVersion"].Value, out var stdVersion))
            {
                return false;
            }

            var prereleaseLabel = match.Groups["PrereleaseLabel"]?.Value;
            if (string.IsNullOrWhiteSpace(prereleaseLabel))
            {
                prereleaseLabel = null;
            }

            version = new SemVerVersion(stdVersion, prereleaseLabel);
            return true;
        }

        public static SemVerVersion? ParseOrDefault(string value)
        {
            if (TryParse(value, out var version))
            {
                return version;
            }

            return null;
        }

        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(PrereleaseLabel))
            {
                return Version.ToString();
            }

            return $"{Version}-{PrereleaseLabel}";
        }

        static readonly Regex SemVerRegex = new(@"(?<MainVersion>\d+\.\d+\.\d(\.\d+)?)+(-(?<PrereleaseLabel>[\w\-\.]+))?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
