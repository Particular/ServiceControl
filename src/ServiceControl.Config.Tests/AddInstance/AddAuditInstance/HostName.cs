namespace ServiceControl.Config.Tests.AddInstance.AddAuditInstance
{
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceAdd;
    using static AddingAuditHostNameExtensions;

    public static class AddingAuditHostNameExtensions
    {
        public static ServiceControlAddViewModel Given_adding_audit_instance()
        {
            var viewModel = new ServiceControlAddViewModel();

            return viewModel;
        }

        public static ServiceControlAddViewModel When_the_user_doesnt_use_localhost(this ServiceControlAddViewModel viewModel, string hostName)
        {
            viewModel.AuditHostName = hostName;

            return viewModel;
        }

    }

    class AddAuditInstanceHostNameTests
    {
        [TestCase("     ")]
        [TestCase("blahblah")]
        [TestCase("*")]
        public void Using_a_different_value_than_localhost(string hostName)
        {
            var viewModel = Given_adding_audit_instance()
                .When_the_user_doesnt_use_localhost(hostName);

            Assert.That(viewModel.AuditHostNameWarning, Is.EqualTo("Not using localhost can expose ServiceControl to anonymous access."));
        }

        [Test]
        public void Screen_Loaded()
        {
            var viewModel = Given_adding_audit_instance();

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.AuditHostName, Is.EqualTo("localhost"));
                Assert.That(viewModel.AuditHostNameWarning, Is.Not.EqualTo("Not using localhost can expose ServiceControl to anonymous access."));
            });
        }
    }
}