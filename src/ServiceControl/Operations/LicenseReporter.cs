namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using Contracts.Operations;
    using NServiceBus;
    using NServiceBus.Features;

    public class LicenseReporter : Feature
    {
        public LicenseReporter()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<UpdateLicenseEnricher>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<LicenseStatusKeeper>(DependencyLifecycle.SingleInstance);
        }

        public class UpdateLicenseEnricher : ImportEnricher
        {
            LicenseStatusKeeper licenseStatusKeeper;

            public UpdateLicenseEnricher(LicenseStatusKeeper licenseStatusKeeper)
            {
                this.licenseStatusKeeper = licenseStatusKeeper;
            }

            public override void Enrich(ImportMessage message)
            {
                var status = GetLicenseStatus(message.PhysicalMessage.Headers);
                var endpoint = EndpointDetailsParser.ReceivingEndpoint(message.PhysicalMessage.Headers);

                // The ReceivingEndpoint will be null for messages from v3.3.x endpoints that were successfully
                // processed because we dont have the information from the relevant headers.
                if (endpoint != null)
                {
                    licenseStatusKeeper.Set(endpoint.Name + endpoint.Host, status);
                }
            }

            public string GetLicenseStatus(Dictionary<string, string> headers)
            {
                string expired;
                if (!headers.TryGetValue("$.diagnostics.license.expired", out expired))
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
}