namespace ServiceControl.UnitTests
{
    using System.Configuration;
    using System.Reflection;
    using NUnit.Framework;

    [TestFixture]
    public class PrivateReflectionTests
    {
        [Test]
        public void Should_be_able_to_add_connection_string_to_collection()
        {
            var connectionString = new ConnectionStringSettings("reflection-test", "test-connection-string");

            Assert.Throws<ConfigurationErrorsException>(() => ConfigurationManager.ConnectionStrings.Add(connectionString));

            var type = typeof(ConfigurationElementCollection);
            var field = type.GetField("_readOnly", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null);

            field?.SetValue(ConfigurationManager.ConnectionStrings, false);

            Assert.DoesNotThrow(() => ConfigurationManager.ConnectionStrings.Add(new ConnectionStringSettings("reflection-test", "test-connection-string")));
        }
    }
}
