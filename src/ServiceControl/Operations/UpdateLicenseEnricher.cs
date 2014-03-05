namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using Contracts.Operations;
 
    public class UpdateLicenseEnricher : ImportEnricher
    {
        public LicenseStatusKeeper LicenseStatusKeeper { get; set; }

        public override void Enrich(ImportMessage message)
        {   
            var status = GetLicenseStatus(message.PhysicalMessage.Headers);
            if (string.IsNullOrEmpty(status)) return;

            var endpoint = EndpointDetails.ReceivingEndpoint(message.PhysicalMessage.Headers);
            LicenseStatusKeeper.Set(endpoint.Name + endpoint.Machine, status);
        }

        public string GetLicenseStatus(Dictionary<string,string> headers)
        {   
            string expired;
            if (!headers.TryGetValue("$.diagnostics.license.expired", out expired))
            {
                return string.Empty;
            }
            bool hasLicenseExpired;
            bool.TryParse(expired, out hasLicenseExpired);
            
            return hasLicenseExpired ? "expired" : "valid";
        }
    }
}