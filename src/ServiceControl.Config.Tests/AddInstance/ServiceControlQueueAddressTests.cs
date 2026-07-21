namespace ServiceControl.Config.Tests.AddInstance
{
    using System.ComponentModel;
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceAdd;

    /// <summary>
    /// Spec for https://github.com/Particular/ServiceControl/issues/4753
    /// SCMU does not set ServiceControl.Audit/ServiceControlQueueAddress when only adding audit instances.
    ///
    /// Expected behavior:
    /// - Installing error + audit together: queue address is the name of the error instance being installed.
    /// - Installing audit only, one existing error instance on the machine: auto-detect and use its name.
    /// - Installing audit only, multiple existing error instances: user must pick one from a dropdown
    ///   (dropdown only visible in this case); Save is blocked until a selection is made.
    /// - Installing audit only, no existing error instance: Save is blocked by a validation error.
    /// </summary>
    public class ServiceControlQueueAddressTests
    {
        [Test]
        public void Queue_address_is_the_new_error_instance_name_when_error_instance_is_installed_together()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                InstallAuditInstance = true,
                SubmitAttempted = true,
                // No pre-existing error instances on the machine
                GetInstalledErrorInstanceNames = () => new string[0]
            };

            viewModel.ErrorInstanceName = "My.Error.Instance";

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ServiceControlQueueAddress));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.ServiceControlQueueAddress, Is.EqualTo("My.Error.Instance"));
                Assert.That(viewModel.ShowServiceControlQueueAddressSelection, Is.False);
                Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlQueueAddress)), Is.Empty);
            }
        }

        [Test]
        public void Queue_address_is_autodetected_when_adding_audit_only_and_a_single_error_instance_exists()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = false,
                InstallAuditInstance = true,
                SubmitAttempted = true,
                GetInstalledErrorInstanceNames = () => new string[] { "Particular.ServiceControl" }
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ServiceControlQueueAddress));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.ServiceControlQueueAddress, Is.EqualTo("Particular.ServiceControl"));
                Assert.That(viewModel.ShowServiceControlQueueAddressSelection, Is.False, "Dropdown must not show when there is only one existing error instance");
                Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlQueueAddress)), Is.Empty);
            }
        }

        [Test]
        public void Queue_address_dropdown_is_shown_only_when_adding_audit_only_and_multiple_error_instances_exist()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = false,
                InstallAuditInstance = true,
                GetInstalledErrorInstanceNames = () => new string[] { "Particular.ServiceControl", "Particular.ServiceControl.2" }
            };

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.ShowServiceControlQueueAddressSelection, Is.True);
                Assert.That(viewModel.ServiceControlQueueAddressOptions, Is.EquivalentTo(new[]
                {
                    "Particular.ServiceControl",
                    "Particular.ServiceControl.2"
                }));
            }
        }

        [Test]
        public void Queue_address_dropdown_is_not_shown_when_error_instance_is_installed_together_even_if_multiple_error_instances_exist()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                InstallAuditInstance = true,
                GetInstalledErrorInstanceNames = () => new string[] { "Particular.ServiceControl", "Particular.ServiceControl.2" }
            };

            Assert.That(viewModel.ShowServiceControlQueueAddressSelection, Is.False);
        }

        [Test]
        public void Save_is_blocked_when_adding_audit_only_and_multiple_error_instances_exist_until_one_is_selected()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = false,
                InstallAuditInstance = true,
                SubmitAttempted = true,
                GetInstalledErrorInstanceNames = () => new string[] { "Particular.ServiceControl", "Particular.ServiceControl.2" }
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ServiceControlQueueAddress));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            // No selection made yet: must be blocked
            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlQueueAddress)), Is.Not.Empty,
                "A validation error is expected until the user picks one of the existing error instances");

            // User picks an instance from the dropdown
            viewModel.ServiceControlQueueAddress = "Particular.ServiceControl.2";

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.ServiceControlQueueAddress, Is.EqualTo("Particular.ServiceControl.2"));
                Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlQueueAddress)), Is.Empty);
            }
        }

        [Test]
        public void Save_is_blocked_when_adding_audit_only_and_no_error_instance_exists()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = false,
                InstallAuditInstance = true,
                SubmitAttempted = true,
                GetInstalledErrorInstanceNames = () => new string[0]
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ServiceControlQueueAddress));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(viewModel.ServiceControlQueueAddress, Is.Null.Or.Empty);
                Assert.That(viewModel.ShowServiceControlQueueAddressSelection, Is.False);
                Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlQueueAddress)), Is.Not.Empty,
                    "A validation error is expected so the user cannot proceed without an existing error instance to connect to");
            }
        }

        [Test]
        public void No_queue_address_validation_error_when_only_installing_an_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel
            {
                InstallErrorInstance = true,
                InstallAuditInstance = false,
                SubmitAttempted = true,
                GetInstalledErrorInstanceNames = () => new string[0]
            };

            viewModel.NotifyOfPropertyChange(nameof(viewModel.ServiceControlQueueAddress));

            var notifyErrorInfo = GetNotifyErrorInfo(viewModel);

            Assert.That(notifyErrorInfo.GetErrors(nameof(viewModel.ServiceControlQueueAddress)), Is.Empty);
        }

        static INotifyDataErrorInfo GetNotifyErrorInfo(object vm) => vm as INotifyDataErrorInfo;
    }
}
