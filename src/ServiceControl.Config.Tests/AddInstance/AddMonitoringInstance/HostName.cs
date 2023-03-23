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

            Assert.AreEqual("Not using localhost can expose ServiceControl to anonymous access.", viewModel.HostNameWarning);
        }

        [Test]
        public void Screen_Loaded()
        {
            var viewModel = Given_adding_monitoring_instance();

            Assert.AreEqual("localhost", viewModel.HostName);
            Assert.AreNotEqual("Not using localhost can expose ServiceControl to anonymous access.", viewModel.HostNameWarning);
        }
    }
}
