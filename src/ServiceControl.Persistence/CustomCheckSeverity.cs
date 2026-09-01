namespace ServiceControl.Contracts.CustomChecks
{
    /// <summary>
    /// The impact a failing internal custom check has on platform health, as rendered by ServicePulse.
    /// Serialized lowercase on the wire: "ignore", "degraded", "unavailable".
    /// </summary>
    public enum CustomCheckSeverity
    {
        Ignore = 0,
        Degraded = 1,
        Unavailable = 2
    }
}