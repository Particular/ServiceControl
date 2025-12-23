namespace ServiceControl.Config.UI.InstanceEdit
{
    using FluentValidation;
    using Validation;
    using Validations = Extensions.Validations;

    public class ServiceControlEditViewModelValidator : AbstractValidator<ServiceControlEditViewModel>
    {
        public ServiceControlEditViewModelValidator()
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
                    .WithMessage(string.Format(Validation.Validations.MSG_USE_PORTS_IN_RANGE, "ServiceControl Port"))
                .ErrorInstancePortAvailable(x => x.InstanceName) //across windows
                    .WithMessage(string.Format(Validation.Validations.MSG_PORT_IN_USE, "ServiceControl Port"))
                .MustNotBeIn(x => Validations.UsedPorts(x.InstanceName)) //across all instances
                    .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "ServiceControl Port"))
                .NotEqual(x => x.DatabaseMaintenancePortNumber)
                    .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "ServiceControl",
                    "Database Maintenance"))
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.DatabaseMaintenancePortNumber)
                .NotEmpty()
                .ValidPort()
                    .WithMessage(string.Format(Validation.Validations.MSG_USE_PORTS_IN_RANGE,
                        "ServiceControl Database Maintenance Port"))
                .ErrorInstanceDatabaseMaintenancePortAvailable(x => x.InstanceName) //across windows
                    .WithMessage(string.Format(Validation.Validations.MSG_PORT_IN_USE,
                        "ServiceControl Database Maintenance Port"))
                .MustNotBeIn(x => Validations.UsedPorts(x.ServiceControl.InstanceName)) //across all instances
                    .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE,
                        "ServiceControl Database Maintenance Port"))
                .NotEqual(x => x.PortNumber)
                    .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "Database Maintenance",
                        "ServiceControl"))
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
                .NotEmpty().WithMessage(Validation.Validations.MSG_THIS_TRANSPORT_REQUIRES_A_CONNECTION_STRING)
                .When(x => !string.IsNullOrWhiteSpace(x.SelectedTransport?.SampleConnectionString) && x.SubmitAttempted);

            RuleFor(x => x.ErrorForwarding)
                .NotNull()
                    .WithMessage(Validation.Validations.MSG_SELECTERRORFORWARDING);

            RuleFor(x => x.ErrorQueueName)
                .NotEmpty()
                .NotEqual(x => x.ErrorForwardingQueueName)
                    .WithMessage(string.Format(Validation.Validations.MSG_QUEUENAMES_NOT_EQUAL, "Error", "Error Forwarding "))
                .MustNotBeIn(x =>
                    Validations.UsedAuditQueueNames(x.SelectedTransport, x.InstanceName, x.ConnectionString))
                    .WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Error"))
                .MustNotBeIn(x =>
                    Validations.UsedErrorQueueNames(x.SelectedTransport, x.InstanceName, x.ConnectionString))
                    .WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Error"))
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ErrorForwardingQueueName)
                .NotEmpty()
                .NotEqual(x => x.ErrorQueueName)
                    .WithMessage(string.Format(Validation.Validations.MSG_QUEUENAMES_NOT_EQUAL, "Error Forwarding", "Error"))
                .MustNotBeIn(x => Validations.UsedAuditQueueNames(x.SelectedTransport, x.InstanceName, x.ConnectionString))
                    .WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Error Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.SelectedTransport, x.InstanceName, x.ConnectionString))
                    .WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Error Forwarding"))
                .When(x => x.ServiceControl.ErrorForwarding.Value && x.SubmitAttempted);
        }
    }
}