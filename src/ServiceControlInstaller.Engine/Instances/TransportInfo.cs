namespace ServiceControlInstaller.Engine.Instances
{
    using System;

    public class TransportInfo
    {
        public TransportInfo()
        {
            Matches = name => name.Equals(TypeName, StringComparison.OrdinalIgnoreCase);
        }

        public string DisplayName { get; set; }
        public string TypeName { get; set; }
        public string ZipName { get; set; }
        public string SampleConnectionString { get; set; }
        public string Help { get; set; }
        public bool Default { get; set; }
        public bool AvailableInSCMU { get; set; }
        public string AutoMigrateTo { get; set; }
        public Func<string, bool> Matches { get; set; }

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
}