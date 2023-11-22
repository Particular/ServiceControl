namespace ServiceControl.CustomChecks
{
    using System.Threading.Tasks;
    using Connection;
    using NServiceBus;

    class CustomChecksPlatformConnectionDetailsProvider : IProvidePlatformConnectionDetails
    {
        readonly string instanceMainQueue;

        public CustomChecksPlatformConnectionDetailsProvider(ReceiveAddresses receiveAddresses)
            => instanceMainQueue = receiveAddresses.MainReceiveAddress;

        public Task ProvideConnectionDetails(PlatformConnectionDetails connection)
        {
            connection.Add(
                "CustomChecks",
                new CustomChecksConnectionDetails
                {
                    Enabled = true,
                    CustomChecksQueue = instanceMainQueue
                });
            return Task.CompletedTask;
        }

        // HINT: This should match the type in the PlatformConnector package
        class CustomChecksConnectionDetails
        {
            public bool Enabled { get; set; }
            public string CustomChecksQueue { get; set; }
        }
    }
}