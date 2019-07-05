namespace ServiceControl.Config.UI.InstanceEdit
{
    using Extensions;
    using FluentValidation;
    using Validation;
    using Validations = Extensions.Validations;

    public class ServiceControlAuditEditViewModelValidator : AbstractValidator<ServiceControlAuditEditViewModel>
    {
        public ServiceControlAuditEditViewModelValidator()
        {
            RuleFor(x => x.ServiceControlAudit.ServiceAccount)
                .NotEmpty()
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.SelectedTransport)
                .NotEmpty();

            RuleFor(x => x.ServiceControlAudit.AuditForwarding)
                .NotNull().WithMessage(Validation.Validations.MSG_SELECTAUDITFORWARDING);

            RuleFor(x => x.ServiceControlAudit.AuditQueueName)
                .NotEmpty()
                .NotEqual(x => x.ServiceControlAudit.AuditForwardingQueueName).WithMessage(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding")
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.SelectedTransport, x.ServiceControlAudit.InstanceName, x.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .When(x => x.SubmitAttempted && x.ServiceControlAudit.AuditQueueName != "!disable");

            RuleFor(x => x.ServiceControlAudit.AuditForwardingQueueName)
                .NotEmpty()
                .NotEqual(x => x.ServiceControlAudit.AuditQueueName).WithMessage(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit")
                .MustNotBeIn(x => Validations.UsedAuditQueueNames(x.SelectedTransport, x.ServiceControlAudit.InstanceName, x.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.SelectedTransport, x.ServiceControlAudit.InstanceName, x.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .When(x => x.SubmitAttempted && x.ServiceControlAudit.AuditForwarding.Value);

            RuleFor(x => x.ConnectionString)
                .NotEmpty().WithMessage(Validation.Validations.MSG_THIS_TRANSPORT_REQUIRES_A_CONNECTION_STRING)
                .When(x => !string.IsNullOrWhiteSpace(x.SelectedTransport?.SampleConnectionString) && x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .NotEmpty()
                .ValidPort()
                .MustNotBeIn(x => Validations.UsedPorts(x.ServiceControlAudit.InstanceName))
                .NotEqual(x => x.ServiceControlAudit.PortNumber)
                .WithMessage(Validation.Validations.MSG_MUST_BE_UNIQUE, "Ports")
                .When(x => x.SubmitAttempted);
        }
    }
}