namespace ServiceControlInstaller.Engine.Instances
{
    using System;

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

        public override bool Equals(object obj)
        {
            var that = obj as TransportInfo;
            if (that == null)
            {
                return false;
            }

            return Name.Equals(that.Name, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }
    }
}