namespace ServiceControl.Monitoring.SmokeTests.SQS.Tests
{
    using NUnit.Framework;
    using Transports.SQLServer;

    public class SqlNameHelperTests
    {
        [TestCase("[quoted]")]
        [TestCase("[quote]]d]")]
        public void Quoted_names_does_not_change(string name)
        {
            var quotedName = SqlNameHelper.Quote(name);

            Assert.AreEqual(name, quotedName);
        }

        [TestCase("adfasfd", "[adfasfd]")]
        [TestCase("ad[fasfd]", "[ad[fasfd]]]")]
        public void Unquoted_name_by_appending_brackets_and_escaping_right_content_brackest(string name, string expectedQuotedName)
        {
            var quotedName = SqlNameHelper.Quote(name);

            Assert.AreEqual(expectedQuotedName, quotedName);
        }

        [Test]
        public void Quoted_name_is_unchanged_after_quoting()
        {
            var quotedName = "[name]";
            var afterQuoting = SqlNameHelper.Quote(quotedName);

            Assert.AreEqual(quotedName, afterQuoting);
        }
    }
}