namespace ServiceControl.Config.UI.InstanceAdd
{
    using FluentValidation;
    using Validation;
    using Validations = Extensions.Validations;

    public class MonitoringAddViewModelValidator : SharedMonitoringEditorViewModelValidator<MonitoringAddViewModel>
    {
        public MonitoringAddViewModelValidator()
        {
            RuleFor(x => x.ServiceAccount)
                .NotEmpty()
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.SelectedTransport)
                .NotEmpty();

            RuleFor(x => x.PortNumber)
                .NotEmpty()
                .ValidPort()
                .PortAvailable()
                .MustNotBeIn(x => Validations.UsedPorts(x.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Monitoring Port"))
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.DestinationPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Destination Path"))
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ErrorQueueName)
                .NotEmpty();

            RuleFor(x => x.ConnectionString)
                .NotEmpty().WithMessage(Validation.Validations.MSG_THIS_TRANSPORT_REQUIRES_A_CONNECTION_STRING)
                .When(x => !string.IsNullOrWhiteSpace(x.SelectedTransport?.SampleConnectionString) && x.SubmitAttempted);
        }
    }
}