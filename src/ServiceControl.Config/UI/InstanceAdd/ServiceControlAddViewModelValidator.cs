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

            RuleFor(x => x.ConnectionString)
                .NotEmpty().WithMessage(Validation.Validations.MSG_THIS_TRANSPORT_REQUIRES_A_CONNECTION_STRING)
                .When(x => !string.IsNullOrWhiteSpace(x.SelectedTransport?.SampleConnectionString) && x.SubmitAttempted);
        }
    }
}