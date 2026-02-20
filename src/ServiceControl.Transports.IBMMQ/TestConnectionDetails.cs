#nullable enable
using System;
using System.Collections.Specialized;
using System.Web;
using NServiceBus.Transport.IbmMq;

/// <summary>
/// Copied directly from:
///
///    https://github.com/ParticularLabs/NServiceBus.IBMMQ/blob/main/src/Testing/TestConnectionDetails.cs
///
/// </summary>
static class TestConnectionDetails
{
    // mq://admin:passw0rd@localhost:1414/QM1?appname=&sslkeyrepo=&cipherspec=&sslpeername=&topicprefix=DEV&channel=DEV.ADMIN.SVRCONN
    static readonly Uri ConnectionUri = new(Environment.GetEnvironmentVariable("IBMMQ_CONNECTIONSTRING") ?? "mq://admin:passw0rd@localhost:1414");
    static readonly NameValueCollection Query = HttpUtility.ParseQueryString(ConnectionUri.Query);

    public static string Host => ConnectionUri.Host;
    public static int Port => ConnectionUri.Port > 0 ? ConnectionUri.Port : 1414;
    public static string User => Uri.UnescapeDataString(ConnectionUri.UserInfo.Split(':')[0]);
    public static string Password => Uri.UnescapeDataString(ConnectionUri.UserInfo.Split(':')[1]);
    public static string QueueManagerName => ConnectionUri.AbsolutePath.Trim('/') is { Length: > 0 } path ? Uri.UnescapeDataString(path) : "QM1";
    public static string Channel => Query["channel"] ?? "DEV.ADMIN.SVRCONN";
    public static string TopicPrefix => Query["topicprefix"] ?? "DEV";


    public static void Apply(IbmMqTransportOptions options)
    {
        options.Host = Host;
        options.Port = Port;
        options.User = User;
        options.Password = Password;
        options.QueueManagerName = QueueManagerName;
        options.Channel = Channel;

        if (Query["appname"] is { } appName)
        {
            options.ApplicationName = appName;
        }
        if (Query["sslkeyrepo"] is { } sslKeyRepo)
        {
            options.SslKeyRepository = sslKeyRepo;
        }
        if (Query["cipherspec"] is { } cipherSpec)
        {
            options.CipherSpec = cipherSpec;
        }
        if (Query["sslpeername"] is { } sslPeerName)
        {
            options.SslPeerName = sslPeerName;
        }
    }
}
