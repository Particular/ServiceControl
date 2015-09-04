namespace ServiceControlInstaller.PowerShell
{
    using ServiceControlInstaller.Engine.Instances;

    public class PsTransportInfo
    {
        public static PsTransportInfo FromTransport(TransportInfo transport)
        {
            return new PsTransportInfo
            {
                Name = transport.Name,
                TypeName = transport.TypeName,
                SampleConnectionString = transport.SampleConnectionString
            };
        }
        public string Name { get; set; }
        public string TypeName { get; set; }
        public string SampleConnectionString { get; set; }
    }
}