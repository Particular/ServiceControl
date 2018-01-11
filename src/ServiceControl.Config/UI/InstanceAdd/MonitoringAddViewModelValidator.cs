namespace ServiceControl.Config.UI.InstanceAdd
{
    using FluentValidation;
    using Validation;

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
               .MustNotBeIn(x => UsedPorts(x.InstanceName))
               .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Ports")
               .When(x => x.SubmitAttempted);

            RuleFor(x => x.DestinationPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => UsedPaths(x.InstanceName))
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Paths")
                .When(x => x.SubmitAttempted);
            
            RuleFor(x => x.ErrorQueueName)
                .NotEmpty();

            RuleFor(x => x.ConnectionString)
                .NotEmpty().WithMessage(Validations.MSG_THIS_TRANSPORT_REQUIRES_A_CONNECTION_STRING)
                .When(x => (x.SelectedTransport?.ConnectionStringRequired).GetValueOrDefault(false) && x.SubmitAttempted);
        }
    }
}