﻿namespace ServiceControl.Config.Tests.AddInstance.AddMonitoringInstance
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
            var transportInfo = ServiceControlCoreTransports.Find(TransportNames.MSMQ);

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

            Assert.IsFalse(viewModel.ShowConnectionString);
            Assert.AreEqual(TransportNames.MSMQ, viewModel.SelectedTransport.DisplayName);
            Assert.IsEmpty(viewModel.SampleConnectionString);
            Assert.IsNull(viewModel.TransportWarning);
        }

        [TestCase(TransportNames.AmazonSQS, true)]
        [TestCase(TransportNames.AzureServiceBus, false)]
        [TestCase(TransportNames.SQLServer, true)]
        [TestCase(TransportNames.RabbitMQClassicDirectRoutingTopology, false)]
        [TestCase(TransportNames.AzureStorageQueue, true)]
        public void Non_MSMQ_transport_is_selected(string transportInfoName, bool showTransportWarning)
        {
            var viewModel = Given_a_monitoring_instance()
                .When_a_transport_is_selected(transportInfoName);

            Assert.IsTrue(viewModel.ShowConnectionString);
            Assert.AreEqual(transportInfoName, viewModel.SelectedTransport.DisplayName);
            Assert.IsNotEmpty(viewModel.SampleConnectionString);

            if (showTransportWarning)
            {
                Assert.IsNotEmpty(viewModel.TransportWarning);
            }
            else
            {
                Assert.IsNull(viewModel.TransportWarning);
            }
        }

        [Test]
        public void Screen_Loaded()
        {
            var viewModel = Given_a_monitoring_instance()
                .When_no_transport_is_selected();

            Assert.IsFalse(viewModel.ShowConnectionString);
            Assert.IsNull(viewModel.SampleConnectionString);
            Assert.IsNull(viewModel.TransportWarning);
        }

    }
}
