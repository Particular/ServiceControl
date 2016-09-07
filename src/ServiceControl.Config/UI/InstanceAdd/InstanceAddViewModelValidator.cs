namespace ServiceControl.Config.UI.InstanceAdd
{
    using FluentValidation;
    using Validation;

    public class InstanceAddViewModelValidator : SharedInstanceEditorViewModelValidator<InstanceAddViewModel>
    {
        public InstanceAddViewModelValidator()
        {
            RuleFor(x => x.ServiceAccount)
                .NotEmpty()
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.SelectedTransport)
                .NotEmpty();

            RuleFor(x => x.DestinationPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => UsedPaths(x.InstanceName))
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Paths")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.DatabasePath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => UsedPaths(x.InstanceName))
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Paths")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.BodyStoragePath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => UsedPaths(x.InstanceName))
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Paths")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.IngestionCachePath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => UsedPaths(x.InstanceName))
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Paths")
                .When(x => x.SubmitAttempted);
        }
    }
}