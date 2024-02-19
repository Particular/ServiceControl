namespace Particular.License.Contracts
{
    class LicenseData
    {
        public required string ServiceControlAPI { get; set; }
        public required string Broker { get; set; }
        public required string AuditQueue { get; set; }
        public required string ErrorQueue { get; set; }
    }
}
