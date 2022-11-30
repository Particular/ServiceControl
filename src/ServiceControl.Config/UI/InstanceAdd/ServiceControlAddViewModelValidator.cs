namespace ServiceControl.Config.UI.InstanceAdd
{
    using FluentValidation;
    using Validation;
    using Validations = Extensions.Validations;

    public class ServiceControlAddViewModelValidator : AbstractValidator<ServiceControlAddViewModel>
    {
        public ServiceControlAddViewModelValidator()
        {
            RuleFor(x => x.ConventionName)
                .NotEmpty()
                .When(x => x.SubmitAttempted &&
                           !x.IsServiceControlAuditExpanded &&
                           !x.IsServiceControlExpanded);

            RuleFor(x => x.SelectedTransport)
                .NotEmpty();

            RuleFor(x => x.ServiceControl.ServiceAccount)
                .NotEmpty()
                .Unless(x => !x.InstallErrorInstance)
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.ServiceAccount)
                .NotEmpty()
                .Unless(x => !x.InstallAuditInstance)
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControl.PortNumber)
                .NotEmpty()
                .ValidPort()
                .PortAvailable()
                .MustNotBeIn(x => Validations.UsedPorts(x.ServiceControl.InstanceName))
                .NotEqual(x => x.ServiceControl.DatabaseMaintenancePortNumber)
                .NotEqual(x => x.ServiceControlAudit.PortNumber)
                .NotEqual(x => x.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "ServiceControl Ports"))
                .Unless(x => !x.InstallErrorInstance)
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.PortNumber)
                .NotEmpty()
                .ValidPort()
                .PortAvailable()
                .MustNotBeIn(x => Validations.UsedPorts(x.ServiceControlAudit.InstanceName))
                .NotEqual(x => x.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .NotEqual(x => x.ServiceControl.PortNumber)
                .NotEqual(x => x.ServiceControl.DatabaseMaintenancePortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "ServiceControl Audit Instance Ports"))
                .Unless(x => !x.InstallAuditInstance)
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControl.DatabaseMaintenancePortNumber)
                .NotEmpty()
                .ValidPort()
                .PortAvailable()
                .MustNotBeIn(x => Validations.UsedPorts(x.ServiceControl.InstanceName))
                .NotEqual(x => x.ServiceControl.PortNumber)
                .NotEqual(x => x.ServiceControlAudit.PortNumber)
                .NotEqual(x => x.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "ServiceControl Database Maintenance Ports"))
                .Unless(x => !x.InstallErrorInstance)
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .NotEmpty()
                .ValidPort()
                .PortAvailable()
                .MustNotBeIn(x => Validations.UsedPorts(x.ServiceControlAudit.InstanceName))
                .NotEqual(x => x.ServiceControlAudit.PortNumber)
                .NotEqual(x => x.ServiceControl.PortNumber)
                .NotEqual(x => x.ServiceControl.DatabaseMaintenancePortNumber)
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "ServiceControl Audit Instance Ports"))
                .Unless(x => !x.InstallAuditInstance)
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControl.DestinationPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.ServiceControl.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Destination Paths"))
                .Unless(x => !x.InstallErrorInstance)
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.DestinationPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.ServiceControlAudit.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Destination Paths"))
                .Unless(x => !x.InstallAuditInstance)
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControl.DatabasePath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.ServiceControl.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Database Paths"))
                .Unless(x => !x.InstallErrorInstance)
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.DatabasePath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => Validations.UsedPaths(x.ServiceControlAudit.InstanceName))
                .WithMessage(string.Format(Validation.Validations.MSG_MUST_BE_UNIQUE, "Database Paths"))
                .Unless(x => !x.InstallAuditInstance)
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.AuditForwarding)
                .NotNull().WithMessage(Validation.Validations.MSG_SELECTAUDITFORWARDING)
                .Unless(x => !x.InstallAuditInstance);

            RuleFor(x => x.ServiceControl.ErrorForwarding)
                .NotNull().WithMessage(Validation.Validations.MSG_SELECTERRORFORWARDING)
                .Unless(x => !x.InstallErrorInstance);


            RuleFor(x => x.ServiceControl.ErrorQueueName)
                .NotEmpty()
                .NotEqual(x => x.ServiceControl.ErrorForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Error Forwarding"))
                .NotEqual(x => x.ServiceControlAudit.AuditQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit"))
                .NotEqual(x => x.ServiceControlAudit.AuditForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.SelectedTransport, x.ServiceControl.InstanceName, x.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .MustNotBeIn(x => Validations.UsedAuditQueueNames(x.SelectedTransport, x.ServiceControl.InstanceName, x.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .Unless(x => !x.InstallErrorInstance)
                .When(x => x.SubmitAttempted && x.ServiceControl.ErrorQueueName != "!disable");

            RuleFor(x => x.ServiceControl.ErrorForwardingQueueName)
                .NotEmpty()
                .NotEqual(x => x.ServiceControl.ErrorQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Error"))
                .NotEqual(x => x.ServiceControlAudit.AuditQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit"))
                .NotEqual(x => x.ServiceControlAudit.AuditForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.SelectedTransport, x.ServiceControl.InstanceName, x.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .MustNotBeIn(x => Validations.UsedAuditQueueNames(x.SelectedTransport, x.ServiceControl.InstanceName, x.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .Unless(x => !x.InstallErrorInstance)
                .When(x => x.SubmitAttempted && x.ServiceControl.ErrorForwarding.Value);

            RuleFor(x => x.ServiceControlAudit.AuditQueueName)
                .NotEmpty()
                .NotEqual(x => x.ServiceControl.ErrorQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Error"))
                .NotEqual(x => x.ServiceControl.ErrorForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Error Forwarding"))
                .NotEqual(x => x.ServiceControlAudit.AuditForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.SelectedTransport, x.ServiceControlAudit.InstanceName, x.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .Unless(x => !x.InstallAuditInstance)
                .When(x => x.SubmitAttempted && x.ServiceControlAudit.AuditQueueName != "!disable");

            RuleFor(x => x.ServiceControlAudit.AuditForwardingQueueName)
                .NotEmpty()
                .NotEqual(x => x.ServiceControl.ErrorQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Error"))
                .NotEqual(x => x.ServiceControlAudit.AuditQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Audit"))
                .NotEqual(x => x.ServiceControl.ErrorForwardingQueueName).WithMessage(string.Format(Validation.Validations.MSG_UNIQUEQUEUENAME, "Error Forwarding"))
                .MustNotBeIn(x => Validations.UsedErrorQueueNames(x.SelectedTransport, x.ServiceControl.InstanceName, x.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .MustNotBeIn(x => Validations.UsedAuditQueueNames(x.SelectedTransport, x.ServiceControl.InstanceName, x.ConnectionString)).WithMessage(Validation.Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .Unless(x => !x.InstallAuditInstance)
                .When(x => x.SubmitAttempted && (x.ServiceControlAudit.AuditForwarding?.Value ?? false));

            RuleFor(x => x.ConnectionString)
                .NotEmpty().WithMessage(Validation.Validations.MSG_THIS_TRANSPORT_REQUIRES_A_CONNECTION_STRING)
                .When(x => !string.IsNullOrWhiteSpace(x.SelectedTransport?.SampleConnectionString) && x.SubmitAttempted);
        }
    }
}