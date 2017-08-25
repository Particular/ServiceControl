namespace ServiceControl.Config.UI.AdvancedOptions
{
    using System.Windows.Input;
    using Caliburn.Micro;
    using ServiceControl.Config.Commands;
    using ServiceControl.Config.Events;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Rx;
    using ServiceControlInstaller.Engine.Instances;

    class MonitoringAdvancedViewModel : RxProgressScreen
    {
        public MonitoringAdvancedViewModel(BaseService instance, IEventAggregator eventAggregator, DeleteMonitoringlnstanceCommand deleteInstanceCommand)
        {
            MonitoringInstance = (MonitoringInstance) instance;
            DisplayName = "ADVANCED OPTIONS";

            DeleteCommand = deleteInstanceCommand;

            CopyToClipboard = new CopyToClipboardCommand();

            Cancel = Command.Create(() =>
            {
                TryClose(false);
                eventAggregator.PublishOnUIThread(new RefreshInstances());
            }, () => !InProgress);
        }

        public MonitoringInstance MonitoringInstance { get; }

        public ICommand DeleteCommand { get; set; }

        public ICommand Cancel { get; set; }


        public ICommand CopyToClipboard { get; private set; }

        public string Name => MonitoringInstance.Name;
    }
}