namespace ServiceControl.Config.Tests.AddInstance.AddAuditInstance
{
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceAdd;
    using System.Collections.Generic;
    using System.Linq;
    using static AddingAuditForwardingQueueExtensions;

    public static class AddingAuditForwardingQueueExtensions
    {
        public static ServiceControlAddViewModel Given_a_service_control_instance()
        {
            var viewModel = new ServiceControlAddViewModel();

            return viewModel;
        }

        public static ServiceControlAddViewModel When_a_audit_forwarding_is_on(this ServiceControlAddViewModel viewModel)
        {
            viewModel.AuditForwarding = viewModel.AuditForwardingOptions.Where(option => option.Name == "On").FirstOrDefault();

            return viewModel;
        }
        public static ServiceControlAddViewModel When_a_audit_forwarding_is_off(this ServiceControlAddViewModel viewModel)
        {
            viewModel.AuditForwarding = viewModel.AuditForwardingOptions.Where(option => option.Name == "Off").FirstOrDefault();

            return viewModel;
        }
    }

    class AddAuditForwardingTests
    {
        [Test]
        public void AuditForwardingIsTurnedOn()
        {
            var changedProperties = new List<string>();
            var viewModel = Given_a_service_control_instance()
                .Collect_changed_properties(changedProperties)
                .When_a_audit_forwarding_is_on();

            nameof(viewModel.AuditForwardingQueueName).Was_notified_of_change(changedProperties);
            Assert.IsTrue(viewModel.ShowAuditForwardingQueue);
            Assert.IsNotEmpty(viewModel.AuditForwardingWarning);
            Assert.AreEqual("audit.log", viewModel.AuditForwardingQueueName);
        }

        [Test]
        public void AuditForwardingIsTurnedOFF()
        {
            var changedProperties = new List<string>();

            var viewModel = Given_a_service_control_instance()
                .Collect_changed_properties(changedProperties)
                .When_a_audit_forwarding_is_off();

            Assert.That(viewModel.ShowAuditForwardingQueue, Is.False);
            Assert.IsNull(viewModel.AuditForwardingWarning);
            Assert.IsNull(viewModel.AuditForwardingQueueName);
        }
    }
}
