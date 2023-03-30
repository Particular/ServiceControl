namespace ServiceControl.Config.UI.InstanceAdd
{
    using FluentValidation;
    using Validation;
    using Validations = Extensions.Validations;

    public class ServiceControlInformationValidator : AbstractValidator<ServiceControlInformation>
    {
        public ServiceControlInformationValidator()
        {
            RuleFor(viewModel => viewModel.InstanceName)
                .NotEmpty()
                .When(viewModel => viewModel.ViewModelParent.InstallErrorInstance);                

            RuleFor(x => x.ServiceAccount)
                .NotEmpty()
                .When(x => x.ViewModelParent.InstallErrorInstance);

            RuleFor(x => x.PortNumber)
                .NotEmpty()
                .ValidPort()
                .WithMessage(string.Format(Validation.Validations.MSG_USE_PORTS_IN_RANGE, "ServiceControl Port"))
                .PortAvailable() //across windows
                .WithMessage(string.Format(Validation.Validations.MSG_PORT_IN_USE, "ServiceControl Port"))
                .MustNotBeIn(x => Validations.UsedPorts(x.InstanceName)) //across all instances
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "ServiceControl Port"))
                .NotEqual(x => x.DatabaseMaintenancePortNumber)
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.PortNumber)
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "ServiceControl Port"))
                .When(x => x.ViewModelParent.InstallErrorInstance);

            RuleFor(x => x.DatabaseMaintenancePortNumber)
                .NotEmpty()
                .ValidPort()
                .WithMessage(string.Format(Validation.Validations.MSG_USE_PORTS_IN_RANGE, "ServiceControl Database Maintenance Port"))
                .PortAvailable() //across windows
                .WithMessage(string.Format(Validation.Validations.MSG_PORT_IN_USE, "ServiceControl Database Maintenance Port"))
                .MustNotBeIn(x => Validations.UsedPorts(x.InstanceName)) //across all instances
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "ServiceControl Database Maintenance Port"))
                .NotEqual(x => x.PortNumber)
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.PortNumber)
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "ServiceControl Database Maintenance Port"))
                .When(x => x.ViewModelParent.InstallErrorInstance);

            RuleFor(x => x.DestinationPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Destination Paths"))
                .When(x => x.ViewModelParent.InstallErrorInstance);

            RuleFor(x => x.DestinationPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Destination Paths"))
                .When(x => x.ViewModelParent.InstallErrorInstance);

            RuleFor(x => x.LogPath)
                .NotEmpty()
                .ValidPath()
                .When(x => x.ViewModelParent.InstallErrorInstance);

            RuleFor(x => x.DatabasePath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Database Paths"))
                .When(x => x.ViewModelParent.InstallErrorInstance);

            RuleFor(x => x.ErrorForwarding)
                .NotNull().WithMessage(Validation.Validations.MSG_SELECTERRORFORWARDING)
                .When(x => x.ViewModelParent.InstallErrorInstance);


            RuleFor(x => x.ErrorQueueName)
                .NotEmpty()
                .NotEqual(x => x.ErrorForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_DISTINCT_QUEUENAME, "Error","Error Forwarding"))
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.AuditQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit"))
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.AuditForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.ViewModelParent.SelectedTransport, x.InstanceName, x.ViewModelParent.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .MustNotBeIn(x => Validations.UsedAuditQueueNames(x.ViewModelParent.SelectedTransport, x.InstanceName, x.ViewModelParent.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .When(x => x.ViewModelParent.InstallErrorInstance)
                .When(x => x.ErrorQueueName != "!disable");

            RuleFor(x => x.ErrorForwardingQueueName)
                .NotEmpty()
                .NotEqual(x => x.ErrorQueueName).WithMessage(string.Format(Validation.Validations.MSG_DISTINCT_QUEUENAME, "Error Forwarding","Error"))
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.AuditQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit"))
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.AuditForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.ViewModelParent.SelectedTransport, x.InstanceName, x.ViewModelParent.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .MustNotBeIn(x => Validations.UsedAuditQueueNames(x.ViewModelParent.SelectedTransport, x.InstanceName, x.ViewModelParent.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .When(x => x.ViewModelParent.InstallErrorInstance)
                .When(x => x.ErrorForwarding.Value);           
        }       
    }
}