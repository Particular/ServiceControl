namespace ServiceControl.Config.UI.InstanceEdit
{
    using FluentValidation;
    using Validation;

    public class MonitoringEditViewModelValidator : SharedMonitoringEditorViewModelValidator<MonitoringEditViewModel>
    {
        public MonitoringEditViewModelValidator()
        {
            RuleFor(x => x.ServiceAccount)
                .NotEmpty()
                .When(x => x.SubmitAttempted); 

            RuleFor(x => x.SelectedTransport)
                .NotEmpty();

            RuleFor(x => x.ConnectionString)
               .NotEmpty().WithMessage(Validations.MSG_THIS_TRANSPORT_REQUIRES_A_CONNECTION_STRING)
               .When(x => !string.IsNullOrWhiteSpace(x.SelectedTransport?.SampleConnectionString) && x.SubmitAttempted);
        }
    }
}