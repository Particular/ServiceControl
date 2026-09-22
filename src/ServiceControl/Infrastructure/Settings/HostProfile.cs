namespace ServiceBus.Management.Infrastructure.Settings
{
    /// <summary>
    /// What this process is responsible for, derived once from the command it was started with.
    /// Components ask for a capability rather than for the mode, so adding a mode is a change here
    /// and not in every component.
    /// </summary>
    /// <param name="HostsApi">Serves the HTTP API and the platform connection details behind it.</param>
    /// <param name="HostsPrimaryEndpoint">Runs the primary NServiceBus endpoint on the instance queue.</param>
    /// <param name="OwnsSingletonWork">Runs what a deployment may only run once: retries, integration event dispatch, licensing, notifications.</param>
    /// <param name="OwnsRetention">Sweeps the database it is configured against.</param>
    /// <param name="MonitorsHeartbeats">Checks endpoint heartbeats, rather than only warming the endpoint monitor for ingestion.</param>
    /// <param name="ReportsToPrimary">Sends custom check results and detected endpoints to another primary's queue instead of storing them.</param>
    public sealed record HostProfile(
        bool HostsApi,
        bool HostsPrimaryEndpoint,
        bool OwnsSingletonWork,
        bool OwnsRetention,
        bool MonitorsHeartbeats,
        bool ReportsToPrimary);
}
