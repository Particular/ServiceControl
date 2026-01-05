namespace ServiceControl.Config.UI.InstanceEdit
{
    using FluentValidation;
    using SharedInstanceEditor;
    using Validation;

    public class MonitoringEditViewModelValidator : SharedMonitoringEditorViewModelValidator<MonitoringEditViewModel>
    {
        public MonitoringEditViewModelValidator()
        {
            RuleFor(x => x.ServiceAccount)
                .NotEmpty()
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.PortNumber)
                .NotEmpty()
                .ValidPort()
                    .WithMessage(string.Format(Validations.MSG_USE_PORTS_IN_RANGE, "Monitoring Port"))
                .MonitoringInstancePortAvailable(x => x.InstanceName)
                    .WithMessage(string.Format(Validations.MSG_PORT_IN_USE, "Monitoring Port"))
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ErrorQueueName)
               .NotEmpty();

            RuleFor(x => x.SelectedTransport)
                .NotEmpty();

            RuleFor(x => x.ConnectionString)
                .NotEmpty()
                    .WithMessage(Validations.MSG_THIS_TRANSPORT_REQUIRES_A_CONNECTION_STRING)
                .When(x => !string.IsNullOrWhiteSpace(x.SelectedTransport?.SampleConnectionString) && x.SubmitAttempted);
        }
    }
}
