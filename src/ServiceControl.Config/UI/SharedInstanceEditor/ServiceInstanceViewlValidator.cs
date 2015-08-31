namespace ServiceControl.Config.Validation
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using FluentValidation;
    using ServiceControlInstaller.Engine.Instances;
    using UI.SharedInstanceEditor;

    public class SharedInstanceEditorViewModelValidator<T> : AbstractValidator<T> where T : SharedInstanceEditorViewModel
    {
        ReadOnlyCollection<ServiceControlInstance> ServiceControlInstances;

        // We need this to ignore the instance that represents the edit screen
        protected List<string> UsedPaths(string instanceName = null)
        {
            return ServiceControlInstances
                .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
               .SelectMany(p => new[]
               {
                    p.DBPath,
                    p.LogPath,
                    p.InstallPath
               })
               .Distinct()
               .ToList();
        }

        // We need this to ignore the instance that represents the edit screen
        protected List<string> UsedQueueNames(string instanceName = null)
        {
            return ServiceControlInstances
                .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
                .SelectMany(p => new[]
                {
                    p.ErrorLogQueue,
                    p.ErrorQueue,
                    p.AuditQueue,
                    p.AuditLogQueue
                })
                .Distinct()
                .ToList();
        }

        // We need this to ignore the instance that represents the edit screen
        protected List<string> UsedPorts(string instanceName = null)
        {
            return ServiceControlInstances
                .Where(p => string.IsNullOrWhiteSpace(instanceName) || p.Name != instanceName)
               .Select(p => p.Port.ToString())
               .Distinct()
               .ToList();
        }

        protected SharedInstanceEditorViewModelValidator()
        {
            ServiceControlInstances = ServiceControlInstance.Instances();

            RuleFor(x => x.InstanceName)
                .NotEmpty()
                .MustNotContainWhitespace()
                ;

            RuleFor(x => x.HostName)
                .NotEmpty()
               ;

            RuleFor(x => x.PortNumber)
                .NotEmpty()
                .ValidPort()
                .MustNotBeIn(x => UsedPorts(x.InstanceName))
                    .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Ports")
                ;

            RuleFor(x => x.ConnectionString)
                .TransportConnectionStringValid()
                ;

            RuleFor(x => x.AuditForwarding)
                .NotNull()
                    .WithMessage(Validations.MSG_SELECTAUDITFORWARDING)
                ;

            RuleFor(x => x.LogPath)
                .NotEmpty()
                .MustNotBeIn(x => UsedPaths(x.InstanceName))
                    .WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Paths");
            ;

            RuleFor(x => x.ErrorQueueName)
                .NotEmpty()
                .NotEqual(x => x.AuditQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit")
                .NotEqual(x => x.ErrorForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Error Forwarding")
                .NotEqual(x => x.AuditForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding")
                .MustNotBeIn(x => UsedQueueNames(x.InstanceName)).WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Queue names")
                .MustNotContainWhitespace()
                ;

            RuleFor(x => x.ErrorForwardingQueueName)
                .NotEmpty()
                .NotEqual(x => x.ErrorQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Error")
                .NotEqual(x => x.AuditQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit")
                .NotEqual(x => x.AuditForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding")
                .MustNotBeIn(x => UsedQueueNames(x.InstanceName)).WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Queue names")
                .MustNotContainWhitespace()
                ;

            RuleFor(x => x.AuditQueueName)
                .NotEmpty()
                .NotEqual(x => x.ErrorQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Error")
                .NotEqual(x => x.ErrorForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Error Forwarding")
                .NotEqual(x => x.AuditForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit Forwarding")
                .MustNotBeIn(x => UsedQueueNames(x.InstanceName)).WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Queue names")
                .MustNotContainWhitespace()
                ;

            RuleFor(x => x.AuditForwardingQueueName).NotEmpty()
                .NotEmpty().When(t => t.AuditForwarding == true)
                .NotEqual(x => x.ErrorQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Error")
                .NotEqual(x => x.AuditQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Audit")
                .NotEqual(x => x.ErrorForwardingQueueName).WithMessage(Validations.MSG_UNIQUEQUEUENAME, "Error Forwarding")
                .MustNotBeIn(x => UsedQueueNames(x.InstanceName)).WithMessage(Validations.MSG_MUST_BE_UNIQUE, "Queue names")
                .MustNotContainWhitespace()
                ;
        }
    }
}