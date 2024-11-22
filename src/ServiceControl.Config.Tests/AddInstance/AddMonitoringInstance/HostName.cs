namespace ServiceControl.Config.Tests.AddInstance.AddMonitoringInstance
{
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceAdd;
    using static AddingMonitoringHostNameExtensions;

    public static class AddingMonitoringHostNameExtensions
    {
        public static MonitoringAddViewModel Given_adding_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel();
            return viewModel;
        }

        public static MonitoringAddViewModel When_the_user_doesnt_use_localhost(this MonitoringAddViewModel viewModel, string hostName)
        {
            viewModel.HostName = hostName;

            return viewModel;
        }

    }

    class AddMonitoringInstanceHostNameTests
    {
        [TestCase("     ")]
        [TestCase("blahblah")]
        [TestCase("*")]
        public void Using_a_different_value_than_localhost(string hostName)
        {
            var viewModel = Given_adding_monitoring_instance()
                .When_the_user_doesnt_use_localhost(hostName);

            Assert.That(viewModel.HostNameWarning, Is.EqualTo("Not using localhost can expose ServiceControl to anonymous access."));
        }

        [Test]
        public void Screen_Loaded()
        {
            var viewModel = Given_adding_monitoring_instance();

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.HostName, Is.EqualTo("localhost"));
                Assert.That(viewModel.HostNameWarning, Is.Not.EqualTo("Not using localhost can expose ServiceControl to anonymous access."));
            });
        }
    }
}