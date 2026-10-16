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

        public string StorageDisplayName { get; set; }

        // False for storage backends with nothing local to clean up (e.g. an external SQL Server/PostgreSQL database).
        public bool HasCleanableStoragePaths { get; set; } = true;

        public bool RemoveDatabase { get; set; }
        public bool RemoveLogs { get; set; }

        public ICommand RemoveCommand { get; set; }
        public ICommand CancelCommand { get; set; }
    }
}