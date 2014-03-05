﻿namespace ServiceControl.Plugin.CustomChecks.Internal
{
    // needed for DI
    public interface ICustomCheck
    {
        string Category { get; }
        string Id { get; }
        void ReportPass();
        void ReportFailed(string failureReason);
    }
}
