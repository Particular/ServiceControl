namespace ServiceControl.UnitTests
{
    using System.Configuration;
    using System.Runtime.CompilerServices;
    using NUnit.Framework;

    [TestFixture]
    public class UnsafeAccessorTests
    {
        [Test]
        public void Should_be_able_to_add_connection_string_to_collection_and_make_readonly_again()
        {
            var connectionString = new ConnectionStringSettings("reflection-test", "test-connection-string");

            Assert.Throws<ConfigurationErrorsException>(() => ConfigurationManager.ConnectionStrings.Add(connectionString));

            ref bool field = ref GetReadOnlyFieldRef(ConfigurationManager.ConnectionStrings);
            field = false;

            Assert.DoesNotThrow(() => ConfigurationManager.ConnectionStrings.Add(new ConnectionStringSettings("reflection-test", "test-connection-string")));

            SetCollectionReadOnly(ConfigurationManager.ConnectionStrings);

            Assert.Throws<ConfigurationErrorsException>(() => ConfigurationManager.ConnectionStrings.Add(connectionString));
        }

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_readOnly")]
        static extern ref bool GetReadOnlyFieldRef(ConfigurationElementCollection collection);

        [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "SetReadOnly")]
        static extern void SetCollectionReadOnly(ConfigurationElementCollection collection);
    }
}