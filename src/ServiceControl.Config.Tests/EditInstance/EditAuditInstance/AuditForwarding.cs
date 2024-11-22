namespace ServiceControl.Config.Tests.EditInstance.EditAuditInstance
{
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceEdit;
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
            Assert.Multiple(() =>
            {
                Assert.That(viewModel.ShowAuditForwardingQueue, Is.True);
                Assert.That(viewModel.AuditForwardingWarning, Is.Not.Empty);
                Assert.That(viewModel.AuditForwardingQueueName, Is.EqualTo("audit.log"));
            });
        }

        [Test]
        public void AuditForwardingIsTurnedOFF()
        {
            var changedProperties = new List<string>();

            var viewModel = Given_a_audit_service_control_instance()
                .Collect_changed_properties(changedProperties)
                .When_a_audit_forwarding_is_off();

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.ShowAuditForwardingQueue, Is.False);
                Assert.That(viewModel.AuditForwardingWarning, Is.Null);
                Assert.That(viewModel.AuditForwardingQueueName, Is.Null);
            });
        }
    }
}