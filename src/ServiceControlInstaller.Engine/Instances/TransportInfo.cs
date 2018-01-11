namespace ServiceControlInstaller.Engine.Instances
{
    public class TransportInfo
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public string SampleConnectionString { get; set; }
        public string MatchOn { get; set; }
        public string Help { get; set; }
        public bool Default { get; set; }
        public bool ConnectionStringRequired { get; set; } = true;
    }
}
