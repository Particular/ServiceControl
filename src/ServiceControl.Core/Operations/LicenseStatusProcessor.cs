namespace ServiceControl.Operations
{
    using Particular.Operations.Ingestion.Api;

    public class LicenseStatusProcessor : IProcessSuccessfulMessages, IProcessFailedMessages
    {
        readonly LicenseStatusKeeper licenseStatusKeeper;
        readonly LicenseStatusChecker checker;

        public LicenseStatusProcessor(LicenseStatusKeeper licenseStatusKeeper, LicenseStatusChecker checker)
        {
            this.licenseStatusKeeper = licenseStatusKeeper;
            this.checker = checker;
        }


        public void ProcessSuccessful(IngestedMessage message)
        {
            CheckLicense(message);
        }

        public void ProcessFailed(IngestedMessage message)
        {
            CheckLicense(message);
        }

        void CheckLicense(IngestedMessage message)
        {
            var status = checker.GetLicenseStatus(message.Headers);
            
            // The ReceivingEndpoint will be null for messages from v3.3.x endpoints that were successfully
            // processed because we dont have the information from the relevant headers.
            if (message.ProcessedAt != EndpointInstance.Unknown)
            {
                licenseStatusKeeper.Set(message.ProcessedAt.ToString(), status);
            }
        }

        

    }
}