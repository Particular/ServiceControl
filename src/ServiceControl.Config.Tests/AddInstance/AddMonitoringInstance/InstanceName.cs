namespace ServiceControl.Config.Tests.AddInstance.AddMonitoringInstance
{
    using NUnit.Framework;
    using UI.InstanceAdd;

    class InstanceName
    {
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
        public void Add_instance_name_with_illegal_characters_get_sanitized(string instanceName, string expected)
        {
            var viewModel = new MonitoringAddViewModel
            {
                InstanceName = instanceName,
                SubmitAttempted = true
            };

            Assert.That(viewModel.InstanceName, Is.EqualTo(expected));
        }
    }
}