namespace ServiceControlInstaller.Engine.UnitTests.Configuration
{
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Instances;

    [TestFixture]
    class NewAuditInstanceTests
    {
        [Test]
        public void Should_default_persistence_to_raven5()
        {
            var newInstance = new ServiceControlAuditNewInstance();

            StringAssert.Contains("RavenDb5", newInstance.PersistenceType);
        }
    }
}
