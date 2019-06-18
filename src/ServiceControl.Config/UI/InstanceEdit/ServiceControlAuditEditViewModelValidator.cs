namespace ServiceControl.Config.UI.InstanceEdit
{
    using Extensions;
    using FluentValidation;
    using ServiceControlInstaller.Engine.Instances;
    using Validation;

    public class ServiceControlAuditEditViewModelValidator : AbstractValidator<ServiceControlAuditEditViewModel>
    {
        public ServiceControlAuditEditViewModelValidator()
        {
            var instances = InstanceFinder.ServiceControlInstances();

            RuleFor(x => x.ServiceControlAudit.ServiceAccount)
                .NotEmpty()
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.SelectedTransport)
                .NotEmpty();

            RuleFor(x => x.AuditForwarding)
                .NotNull().WithMessage(Validations.MSG_SELECTAUDITFORWARDING);

            RuleFor(x => x.AuditQueueName)
                .NotEmpty()
                .NotEqual(x => x.AuditForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding")
                .MustNotBeIn(x => instances.UsedQueueNames(x.SelectedTransport, x.ServiceControlAudit.InstanceName, x.ConnectionString)).WithMessage(Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .When(x => x.SubmitAttempted && x.AuditQueueName != "!disable");

            RuleFor(x => x.AuditForwardingQueueName)
                .NotEmpty()
                .NotEqual(x => x.AuditQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit")
                .MustNotBeIn(x => instances.UsedQueueNames(x.SelectedTransport, x.ServiceControlAudit.InstanceName, x.ConnectionString)).WithMessage(Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .When(x => x.SubmitAttempted && x.AuditForwarding.Value);

            RuleFor(x => x.ConnectionString)
                .NotEmpty().WithMessage(Validations.MSG_THIS_TRANSPORT_REQUIRES_A_CONNECTION_STRING)
                .When(x => !string.IsNullOrWhiteSpace(x.SelectedTransport?.SampleConnectionString) && x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .NotEmpty()
                .ValidPort()
                .MustNotBeIn(x => instances.UsedPorts(x.ServiceControlAudit.InstanceName))
                .NotEqual(x => x.ServiceControlAudit.PortNumber)
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Ports")
                .When(x => x.DatabaseMaintenancePortNumberRequired && x.SubmitAttempted);
        }
    }
}