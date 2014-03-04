namespace ServiceControl.Operations
{
    using System;
    using Contracts.Operations;
    using NServiceBus;

    public class UpdateLicenseEnricher : ImportEnricher
    {
        public LicenseStatusKeeper LicenseStatusKeeper { get; set; }

        public override void Enrich(ImportMessage message)
        {
            string expiresin;
            if (!message.PhysicalMessage.Headers.TryGetValue("$.diagnostics.licenseexpires", out expiresin))
            {
                return;
            }
            
            var expired = DateTimeExtensions.ToUtcDateTime(expiresin);
            var status = "valid";
            
            if (expired <= DateTime.UtcNow)
            {
                status = "expired";
            }

            var endpoint = EndpointDetails.ReceivingEndpoint(message.PhysicalMessage.Headers);

            LicenseStatusKeeper.Set(endpoint.Name + endpoint.Machine, status);
        }
    }
}