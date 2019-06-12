namespace ServiceControlInstaller.Engine.Validation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Instances;

    internal class QueueNameValidator
    {
        internal QueueNameValidator(IServiceControlInstance instance)
        {
            DetermineServiceControlQueueNames(instance.ErrorQueue, instance.ErrorLogQueue, instance.ConnectionString);
        }

        internal QueueNameValidator(IServiceControlAuditInstance instance)
        {
            DetermineAuditQueueNames(instance.AuditQueue, instance.AuditLogQueue, instance.ConnectionString);
        }

        public static void Validate(IServiceControlAuditInstance instance)
        {
            var validator = new QueueNameValidator(instance)
            {
                AuditInstances = InstanceFinder.ServiceControlAuditInstances().Where(p => p.Name != instance.Name & p.TransportPackage.Equals(instance.TransportPackage)).AsEnumerable<IServiceControlAuditInstance>().ToList()
            };
            validator.RunValidation();
        }

        public static void Validate(IServiceControlInstance instance)
        {
            var validator = new QueueNameValidator(instance)
            {
                SCInstances = InstanceFinder.ServiceControlInstances().Where(p => p.Name != instance.Name & p.TransportPackage.Equals(instance.TransportPackage)).AsEnumerable<IServiceControlInstance>().ToList()
            };
            validator.RunValidation();
        }

        void DetermineAuditQueueNames(string audit, string auditLog, string connectionString)
        {
            var auditQueueInfo = new QueueInfo
            {
                PropertyName = "AuditQueue",
                ConnectionString = connectionString,
                QueueName = string.IsNullOrWhiteSpace(audit) ? "audit" : audit
            };

            var auditLogQueueInfo = new QueueInfo
            {
                PropertyName = "AuditLogQueue",
                ConnectionString = connectionString,
                QueueName = string.IsNullOrWhiteSpace(auditLog) ? audit + ".log" : auditLog
            };

            queues = new List<QueueInfo>
            {
                auditLogQueueInfo,
                auditQueueInfo,
            };
        }

        void DetermineServiceControlQueueNames(string error, string errorLog, string connectionString)
        {
            var errorQueueInfo = new QueueInfo
            {
                PropertyName = "ErrorQueue",
                ConnectionString = connectionString,
                QueueName = string.IsNullOrWhiteSpace(error) ? "error" : error
            };

            var errorLogQueueInfo = new QueueInfo
            {
                PropertyName = "ErrorLogQueue",
                ConnectionString = connectionString,
                QueueName = string.IsNullOrWhiteSpace(errorLog) ? error + ".log" : errorLog
            };

            queues = new List<QueueInfo>
            {
                errorLogQueueInfo,
                errorQueueInfo
            };
        }

        void RunValidation()
        {
            CheckQueueNamesAreUniqueWithinInstance();
            CheckQueueNamesAreNotTakenByAnotherServiceControlInstance();
            CheckQueueNamesAreNotTakenByAnotherAuditInstance();
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

        internal void CheckQueueNamesAreNotTakenByAnotherAuditInstance()
        {
            var allQueues = AuditInstances.SelectMany(instance => new QueueNameValidator(instance).queues);
            CheckQueueNamesAreNotTaken(allQueues);
        }

        internal void CheckQueueNamesAreNotTakenByAnotherServiceControlInstance()
        {
            var allQueues = SCInstances.SelectMany(instance => new QueueNameValidator(instance).queues);
            CheckQueueNamesAreNotTaken(allQueues);
        }

        void CheckQueueNamesAreNotTaken(IEnumerable<QueueInfo> allQueues)
        {
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

        internal List<IServiceControlInstance> SCInstances;
        internal List<IServiceControlAuditInstance> AuditInstances;
        List<QueueInfo> queues;

        class QueueInfo
        {
            public string ConnectionString { get; set; }
            public string PropertyName { get; set; }
            public string QueueName { get; set; }
        }
    }
}