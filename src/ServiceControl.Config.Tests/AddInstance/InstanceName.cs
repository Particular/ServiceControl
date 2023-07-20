namespace ServiceControl.Config.Tests.AddInstance
{
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceAdd;

    public class InstanceName
    {
        [Test]
        public void Add_instance_name_taken_adds_1_to_instance_name()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                ConventionName = "Foo",
                GetWindowsServiceNames = () => new string[] { "Particular.Foo" }
            };

            viewModel.OnConventionNameChanged();

            var expectedAuditInstanceServiceName = $"Particular.{viewModel.ConventionName}-1.Audit";

            var expectedErrorInstanceServiceName = $"Particular.{viewModel.ConventionName}-1";

            Assert.AreEqual(expectedAuditInstanceServiceName, viewModel.AuditInstanceName);

            Assert.AreEqual(expectedErrorInstanceServiceName, viewModel.ErrorInstanceName);
        }

        [Test]
        [TestCase("Foo/*", "Foo")]
        [TestCase("Foo     ", "Foo")]
        [TestCase("  Foo/*", "Foo")]
        [TestCase("  Foo     ", "Foo")]
        [TestCase("  Foo a     ", "Foo.a")]
        [TestCase(@"<foo", "foo")]
        [TestCase(@">  foo", "foo")]
        [TestCase(@"foo | foo", "foo.foo")]
        [TestCase(@"foo?", "foo")]
        [TestCase(@"*      foo", "foo")]
        public void Add_instance_name_with_illegal_characters_gets_sanitized(string instanceName, string expected)
        {
            var viewModel = new ServiceControlAddViewModel
            {
                AuditInstanceName = instanceName,
                ErrorInstanceName = instanceName,
                InstallAuditInstance = true,
                InstallErrorInstance = true,
                SubmitAttempted = true
            };

            Assert.AreEqual(expected, viewModel.AuditInstanceName);
            Assert.AreEqual(expected, viewModel.ErrorInstanceName);
        }
    }
}
