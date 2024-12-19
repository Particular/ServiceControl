namespace ServiceControl.Config.Tests
{
    using NUnit.Framework;
    using ServiceControl.Config.Extensions;

    class FilePathExtensionsTests
    {
        [TestCase(null)]
        [TestCase(@"C:\")]
        [TestCase(@"C:")]
        [TestCase(@"C:\foo\bar")]
        //[TestCase(@"/foo/bar", @"\foo\bar")] // NOTE: Returns \foobar
        [TestCase(@"C:\foo\bar", @"C:\foo\bar")]
        [TestCase(@"\\foo\bar", @"\foo\bar")]
        [TestCase(@"C:\foo\bar\", @"C:\foo\bar\")]
        [TestCase(@"C:\foo:bar", @"C:\foobar")]
        [TestCase(@"C:\foo|bar", @"C:\foobar")]
        public void TestSanitization(string original, string sanitized = null)
        {
            var converted = FilePathExtensions.SanitizeFilePath(original);

            Assert.That(converted, Is.EqualTo(sanitized ?? original));
        }
    }
}