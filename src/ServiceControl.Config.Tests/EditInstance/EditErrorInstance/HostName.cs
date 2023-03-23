namespace ServiceControl.Config.Tests.EditInstance.EditErrorInstance
{
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceEdit;
    using static EditingErrorHostNameExtensions;

    public static class EditingErrorHostNameExtensions
    {
        public static ServiceControlEditViewModel Given_editing_error_instance()
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

    class EditErrorInstanceHostNameTests
    {
        [TestCase("     ")]
        [TestCase("blahblah")]
        [TestCase("*")]
        public void Using_a_different_value_than_localhost(string hostName)
        {
            var viewModel = Given_editing_error_instance()
                .When_the_user_doesnt_use_localhost(hostName);

            Assert.AreEqual("Not using localhost can expose ServiceControl to anonymous access.", viewModel.HostNameWarning);
        }

        [Test]
        public void Screen_Loaded()
        {
            var viewModel = Given_editing_error_instance();

            Assert.AreEqual("localhost", viewModel.HostName);
            Assert.AreNotEqual("Not using localhost can expose ServiceControl to anonymous access.", viewModel.HostNameWarning);
        }
    }
}
