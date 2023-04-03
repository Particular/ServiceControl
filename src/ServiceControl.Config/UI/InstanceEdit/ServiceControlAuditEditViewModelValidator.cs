namespace ServiceControl.Config.UI.InstanceEdit
{
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
                .NotEmpty().WithMessage(string.Format(Validation.Validations.MSG_QUEUENAME, "Audit"))
                .NotEqual(x => x.ServiceControlAudit.AuditForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.SelectedTransport, x.ServiceControlAudit.InstanceName, x.ConnectionString)).WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Audit"))
                .MustNotBeIn(x => Validations.UsedAuditQueueNames(x.SelectedTransport, x.ServiceControlAudit.InstanceName, x.ConnectionString)).WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Audit"))
                .When(x => x.SubmitAttempted && x.ServiceControlAudit.AuditQueueName != "!disable");

            RuleFor(x => x.ServiceControlAudit.AuditForwardingQueueName)
                .NotEmpty().WithMessage(string.Format(Validation.Validations.MSG_FORWARDINGQUEUENAME, "Audit Forwarding"))
                .NotEqual(x => x.ServiceControlAudit.AuditQueueName).WithMessage(string.Format(Validation.Validations.MSG_QUEUENAMES_NOT_EQUAL, "Audit Forwarding", "Audit"))
                .MustNotBeIn(x => Validations.UsedAuditQueueNames(x.SelectedTransport, x.ServiceControlAudit.InstanceName, x.ConnectionString)).WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Audit Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.SelectedTransport, x.ServiceControlAudit.InstanceName, x.ConnectionString)).WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Audit Forwarding"))
                .When(x => x.SubmitAttempted && x.ServiceControlAudit.AuditForwarding.Value);

            RuleFor(x => x.ConnectionString)
                .NotEmpty().WithMessage(Validation.Validations.MSG_THIS_TRANSPORT_REQUIRES_A_CONNECTION_STRING)
                .When(x => !string.IsNullOrWhiteSpace(x.SelectedTransport?.SampleConnectionString) && x.SubmitAttempted);

            RuleFor(x => x.ServiceControl.PortNumber)
                .NotEmpty()
                .ValidPort().WithMessage(string.Format(Validation.Validations.MSG_USE_PORTS_IN_RANGE, "Audit Port"))
                .PortAvailable().WithMessage(string.Format(Validation.Validations.MSG_PORT_IN_USE, "Audit Port"))
                .MustNotBeIn(x => Validations.UsedPorts(x.ServiceControlAudit.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Audit Port"))
                .NotEqual(x => x.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "Audit", "Database Maintenance"))
                .When(x => x.SubmitAttempted);


            RuleFor(x => x.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .NotEmpty()
                .ValidPort().WithMessage(string.Format(Validation.Validations.MSG_USE_PORTS_IN_RANGE, "Audit Database Maintenance Port"))
                .PortAvailable().WithMessage(string.Format(Validation.Validations.MSG_PORT_IN_USE, "Audit Database Maintenance Port"))
                .MustNotBeIn(x => Validations.UsedPorts(x.ServiceControlAudit.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Audit Database Maintenance Port"))
                .NotEqual(x => x.ServiceControlAudit.PortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "Database Maintenance", "Audit"))
                .When(x => x.SubmitAttempted);



          
        }
    }
}