namespace ServiceControl.Config.UI.InstanceEdit
{
    using Extensions;
    using FluentValidation;
    using ServiceControlInstaller.Engine.Instances;
    using Validation;

    public class ServiceControlEditViewModelValidator : AbstractValidator<ServiceControlEditViewModel>
    {
        public ServiceControlEditViewModelValidator()
        {
            var instances = InstanceFinder.ServiceControlInstances();

            RuleFor(x => x.ServiceControl.ServiceAccount)
                .NotEmpty()
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.SelectedTransport)
                .NotEmpty();

            RuleFor(x => x.ErrorForwarding)
                .NotNull().WithMessage(Validations.MSG_SELECTERRORFORWARDING);

            RuleFor(x => x.ErrorQueueName)
                .NotEmpty()
                .NotEqual(x => x.ErrorForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Error Forwarding")
                .MustNotBeIn(x => instances.UsedQueueNames(x.SelectedTransport, x.ServiceControl.InstanceName, x.ConnectionString)).WithMessage(Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .When(x => x.SubmitAttempted && x.ErrorQueueName != "!disable");

            RuleFor(x => x.ErrorForwardingQueueName)
                .NotEmpty()
                .NotEqual(x => x.ErrorQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Error")
                .MustNotBeIn(x => instances.UsedQueueNames(x.SelectedTransport, x.ServiceControl.InstanceName, x.ConnectionString)).WithMessage(Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .When(x => x.SubmitAttempted && x.ErrorForwarding.Value);

            RuleFor(x => x.ConnectionString)
                .NotEmpty().WithMessage(Validations.MSG_THIS_TRANSPORT_REQUIRES_A_CONNECTION_STRING)
                .When(x => !string.IsNullOrWhiteSpace(x.SelectedTransport?.SampleConnectionString) && x.SubmitAttempted);

            RuleFor(x => x.ServiceControl.DatabaseMaintenancePortNumber)
                .NotEmpty()
                .ValidPort()
                .MustNotBeIn(x => instances.UsedPorts(x.ServiceControl.InstanceName))
                .NotEqual(x => x.ServiceControl.PortNumber)
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Ports")
                .When(x => x.SubmitAttempted);
        }
    }
}