namespace ServiceControl.Operations
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using Infrastructure;
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

            public override Task Enrich(IReadOnlyDictionary<string, string> headers, IDictionary<string, object> metadata)
            {
                var status = GetLicenseStatus(headers);
                var endpoint = EndpointDetailsParser.ReceivingEndpoint(headers);

                // The ReceivingEndpoint will be null for messages from v3.3.x endpoints that were successfully
                // processed because we dont have the information from the relevant headers.
                if (endpoint != null)
                {
                    licenseStatusKeeper.Set(endpoint.Name + endpoint.Host, status);
                }

                return TaskEx.CompletedTask;
            }

            public string GetLicenseStatus(IReadOnlyDictionary<string, string> headers)
            {
                if (!headers.TryGetValue("$.diagnostics.license.expired", out var expired))
                {
                    return "unknown";
                }

                if (string.IsNullOrEmpty(expired))
                {
                    return "unknown";
                }

                bool.TryParse(expired, out var hasLicenseExpired);

                return hasLicenseExpired ? "expired" : "valid";
            }
        }
    }
}