namespace ServiceControl.Config.Tests.AddInstance.AddMonitoringInstance
{
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceAdd;
    using ServiceControlInstaller.Engine.Instances;
    using static AddingConnectionStringExtensions;

    public static class AddingConnectionStringExtensions
    {
        public static MonitoringAddViewModel Given_a_monitoring_instance()
        {
            var viewModel = new MonitoringAddViewModel();

            return viewModel;
        }

        public static MonitoringAddViewModel When_a_transport_is_selected(this MonitoringAddViewModel viewModel, string transportName)
        {
            var transportInfo = ServiceControlCoreTransports.Find(transportName);

            viewModel.SelectedTransport = transportInfo;

            return viewModel;
        }

        public static MonitoringAddViewModel When_MSQMQ_transport_is_selected(this MonitoringAddViewModel viewModel)
        {
            var transportInfo = ServiceControlCoreTransports.Find("MSMQ");

            viewModel.SelectedTransport = transportInfo;

            return viewModel;
        }

        public static MonitoringAddViewModel When_no_transport_is_selected(this MonitoringAddViewModel viewModel)
        {
            return viewModel;
        }

    }

    class AddMonitoringConnectionStringsTests
    {
        [Test]
        public void MSMQ_transport_is_selected()
        {
            var viewModel = Given_a_monitoring_instance()
                .When_MSQMQ_transport_is_selected();

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.ShowConnectionString, Is.False);
                Assert.That(viewModel.SelectedTransport.Name, Is.EqualTo("MSMQ"));
                Assert.That(viewModel.SampleConnectionString, Is.Empty);
                Assert.That(viewModel.TransportWarning, Is.Null);
            });
        }

        [TestAllTransportsExcept("MSMQ")]
        public void Non_MSMQ_transport_is_selected(string transportInfoName)
        {
            var viewModel = Given_a_monitoring_instance()
                .When_a_transport_is_selected(transportInfoName);

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.ShowConnectionString, Is.True);
                Assert.That(viewModel.SelectedTransport.Name, Does.StartWith(transportInfoName));
                Assert.That(viewModel.SampleConnectionString, Is.Not.Empty);
            });

            if (transportInfoName is "SQLServer" or "AmazonSQS" or "AzureStorageQueue" or "PostgreSQL")
            {
                Assert.That(viewModel.TransportWarning, Is.Not.Null);
                Assert.That(viewModel.TransportWarning, Is.Not.Empty);
            }
            else
            {
                Assert.That(viewModel.TransportWarning, Is.Null);
            }
        }

        [Test]
        public void Screen_Loaded()
        {
            var viewModel = Given_a_monitoring_instance()
                .When_no_transport_is_selected();

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.ShowConnectionString, Is.False);
                Assert.That(viewModel.SampleConnectionString, Is.Null);
                Assert.That(viewModel.TransportWarning, Is.Null);
            });
        }

    }
}