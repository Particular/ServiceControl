namespace ServiceControl.Monitoring.UnitTests.API
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

            Assert.That(result.Id, Is.EqualTo(typeName));
            Assert.That(result.TypeName, Is.EqualTo(typeName));
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

            Assert.That(result.TypeName, Is.EqualTo(incorrectTypeName));
        }

        [Test]
        public void Parsing_type_info_with_incorrect_assembly_name_copies_value_to_type_name()
        {
            var incorrectTypeName = "This/is not a proper file type, this-is-not assembly name";
            var result = MonitoredEndpointMessageTypeParser.Parse(incorrectTypeName);

            Assert.That(result.TypeName, Is.EqualTo(incorrectTypeName));
        }

        static void AssertParsedTypeInfo(Type type, MonitoredEndpointMessageType result)
        {
            var assemblyName = type.Assembly.GetName();

            var keyToken = assemblyName.GetPublicKeyToken();

            Assert.That(result.TypeName, Is.EqualTo(type.FullName));
            Assert.That(result.AssemblyName, Is.EqualTo(assemblyName.Name));
            Assert.That(result.AssemblyVersion, Is.EqualTo(assemblyName.Version.ToString()));
            Assert.That(result.Culture, Is.EqualTo(assemblyName.CultureName));
            Assert.That(result.PublicKeyToken, Is.EqualTo(string.Concat(keyToken.Select(b => b.ToString("x2")))));
        }
    }
}