namespace ServiceControl.Config.Tests.EditInstance.EditMonitoringInstance
{
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceEdit;
    using static EditingMonitoringHostNameExtensions;

    public static class EditingMonitoringHostNameExtensions
    {
        public static MonitoringEditViewModel Given_editing_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel
            {
                HostName = "localhost"
            };

            return viewModel;
        }

        public static MonitoringEditViewModel When_the_user_doesnt_use_localhost(this MonitoringEditViewModel viewModel, string hostName)
        {
            viewModel.HostName = hostName;

            return viewModel;
        }

    }

    class EditMonitoringInstanceHostNameTests
    {
        [TestCase("     ")]
        [TestCase("blahblah")]
        [TestCase("*")]
        public void Using_a_different_value_than_localhost(string hostName)
        {
            var viewModel = Given_editing_monitoring_instance()
                .When_the_user_doesnt_use_localhost(hostName);

            Assert.That(viewModel.HostNameWarning, Is.EqualTo("Not using localhost can expose ServiceControl to anonymous access."));
        }

        [Test]
        public void Screen_Loaded()
        {
            var viewModel = Given_editing_monitoring_instance();

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.HostName, Is.EqualTo("localhost"));
                Assert.That(viewModel.HostNameWarning, Is.Not.EqualTo("Not using localhost can expose ServiceControl to anonymous access."));
            });
        }
    }
}