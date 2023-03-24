namespace ServiceControl.Config.UI.InstanceAdd
{
    using FluentValidation;
    using Validation;
    using Validations = Extensions.Validations;

    public class ServiceControlInformationValidator : AbstractValidator<ServiceControlInformation>
    {
        public ServiceControlInformationValidator()
        {
            RuleFor(x => x.ServiceAccount)
                .NotEmpty()
                .Unless(x => !x.ViewModelParent.InstallErrorInstance);

            RuleFor(x => x.PortNumber)
                .NotEmpty()
                .ValidPort()
                .PortAvailable()
                .MustNotBeIn(x => Validations.UsedPorts(x.InstanceName))
                .NotEqual(x => x.DatabaseMaintenancePortNumber)
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.PortNumber)
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "ServiceControl Ports"))
                .Unless(x => !x.ViewModelParent.InstallErrorInstance);

            RuleFor(x => x.DatabaseMaintenancePortNumber)
                .NotEmpty()
                .ValidPort()
                .PortAvailable()
                .MustNotBeIn(x => Validations.UsedPorts(x.InstanceName))
                .NotEqual(x => x.PortNumber)
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.PortNumber)
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "ServiceControl Database Maintenance Ports"))
                .Unless(x => !x.ViewModelParent.InstallErrorInstance);

            RuleFor(x => x.DestinationPath)
                .NotEmpty().WithMessage("not empty")
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Destination Paths"))
                .Unless(x => !x.ViewModelParent.InstallErrorInstance);

            RuleFor(x => x.DatabasePath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Database Paths"))
                .Unless(x => !x.ViewModelParent.InstallErrorInstance);

            RuleFor(x => x.ErrorForwarding)
                .NotNull().WithMessage(Validation.Validations.MSG_SELECTERRORFORWARDING)
                .Unless(x => !x.ViewModelParent.InstallErrorInstance);


            RuleFor(x => x.ErrorQueueName)
                .NotEmpty()
                .NotEqual(x => x.ErrorForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Error Forwarding"))
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.AuditQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit"))
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.AuditForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.ViewModelParent.SelectedTransport, x.InstanceName, x.ViewModelParent.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .MustNotBeIn(x => Validations.UsedAuditQueueNames(x.ViewModelParent.SelectedTransport, x.InstanceName, x.ViewModelParent.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .Unless(x => !x.ViewModelParent.InstallErrorInstance)
                .When(x => x.ErrorQueueName != "!disable");

            RuleFor(x => x.ErrorForwardingQueueName)
                .NotEmpty()
                .NotEqual(x => x.ErrorQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Error"))
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.AuditQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit"))
                .NotEqual(x => x.ViewModelParent.ServiceControlAudit.AuditForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.ViewModelParent.SelectedTransport, x.InstanceName, x.ViewModelParent.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .MustNotBeIn(x => Validations.UsedAuditQueueNames(x.ViewModelParent.SelectedTransport, x.InstanceName, x.ViewModelParent.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .Unless(x => !x.ViewModelParent.InstallErrorInstance)
                .When(x => x.ErrorForwarding.Value);           
        }       
    }
}