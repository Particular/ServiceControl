using System;

namespace ServiceControlInstaller.Engine.Instances
{
    public class TransportInfo
    {
        public TransportInfo()
        {
            Matches = name => name.Equals(TypeName, StringComparison.OrdinalIgnoreCase);
        }

        public string Name { get; set; }
        public string TypeName { get; set; }
        public string ZipName { get; set; }
        public string SampleConnectionString { get; set; }
        public string Help { get; set; }
        public bool Default { get; set; }

        public Func<string, bool> Matches { get; set; }
    }
}