namespace ServiceControl.Api.Contracts;

using System;

public class RemoteConfiguration
{
    public string ApiUri { get; set; }
    public string Version { get; set; }
    public string Status { get; set; }
    public InstanceConfiguration Configuration { get; set; } = new();
}

public static class RemoteStatus
{
    public static string Online = "online";
    public static string Unavailable = "unavailable";
    public static string Error = "error";
}

public class Host
{
    public string ServiceName { get; set; }
    public Logging Logging { get; set; } = new();
}

public class Logging
{
    public string LogPath { get; set; }
    public string LoggingLevel { get; set; }
}

public class DataRetention
{
    public TimeSpan AuditRetentionPeriod { get; set; }
}

public class PerformanceTunning
{
    public int MaxBodySizeToStore { get; set; }
    public int HttpDefaultConnectionLimit { get; set; }
}

public class Transport
{
    public string TransportType { get; set; }
    public string AuditLogQueue { get; set; }
    public string AuditQueue { get; set; }
    public bool ForwardAuditMessages { get; set; }
}

public class Peristence
{
    public string PersistenceType { get; set; }
}

public class Plugins
{
}

public class InstanceConfiguration
{
    public Host Host { get; set; } = new();
    public DataRetention DataRetention { get; set; } = new();
    public PerformanceTunning PerformanceTunning { get; set; } = new();
    public Transport Transport { get; set; } = new();
    public Peristence Peristence { get; set; } = new();
    public Plugins Plugins { get; set; } = new();
}