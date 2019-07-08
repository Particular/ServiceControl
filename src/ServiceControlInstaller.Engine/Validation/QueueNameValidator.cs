namespace ServiceControlInstaller.Engine.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Instances;

    internal class QueueNameValidator
    {
        QueueNameValidator()
        {
            SCInstances = new List<IServiceControlInstance>();
            AuditInstances = new List<IServiceControlAuditInstance>();
        }

        internal QueueNameValidator(IServiceControlInstance instance) : this()
        {
            DetermineServiceControlQueueNames(instance);
        }

        internal QueueNameValidator(IServiceControlAuditInstance instance) : this()
        {
            DetermineAuditQueueNames(instance);
        }

        public static void Validate(IServiceControlInstance instance)
        {
            var validator = new QueueNameValidator(instance)
            {
                SCInstances = InstanceFinder.ServiceControlInstances().Where(p => p.Name != instance.Name & p.TransportPackage.Equals(instance.TransportPackage)).AsEnumerable<IServiceControlInstance>().ToList(),
                AuditInstances = InstanceFinder.ServiceControlAuditInstances().Where(p => p.Name != instance.Name & p.TransportPackage.Equals(instance.TransportPackage)).AsEnumerable<IServiceControlAuditInstance>().ToList(),
            };

            validator.RunValidation();
        }

        public static void Validate(IServiceControlAuditInstance instance)
        {
            var validator = new QueueNameValidator(instance)
            {
                SCInstances = InstanceFinder.ServiceControlInstances().Where(p => p.Name != instance.Name & p.TransportPackage.Equals(instance.TransportPackage)).AsEnumerable<IServiceControlInstance>().ToList(),
                AuditInstances = InstanceFinder.ServiceControlAuditInstances().Where(p => p.Name != instance.Name & p.TransportPackage.Equals(instance.TransportPackage)).AsEnumerable<IServiceControlAuditInstance>().ToList(),
            };

            validator.RunValidation();
        }

        void DetermineAuditQueueNames(IServiceControlAuditInstance instance)
        {
            queues = new List<QueueInfo>
            {
                new QueueInfo
                {
                    PropertyName = "AuditQueue",
                    ConnectionString = instance.ConnectionString,
                    QueueName = instance.AuditQueue,
                    QueueType = QueueType.Audit
                }
            };

            if(instance.ForwardAuditMessages)
            {
                queues.Add(new QueueInfo
                {
                    PropertyName = "AuditLogQueue",
                    ConnectionString = instance.ConnectionString,
                    QueueName = instance.AuditLogQueue,
                    QueueType = QueueType.AuditLog
                });
            }
        }

        void DetermineServiceControlQueueNames(IServiceControlInstance instance)
        {
            queues = new List<QueueInfo>
            {
                new QueueInfo
                {
                    PropertyName = "ErrorQueue",
                    ConnectionString = instance.ConnectionString,
                    QueueName = instance.ErrorQueue,
                    QueueType = QueueType.Error
                }
            };

            if (instance.ForwardErrorMessages)
            {
                queues.Add(new QueueInfo
                {
                    PropertyName = "ErrorLogQueue",
                    ConnectionString = instance.ConnectionString,
                    QueueName = instance.ErrorLogQueue,
                    QueueType = QueueType.ErrorLog
                });
            }
        }

        void RunValidation()
        {
            CheckQueueNamesAreUniqueWithinInstance();
            CheckQueueNamesAreNotTakenByAnotherServiceControlInstance();
            CheckQueueNamesAreNotTakenByAnotherAuditInstance();
        }

        internal void CheckQueueNamesAreUniqueWithinInstance()
        {
            var duplicatedQueues = queues
                .Where(x => x.QueueName != null)
                .ToLookup(x => x.QueueName.ToLower())
                .Where(x => x.Key != "!disable" && x.Key != "!disable.log" && x.Count() > 1);

            if (duplicatedQueues.Any())
            {
                throw new EngineValidationException("Each of the queue names specified for a instance should be unique");
            }
        }

        internal void CheckQueueNamesAreNotTakenByAnotherAuditInstance()
        {
            var allQueues = AuditInstances.SelectMany(instance => new QueueNameValidator(instance).queues);
            var duplicates = (
                from queue in queues
                where allQueues.Any(p =>
                    p.QueueType != QueueType.Audit &&
                    string.Equals(p.ConnectionString, queue.ConnectionString, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.QueueName, queue.QueueName, StringComparison.OrdinalIgnoreCase) &&
                    string.Compare("!disable", queue.QueueName, StringComparison.OrdinalIgnoreCase) != 0 &&
                    string.Compare("!disable.log", queue.QueueName, StringComparison.OrdinalIgnoreCase) != 0)
                select queue.PropertyName
            ).ToList();

            ThrowIfDuplicateFound(duplicates);
        }

        internal void CheckQueueNamesAreNotTakenByAnotherServiceControlInstance()
        {
            var allQueues = SCInstances.SelectMany(instance => new QueueNameValidator(instance).queues);
            var duplicates = (
                from queue in queues
                where allQueues.Any(p =>
                    string.Equals(p.ConnectionString, queue.ConnectionString, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.QueueName, queue.QueueName, StringComparison.OrdinalIgnoreCase) &&
                    string.Compare("!disable", queue.QueueName, StringComparison.OrdinalIgnoreCase) != 0 &&
                    string.Compare("!disable.log", queue.QueueName, StringComparison.OrdinalIgnoreCase) != 0)
                select queue.PropertyName
            ).ToList();

            ThrowIfDuplicateFound(duplicates);
        }

        void ThrowIfDuplicateFound(IList<string> duplicates)
        {
            if (duplicates.Count == 1)
            {
                throw new EngineValidationException($"The queue name for {duplicates[0]} is already assigned to another ServiceControl instance");
            }

            if (duplicates.Count > 1)
            {
                throw new EngineValidationException($"Some queue names specified are already assigned to another ServiceControl instance - Correct the values for {string.Join(", ", duplicates.OrderBy(x => x))}");
            }
        }

        internal List<IServiceControlInstance> SCInstances;
        internal List<IServiceControlAuditInstance> AuditInstances;
        List<QueueInfo> queues;

        class QueueInfo
        {
            public string ConnectionString { get; set; }
            public string PropertyName { get; set; }
            public string QueueName { get; set; }
            public QueueType QueueType { get; set; }
        }

        public enum QueueType
        {
            Audit,
            AuditLog,
            Error,
            ErrorLog
        }
    }
}