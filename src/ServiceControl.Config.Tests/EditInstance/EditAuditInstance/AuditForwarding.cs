namespace ServiceControl.Config.Tests.EditInstance.EditAuditInstance
{
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceEdit;
    using System.Collections.Generic;
    using System.Linq;
    using static EditingAuditForwardingQueueExtensions;

    public static class EditingAuditForwardingQueueExtensions
    {
        public static ServiceControlAuditEditViewModel Given_a_audit_service_control_instance()
        {
            var viewModel = new ServiceControlAuditEditViewModel();

            return viewModel;
        }

        public static ServiceControlAuditEditViewModel When_a_audit_forwarding_is_on(this ServiceControlAuditEditViewModel viewModel)
        {
            viewModel.AuditForwarding = viewModel.AuditForwardingOptions.Where(option => option.Name == "On").FirstOrDefault();

            return viewModel;
        }
        public static ServiceControlAuditEditViewModel When_a_audit_forwarding_is_off(this ServiceControlAuditEditViewModel viewModel)
        {
            viewModel.AuditForwarding = viewModel.AuditForwardingOptions.Where(option => option.Name == "Off").FirstOrDefault();

            return viewModel;
        }
    }

    class EditAuditForwardingTests
    {
        [Test]
        public void AuditForwardingIsTurnedOn()
        {
            var changedProperties = new List<string>();
            var viewModel = Given_a_audit_service_control_instance()
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

            var viewModel = Given_a_audit_service_control_instance()
                .Collect_changed_properties(changedProperties)
                .When_a_audit_forwarding_is_off();

            Assert.That(viewModel.ShowAuditForwardingQueue, Is.False);
            Assert.IsNull(viewModel.AuditForwardingWarning);
            Assert.IsNull(viewModel.AuditForwardingQueueName);
        }
    }
}
