namespace ServiceControl.Transports.RabbitMQ;

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using NServiceBus;

static class RabbitMQTransportExtensions
{
    public static void ApplySettingsFromConnectionString(this RabbitMQTransport transport, string connectionString)
    {
        if (connectionString.StartsWith("amqp", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var dictionary = new DbConnectionStringBuilder { ConnectionString = connectionString }
            .OfType<KeyValuePair<string, object>>()
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        if (dictionary.TryGetValue("ValidateDeliveryLimits", out var validateDeliveryLimitsString))
        {
            _ = bool.TryParse(validateDeliveryLimitsString, out var validateDeliveryLimits);
            transport.ValidateDeliveryLimits = validateDeliveryLimits;
        }

        if (dictionary.TryGetValue("ManagementApiUrl", out var url))
        {
            if (dictionary.TryGetValue("ManagementApiUserName", out var userName) && dictionary.TryGetValue("ManagementApiPassword", out var password))
            {
                transport.ManagementApiConfiguration = new(url, userName, password);
            }
            else
            {
                transport.ManagementApiConfiguration = new(url);
            }
        }
        else if (dictionary.TryGetValue("ManagementApiUserName", out var userName) && dictionary.TryGetValue("ManagementApiPassword", out var password))
        {
            transport.ManagementApiConfiguration = new(userName, password);
        }

        if (dictionary.TryGetValue("DisableRemoteCertificateValidation", out var disableRemoteCertificateValidationString))
        {
            _ = bool.TryParse(disableRemoteCertificateValidationString, out var disableRemoteCertificateValidation);
            transport.ValidateRemoteCertificate = !disableRemoteCertificateValidation;
        }

        if (dictionary.TryGetValue("UseExternalAuthMechanism", out var useExternalAuthMechanismString))
        {
            _ = bool.TryParse(useExternalAuthMechanismString, out var useExternalAuthMechanism);
            transport.UseExternalAuthMechanism = useExternalAuthMechanism;
        }
    }
}
