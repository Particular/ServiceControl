namespace ServiceControl.Monitoring.Tests.API
{
    using System;
    using System.Linq;
    using Http.Diagrams;
    using NServiceBus;
    using NUnit.Framework;

    public class MonitoredEndpointMessageTypeParserTests
    {
        [Test]
        public void Parsing_empty_string_returns_empty_type_name()
        {
            var result = MonitoredEndpointMessageTypeParser.Parse("");

            Assert.IsNull(result.Id);
            Assert.IsNull(result.TypeName);
            Assert.IsNull(result.AssemblyName);
            Assert.IsNull(result.AssemblyVersion);
            Assert.IsNull(result.Culture);
            Assert.IsNull(result.PublicKeyToken);
        }

        [Test]
        public void Parsing_TypeName_only_value_sets_type_name()
        {
            var typeName = "SomeNamespace.SomeType";

            var result = MonitoredEndpointMessageTypeParser.Parse(typeName);

            Assert.AreEqual(typeName, result.Id);
            Assert.AreEqual(typeName, result.TypeName);
            Assert.IsNull(result.AssemblyName);
            Assert.IsNull(result.AssemblyVersion);
            Assert.IsNull(result.Culture);
            Assert.IsNull(result.PublicKeyToken);
        }

        [Test]
        public void Parsing_AssemblyQualifiedName_of_a_type_from_signed_assembly_sets_all_values()
        {
            var type = typeof(EndpointConfiguration);

            var result = MonitoredEndpointMessageTypeParser.Parse(type.AssemblyQualifiedName);

            AssertParsedTypeInfo(type, result);
        }

        [Test]
        public void Parsing_AssemblyQualifiedName_of_a_type_from_unsigned_assembly_sets_all_values()
        {
            var type = typeof(MonitoredEndpointMessageTypeParserTests);

            var result = MonitoredEndpointMessageTypeParser.Parse(type.AssemblyQualifiedName);

            AssertParsedTypeInfo(type, result);
        }

        [Test]
        public void Parsing_type_info_ending_with_comma_copies_value_to_type_name()
        {
            var incorrectTypeName = "This/is not a proper file type,";
            var result = MonitoredEndpointMessageTypeParser.Parse(incorrectTypeName);

            Assert.AreEqual(incorrectTypeName, result.TypeName);
        }

        [Test]
        public void Parsing_type_info_with_incorrect_assembly_name_copies_value_to_type_name()
        {
            var incorrectTypeName = "This/is not a proper file type, this-is-not assembly name";
            var result = MonitoredEndpointMessageTypeParser.Parse(incorrectTypeName);

            Assert.AreEqual(incorrectTypeName, result.TypeName);
        }

        static void AssertParsedTypeInfo(Type type, MonitoredEndpointMessageType result)
        {
            var assemblyName = type.Assembly.GetName();

            var keyToken = assemblyName.GetPublicKeyToken();

            Assert.AreEqual(type.FullName, result.TypeName);
            Assert.AreEqual(assemblyName.Name, result.AssemblyName);
            Assert.AreEqual(assemblyName.Version.ToString(), result.AssemblyVersion);
            Assert.AreEqual(assemblyName.CultureName, result.Culture);
            Assert.AreEqual(string.Concat(keyToken.Select(b => b.ToString("x2"))), result.PublicKeyToken);
        }
    }
}