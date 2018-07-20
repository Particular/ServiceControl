namespace ServiceControl.Config.UI.DeleteInstanceConfirmation
{
    using System.Windows.Input;
    using Framework;
    using Framework.Rx;

    class DeleteMonitoringConfirmationViewModel : RxScreen
    {
        public DeleteMonitoringConfirmationViewModel()
        {
            RemoveCommand = Command.Create(() => TryClose(true));
            CancelCommand = Command.Create(() => TryClose(false));
        }

        public string InstanceName { get; set; }

        public bool RemoveLogs { get; set; }

        public ICommand RemoveCommand { get; set; }
        public ICommand CancelCommand { get; set; }
    }
}