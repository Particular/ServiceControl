namespace ServiceControl.Config.Tests.EditInstance.EditMonitoringInstance
{
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceEdit;
    using ServiceControlInstaller.Engine.Instances;
    using static EditingAuditConnectionStringExtensions;

    public static class EditingAuditConnectionStringExtensions
    {
        public static ServiceControlAuditEditViewModel Given_an_audit_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel();

            return viewModel;
        }

        public static ServiceControlAuditEditViewModel When_a_transport_is_selected(this ServiceControlAuditEditViewModel viewModel, string transportName)
        {
            var transportInfo = ServiceControlCoreTransports.Find(transportName);

            viewModel.SelectedTransport = transportInfo;

            return viewModel;
        }

        public static ServiceControlAuditEditViewModel When_MSQMQ_transport_is_selected(this ServiceControlAuditEditViewModel viewModel)
        {
            var transportInfo = ServiceControlCoreTransports.Find(TransportNames.MSMQ);

            viewModel.SelectedTransport = transportInfo;

            return viewModel;
        }

        public static ServiceControlAuditEditViewModel When_no_transport_is_selected(this ServiceControlAuditEditViewModel viewModel)
        {
            return viewModel;
        }
    }

    class EditAuditConnectionStringsTests
    {
        [Test]
        public void MSMQ_transport_is_selected()
        {
            var viewModel = Given_an_audit_instance()
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
            var viewModel = Given_an_audit_instance()
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
            var viewModel = Given_an_audit_instance()
                .When_no_transport_is_selected();

            Assert.IsFalse(viewModel.ShowConnectionString);
            Assert.IsNull(viewModel.SampleConnectionString);
            Assert.IsNull(viewModel.TransportWarning);
        }
    }
}
