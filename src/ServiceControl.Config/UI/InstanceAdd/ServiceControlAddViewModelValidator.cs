namespace ServiceControl.Config.UI.InstanceAdd
{
    using FluentValidation;

    public class ServiceControlAddViewModelValidator : AbstractValidator<ServiceControlAddViewModel>
    {
        public bool Test(ServiceControlAddViewModel x) 
        {
            var result =  x.SubmitAttempted &&
                               !x.IsServiceControlAuditExpanded &&
                               !x.IsServiceControlExpanded;

            return result;
        }
       
        public ServiceControlAddViewModelValidator()
        {
            RuleFor(x => x.ConventionName)
                .NotEmpty()
                .When(Test);

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

            RuleFor(vm => vm.ErrorInstanceName)
                .NotEmpty()
                .When(vm => vm.InstallErrorInstance)
                .WithMessage("Error instance required");
        }
    }
}