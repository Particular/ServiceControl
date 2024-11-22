namespace ServiceControlInstaller.Engine.Instances
{
    using System;
    using System.Linq;

    public class TransportInfo
    {
        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string TypeName { get; set; }

        public string SampleConnectionString { get; set; }

        public string Help { get; set; }

        public bool AvailableInSCMU { get; set; } = true;

        public bool Removed { get; set; }

        public string AutoMigrateTo { get; set; }

        public string[] Aliases { get; set; } = [];

        public string ZipName
        {
            get
            {
                var dotLocation = Name.IndexOf('.');
                return dotLocation > 0
                    ? Name.Substring(0, dotLocation)
                    : Name;
            }
        }

        public bool Matches(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            return input.Equals(Name, StringComparison.OrdinalIgnoreCase)
                || input.Equals(DisplayName, StringComparison.OrdinalIgnoreCase)
                || input.Equals(TypeName, StringComparison.OrdinalIgnoreCase)
                || Aliases.Contains(input, StringComparer.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            if (obj is not TransportInfo that)
            {
                return false;
            }

            return DisplayName.Equals(that.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return DisplayName.GetHashCode();
        }
    }

    public class TransportManifest
    {
        public TransportInfo[] Definitions { get; set; } = [];
    }
}