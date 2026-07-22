namespace ServiceControl.Config.Tests.AddInstance
{
    using System.ComponentModel;
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceAdd;

    /// <summary>
    /// Executable specification for docs/specs/audit-instance-servicecontrol-queue-address.md
    /// (bug https://github.com/Particular/ServiceControl/issues/4753).
    ///
    /// Organized as feature > rule > examples:
    /// - this outer class is the feature,
    /// - each nested fixture is one rule from the spec,
    /// - each test is one example, named with the spec's "The one where ..." language.
    ///
    /// These tests are the OUTER loop of a double-loop TDD process. They observe the view
    /// model and its validator through INotifyDataErrorInfo - the same mechanism the UI
    /// uses to block Save - and reference members that do not exist yet:
    ///   GetInstalledErrorInstanceNames, ServiceControlQueueAddress,
    ///   ServiceControlQueueAddressOptions, ShowServiceControlQueueAddressSelection.
    /// </summary>
    public class AuditInstanceServiceControlQueueAddress
    {
        [TestFixture]
        public class Rule_1_Must_address_the_audit_instance_to_the_error_instance_installed_in_the_same_session
        {
            [Test]
            public void The_one_where_both_instances_are_installed_together_and_the_new_error_instance_name_is_used()
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
            public void The_one_where_other_error_instances_already_exist_yet_no_choice_is_offered_because_the_instance_being_installed_wins()
            {
                var viewModel = new ServiceControlAddViewModel
                {
                    InstallErrorInstance = true,
                    InstallAuditInstance = true,
                    GetInstalledErrorInstanceNames = () => new string[] { "Particular.ServiceControl", "Particular.ServiceControl.2" }
                };

                Assert.That(viewModel.ShowServiceControlQueueAddressSelection, Is.False);
            }
        }

        [TestFixture]
        public class Rule_2_Should_auto_detect_the_existing_error_instance_when_adding_an_audit_instance_alone
        {
            [Test]
            public void The_one_where_a_single_error_instance_exists_and_its_name_is_used_without_any_user_input()
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
        }

        [TestFixture]
        public class Rule_3_Must_require_an_explicit_choice_when_multiple_existing_error_instances_are_found
        {
            [Test]
            public void The_one_where_two_error_instances_exist_and_the_dropdown_offers_both()
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
            public void The_one_where_save_is_blocked_until_the_user_picks_one_of_the_detected_instances()
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
        }

        [TestFixture]
        public class Rule_4_Must_block_installation_when_no_error_instance_exists_to_connect_to
        {
            [Test]
            public void The_one_where_no_error_instance_exists_and_a_validation_error_prevents_the_installation_from_proceeding()
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
            public void The_one_where_only_an_error_instance_is_installed_and_the_queue_address_does_not_apply()
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
        }

        static INotifyDataErrorInfo GetNotifyErrorInfo(object vm) => vm as INotifyDataErrorInfo;
    }
}
