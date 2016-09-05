using System.Windows.Input;
using Caliburn.Micro;
using ServiceControl.Config.Framework;
using ServiceControl.Config.Framework.Rx;

namespace ServiceControl.Config.UI.MessageBox
{
    class MessageBoxViewModel : RxScreen
    {
        public MessageBoxViewModel(string title, string message, string acceptText, bool hideCancel)
        {
            Title = title;
            Message = message;
            AcceptText = acceptText;

            Ok = Command.Create(() => { Result = true; ((IDeactivate)this).Deactivate(true); });
            Cancel = Command.Create(() => { Result = false; ((IDeactivate)this).Deactivate(true); });
            HideCancel = hideCancel;
        }

        public bool HideCancel { get; private set; }

        public string Title { get; private set; }

        public string Message { get; private set; }

        public string AcceptText { get; private set; }

        public ICommand Ok { get; private set; }
        public ICommand Cancel { get; private set; }
    }
}