namespace ServiceControl.Config.Tests.EditInstance.EditAuditInstance
{
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceEdit;
    using static EditingAuditHostNameExtensions;

    public static class EditingAuditHostNameExtensions
    {
        public static ServiceControlEditViewModel Given_editing_audit_instance()
        {
            var viewModel = new ServiceControlEditViewModel();

            return viewModel;
        }

        public static ServiceControlEditViewModel When_the_user_doesnt_use_localhost(this ServiceControlEditViewModel viewModel, string hostName)
        {
            viewModel.HostName = hostName;

            return viewModel;
        }

    }

    class EditAuditInstanceHostNameTests
    {
        [TestCase("     ")]
        [TestCase("blahblah")]
        [TestCase("*")]
        public void Using_a_different_value_than_localhost(string hostName)
        {
            var viewModel = Given_editing_audit_instance()
                .When_the_user_doesnt_use_localhost(hostName);

            Assert.That(viewModel.HostNameWarning, Is.EqualTo("Not using localhost can expose ServiceControl to anonymous access."));
        }

        [Test]
        public void Screen_Loaded()
        {
            var viewModel = Given_editing_audit_instance();

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.HostName, Is.EqualTo("localhost"));
                Assert.That(viewModel.HostNameWarning, Is.Not.EqualTo("Not using localhost can expose ServiceControl to anonymous access."));
            });
        }
    }
}