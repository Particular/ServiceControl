namespace ServiceControl.Transports.IBMMQ;

using System;
using System.Collections;
using System.Web;
using IBM.WMQ;

static class ConnectionProperties
{
    public static (string queueManagerName, Hashtable properties) Parse(string connectionString)
    {
        var connectionUri = new Uri(connectionString);
        var query = HttpUtility.ParseQueryString(connectionUri.Query);

        var queueManagerName = connectionUri.AbsolutePath.Trim('/') is { Length: > 0 } path
            ? Uri.UnescapeDataString(path)
            : "QM1";

        var properties = new Hashtable
        {
            [MQC.TRANSPORT_PROPERTY] = MQC.TRANSPORT_MQSERIES_MANAGED,
            [MQC.HOST_NAME_PROPERTY] = connectionUri.Host,
            [MQC.PORT_PROPERTY] = connectionUri.Port > 0 ? connectionUri.Port : 1414,
            [MQC.CHANNEL_PROPERTY] = query["channel"] ?? "DEV.ADMIN.SVRCONN"
        };

        var userInfo = connectionUri.UserInfo;
        if (!string.IsNullOrEmpty(userInfo))
        {
            var parts = userInfo.Split(':');
            var user = Uri.UnescapeDataString(parts[0]);

            if (!string.IsNullOrWhiteSpace(user))
            {
                properties[MQC.USE_MQCSP_AUTHENTICATION_PROPERTY] = true;
                properties[MQC.USER_ID_PROPERTY] = user;
            }

            if (parts.Length > 1)
            {
                var password = Uri.UnescapeDataString(parts[1]);
                if (!string.IsNullOrWhiteSpace(password))
                {
                    properties[MQC.PASSWORD_PROPERTY] = password;
                }
            }
        }

        if (query["sslkeyrepo"] is { } sslKeyRepo)
        {
            properties[MQC.SSL_CERT_STORE_PROPERTY] = sslKeyRepo;
        }

        if (query["cipherspec"] is { } cipherSpec)
        {
            properties[MQC.SSL_CIPHER_SPEC_PROPERTY] = cipherSpec;
        }

        if (query["sslpeername"] is { } sslPeerName)
        {
            properties[MQC.SSL_PEER_NAME_PROPERTY] = sslPeerName;
        }

        return (queueManagerName, properties);
    }
}
