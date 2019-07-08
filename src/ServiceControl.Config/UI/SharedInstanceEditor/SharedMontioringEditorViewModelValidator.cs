namespace ServiceControl.Config.Validation
{
    using FluentValidation;
    using UI.SharedInstanceEditor;

    public class SharedMonitoringEditorViewModelValidator<T> : AbstractValidator<T> where T : SharedMonitoringEditorViewModel
    {
        protected SharedMonitoringEditorViewModelValidator()
        {
            RuleFor(x => x.InstanceName)
                .NotEmpty()
                .MustNotContainWhitespace()
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.HostName)
                .NotEmpty().When(x => x.SubmitAttempted);


            RuleFor(x => x.LogPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Extensions.Validations.UsedPaths(x.InstanceName))
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Paths")
                .When(x => x.SubmitAttempted);
        }
    }
}