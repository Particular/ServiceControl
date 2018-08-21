namespace ServiceControlInstaller.PowerShell
{
    using Engine.Instances;

    public class PsTransportInfo
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public string SampleConnectionString { get; set; }

        public static PsTransportInfo FromTransport(TransportInfo transport)
        {
            return new PsTransportInfo
            {
                Name = transport.Name,
                TypeName = transport.TypeName,
                SampleConnectionString = transport.SampleConnectionString
            };
        }
    }
}