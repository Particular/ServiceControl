namespace ServiceControlInstaller.Engine.UnitTests.Configuration
{
    using NUnit.Framework;
    using ServiceControlInstaller.Engine.Instances;

    [TestFixture]
    class AuditInstanceTests
    {
        [Test]
        public void Should_default_to_raven35_when_no_config_entry_exists()
        {
            var instance = new ServiceControlAuditInstance(null);

            instance.Reload();

            StringAssert.EndsWith("RavenDb", instance.PersistenceType);
        }
    }
}
