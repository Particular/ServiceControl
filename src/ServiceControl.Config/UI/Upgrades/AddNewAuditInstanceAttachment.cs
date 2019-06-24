namespace ServiceControl.Config.UI.Upgrades
{
    using Framework;
    using ReactiveUI;
    using Validation;

    public class AddNewAuditInstanceAttachment : Attachment<AddNewAuditInstanceViewModel>
    {
        IWindowManagerEx windowManager;

        public AddNewAuditInstanceAttachment(IWindowManagerEx windowManager)
        {
            this.windowManager = windowManager;
        }

        protected override void OnAttach()
        {
            var validationTemplate = new ValidationTemplate(viewModel);
            viewModel.ValidationTemplate = validationTemplate;

            viewModel.Cancel = Command.Create(() =>
            {
                viewModel.Result = null;
                viewModel.TryClose(false);
            });

            viewModel.Continue = new ReactiveCommand().DoAction(Continue);
        }

        void Continue(object arg)
        {
            viewModel.SubmitAttempted = true;

            if (!viewModel.ValidationTemplate.Validate())
            {
                viewModel.NotifyOfPropertyChange(string.Empty);
                viewModel.SubmitAttempted = false;
                windowManager.ScrollFirstErrorIntoView(viewModel);

                return;
            }

            viewModel.TryClose(true);
        }
    }
}