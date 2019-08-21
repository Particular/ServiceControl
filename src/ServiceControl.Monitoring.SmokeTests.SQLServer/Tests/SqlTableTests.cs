namespace ServiceControl.Monitoring.SmokeTests.SQLServer.Tests
{
    using NUnit.Framework;
    using Transports.SQLServer;

    public class SqlTableTests
    {
        [Test]
        public void When_no_schema_default_is_used_instead()
        {
            var sqlTable = SqlTable.Parse("Endpoint", "dbo");

            Assert.AreEqual("Endpoint", sqlTable.UnquotedName);
            Assert.AreEqual("dbo", sqlTable.UnquotedSchema);
        }

        [Test]
        public void When_no_catalog_specified_the_value_in_sqlTable_is_null()
        {
            var sqlTable = SqlTable.Parse("Endpoint@[some-schema]", "dbo");

            Assert.AreEqual("Endpoint", sqlTable.UnquotedName);
            Assert.AreEqual(null, sqlTable.UnquotedCatalog);
        }

        [TestCase("Endpoint@[s]@[c]", "[Endpoint]", "[s]", "[c]")]
        [TestCase("Endpo]int@[schema--x]@[D234F]", "[Endpo]]int]", "[schema--x]", "[D234F]")]
        [TestCase("[Quoted]@[x]@[z]", "[Quoted]", "[x]", "[z]")]
        public void Endpoint_name_schema_and_catalog_are_parsed_from_address_string_representation(string address, string endpoint, string schema, string catalog)
        {
            var sqlTable = SqlTable.Parse(address, "dbo");

            Assert.AreEqual(endpoint, sqlTable.QuotedName);
            Assert.AreEqual(schema, sqlTable.QuotedSchema);
            Assert.AreEqual(catalog, sqlTable.QuotedCatalog);
        }

        [Test]
        public void Default_schema_is_used_if_none_is_specified_in_the_address()
        {
            var sqlTable = SqlTable.Parse("Endpoint", "custom-schema");

            Assert.AreEqual(sqlTable.UnquotedSchema, "custom-schema");
        }
    }
}