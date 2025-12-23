namespace ServiceControl.Config.UI.SharedInstanceEditor
{
    using FluentValidation;
    using Validation;

    public class SharedMonitoringEditorViewModelValidator<T> : AbstractValidator<T> where T : SharedMonitoringEditorViewModel
    {
        protected SharedMonitoringEditorViewModelValidator()
        {
            RuleFor(x => x.HostName)
                .NotEmpty()
                .ValidHostname(); // Removed the .When(x => x.SubmitAttempted) condition

            RuleFor(x => x.LogPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Extensions.Validations.UsedPaths(x.InstanceName))
                    .WithMessage(string.Format(Validations.MSG_MUST_BE_UNIQUE, "Paths"))
                .When(x => x.SubmitAttempted);
        }
    }
}