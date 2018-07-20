namespace ServiceControl.Config.UI.DeleteInstanceConfirmation
{
    using System.Windows.Input;
    using Framework;
    using Framework.Rx;

    class DeleteServiceControlConfirmationViewModel : RxScreen
    {
        public DeleteServiceControlConfirmationViewModel()
        {
            RemoveCommand = Command.Create(() => TryClose(true));
            CancelCommand = Command.Create(() => TryClose(false));
        }

        public string InstanceName { get; set; }

        public bool RemoveDatabase { get; set; }
        public bool RemoveLogs { get; set; }

        public ICommand RemoveCommand { get; set; }
        public ICommand CancelCommand { get; set; }
    }
}