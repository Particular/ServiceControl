namespace ServiceControl.Config.UI.InstanceEdit
{
    using FluentValidation;
    using ServiceControl.Config.UI.InstanceAdd;
    using Validation;
    using Validations = Extensions.Validations;

    public class ServiceControlInformationEditValidator : AbstractValidator<ServiceControlInformation>
    {
        public ServiceControlInformationEditValidator()
        {
            RuleFor(viewModel => viewModel.InstanceName)
                .NotEmpty()
                .When(viewModel => !viewModel.ViewModelParent.InstallErrorInstance);                

            RuleFor(x => x.ServiceAccount)
                .NotEmpty()
                .When(x => !x.ViewModelParent.InstallErrorInstance);

            RuleFor(x => x.PortNumber)
                .NotEmpty()
                .ValidPort()
                .WithMessage(string.Format(Validation.Validations.MSG_USE_PORTS_IN_RANGE, "ServiceControl Port"))
                .PortAvailable() //across windows
                .WithMessage(string.Format(Validation.Validations.MSG_PORT_IN_USE, "ServiceControl Port"))
                .MustNotBeIn(x => Validations.UsedPorts(x.InstanceName)) //across all instances
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "ServiceControl Port"))
                .NotEqual(x => x.DatabaseMaintenancePortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "ServiceControl", "Database Maintenance"))
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.PortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "ServiceControl", "Audit"))
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "ServiceControl", "Audit Database Maintenance"))
                .When(x => !x.ViewModelParent.InstallErrorInstance);

            RuleFor(x => x.DatabaseMaintenancePortNumber)
                .NotEmpty()
                .ValidPort()
                .WithMessage(string.Format(Validation.Validations.MSG_USE_PORTS_IN_RANGE, "ServiceControl Database Maintenance Port"))
                .PortAvailable() //across windows
                .WithMessage(string.Format(Validation.Validations.MSG_PORT_IN_USE, "ServiceControl Database Maintenance Port"))
                .MustNotBeIn(x => Validations.UsedPorts(x.InstanceName)) //across all instances
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "ServiceControl Database Maintenance Port"))
                .NotEqual(x => x.PortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "Database Maintenance", "ServiceControl"))
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.PortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "Database Maintenance", "Audit"))
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_PORTS_NOT_EQUAL, "Database Maintenance", "Audit Database Maintenance"))
                .When(x => !x.ViewModelParent.InstallErrorInstance);

            RuleFor(x => x.DestinationPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Destination Paths"))
                .When(x => !x.ViewModelParent.InstallErrorInstance);

            RuleFor(x => x.DestinationPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Destination Paths"))
                .When(x => !x.ViewModelParent.InstallErrorInstance);

            RuleFor(x => x.LogPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Log Paths"))
                .When(x => !x.ViewModelParent.InstallErrorInstance);

            RuleFor(x => x.DatabasePath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Database Paths"))
                .When(x => !x.ViewModelParent.InstallErrorInstance);

            RuleFor(x => x.ErrorForwarding)
                .NotNull().WithMessage(Validation.Validations.MSG_SELECTERRORFORWARDING)
                .When(x => !x.ViewModelParent.InstallErrorInstance);


            RuleFor(x => x.ErrorQueueName)
                .NotEmpty()
                .NotEqual(x => x.ErrorForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_QUEUENAMES_NOT_EQUAL, "Error","Error Forwarding"))
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.AuditQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit"))
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.AuditForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.ViewModelParent.SelectedTransport, x.InstanceName, x.ViewModelParent.ConnectionString)).WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Error"))
                .MustNotBeIn(x => Validations.UsedAuditQueueNames(x.ViewModelParent.SelectedTransport, x.InstanceName, x.ViewModelParent.ConnectionString)).WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Error"))
                .When(x => !x.ViewModelParent.InstallErrorInstance)
                .When(x => x.ErrorQueueName != "!disable");

            RuleFor(x => x.ErrorForwardingQueueName)
                .NotEmpty()
                .NotEqual(x => x.ErrorQueueName).WithMessage(string.Format(Validation.Validations.MSG_QUEUENAMES_NOT_EQUAL, "Error Forwarding","Error"))
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.AuditQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit"))
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.AuditForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.ViewModelParent.SelectedTransport, x.InstanceName, x.ViewModelParent.ConnectionString)).WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Error Forwarding"))
                .MustNotBeIn(x => Validations.UsedAuditQueueNames(x.ViewModelParent.SelectedTransport, x.InstanceName, x.ViewModelParent.ConnectionString)).WithMessage(string.Format(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED, "Error Forwarding"))
                .When(x => !x.ViewModelParent.InstallErrorInstance)
                .When(x => x.ErrorForwarding.Value);           
        }       
    }
}