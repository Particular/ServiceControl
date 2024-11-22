namespace ServiceControl.Config.Tests.AddInstance.AddErrorInstance
{
    using NUnit.Framework;
    using ServiceControl.Config.UI.InstanceAdd;

    public class PathNotifyPropertyChangesTests
    {
        public ServiceControlAddViewModel Given_adding_error_instance()
        {
            var viewModel = new ServiceControlAddViewModel();

            return viewModel;
        }

        [Test]
        public void ChangesTo_ErrorSharedServiceControlEditorViewModel_DbPath_Notifies_ServiceControlAddViewModel_DbPath()
        {
            var viewModel = Given_adding_error_instance();

            var propertyNotified = false;
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(viewModel.ErrorDatabasePath))
                {
                    propertyNotified = true;
                }
            };

            viewModel.ServiceControl.DatabasePath = "NewValue";

            Assert.That(propertyNotified, Is.True, "Changes to DatabasePath did not notify ServiceControlAddViewModel");
        }

        [Test]
        public void ChangesTo_ErrorSharedServiceControlEditorViewModel_LogPath_Notifies_ServiceControlAddViewModel_LogPath()
        {
            var viewModel = Given_adding_error_instance();

            var propertyNotified = false;
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(viewModel.ErrorLogPath))
                {
                    propertyNotified = true;
                }
            };

            viewModel.ServiceControl.LogPath = "NewValue";

            Assert.That(propertyNotified, Is.True, "Changes to LogPath did not notify ServiceControlAddViewModel");
        }

        [Test]
        public void ChangesTo_ErrorSharedServiceControlEditorViewModel_DestinationPath_Notifies_ServiceControlAddViewModel_DestinationPath()
        {
            var viewModel = Given_adding_error_instance();

            var propertyNotified = false;
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(viewModel.ErrorDestinationPath))
                {
                    propertyNotified = true;
                }
            };

            viewModel.ServiceControl.DestinationPath = "NewValue";

            Assert.That(propertyNotified, Is.True, "Changes to DestinationPath did not notify ServiceControlAddViewModel");
        }

        [Test]
        public void ChangesTo_AuditSharedServiceControlEditorViewModel_DbPath_Notifies_ServiceControlAddViewModel_DbPath()
        {
            var viewModel = Given_adding_error_instance();

            var propertyNotified = false;
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(viewModel.AuditDatabasePath))
                {
                    propertyNotified = true;
                }
            };

            viewModel.ServiceControlAudit.DatabasePath = "NewValue";

            Assert.That(propertyNotified, Is.True, "Changes to DatabasePath did not notify ServiceControlAddViewModel");
        }

        [Test]
        public void ChangesTo_AuditSharedServiceControlEditorViewModel_LogPath_Notifies_ServiceControlAddViewModel_LogPath()
        {
            var viewModel = Given_adding_error_instance();

            var propertyNotified = false;
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(viewModel.AuditLogPath))
                {
                    propertyNotified = true;
                }
            };

            viewModel.ServiceControlAudit.LogPath = "NewValue";

            Assert.That(propertyNotified, Is.True, "Changes to LogPath did not notify ServiceControlAddViewModel");
        }

        [Test]
        public void ChangesTo_AuditSharedServiceControlEditorViewModel_DestinationPath_Notifies_ServiceControlAddViewModel_DestinationPath()
        {
            var viewModel = Given_adding_error_instance();

            var propertyNotified = false;
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(viewModel.AuditDestinationPath))
                {
                    propertyNotified = true;
                }
            };

            viewModel.ServiceControlAudit.DestinationPath = "NewValue";

            Assert.That(propertyNotified, Is.True, "Changes to DestinationPath did not notify ServiceControlAddViewModel");
        }
    }
}