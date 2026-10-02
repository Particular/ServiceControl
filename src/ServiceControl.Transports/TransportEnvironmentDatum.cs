namespace ServiceControl.Transports
{
    using System;

    public sealed record TransportEnvironmentDatum(string Key, Func<string> ReadValue);
}
