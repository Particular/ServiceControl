namespace ServiceControl.Config.UI.MessageBox
{
    using System.Windows.Input;
    using Caliburn.Micro;
    using Framework;
    using Framework.Rx;

    class MessageBoxViewModel : RxScreen
    {
        public MessageBoxViewModel(string title, string message, string acceptText, bool hideCancel)
        {
            Title = title;
            Message = message;
            AcceptText = acceptText;

            Ok = Command.Create(async () =>
            {
                Result = true;
                await this.DeactivateAsync(true);
            });
            Cancel = Command.Create(async () =>
            {
                Result = false;
                await this.DeactivateAsync(true);
            });
            HideCancel = hideCancel;
        }

        public bool HideCancel { get; }

        public string Title { get; }

        public string Message { get; }

        public string AcceptText { get; }

        public ICommand Ok { get; }
        public ICommand Cancel { get; }
    }
}