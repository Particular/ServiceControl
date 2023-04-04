namespace ServiceControl.Config.UI.InstanceAdd
{
    using FluentValidation;
    using Validation;
    using Validations = Extensions.Validations;

    public class ServiceControlAuditInformationValidator : AbstractValidator<ServiceControlAuditInformation>
    {
        public ServiceControlAuditInformationValidator()
        {
            RuleFor(viewModel => viewModel.InstanceName)
               .NotEmpty()
               .When(viewModel => viewModel.ViewModelParent.InstallAuditInstance);

            RuleFor(x => x.ServiceAccount)
                .NotEmpty()
                .When(x => x.ViewModelParent.InstallAuditInstance);

            RuleFor(x => x.PortNumber)
                .NotEmpty()
                .ValidPort()
                .WithMessage(string.Format(Validation.Validations.MSG_USE_PORTS_IN_RANGE, "Audit Port"))
                .PortAvailable()
                .WithMessage(string.Format(Validation.Validations.MSG_PORT_IN_USE, "Audit Port"))
                .MustNotBeIn(x => Validations.UsedPorts(x.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Audit Port"))
                .NotEqual(x => x.DatabaseMaintenancePortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "Audit", "Database Maintenance"))
                .NotEqual(x => x.ViewModelParent.ServiceControl.PortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "Audit", "ServiceControl"))
                .NotEqual(x => x.ViewModelParent.ServiceControl.DatabaseMaintenancePortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "Audit", "ServiceControl Database Maintenance"))
                .When(x => x.ViewModelParent.InstallAuditInstance);

            RuleFor(x => x.DatabaseMaintenancePortNumber)
                .NotEmpty()
                .ValidPort()
                .WithMessage(string.Format(Validation.Validations.MSG_USE_PORTS_IN_RANGE, "Audit Database Maintenance Port"))
                .PortAvailable()
                .WithMessage(string.Format(Validation.Validations.MSG_PORT_IN_USE, "Audit Database Maintenance Port"))
                .MustNotBeIn(x => Validations.UsedPorts(x.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Audit Database Maintenance Port"))
                .NotEqual(x => x.PortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "Audit Database Maintenance", "Audit"))
                .NotEqual(x => x.ViewModelParent.ServiceControl.PortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "Audit Database Maintenance", "ServiceControl"))
                .NotEqual(x => x.ViewModelParent.ServiceControl.DatabaseMaintenancePortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "Audit Database Maintenance", "ServiceControl Database Maintenance"))
                .When(x => x.ViewModelParent.InstallAuditInstance);

            RuleFor(x => x.DestinationPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Destination Paths"))
                .When(x => x.ViewModelParent.InstallAuditInstance);

            RuleFor(x => x.LogPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Log Paths"))
                .When(x => x.ViewModelParent.InstallAuditInstance);

            RuleFor(x => x.DatabasePath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Database Paths"))
                .When(x => x.ViewModelParent.InstallAuditInstance);

            RuleFor(x => x.AuditForwarding)
                .NotNull().WithMessage(Validation.Validations.MSG_SELECTAUDITFORWARDING)
                .When(x => x.ViewModelParent.InstallAuditInstance);

            RuleFor(x => x.AuditQueueName)
                .NotEmpty()
                .NotEqual(x => x.AuditForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_QUEUENAMES_NOT_EQUAL, "Audit", "Audit Forwarding"))
                .NotEqual(x => x.ViewModelParent.ServiceControl.ErrorQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Error"))
                .NotEqual(x => x.ViewModelParent.ServiceControl.ErrorForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Error Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.ViewModelParent.SelectedTransport, x.ViewModelParent.ServiceControlAudit.InstanceName, x.ViewModelParent.ConnectionString)).WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Audit"))
                .MustNotBeIn(x => Validations.UsedAuditQueueNames(x.ViewModelParent.SelectedTransport, x.InstanceName, x.ViewModelParent.ConnectionString)).WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Audit"))
                .When(x => x.ViewModelParent.InstallAuditInstance)
                .When(x => x.AuditQueueName != "!disable");

            RuleFor(x => x.AuditForwardingQueueName)
                .NotEmpty()
                .NotEqual(x => x.AuditQueueName).WithMessage(string.Format(Validation.Validations.MSG_QUEUENAMES_NOT_EQUAL, "Audit Forwarding", "Audit"))
                .NotEqual(x => x.ViewModelParent.ServiceControl.ErrorQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Error"))
                .NotEqual(x => x.ViewModelParent.ServiceControl.ErrorForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Error Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.ViewModelParent.SelectedTransport, x.ViewModelParent.ServiceControl.InstanceName, x.ViewModelParent.ConnectionString)).WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Audit Forwarding"))
                .MustNotBeIn(x => Validations.UsedAuditQueueNames(x.ViewModelParent.SelectedTransport, x.ViewModelParent.ServiceControl.InstanceName, x.ViewModelParent.ConnectionString)).WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Audit Forwarding"))
                .When(x => x.ViewModelParent.InstallAuditInstance)
                .When(x => x.AuditForwarding?.Value ?? false);
        }
    }
}