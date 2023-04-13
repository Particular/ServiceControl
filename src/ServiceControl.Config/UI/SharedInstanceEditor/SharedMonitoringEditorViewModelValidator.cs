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
                .ValidHostName()
                    .WithMessage("Monitoring Hostname can only contain letters, numbers, dashes, or periods.")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.LogPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Extensions.Validations.UsedPaths(x.InstanceName))
                    .WithMessage(string.Format(Validations.MSG_MUST_BE_UNIQUE, "Paths"))
                .When(x => x.SubmitAttempted);
        }
    }
}