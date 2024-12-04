namespace ServiceControl.Config.Tests.AddInstance.AddAuditInstance
{
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceAdd;
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

            var viewModel = Given_a_service_control_instance()
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