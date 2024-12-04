namespace ServiceControl.Config.Tests.AddInstance
{
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceAdd;
    using ServiceControlInstaller.Engine.Instances;
    using static AddingConnectionStringExtensions;

    public static class AddingConnectionStringExtensions
    {
        public static ServiceControlAddViewModel Given_a_service_control_instance()
        {
            var viewModel = new ServiceControlAddViewModel();

            return viewModel;
        }

        public static ServiceControlAddViewModel When_a_transport_is_selected(this ServiceControlAddViewModel viewModel, string transportName)
        {
            var transportInfo = ServiceControlCoreTransports.Find(transportName);

            viewModel.SelectedTransport = transportInfo;

            return viewModel;
        }

        public static ServiceControlAddViewModel When_MSQMQ_transport_is_selected(this ServiceControlAddViewModel viewModel)
        {
            var transportInfo = ServiceControlCoreTransports.Find("MSMQ");

            viewModel.SelectedTransport = transportInfo;

            return viewModel;
        }

        public static ServiceControlAddViewModel When_no_transport_is_selected(this ServiceControlAddViewModel viewModel)
        {
            return viewModel;
        }

    }

    class AddConnectionStringsTests
    {
        [Test]
        public void MSMQ_transport_is_selected()
        {
            var viewModel = Given_a_service_control_instance()
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
            var viewModel = Given_a_service_control_instance()
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
            var viewModel = Given_a_service_control_instance()
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