namespace ServiceControl.Config.Tests.AddInstance.AddErrorInstance
{
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceAdd;
    using static AddingErrorHostNameExtensions;

    public static class AddingErrorHostNameExtensions
    {
        public static ServiceControlAddViewModel Given_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel();

            return viewModel;
        }

        public static ServiceControlAddViewModel When_the_user_does_not_use_localhost(this ServiceControlAddViewModel viewModel, string hostName)
        {
            viewModel.ErrorHostName = hostName;

            return viewModel;
        }
    }

    class AddErrorInstanceHostNameTests
    {
        [TestCase("     ")]
        [TestCase("blahblah")]
        [TestCase("*")]
        public void Using_a_different_value_than_localhost(string hostName)
        {
            var viewModel = Given_adding_error_instance()
                .When_the_user_does_not_use_localhost(hostName);

            Assert.That(viewModel.ErrorHostNameWarning, Is.EqualTo("Not using localhost can expose ServiceControl to anonymous access."));
        }

        [Test]
        public void Screen_Loaded()
        {
            var viewModel = Given_adding_error_instance();

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.ErrorHostName, Is.EqualTo("localhost"));
                Assert.That(viewModel.ErrorHostNameWarning, Is.Empty);
            });
        }
    }
}