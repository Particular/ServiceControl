﻿namespace ServiceControl.Config.Tests.EditInstance.EditErrorInstance
{
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceEdit;
    using System.Collections.Generic;
    using System.Linq;
    using static EditingErrorForwardingQueueExtensions;

    public static class EditingErrorForwardingQueueExtensions
    {
        public static ServiceControlEditViewModel Given_a_service_control_instance()
        {
            var viewModel = new ServiceControlEditViewModel();

            return viewModel;
        }

        public static ServiceControlEditViewModel When_a_error_forwarding_is_on(this ServiceControlEditViewModel viewModel)
        {
            viewModel.ErrorForwarding = viewModel.ErrorForwardingOptions.Where(option => option.Name == "On").FirstOrDefault();

            return viewModel;
        }
        public static ServiceControlEditViewModel When_a_error_forwarding_is_off(this ServiceControlEditViewModel viewModel)
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
            Assert.IsTrue(viewModel.ShowErrorForwardingQueue);
            Assert.IsNotEmpty(viewModel.ErrorForwardingWarning);
            Assert.AreEqual("error.log", viewModel.ErrorForwardingQueueName);
        }

        [Test]
        public void Error_forwarding_is_turned_off()
        {
            var changedProperties = new List<string>();

            var viewModel = Given_a_service_control_instance()
                .Collect_changed_properties(changedProperties)
                .When_a_error_forwarding_is_off();

            Assert.IsFalse(viewModel.ShowErrorForwardingQueue);
            Assert.IsNull(viewModel.ErrorForwardingWarning);
            Assert.IsNull(viewModel.ErrorForwardingQueueName);
        }
    }
}
