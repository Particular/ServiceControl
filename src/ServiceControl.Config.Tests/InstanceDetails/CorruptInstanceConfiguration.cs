namespace ServiceControl.Config.Tests.InstanceDetails
{
    using System;
    using System.IO;
    using System.ServiceProcess;
    using System.Threading;
    using System.Threading.Tasks;
    using Caliburn.Micro;
    using NUnit.Framework;
    using ServiceControl.Config.Events;
    using ServiceControl.Config.UI.InstanceDetails;
    using ServiceControl.Config.UI.ListInstances;
    using ServiceControlInstaller.Engine.Instances;
    using ServiceControlInstaller.Engine.Services;

    /// <summary>
    /// Executable specification for SCMU resilience to corrupt instance configuration files
    /// (bug https://github.com/Particular/ServiceControl/issues/2759).
    ///
    /// Organized as feature > rule > examples:
    /// - this outer class is the feature,
    /// - each nested fixture is one rule,
    /// - each test is one example, named with "The one where ..." language.
    ///
    /// The tests load real ServiceControl / ServiceControl.Audit instances from real
    /// (corrupt or valid) configuration files written to a temp install folder, substituting
    /// the Windows service through the existing IWindowsServiceController seam, and observe
    /// the same InstanceDetailsViewModel state the UI binds to.
    /// </summary>
    public class CorruptInstanceConfiguration
    {
        [TestFixture]
        public class Rule_1_Must_load_every_instance_even_when_its_configuration_file_is_corrupt : CorruptInstanceConfigurationFixture
        {
            [Test]
            public void The_one_where_the_error_instance_config_xml_is_corrupt_and_the_instance_still_loads_flagged_with_the_error()
            {
                WriteErrorInstanceConfig(CorruptXml);

                var instance = LoadErrorInstance();

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(instance.ConfigurationLoadError, Is.Not.Null.And.Not.Empty,
                        "A corrupt config must be reported as a configuration load error");
                    Assert.That(instance.InstanceName, Is.EqualTo(ServiceName),
                        "The instance name must fall back to the Windows service name when the config cannot be read");
                    Assert.That(instance.ReportCard.Errors, Is.Not.Empty);
                }
            }

            [Test]
            public void The_one_where_the_audit_instance_config_xml_is_corrupt_and_the_instance_still_loads_flagged_with_the_error()
            {
                WriteAuditInstanceConfig(CorruptXml);

                var instance = LoadAuditInstance();

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(instance.ConfigurationLoadError, Is.Not.Null.And.Not.Empty,
                        "A corrupt config must be reported as a configuration load error");
                    Assert.That(instance.InstanceName, Is.EqualTo(ServiceName),
                        "The instance name must fall back to the Windows service name when the config cannot be read");
                    Assert.That(instance.ReportCard.Errors, Is.Not.Empty);
                }
            }

            [Test]
            public void The_one_where_the_monitoring_instance_config_xml_is_corrupt_and_the_instance_still_loads_flagged_with_the_error()
            {
                WriteMonitoringInstanceConfig(CorruptXml);

                var instance = LoadMonitoringInstance();

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(instance.ConfigurationLoadError, Is.Not.Null.And.Not.Empty,
                        "A corrupt config must be reported as a configuration load error");
                    Assert.That(instance.InstanceName, Is.EqualTo(ServiceName),
                        "The instance name must fall back to the Windows service name when the config cannot be read");
                    Assert.That(instance.ReportCard.Errors, Is.Not.Empty);
                }
            }

            [Test]
            public void The_one_where_the_configuration_is_valid_and_no_error_is_flagged()
            {
                WriteErrorInstanceConfig(ValidErrorInstanceXml);

                var instance = LoadErrorInstance();

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(instance.ConfigurationLoadError, Is.Null.Or.Empty);
                    Assert.That(instance.TransportPackage, Is.Not.Null,
                        "A valid config must load fully, including the transport");
                }
            }
        }

        [TestFixture]
        public class Rule_2_Must_show_the_configuration_error_in_place_of_the_service_status : CorruptInstanceConfigurationFixture
        {
            [Test]
            public void The_one_where_the_status_reads_configuration_error_and_no_running_or_stopped_indicator_is_shown()
            {
                WriteErrorInstanceConfig(CorruptXml);

                var viewModel = DetailsFor(LoadErrorInstance());

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(viewModel.HasConfigurationError, Is.True);
                    Assert.That(viewModel.Status, Is.EqualTo("CONFIGURATION ERROR"));
                    Assert.That(viewModel.IsRunning, Is.False, "No running indicator for a corrupt instance");
                    Assert.That(viewModel.IsStopped, Is.False, "No stopped indicator for a corrupt instance");
                }
            }

            [Test]
            public void The_one_where_the_error_banner_explains_that_the_configuration_failed_to_load_and_names_the_file()
            {
                WriteErrorInstanceConfig(CorruptXml);

                var instance = LoadErrorInstance();
                var viewModel = DetailsFor(instance);

                Assert.That(viewModel.ConfigurationErrorMessage,
                    Does.Contain("Failed to load configuration file").And.Contain(instance.ConfigurationFilePath),
                    "The banner must point the operator at the file that needs fixing");
            }

            [Test]
            public void The_one_where_the_configuration_is_valid_and_the_windows_service_status_is_shown_as_usual()
            {
                WriteErrorInstanceConfig(ValidErrorInstanceXml);

                var viewModel = DetailsFor(LoadErrorInstance());

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(viewModel.HasConfigurationError, Is.False);
                    Assert.That(viewModel.Status, Is.EqualTo("STOPPED"));
                    Assert.That(viewModel.IsStopped, Is.True);
                }
            }
        }

        [TestFixture]
        public class Rule_3_Must_block_actions_that_require_a_valid_configuration_while_the_error_persists : CorruptInstanceConfigurationFixture
        {
            [Test]
            public void The_one_where_start_and_stop_are_not_allowed()
            {
                WriteErrorInstanceConfig(CorruptXml);

                var viewModel = DetailsFor(LoadErrorInstance());

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(viewModel.AllowStart, Is.False);
                    Assert.That(viewModel.AllowStop, Is.False);
                }
            }

            [Test]
            public void The_one_where_edit_and_advanced_options_are_hidden()
            {
                WriteErrorInstanceConfig(CorruptXml);

                var viewModel = DetailsFor(LoadErrorInstance());

                Assert.That(viewModel.AllowEdit, Is.False);
            }

            [Test]
            public void The_one_where_no_transport_or_persister_is_reported()
            {
                WriteErrorInstanceConfig(CorruptXml);

                var viewModel = DetailsFor(LoadErrorInstance());

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(viewModel.Transport, Is.Null);
                    Assert.That(viewModel.Persister, Is.Empty);
                }
            }

            [Test]
            public void The_one_where_the_configuration_is_valid_and_the_instance_can_be_edited_and_started_as_usual()
            {
                WriteErrorInstanceConfig(ValidErrorInstanceXml);

                var viewModel = DetailsFor(LoadErrorInstance());

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(viewModel.AllowEdit, Is.True);
                    Assert.That(viewModel.AllowStart, Is.True, "A stopped, healthy instance can be started");
                }
            }
        }

        [TestFixture]
        public class Rule_4_Should_recover_the_instance_once_the_configuration_file_is_fixed_and_the_list_is_refreshed : CorruptInstanceConfigurationFixture
        {
            [Test]
            public void The_one_where_the_config_file_is_fixed_on_disk_and_refresh_returns_the_instance_to_normal()
            {
                WriteErrorInstanceConfig(CorruptXml);
                var viewModel = DetailsFor(LoadErrorInstance());
                Assert.That(viewModel.HasConfigurationError, Is.True, "Precondition: the instance starts out corrupt");

                // The operator fixes the file, then SCMU refreshes and re-reads instances from disk
                WriteErrorInstanceConfig(ValidErrorInstanceXml);
                viewModel.UpdateServiceInstance(LoadErrorInstance());

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(viewModel.HasConfigurationError, Is.False);
                    Assert.That(viewModel.Status, Is.EqualTo("STOPPED"));
                    Assert.That(viewModel.AllowEdit, Is.True);
                }
            }

            [Test]
            public async Task The_one_where_the_config_file_becomes_corrupt_after_loading_and_the_next_refresh_flags_the_error()
            {
                WriteErrorInstanceConfig(ValidErrorInstanceXml);
                var instance = LoadErrorInstance();
                var viewModel = DetailsFor(instance);
                Assert.That(viewModel.HasConfigurationError, Is.False, "Precondition: the instance starts out healthy");

                // The file is corrupted while SCMU is running, then a refresh reloads it
                WriteErrorInstanceConfig(CorruptXml);
                await viewModel.HandleAsync(new PostRefreshInstances(), CancellationToken.None);

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(viewModel.HasConfigurationError, Is.True);
                    Assert.That(viewModel.Status, Is.EqualTo("CONFIGURATION ERROR"));
                    Assert.That(viewModel.ConfigurationErrorMessage, Does.Contain(instance.ConfigurationFilePath));
                }
            }

            [Test]
            public async Task The_one_where_the_fix_is_picked_up_through_the_deployed_instances_refresh_flow()
            {
                WriteErrorInstanceConfig(CorruptXml);
                var list = new ListInstancesViewModel(DetailsFor, () => [LoadErrorInstance()])
                {
                    EventAggregator = new EventAggregator()
                };
                Assert.That(list.HasConfigurationErrors, Is.True, "Precondition: the list starts out with a corrupt instance");

                // The operator fixes the file, then triggers the refresh the UI uses
                WriteErrorInstanceConfig(ValidErrorInstanceXml);
                await list.HandleAsync(new RefreshInstances(), CancellationToken.None);

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(list.HasConfigurationErrors, Is.False);
                    Assert.That(list.ConfigurationErrorMessage, Is.Null);
                }
            }

            [Test]
            public void The_one_where_a_refresh_tries_to_apply_data_from_a_differently_named_instance_and_is_rejected()
            {
                WriteErrorInstanceConfig(CorruptXml);
                var viewModel = DetailsFor(LoadErrorInstance());

                var differentInstance = LoadErrorInstance(serviceName: "Particular.ServiceControl.Other");

                Assert.That(() => viewModel.UpdateServiceInstance(differentInstance), Throws.ArgumentException);
            }

            [Test]
            public void The_one_where_a_refresh_tries_to_apply_data_from_an_instance_of_a_different_type_and_is_rejected()
            {
                WriteErrorInstanceConfig(CorruptXml);
                var viewModel = DetailsFor(LoadErrorInstance());

                WriteAuditInstanceConfig(CorruptXml);
                var differentTypeInstance = LoadAuditInstance(); // same service name, different instance type

                Assert.That(() => viewModel.UpdateServiceInstance(differentTypeInstance), Throws.ArgumentException);
            }
        }

        [TestFixture]
        public class Rule_5_Must_summarize_configuration_errors_above_the_instance_list : CorruptInstanceConfigurationFixture
        {
            [Test]
            public void The_one_where_a_single_instance_is_corrupt_and_the_banner_names_it()
            {
                WriteErrorInstanceConfig(CorruptXml);
                WriteAuditInstanceConfig(ValidAuditInstanceXml);

                var list = ListFor(LoadErrorInstance(), LoadAuditInstance("Particular.ServiceControl.Audit"));

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(list.HasConfigurationErrors, Is.True);
                    Assert.That(list.ConfigurationErrorMessage,
                        Is.EqualTo("Particular.ServiceControl instance cannot be loaded due to XML configuration error."));
                }
            }

            [Test]
            public void The_one_where_multiple_instances_are_corrupt_and_the_banner_lists_all_of_them()
            {
                WriteErrorInstanceConfig(CorruptXml);
                WriteAuditInstanceConfig(CorruptXml);

                var list = ListFor(LoadErrorInstance(), LoadAuditInstance("Particular.ServiceControl.Audit"));

                Assert.That(list.ConfigurationErrorMessage,
                    Is.EqualTo("Multiple instances (Particular.ServiceControl, Particular.ServiceControl.Audit) cannot be loaded due to XML configuration errors."));
            }

            [Test]
            public void The_one_where_all_configurations_are_valid_and_no_banner_is_shown()
            {
                WriteErrorInstanceConfig(ValidErrorInstanceXml);

                var list = ListFor(LoadErrorInstance());

                using (Assert.EnterMultipleScope())
                {
                    Assert.That(list.HasConfigurationErrors, Is.False);
                    Assert.That(list.ConfigurationErrorMessage, Is.Null);
                }
            }
        }
    }

    public abstract class CorruptInstanceConfigurationFixture
    {
        protected const string ServiceName = "Particular.ServiceControl";

        // A realistically corrupted file: truncated mid-element, as left behind by a failed edit
        protected const string CorruptXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <appSettings>
                <add key="ServiceControl/TransportType" value="LearningTransport"
            """;

        protected const string ValidErrorInstanceXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <appSettings>
                <add key="ServiceControl/TransportType" value="LearningTransport" />
              </appSettings>
            </configuration>
            """;

        protected const string ValidAuditInstanceXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <appSettings>
                <add key="ServiceControl.Audit/TransportType" value="LearningTransport" />
              </appSettings>
            </configuration>
            """;

        protected string InstallPath { get; private set; }

        [SetUp]
        public void CreateInstallPath()
        {
            InstallPath = Path.Combine(Path.GetTempPath(), "SCMUCorruptConfigSpec", Path.GetRandomFileName());
            Directory.CreateDirectory(InstallPath);
        }

        [TearDown]
        public void DeleteInstallPath() => Directory.Delete(InstallPath, recursive: true);

        protected void WriteErrorInstanceConfig(string contents) =>
            File.WriteAllText(Path.Combine(InstallPath, $"{Constants.ServiceControlExe}.config"), contents);

        protected void WriteAuditInstanceConfig(string contents) =>
            File.WriteAllText(Path.Combine(InstallPath, $"{Constants.ServiceControlAuditExe}.config"), contents);

        protected void WriteMonitoringInstanceConfig(string contents) =>
            File.WriteAllText(Path.Combine(InstallPath, $"{Constants.MonitoringExe}.config"), contents);

        protected ServiceControlInstance LoadErrorInstance(string serviceName = ServiceName) =>
            new(new FakeWindowsServiceController(Path.Combine(InstallPath, Constants.ServiceControlExe), serviceName));

        protected ServiceControlAuditInstance LoadAuditInstance(string serviceName = ServiceName) =>
            new(new FakeWindowsServiceController(Path.Combine(InstallPath, Constants.ServiceControlAuditExe), serviceName));

        protected MonitoringInstance LoadMonitoringInstance(string serviceName = ServiceName) =>
            new(new FakeWindowsServiceController(Path.Combine(InstallPath, Constants.MonitoringExe), serviceName));

        internal static InstanceDetailsViewModel DetailsFor(BaseService instance) =>
            new(instance, null, null, null, null, null, null, null, null);

        internal static ListInstancesViewModel ListFor(params BaseService[] instances) =>
            new(DetailsFor, () => instances);

        class FakeWindowsServiceController(string exePath, string serviceName) : IWindowsServiceController
        {
            public string ServiceName => serviceName;

            public string ExePath => exePath;

            public string Description { get; set; }

            public ServiceControllerStatus Status => ServiceControllerStatus.Stopped;

            public string Account => "LocalSystem";

            public string DisplayName => serviceName;

            public bool Exists() => true;

            public void Refresh()
            {
            }

            public void WaitForStatus(ServiceControllerStatus stopped, TimeSpan timeSpan) => throw new NotSupportedException();
            public void Start() => throw new NotSupportedException();
            public void Stop() => throw new NotSupportedException();
            public void SetStartupMode(string v) => throw new NotSupportedException();
            public void Delete() => throw new NotSupportedException();
            public void ChangeAccountDetails(string accountName, string serviceAccountPwd) => throw new NotSupportedException();
        }
    }
}
