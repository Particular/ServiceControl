namespace ServiceControl.Config.UI.MessageBox
{
    using FluentValidation;
    using ServiceControl.Config.Validation;

    public class FolderPickerDialogViewModelValidator :  AbstractValidator<FolderPickerDialogViewModel>
    {
        public FolderPickerDialogViewModelValidator()
        {
            RuleFor(x => x.Path)
                .ValidPath()
                .RootedPath()
                .When(x => x.SubmitAttempted && !x.ValidateFolderIsEmpty);

            RuleFor(x => x.Path)
                .ValidPath()
                .RootedPath()
                .EmptyFolderIfExists()
                .When(x => x.SubmitAttempted && x.ValidateFolderIsEmpty);
        }
    }
}
