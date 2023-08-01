namespace NServiceBus.Transport.Msmq
{
    using System;
    using System.Configuration;
    using System.Transactions;
    using System.Transactions.Configuration;

    class MsmqScopeOptions
    {
        public MsmqScopeOptions(TimeSpan? requestedTimeout = null, IsolationLevel? requestedIsolationLevel = null)
        {
            var timeout = TransactionManager.DefaultTimeout;
            var isolationLevel = IsolationLevel.ReadCommitted;
            if (requestedTimeout.HasValue)
            {
                var maxTimeout = GetMaxTimeout();

                if (requestedTimeout.Value > maxTimeout)
                {
                    throw new ConfigurationErrorsException(
                        "Timeout requested is longer than the maximum value for this machine. Override using the maxTimeout setting of the system.transactions section in machine.config");
                }

                timeout = requestedTimeout.Value;
            }

            if (requestedIsolationLevel.HasValue)
            {
                isolationLevel = requestedIsolationLevel.Value;
            }

            TransactionOptions = new TransactionOptions
            {
                IsolationLevel = isolationLevel,
                Timeout = timeout
            };
        }

        public TransactionOptions TransactionOptions { get; }

        static TimeSpan GetMaxTimeout()
        {
            var systemTransactionsGroup = ConfigurationManager.OpenMachineConfiguration()
                .GetSectionGroup("system.transactions");

            if (systemTransactionsGroup?.Sections.Get("machineSettings") is MachineSettingsSection machineSettings)
            {
                return machineSettings.MaxTimeout;
            }

            //default is always 10 minutes
            return TimeSpan.FromMinutes(10);
        }
    }
}