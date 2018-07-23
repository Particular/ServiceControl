namespace ServiceControl.Config.Framework
{
    using System;
    using System.Collections.Generic;
    using System.Windows;
    using Caliburn.Micro;
    using Extensions;
    using Rx;
    using ServiceControlInstaller.Engine.ReportCard;
    using UI.MessageBox;
    using UI.Shell;

    public interface IWindowManagerEx : IWindowManager
    {
        void NavigateTo(RxScreen screen, object context = null, IDictionary<string, object> settings = null);

        bool? ShowInnerDialog(RxScreen screen, object context = null, IDictionary<string, object> settings = null);

        bool? ShowOverlayDialog(RxScreen screen, object context = null, IDictionary<string, object> settings = null);

        bool ShowMessage(string title, string message, string acceptText = "Ok", bool hideCancel = false);

        bool? ShowYesNoCancelDialog(string title, string message, string question, string yesText, string noText);

        bool ShowYesNoDialog(string title, string message, string question, string yesText, string noText);

        bool ShowSliderDialog(SliderDialogViewModel viewModel);

        bool ShowTextBoxDialog(TextBoxDialogViewModel viewModel);

        bool ShowActionReport(ReportCard reportcard, string title, string errorsMessage = "", string warningsMessage = "");

        void ScrollFirstErrorIntoView(object viewModel, object context = null);
    }

    class WindowManagerEx : WindowManager, IWindowManagerEx
    {
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

            if (screen is IModalResult modalResult)
            {
                return modalResult.Result;
            }

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

        public bool ShowMessage(string title, string message, string acceptText = "Ok", bool hideCancel = false)
        {
            var messageBox = new MessageBoxViewModel(title, message, acceptText, hideCancel);
            var result = ShowOverlayDialog(messageBox);
            return result ?? false;
        }

        public bool? ShowYesNoCancelDialog(string title, string message, string question, string yesText, string noText)
        {
            var messageBox = new YesNoCancelViewModel(title, message, question, yesText, noText);
            return ShowOverlayDialog(messageBox);
        }

        public bool ShowYesNoDialog(string title, string message, string question, string yesText, string noText)
        {
            var messageBox = new YesNoCancelViewModel(title, message, question, yesText, noText)
            {
                ShowCancelButton = false
            };
            return ShowOverlayDialog(messageBox).Value;
        }

        public bool ShowSliderDialog(SliderDialogViewModel viewModel)
        {
            var result = ShowOverlayDialog(viewModel);
            return result ?? false;
        }

        public bool ShowTextBoxDialog(TextBoxDialogViewModel viewModel)
        {
            var result = ShowOverlayDialog(viewModel);
            return result ?? false;
        }

        public bool ShowActionReport(ReportCard reportcard, string title, string errorsMessage = "", string warningsMessage = "")
        {
            var messageBox = reportCardViewModelFactory(reportcard);
            messageBox.Title = title;
            messageBox.ErrorsMessage = errorsMessage;
            messageBox.WarningsMessage = warningsMessage;
            var result = ShowOverlayDialog(messageBox);
            return result ?? false;
        }

        public void ScrollFirstErrorIntoView(object viewModel, object context = null)
        {
            var view = ViewLocator.LocateForModel(viewModel, null, context);
            var controlInError = view?.FindControlWithError();
            controlInError?.BringIntoView();
        }

        ShellViewModel GetShell()
        {
            if (!(Application.Current.MainWindow.DataContext is ShellViewModel shell))
            {
                throw new Exception("Main window is not a shell.");
            }

            return shell;
        }

        readonly Func<ReportCard, ReportCardViewModel> reportCardViewModelFactory;
    }
}