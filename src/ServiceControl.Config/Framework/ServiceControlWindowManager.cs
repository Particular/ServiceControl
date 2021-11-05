namespace ServiceControl.Config.Framework
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Windows;
    using Caliburn.Micro;
    using Extensions;
    using Rx;
    using ServiceControlInstaller.Engine.ReportCard;
    using UI.MessageBox;
    using UI.Shell;

    public interface IServiceControlWindowManager : IWindowManager
    {
        Task NavigateTo(RxScreen screen, object context = null, IDictionary<string, object> settings = null);

        Task<bool?> ShowInnerDialog(RxScreen screen, object context = null, IDictionary<string, object> settings = null);

        Task<bool?> ShowOverlayDialog(RxScreen screen, object context = null, IDictionary<string, object> settings = null);

        Task<bool> ShowMessage(string title, string message, string acceptText = "Ok", bool hideCancel = false);

        Task<bool?> ShowYesNoCancelDialog(string title, string message, string question, string yesText, string noText);

        Task<bool> ShowYesNoDialog(string title, string message, string question, string yesText, string noText);

        Task<bool> ShowSliderDialog(SliderDialogViewModel viewModel);

        Task<bool> ShowTextBoxDialog(TextBoxDialogViewModel viewModel);

        Task<bool> ShowActionReport(ReportCard reportcard, string title, string errorsMessage = "", string warningsMessage = "");

        void ScrollFirstErrorIntoView(object viewModel, object context = null);
    }

    class ServiceControlWindowManager : WindowManager, IServiceControlWindowManager
    {
        public ServiceControlWindowManager(Func<ReportCard, ReportCardViewModel> reportCardViewModelFactory)
        {
            this.reportCardViewModelFactory = reportCardViewModelFactory;
        }

        public Task NavigateTo(RxScreen screen, object context = null, IDictionary<string, object> settings = null)
        {
            var shell = GetShell();

            shell.ActiveContext = context;
            return shell.ActivateItem(screen);
        }

        public async Task<bool?> ShowInnerDialog(RxScreen screen, object context = null, IDictionary<string, object> settings = null)
        {
            var shell = GetShell();

            var previousContext = shell.ActiveContext;
            shell.IsModal = true;
            shell.ActiveContext = context;
            await shell.ActivateItem(screen);
            screen.RunModal();
            shell.IsModal = false;
            shell.ActiveContext = previousContext;

            if (screen is IModalResult modalResult)
            {
                return modalResult.Result;
            }

            return true;
        }

        public async Task<bool?> ShowOverlayDialog(RxScreen screen, object context = null, IDictionary<string, object> settings = null)
        {
            var shell = GetShell();

            var previousContext = shell.ActiveContext;
            shell.Overlay = screen;
            shell.ActiveContext = context;
            await ((IActivate)screen).ActivateAsync();
            screen.RunModal();
            shell.Overlay = null;
            shell.ActiveContext = previousContext;
            return screen.Result;
        }

        public async Task<bool> ShowMessage(string title, string message, string acceptText = "Ok", bool hideCancel = false)
        {
            var messageBox = new MessageBoxViewModel(title, message, acceptText, hideCancel);
            var result = await ShowOverlayDialog(messageBox);
            return result ?? false;
        }

        public Task<bool?> ShowYesNoCancelDialog(string title, string message, string question, string yesText, string noText)
        {
            var messageBox = new YesNoCancelViewModel(title, message, question, yesText, noText);
            return ShowOverlayDialog(messageBox);
        }

        public async Task<bool> ShowYesNoDialog(string title, string message, string question, string yesText, string noText)
        {
            var messageBox = new YesNoCancelViewModel(title, message, question, yesText, noText)
            {
                ShowCancelButton = false
            };
            var result = await ShowOverlayDialog(messageBox);
            return result.Value;
        }

        public async Task<bool> ShowSliderDialog(SliderDialogViewModel viewModel)
        {
            var result = await ShowOverlayDialog(viewModel);
            return result ?? false;
        }

        public async Task<bool> ShowTextBoxDialog(TextBoxDialogViewModel viewModel)
        {
            var result = await ShowOverlayDialog(viewModel);
            return result ?? false;
        }

        public async Task<bool> ShowActionReport(ReportCard reportcard, string title, string errorsMessage = "", string warningsMessage = "")
        {
            var messageBox = reportCardViewModelFactory(reportcard);
            messageBox.Title = title;
            messageBox.ErrorsMessage = errorsMessage;
            messageBox.WarningsMessage = warningsMessage;
            var result = await ShowOverlayDialog(messageBox);
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