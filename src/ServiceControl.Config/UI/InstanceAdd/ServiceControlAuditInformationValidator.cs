namespace ServiceControl.Config.UI.InstanceAdd
{
    using FluentValidation;
    using Validation;
    using Validations = Extensions.Validations;

    public class ServiceControlAuditInformationValidator : AbstractValidator<ServiceControlAuditInformation>
    {
        public ServiceControlAuditInformationValidator()
        {
            RuleFor(x => x.ServiceAccount)
                .NotEmpty()
                .Unless(x => !x.ViewModelParent.InstallAuditInstance);
            ;

            RuleFor(x => x.PortNumber)
                .NotEmpty()
                .ValidPort()
                .PortAvailable()
                .MustNotBeIn(x => Validations.UsedPorts(x.InstanceName))
                .NotEqual(x => x.DatabaseMaintenancePortNumber)
                .NotEqual(x => x.ViewModelParent.ServiceControl.PortNumber)
                .NotEqual(x => x.ViewModelParent.ServiceControl.DatabaseMaintenancePortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "ServiceControl Audit Instance Ports"))
                .Unless(x => !x.ViewModelParent.InstallAuditInstance);

            RuleFor(x => x.DatabaseMaintenancePortNumber)
                .NotEmpty()
                .ValidPort()
                .PortAvailable()
                .MustNotBeIn(x => Validations.UsedPorts(x.InstanceName))
                .NotEqual(x => x.PortNumber)
                .NotEqual(x => x.ViewModelParent.ServiceControl.PortNumber)
                .NotEqual(x => x.ViewModelParent.ServiceControl.DatabaseMaintenancePortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "ServiceControl Audit Instance Ports"))
                .Unless(x => !x.ViewModelParent.InstallAuditInstance);

            RuleFor(x => x.DestinationPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Destination Paths"))
                .Unless(x => !x.ViewModelParent.InstallAuditInstance);

            RuleFor(x => x.DatabasePath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Database Paths"))
                .Unless(x => !x.ViewModelParent.InstallAuditInstance);

            RuleFor(x => x.AuditForwarding)
                .NotNull().WithMessage(Validation.Validations.MSG_SELECTAUDITFORWARDING)
                .Unless(x => !x.ViewModelParent.InstallAuditInstance);

            RuleFor(x => x.AuditQueueName)
                .NotEmpty()
                .NotEqual(x => x.ViewModelParent.ServiceControl.ErrorQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Error"))
                .NotEqual(x => x.ViewModelParent.ServiceControl.ErrorForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Error Forwarding"))
                .NotEqual(x => x.AuditForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.ViewModelParent.SelectedTransport, x.ViewModelParent.ServiceControlAudit.InstanceName, x.ViewModelParent.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .Unless(x => !x.ViewModelParent.InstallAuditInstance)
                .When(x => x.AuditQueueName != "!disable");

            RuleFor(x => x.AuditForwardingQueueName)
                .NotEmpty()
                .NotEqual(x => x.ViewModelParent.ServiceControl.ErrorQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Error"))
                .NotEqual(x => x.AuditQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit"))
                .NotEqual(x => x.ViewModelParent.ServiceControl.ErrorForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Error Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.ViewModelParent.SelectedTransport, x.ViewModelParent.ServiceControl.InstanceName, x.ViewModelParent.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .MustNotBeIn(x => Validations.UsedAuditQueueNames(x.ViewModelParent.SelectedTransport, x.ViewModelParent.ServiceControl.InstanceName, x.ViewModelParent.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .Unless(x => !x.ViewModelParent.InstallAuditInstance)
                .When(x => x.AuditForwarding?.Value ?? false);
        }
    }
}