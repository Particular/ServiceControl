namespace ServiceControl.Config.UI.DeleteInstanceConfirmation
{
    using System.Windows.Input;
    using Framework;
    using Framework.Rx;

    class DeleteServiceControlConfirmationViewModel : RxScreen
    {
        public DeleteServiceControlConfirmationViewModel()
        {
            RemoveCommand = Command.Create(async () => await TryCloseAsync(true));
            CancelCommand = Command.Create(async () => await TryCloseAsync(false));
        }

        public string InstanceName { get; set; }

        public bool RemoveDatabase { get; set; }
        public bool RemoveLogs { get; set; }

        public ICommand RemoveCommand { get; set; }
        public ICommand CancelCommand { get; set; }
    }
}