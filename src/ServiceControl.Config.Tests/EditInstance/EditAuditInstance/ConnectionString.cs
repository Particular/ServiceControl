﻿namespace ServiceControl.Config.Tests.EditInstance.EditAuditInstance
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
            var transportInfo = ServiceControlCoreTransports.Find("MSMQ");

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

            Assert.That(viewModel.ShowConnectionString, Is.False);
            Assert.That(viewModel.SelectedTransport.Name, Is.EqualTo("MSMQ"));
            Assert.IsEmpty(viewModel.SampleConnectionString);
            Assert.IsNull(viewModel.TransportWarning);
        }

        [TestAllTransportsExcept("MSMQ")]
        public void Non_MSMQ_transport_is_selected(string transportInfoName)
        {
            var viewModel = Given_an_audit_instance()
                .When_a_transport_is_selected(transportInfoName);

            Assert.That(viewModel.ShowConnectionString, Is.True);
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
            var viewModel = Given_an_audit_instance()
                .When_no_transport_is_selected();

            Assert.That(viewModel.ShowConnectionString, Is.False);
            Assert.IsNull(viewModel.SampleConnectionString);
            Assert.IsNull(viewModel.TransportWarning);
        }
    }
}
