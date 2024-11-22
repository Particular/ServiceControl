namespace ServiceControl.Config.Tests.AddInstance.AddErrorInstance
{
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceAdd;
    using static AddingErrorForwardingQueueExtensions;

    public static class AddingErrorForwardingQueueExtensions
    {
        public static ServiceControlAddViewModel Given_a_service_control_instance()
        {
            var viewModel = new ServiceControlAddViewModel();

            return viewModel;
        }

        public static ServiceControlAddViewModel When_a_error_forwarding_is_on(this ServiceControlAddViewModel viewModel)
        {
            viewModel.ErrorForwarding = viewModel.ErrorForwardingOptions.Where(option => option.Name == "On").FirstOrDefault();

            return viewModel;
        }
        public static ServiceControlAddViewModel When_a_error_forwarding_is_off(this ServiceControlAddViewModel viewModel)
        {
            viewModel.ErrorForwarding = viewModel.ErrorForwardingOptions.Where(option => option.Name == "Off").FirstOrDefault();

            return viewModel;
        }
    }

    class AddErrorForwardingTests
    {
        [Test]
        public void Error_forwarding_is_turned_on()
        {
            var changedProperties = new List<string>();
            var viewModel = Given_a_service_control_instance()
                .Collect_changed_properties(changedProperties)
                .When_a_error_forwarding_is_on();

            nameof(viewModel.ErrorForwardingQueueName).Was_notified_of_change(changedProperties);
            Assert.Multiple(() =>
            {
                Assert.That(viewModel.ShowErrorForwardingQueue, Is.True);
                Assert.That(viewModel.ErrorForwardingWarning, Is.Not.Empty);
                Assert.That(viewModel.ErrorForwardingQueueName, Is.EqualTo("error.log"));
            });
        }

        [Test]
        public void Error_forwarding_is_turned_off()
        {
            var changedProperties = new List<string>();

            var viewModel = Given_a_service_control_instance()
                .Collect_changed_properties(changedProperties)
                .When_a_error_forwarding_is_off();

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.ShowErrorForwardingQueue, Is.False);
                Assert.That(viewModel.ErrorForwardingWarning, Is.Null);
                Assert.That(viewModel.ErrorForwardingQueueName, Is.Null);
            });
        }
    }
}