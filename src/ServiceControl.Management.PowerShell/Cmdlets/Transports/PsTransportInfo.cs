namespace ServiceControl.Management.PowerShell
{
    using ServiceControlInstaller.Engine.Instances;

    public class PsTransportInfo
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string SampleConnectionString { get; set; }

        public static PsTransportInfo FromTransport(TransportInfo transport)
        {
            return new PsTransportInfo
            {
                Name = transport.Name,
                DisplayName = (transport.AvailableInSCMU ? "" : "DEPRECATED: ") + transport.DisplayName,
                SampleConnectionString = transport.SampleConnectionString
            };
        }
    }
}