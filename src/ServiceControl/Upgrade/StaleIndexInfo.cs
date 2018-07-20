namespace Particular.ServiceControl.Upgrade
{
    using System;

    public struct StaleIndexInfo
    {
        // ReSharper disable once NotAccessedField.Global
        public DateTime? StartedAt;
        public bool InProgress;
    }
}