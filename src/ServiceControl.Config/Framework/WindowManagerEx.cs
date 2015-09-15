using System;
using System.Collections.Generic;
using System.Windows;
using Caliburn.Micro;
using ServiceControl.Config.Framework.Rx;
using ServiceControl.Config.UI.MessageBox;
using ServiceControl.Config.UI.Shell;

namespace ServiceControl.Config.Framework
{
    using ServiceControl.Config.Extensions;
    using ServiceControlInstaller.Engine.ReportCard;

    public interface IWindowManagerEx : IWindowManager
    {
        void NavigateTo(RxScreen screen, object context = null, IDictionary<string, object> settings = null);

        bool? ShowInnerDialog(RxScreen screen, object context = null, IDictionary<string, object> settings = null);

        bool? ShowOverlayDialog(RxScreen screen, object context = null, IDictionary<string, object> settings = null);

        bool ShowMessage(string title, string message, string acceptText = "Ok");

        bool ShowActionReport(ReportCard reportcard, string title, string errorsMessage, string warningsMessage);
    }

    class WindowManagerEx : WindowManager, IWindowManagerEx
    {
        readonly Func<ReportCard, ReportCardViewModel> reportCardViewModelFactory;

        public WindowManagerEx(Func<ReportCard, ReportCardViewModel> reportCardViewModelFactory)
        {
            this.reportCardViewModelFactory = reportCardViewModelFactory;
        }

        public void NavigateTo(RxScreen screen, object context = null, IDictionary<string, object> settings = null)
        {
            var shell = GetShell();

            shell.ActiveContext = context;
            shell.ActivateItem(screen);
        }

        public bool? ShowInnerDialog(RxScreen screen, object context = null, IDictionary<string, object> settings = null)
        {
            var shell = GetShell();

            var previousContext = shell.ActiveContext;
            shell.IsModal = true;
            shell.ActiveContext = context;
            shell.ActivateItem(screen);
            screen.RunModal();
            shell.IsModal = false;
            shell.ActiveContext = previousContext;

            var modalResult = screen as IModalResult;
            if (modalResult != null)
                return modalResult.Result;

            return true;
        }

        public bool? ShowOverlayDialog(RxScreen screen, object context = null, IDictionary<string, object> settings = null)
        {
            var shell = GetShell();

            var previousContext = shell.ActiveContext;
            shell.Overlay = screen;
            shell.ActiveContext = context;
            ((IActivate)screen).Activate();
            screen.RunModal();
            shell.Overlay = null;
            shell.ActiveContext = previousContext;

            return screen.Result;
        }

        public bool ShowMessage(string title, string message, string acceptText = "Ok")
        {
            var messageBox = new MessageBoxViewModel(title, message, acceptText);
            var result = ShowOverlayDialog(messageBox);

            return result ?? false;
        }

        public bool ShowActionReport(ReportCard reportcard, string title, string errorsMessage, string warningsMessage)
        {
            var messageBox = reportCardViewModelFactory(reportcard);
            messageBox.Title = title;
            messageBox.ErrorsMessage = errorsMessage;
            messageBox.WarningsMessage = warningsMessage;
            var result = ShowOverlayDialog(messageBox);

            return result ?? false;
        }

        private ShellViewModel GetShell()
        {
            var shell = Application.Current.MainWindow.DataContext as ShellViewModel;

            if (shell == null)
            {
                throw new Exception("Main window is not a shell.");
            }

            return shell;
        }
    }
}