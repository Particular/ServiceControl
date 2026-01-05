namespace ServiceControl.Config.UI.InstanceEdit
{
    using FluentValidation;
    using Validation;
    using Validations = Extensions.Validations;

    public class ServiceControlAuditEditViewModelValidator : AbstractValidator<ServiceControlAuditEditViewModel>
    {
        public ServiceControlAuditEditViewModelValidator()
        {
            RuleFor(x => x.ServiceAccount)
                .NotEmpty()
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.HostName)
                .NotEmpty()
                .ValidHostname()
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.PortNumber)
                .NotEmpty()
                .ValidPort()
                    .WithMessage(string.Format(Validation.Validations.MSG_USE_PORTS_IN_RANGE, "Audit Port"))
                .AuditInstancePortAvailable(x => x.InstanceName)
                    .WithMessage(string.Format(Validation.Validations.MSG_PORT_IN_USE, "Audit Port"))
                .MustNotBeIn(x => Validations.UsedPorts(x.InstanceName))
                    .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Audit Port"))
                .NotEqual(x => x.DatabaseMaintenancePortNumber)
                    .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "Audit", "Database Maintenance"))
                .When(x => x.SubmitAttempted);


            RuleFor(x => x.DatabaseMaintenancePortNumber)
                .NotEmpty()
                .ValidPort()
                    .WithMessage(string.Format(Validation.Validations.MSG_USE_PORTS_IN_RANGE, "Audit Database Maintenance Port"))
                .AuditInstanceDatabaseMaintenancePortAvailable(x => x.InstanceName)
                    .WithMessage(string.Format(Validation.Validations.MSG_PORT_IN_USE, "Audit Database Maintenance Port"))
                .MustNotBeIn(x => Validations.UsedPorts(x.InstanceName))
                    .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Audit Database Maintenance Port"))
                .NotEqual(x => x.PortNumber)
                    .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "Database Maintenance", "Audit"))
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.LogPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.InstanceName))
                    .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Log Paths"))
                .When(x => x.SubmitAttempted);


            RuleFor(x => x.SelectedTransport)
                .NotEmpty();


            RuleFor(x => x.ConnectionString)
                .NotEmpty()
                    .WithMessage(Validation.Validations.MSG_THIS_TRANSPORT_REQUIRES_A_CONNECTION_STRING)
                .When(x => !string.IsNullOrWhiteSpace(x.SelectedTransport?.SampleConnectionString) && x.SubmitAttempted);

            RuleFor(x => x.AuditForwarding)
                .NotNull()
                    .WithMessage(Validation.Validations.MSG_SELECTAUDITFORWARDING);

            RuleFor(x => x.AuditQueueName)
                .NotEmpty()
                    .WithMessage(string.Format(Validation.Validations.MSG_QUEUENAME, "Audit"))
                .NotEqual(x => x.AuditForwardingQueueName)
                    .WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.SelectedTransport, x.InstanceName, x.ConnectionString))
                    .WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Audit"))
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.AuditForwardingQueueName)
                .NotEmpty()
                    .WithMessage(string.Format(Validation.Validations.MSG_FORWARDINGQUEUENAME, "Audit Forwarding"))
                .NotEqual(x => x.AuditQueueName)
                    .WithMessage(string.Format(Validation.Validations.MSG_QUEUENAMES_NOT_EQUAL, "Audit Forwarding", "Audit"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.SelectedTransport, x.InstanceName, x.ConnectionString))
                    .WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Audit Forwarding"))
                .When(x => x.SubmitAttempted && x.AuditForwarding.Value);

        }
    }
}