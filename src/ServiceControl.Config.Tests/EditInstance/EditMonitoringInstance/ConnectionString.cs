namespace ServiceControl.Config.Tests.EditInstance.EditMonitoringInstance
{
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceEdit;
    using ServiceControlInstaller.Engine.Instances;
    using static EditingConnectionStringExtensions;

    public static class EditingConnectionStringExtensions
    {
        public static MonitoringEditViewModel Given_a_monitoring_instance()
        {
            var viewModel = new MonitoringEditViewModel();

            return viewModel;
        }

        public static MonitoringEditViewModel When_a_transport_is_selected(this MonitoringEditViewModel viewModel, string transportName)
        {
            var transportInfo = ServiceControlCoreTransports.Find(transportName);

            viewModel.SelectedTransport = transportInfo;

            return viewModel;
        }

        public static MonitoringEditViewModel When_MSQMQ_transport_is_selected(this MonitoringEditViewModel viewModel)
        {
            var transportInfo = ServiceControlCoreTransports.Find("MSMQ");

            viewModel.SelectedTransport = transportInfo;

            return viewModel;
        }

        public static MonitoringEditViewModel When_no_transport_is_selected(this MonitoringEditViewModel viewModel)
        {
            return viewModel;
        }

    }

    class EditMonitoringConnectionStringsTests
    {
        [Test]
        public void MSMQ_transport_is_selected()
        {
            var viewModel = Given_a_monitoring_instance()
                .When_MSQMQ_transport_is_selected();

            Assert.That(viewModel.ShowConnectionString, Is.False);
            Assert.AreEqual("MSMQ", viewModel.SelectedTransport.Name);
            Assert.IsEmpty(viewModel.SampleConnectionString);
            Assert.IsNull(viewModel.TransportWarning);
        }

        [TestAllTransportsExcept("MSMQ")]
        public void Non_MSMQ_transport_is_selected(string transportInfoName)
        {
            var viewModel = Given_a_monitoring_instance()
                .When_a_transport_is_selected(transportInfoName);

            Assert.IsTrue(viewModel.ShowConnectionString);
            StringAssert.StartsWith(transportInfoName, viewModel.SelectedTransport.Name);
            Assert.IsNotEmpty(viewModel.SampleConnectionString);
            if (transportInfoName is "SQLServer" or "AmazonSQS" or "AzureStorageQueue")
            {
                Assert.IsNotNull(viewModel.TransportWarning);
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

            Assert.That(viewModel.ShowConnectionString, Is.False);
            Assert.IsNull(viewModel.SampleConnectionString);
            Assert.IsNull(viewModel.TransportWarning);
        }

    }
}
