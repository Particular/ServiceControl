namespace ServiceControlInstaller.Engine.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ServiceControlInstaller.Engine.Instances;

    internal class QueueNameValidator
    {
        internal List<IContainQueueNames> instances;
        List<QueueInfo> queues;

        class QueueInfo
        {
            public string PropertyName { get; set; }
            public string QueueName { get; set; }
        }

        public static void Validate(ServiceControlInstanceMetadata instance)
        {
            var validator = new QueueNameValidator(instance)
            {
                instances = ServiceControlInstance.Instances().Where(p => p.TransportPackage.Equals(instance.TransportPackage, StringComparison.OrdinalIgnoreCase)).AsEnumerable<IContainQueueNames>().ToList()
            };
            validator.RunValidation();
        }

        public static void Validate(ServiceControlInstance instance)
        {
            var validator = new QueueNameValidator(instance)
            {
                instances = ServiceControlInstance.Instances().Where(p => p.Name != instance.Name  & p.TransportPackage.Equals(instance.TransportPackage, StringComparison.OrdinalIgnoreCase)).AsEnumerable<IContainQueueNames>().ToList()
            };
            validator.RunValidation();
        }

        internal QueueNameValidator(IContainQueueNames instance)
        {
            DetermineQueueNames(instance.AuditQueue, instance.ErrorQueue, instance.AuditLogQueue, instance.ErrorLogQueue, instance.TransportPackage);
        }

        void DetermineQueueNames(string audit, string error, string auditLog, string errorLog, string transport)
        {
            var auditQueueInfo = new QueueInfo
            {
                PropertyName = "AuditQueue",
                QueueName = string.IsNullOrWhiteSpace(audit) ? "audit" : audit
            };

            var errorQueueInfo = new QueueInfo
            {
                PropertyName = "ErrorQueue",
                QueueName = string.IsNullOrWhiteSpace(error) ? "error" : error
            };

            var auditLogQueueInfo = new QueueInfo
            {
                PropertyName = "AuditLogQueue",
                QueueName = string.IsNullOrWhiteSpace(auditLog) ? audit + "log" : auditLog
            };

            var errorLogQueueInfo = new QueueInfo
            {
                PropertyName = "ErrorLogQueue",
                QueueName = string.IsNullOrWhiteSpace(errorLog) ? error + "log" : errorLog
            };

            queues = new List<QueueInfo>
            {
                auditLogQueueInfo,
                auditQueueInfo,
                errorLogQueueInfo,
                errorQueueInfo
            };
        }

        void RunValidation()
        {
            CheckQueueNamesAreUnique();
            CheckQueueNamesAreNotTakenByAnotherInstance();
        }

        internal void CheckQueueNamesAreUnique()
        {
            if (queues.Select(p => p.QueueName.ToLower()).Distinct().Count() != queues.Count)
            {
                throw new Exception("Each of the queue names specified for a instance should be unique");
            }
        }

        internal void CheckQueueNamesAreNotTakenByAnotherInstance()
        {
            var allQueues = new List<QueueInfo>();
            foreach (var instance in instances)
            {
                allQueues.AddRange(new QueueNameValidator(instance).queues);
            }
            var uniqueQueueNames = allQueues.Select(p => p.QueueName.ToLower()).Distinct().ToList();
            var duplicates = queues.Where(queue => uniqueQueueNames.Contains(queue.QueueName, StringComparer.OrdinalIgnoreCase)).ToList();
            if (duplicates.Count == 1)
            {
                throw new Exception(string.Format("The queue name for {0} is already assigned to another ServiceControl instance", duplicates[0].PropertyName));
            }

            if (duplicates.Count > 1)
            {
                throw new Exception(string.Format("Some queue names specified are already assigned to another ServiceControl instance - Correct the values for {0}", string.Join(", ", duplicates.Select(p => p.PropertyName))));
            }
        }
    }
}
