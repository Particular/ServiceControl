namespace ServiceControlInstaller.Engine.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Instances;

    internal class ServiceControlQueueNameValidator
    {
        internal ServiceControlQueueNameValidator(IServiceControlTransportConfig instance)
        {
            DetermineQueueNames(instance.AuditQueue, instance.ErrorQueue, instance.AuditLogQueue, instance.ErrorLogQueue, instance.ConnectionString);
        }

        public static void Validate(ServiceControlNewInstance instance)
        {
            var validator = new ServiceControlQueueNameValidator(instance)
            {
                Instances = InstanceFinder.ServiceControlInstances().Where(p => p.Name != instance.Name & p.TransportPackage.Equals(instance.TransportPackage)).AsEnumerable<IServiceControlTransportConfig>().ToList()
            };
            validator.RunValidation();
        }

        public static void Validate(ServiceControlInstance instance)
        {
            var validator = new ServiceControlQueueNameValidator(instance)
            {
                Instances = InstanceFinder.ServiceControlInstances().Where(p => p.Name != instance.Name & p.TransportPackage.Equals(instance.TransportPackage)).AsEnumerable<IServiceControlTransportConfig>().ToList()
            };
            validator.RunValidation();
        }

        void DetermineQueueNames(string audit, string error, string auditLog, string errorLog, string connectionString)
        {
            var auditQueueInfo = new QueueInfo
            {
                PropertyName = "AuditQueue",
                ConnectionString = connectionString,
                QueueName = string.IsNullOrWhiteSpace(audit) ? "audit" : audit
            };

            var errorQueueInfo = new QueueInfo
            {
                PropertyName = "ErrorQueue",
                ConnectionString = connectionString,
                QueueName = string.IsNullOrWhiteSpace(error) ? "error" : error
            };

            var auditLogQueueInfo = new QueueInfo
            {
                PropertyName = "AuditLogQueue",
                ConnectionString = connectionString,
                QueueName = string.IsNullOrWhiteSpace(auditLog) ? audit + ".log" : auditLog
            };

            var errorLogQueueInfo = new QueueInfo
            {
                PropertyName = "ErrorLogQueue",
                ConnectionString = connectionString,
                QueueName = string.IsNullOrWhiteSpace(errorLog) ? error + ".log" : errorLog
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
            CheckQueueNamesAreUniqueWithinInstance();
            CheckQueueNamesAreNotTakenByAnotherInstance();
        }

        internal void CheckQueueNamesAreUniqueWithinInstance()
        {
            var duplicatedQueues = queues.ToLookup(x => x.QueueName.ToLower())
                .Where(x => x.Key != "!disable" && x.Key != "!disable.log" && x.Count() > 1);

            if (duplicatedQueues.Any())
            {
                throw new EngineValidationException("Each of the queue names specified for a instance should be unique");
            }
        }

        internal void CheckQueueNamesAreNotTakenByAnotherInstance()
        {
            var allQueues = new List<QueueInfo>();
            foreach (var instance in Instances)
            {
                allQueues.AddRange(new ServiceControlQueueNameValidator(instance).queues);
            }

            var duplicates = (
                from queue in queues
                where allQueues.Any(p =>
                    string.Equals(p.ConnectionString, queue.ConnectionString, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.QueueName, queue.QueueName, StringComparison.OrdinalIgnoreCase) &&
                    string.Compare("!disable", queue.QueueName, StringComparison.OrdinalIgnoreCase) != 0 &&
                    string.Compare("!disable.log", queue.QueueName, StringComparison.OrdinalIgnoreCase) != 0)
                select queue.PropertyName
            ).ToList();

            if (duplicates.Count == 1)
            {
                throw new EngineValidationException($"The queue name for {duplicates[0]} is already assigned to another ServiceControl instance");
            }

            if (duplicates.Count > 1)
            {
                throw new EngineValidationException($"Some queue names specified are already assigned to another ServiceControl instance - Correct the values for {string.Join(", ", duplicates)}");
            }
        }

        internal List<IServiceControlTransportConfig> Instances;
        List<QueueInfo> queues;

        class QueueInfo
        {
            public string ConnectionString { get; set; }
            public string PropertyName { get; set; }
            public string QueueName { get; set; }
        }
    }
}