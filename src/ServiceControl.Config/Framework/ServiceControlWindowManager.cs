namespace ServiceControl.Config.Framework
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
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
        Task NavigateTo(RxScreen screen, object context = null, IDictionary<string, object> settings = null, CancellationToken cancellationToken = default);

        Task<bool?> ShowInnerDialog(RxScreen screen, object context = null, IDictionary<string, object> settings = null, CancellationToken cancellationToken = default);

        Task<bool?> ShowOverlayDialog(RxScreen screen, object context = null, IDictionary<string, object> settings = null, CancellationToken cancellationToken = default);

        Task<bool> ShowMessage(string title, string message, string acceptText = "Ok", bool hideCancel = false, CancellationToken cancellationToken = default);

        Task<bool?> ShowYesNoCancelDialog(string title, string message, string question, string yesText, string noText, CancellationToken cancellationToken = default);

        Task<bool> ShowYesNoDialog(string title, string message, string question, string yesText, string noText, CancellationToken cancellationToken = default);

        Task<bool> ShowSliderDialog(SliderDialogViewModel viewModel, CancellationToken cancellationToken = default);

        Task<bool> ShowTextBoxDialog(TextBoxDialogViewModel viewModel, CancellationToken cancellationToken = default);

        Task<bool> ShowActionReport(ReportCard reportcard, string title, string errorsMessage = "", string warningsMessage = "", CancellationToken cancellationToken = default);

        void ScrollFirstErrorIntoView(object viewModel, object context = null);
    }

    class ServiceControlWindowManager : WindowManager, IServiceControlWindowManager
    {
        public ServiceControlWindowManager(Func<ReportCard, ReportCardViewModel> reportCardViewModelFactory)
        {
            this.reportCardViewModelFactory = reportCardViewModelFactory;
        }

        public Task NavigateTo(RxScreen screen, object context = null, IDictionary<string, object> settings = null, CancellationToken cancellationToken = default)
        {
            var shell = GetShell();

            shell.ActiveContext = context;
            return shell.ActivateItem(screen, cancellationToken);
        }

        public async Task<bool?> ShowInnerDialog(RxScreen screen, object context = null, IDictionary<string, object> settings = null, CancellationToken cancellationToken = default)
        {
            var shell = GetShell();

            var previousContext = shell.ActiveContext;
            shell.IsModal = true;
            shell.ActiveContext = context;
            await shell.ActivateItem(screen, cancellationToken);
            screen.RunModal();
            shell.IsModal = false;
            shell.ActiveContext = previousContext;

            if (screen is IModalResult modalResult)
            {
                return modalResult.Result;
            }

            return true;
        }

        public async Task<bool?> ShowOverlayDialog(RxScreen screen, object context = null, IDictionary<string, object> settings = null, CancellationToken cancellationToken = default)
        {
            var shell = GetShell();

            var previousContext = shell.ActiveContext;
            shell.Overlay = screen;
            shell.ActiveContext = context;
            await ((IActivate)screen).ActivateAsync(cancellationToken);
            screen.RunModal();
            shell.Overlay = null;
            shell.ActiveContext = previousContext;
            return screen.Result;
        }

        public async Task<bool> ShowMessage(string title, string message, string acceptText = "Ok", bool hideCancel = false, CancellationToken cancellationToken = default)
        {
            var messageBox = new MessageBoxViewModel(title, message, acceptText, hideCancel);
            var result = await ShowOverlayDialog(messageBox, cancellationToken: cancellationToken);
            return result ?? false;
        }

        public Task<bool?> ShowYesNoCancelDialog(string title, string message, string question, string yesText, string noText, CancellationToken cancellationToken = default)
        {
            var messageBox = new YesNoCancelViewModel(title, message, question, yesText, noText);
            return ShowOverlayDialog(messageBox, cancellationToken: cancellationToken);
        }

        public async Task<bool> ShowYesNoDialog(string title, string message, string question, string yesText, string noText, CancellationToken cancellationToken = default)
        {
            var messageBox = new YesNoCancelViewModel(title, message, question, yesText, noText)
            {
                ShowCancelButton = false
            };
            var result = await ShowOverlayDialog(messageBox, cancellationToken: cancellationToken);
            return result.Value;
        }

        public async Task<bool> ShowSliderDialog(SliderDialogViewModel viewModel, CancellationToken cancellationToken = default)
        {
            var result = await ShowOverlayDialog(viewModel, cancellationToken: cancellationToken);
            return result ?? false;
        }

        public async Task<bool> ShowTextBoxDialog(TextBoxDialogViewModel viewModel, CancellationToken cancellationToken = default)
        {
            var result = await ShowOverlayDialog(viewModel, cancellationToken: cancellationToken);
            return result ?? false;
        }

        public async Task<bool> ShowActionReport(ReportCard reportcard, string title, string errorsMessage = "", string warningsMessage = "", CancellationToken cancellationToken = default)
        {
            var messageBox = reportCardViewModelFactory(reportcard);
            messageBox.Title = title;
            messageBox.ErrorsMessage = errorsMessage;
            messageBox.WarningsMessage = warningsMessage;
            var result = await ShowOverlayDialog(messageBox, cancellationToken: cancellationToken);
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
            if (Application.Current.MainWindow.DataContext is not ShellViewModel shell)
            {
                throw new Exception("Main window is not a shell.");
            }

            return shell;
        }

        readonly Func<ReportCard, ReportCardViewModel> reportCardViewModelFactory;
    }
}