namespace ServiceControl.Transports.ASBS
{
    public class SharedAccessSignatureAuthentication : AuthenticationMethod
    {
        public SharedAccessSignatureAuthentication(string connectionString) => ConnectionString = connectionString;

        public string ConnectionString { get; }
    }
}