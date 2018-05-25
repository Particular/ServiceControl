namespace Particular.ServiceControl.Upgrade
{
    using System;

    public struct StaleIndexInfo
    {
        public DateTime? StartedAt;
        public bool InProgress;
    }
}