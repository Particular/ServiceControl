namespace ServiceControl.Transports.ASBS
{
    public class SharedAccessSignatureAuthentication : AuthenticationSettings
    {
        public SharedAccessSignatureAuthentication(string connectionString) => ConnectionString = connectionString;

        public string ConnectionString { get; }
    }
}