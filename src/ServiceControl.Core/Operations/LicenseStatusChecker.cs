namespace ServiceControl.Operations
{
    using Particular.Operations.Ingestion.Api;

    public class LicenseStatusChecker
    {
        public string GetLicenseStatus(HeaderCollection headers)
        {
            string expired;
            if (!headers.TryGet("$.diagnostics.license.expired", out expired))
            {
                return "unknown";
            }

            if (string.IsNullOrEmpty(expired))
            {
                return "unknown";
            }

            bool hasLicenseExpired;
            bool.TryParse(expired, out hasLicenseExpired);

            return hasLicenseExpired ? "expired" : "valid";
        }
    }
}