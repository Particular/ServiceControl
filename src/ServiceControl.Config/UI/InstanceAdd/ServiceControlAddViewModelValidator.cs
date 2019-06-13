namespace ServiceControl.Config.UI.InstanceAdd
{
    using Extensions;
    using FluentValidation;
    using ServiceControlInstaller.Engine.Instances;
    using Validation;

    public class ServiceControlAddViewModelValidator : AbstractValidator<ServiceControlEditorViewModel>
    {
        public ServiceControlAddViewModelValidator()
        {
            var instances = InstanceFinder.ServiceControlInstances();

            RuleFor(x => x.SelectedTransport)
                .NotEmpty();

            RuleFor(x => x.ServiceControl.ServiceAccount)
                .NotEmpty()
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.ServiceAccount)
                .NotEmpty()
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControl.PortNumber)
                .NotEmpty()
                .ValidPort()
                .PortAvailable()
                .MustNotBeIn(x => instances.UsedPorts(x.ServiceControl.InstanceName))
                .NotEqual(x => x.ServiceControl.DatabaseMaintenancePortNumber)
                .NotEqual(x => x.ServiceControlAudit.PortNumber)
                .NotEqual(x => x.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Ports")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.PortNumber)
                .NotEmpty()
                .ValidPort()
                .PortAvailable()
                .MustNotBeIn(x => instances.UsedPorts(x.ServiceControlAudit.InstanceName))
                .NotEqual(x => x.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .NotEqual(x => x.ServiceControl.PortNumber)
                .NotEqual(x => x.ServiceControl.DatabaseMaintenancePortNumber)
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Ports")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControl.DatabaseMaintenancePortNumber)
                .NotEmpty()
                .ValidPort()
                .PortAvailable()
                .MustNotBeIn(x => instances.UsedPorts(x.ServiceControl.InstanceName))
                .NotEqual(x => x.ServiceControl.PortNumber)
                .NotEqual(x => x.ServiceControlAudit.PortNumber)
                .NotEqual(x => x.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Ports")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.DatabaseMaintenancePortNumber)
                .NotEmpty()
                .ValidPort()
                .PortAvailable()
                .MustNotBeIn(x => instances.UsedPorts(x.ServiceControlAudit.InstanceName))
                .NotEqual(x => x.ServiceControlAudit.PortNumber)
                .NotEqual(x => x.ServiceControl.PortNumber)
                .NotEqual(x => x.ServiceControl.DatabaseMaintenancePortNumber)
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Ports")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControl.DestinationPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => instances.UsedPaths(x.ServiceControl.InstanceName))
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Paths")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.DestinationPath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => instances.UsedPaths(x.ServiceControlAudit.InstanceName))
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Paths")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControl.DatabasePath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => instances.UsedPaths(x.ServiceControl.InstanceName))
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Paths")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.DatabasePath)
                .NotEmpty()
                .ValidPath()
                .MustNotBeIn(x => instances.UsedPaths(x.ServiceControlAudit.InstanceName))
                .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Paths")
                .When(x => x.SubmitAttempted);

            RuleFor(x => x.ServiceControlAudit.AuditForwarding)
                .NotNull().WithMessage(Validations.MSG_SELECTAUDITFORWARDING);

            RuleFor(x => x.ServiceControl.ErrorForwarding)
                .NotNull().WithMessage(Validations.MSG_SELECTERRORFORWARDING);


            RuleFor(x => x.ServiceControl.ErrorQueueName)
                .NotEmpty()
                .NotEqual(x => x.ServiceControl.ErrorForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Error Forwarding")
                .NotEqual(x => x.ServiceControlAudit.AuditQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit")
                .NotEqual(x => x.ServiceControlAudit.AuditForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding")
                .MustNotBeIn(x => instances.UsedQueueNames(x.SelectedTransport, x.ServiceControl.InstanceName, x.ConnectionString)).WithMessage(Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .When(x => x.SubmitAttempted && x.ServiceControl.ErrorQueueName != "!disable");

            RuleFor(x => x.ServiceControl.ErrorForwardingQueueName)
                .NotEmpty()
                .NotEqual(x => x.ServiceControl.ErrorQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Error")
                .NotEqual(x => x.ServiceControlAudit.AuditQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit")
                .NotEqual(x => x.ServiceControlAudit.AuditForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding")
                .MustNotBeIn(x => instances.UsedQueueNames(x.SelectedTransport, x.ServiceControl.InstanceName, x.ConnectionString)).WithMessage(Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .When(x => x.SubmitAttempted && x.ServiceControl.ErrorForwarding.Value);

            RuleFor(x => x.ServiceControlAudit.AuditQueueName)
                .NotEmpty()
                .NotEqual(x => x.ServiceControl.ErrorQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Error")
                .NotEqual(x => x.ServiceControl.ErrorForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Error Forwarding")
                .NotEqual(x => x.ServiceControlAudit.AuditForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding")
                .MustNotBeIn(x => instances.UsedQueueNames(x.SelectedTransport, x.ServiceControlAudit.InstanceName, x.ConnectionString)).WithMessage(Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .When(x => x.SubmitAttempted && x.ServiceControlAudit.AuditQueueName != "!disable");

            RuleFor(x => x.ServiceControlAudit.AuditForwardingQueueName)
                .NotEmpty()
                .NotEqual(x => x.ServiceControl.ErrorQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Error")
                .NotEqual(x => x.ServiceControlAudit.AuditQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit")
                .NotEqual(x => x.ServiceControl.ErrorForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Error Forwarding")
                .MustNotBeIn(x => instances.UsedQueueNames(x.SelectedTransport, x.ServiceControl.InstanceName, x.ConnectionString)).WithMessage(Validations.MSG_QUEUE_ALREADY_ASSIGNED)
                .When(x => x.SubmitAttempted && (x.ServiceControlAudit.AuditForwarding?.Value ?? false));

            RuleFor(x => x.ConnectionString)
                .NotEmpty().WithMessage(Validations.MSG_THIS_TRANSPORT_REQUIRES_A_CONNECTION_STRING)
                .When(x => !string.IsNullOrWhiteSpace(x.SelectedTransport?.SampleConnectionString) && x.SubmitAttempted);
        }
    }
}