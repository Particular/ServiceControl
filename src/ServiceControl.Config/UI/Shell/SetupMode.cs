namespace ServiceControl.Config.UI.Shell
{
    // The scenario chosen in the new-instance popup. It is the only thing that decides
    // which instances get installed, so the mapping lives here rather than in the code
    // behind, where it would sit behind WPF's IsLoaded guard and be untestable.
    enum SetupMode
    {
        ErrorHandling,
        ErrorAndAudit,
        AuditOnly,
        MonitoringOnly
    }

    static class SetupModeExtensions
    {
        public static bool InstallsServiceControl(this SetupMode mode) =>
            mode is SetupMode.ErrorHandling or SetupMode.ErrorAndAudit;

        public static bool InstallsAudit(this SetupMode mode) =>
            mode is SetupMode.ErrorAndAudit or SetupMode.AuditOnly;

        public static bool InstallsMonitoring(this SetupMode mode) =>
            mode is SetupMode.MonitoringOnly;

        // Integrated ServicePulse is hosted by the error instance, so it is only on offer
        // in the scenarios that install one.
        public static bool InstallsServicePulse(this SetupMode mode, bool wanted) =>
            mode.InstallsServiceControl() && wanted;
    }
}
