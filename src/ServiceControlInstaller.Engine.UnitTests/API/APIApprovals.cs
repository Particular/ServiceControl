namespace ServiceControl.UnitTests.API
{
    using NUnit.Framework;
    using Particular.Approvals;
    using PublicApiGenerator;
    using ServiceControlInstaller.Engine.Instances;

    [TestFixture]
    class APIApprovals
    {
        [Test]
        public void TransportNames()
        {
            //HINT: Those names are used in PowerShell scripts thus constitute a public api.
            //Also Particular.PlatformSamples relies on it to specify the learning transport.
            var transportNamesType = typeof(TransportNames);
            var publicTransportNames = transportNamesType.Assembly.GeneratePublicApi(new ApiGeneratorOptions
            {
                IncludeTypes = new[] { transportNamesType },
                ExcludeAttributes = new[] { "System.Reflection.AssemblyMetadataAttribute" }
            });

            Approver.Verify(publicTransportNames);
        }
    }
}