namespace ServiceControl.UnitTests.ArgumentParsing
{
    using NUnit.Framework;
    using Particular.ServiceControl.Hosting;

    [TestFixture]
    public class OptionsTests
    {
        [Test]
        public void CanParseAzureConnectionString()
        {
            var args = new HostArguments(new[] { "--install", "-d=ServiceControl/TransportType==NServiceBus.AzureStorageQueue, NServiceBus.Azure.Transports.WindowsAzureStorageQueues", "-d=NServiceBus/Transport==DefaultEndpointsProtocol=https;AccountName=account-name;AccountKey=XXXXX/XXXXX+XXXXX....XXX==;" });

            Assert.AreEqual("DefaultEndpointsProtocol=https;AccountName=account-name;AccountKey=XXXXX/XXXXX+XXXXX....XXX==;", args.Options["NServiceBus/Transport"]);
        }
    }
}
