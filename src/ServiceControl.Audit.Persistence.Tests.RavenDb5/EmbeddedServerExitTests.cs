namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;
    using NUnit.Framework;

    [TestFixture]
    class EmbeddedServerExitTests
    {
        [Test]
        public async Task Verify_onerror_is_called_if_dbserver_stopped()
        {
            var _ = await SharedEmbeddedServer.GetInstance();
            Assert.IsFalse(SharedEmbeddedServer.OnErrorCalled, "OnErrorCalled should be false");
            await SharedEmbeddedServer.Stop();
            Assert.IsTrue(SharedEmbeddedServer.OnErrorCalled, "OnErrorCalled should be true");
        }
    }
}