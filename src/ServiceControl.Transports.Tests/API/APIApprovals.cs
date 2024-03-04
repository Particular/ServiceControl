namespace ServiceControl.Transport.Tests
{
    using NUnit.Framework;
    using Particular.Approvals;
    using PublicApiGenerator;
    using Transports;

    [TestFixture]
    class APIApprovals
    {
        [Test]
        public void ServiceControlTransport()
        {
            var serviceControlTransportApi = typeof(TransportSettings).Assembly.GeneratePublicApi(new ApiGeneratorOptions
            {
                ExcludeAttributes = ["System.Reflection.AssemblyMetadataAttribute"]
            });

            Approver.Verify(serviceControlTransportApi);
        }
    }
}