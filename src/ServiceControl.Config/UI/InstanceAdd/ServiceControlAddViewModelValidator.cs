namespace ServiceControl.Config.UI.InstanceAdd
{
    using FluentValidation;

    public class ServiceControlAddViewModelValidator : AbstractValidator<ServiceControlAddViewModel>
    {
        public ServiceControlAddViewModelValidator()
        {
            RuleFor(x => x.ConventionName)
                .NotEmpty()
                .When(x => x.SubmitAttempted &&
                           !x.IsServiceControlAuditExpanded &&
                           !x.IsServiceControlExpanded);

            RuleFor(x => x.SelectedTransport)
                .NotEmpty();

            RuleFor(x => x.InstallAuditInstance)
                .Must(install => install)
                .When(x => !x.InstallErrorInstance)
                .WithMessage("Must select either an audit or an error instance.");

            RuleFor(x => x.InstallErrorInstance)
                .Must(install => install)
                .When(x => !x.InstallAuditInstance)
                .WithMessage("Must select either an audit or an error instance.");

            RuleFor(x => x.ConnectionString)
                .NotEmpty().WithMessage(Validation.Validations.MSG_THIS_TRANSPORT_REQUIRES_A_CONNECTION_STRING)
                .When(x => !string.IsNullOrWhiteSpace(x.SelectedTransport?.SampleConnectionString) && x.SubmitAttempted);
        }
    }
}