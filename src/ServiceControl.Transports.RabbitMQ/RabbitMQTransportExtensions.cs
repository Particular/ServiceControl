#nullable enable
namespace ServiceControl.Transports.RabbitMQ;

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using NServiceBus;

static class RabbitMQTransportExtensions
{
    public static bool HasBrokerRequirementChecksDisabled(string connectionString)
    {
        var dictionary = ParseConnectionString(connectionString);
        if (dictionary is null)
        {
            return false;
        }

        return dictionary.TryGetValue("DisableBrokerRequirementChecks", out var value)
            && bool.TryParse(value, out var disabled)
            && disabled;
    }

    public static void ApplySettingsFromConnectionString(this RabbitMQTransport transport, string connectionString)
    {
        var dictionary = ParseConnectionString(connectionString);
        if (dictionary is null)
        {
            return;
        }

        if (dictionary.TryGetValue("ValidateDeliveryLimits", out var validateDeliveryLimitsString))
        {
            _ = bool.TryParse(validateDeliveryLimitsString, out var validateDeliveryLimits);
            transport.ValidateDeliveryLimits = validateDeliveryLimits;
        }

        dictionary.TryGetValue("ManagementApiUrl", out var url);
        dictionary.TryGetValue("ManagementApiUserName", out var userName);
        dictionary.TryGetValue("ManagementApiPassword", out var password);

        transport.ManagementApiConfiguration = ManagementApiConfiguration.Create(url, userName, password);

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

        if (dictionary.TryGetValue("DisableBrokerRequirementChecks", out var disableBrokerRequirementChecksString)
            && bool.TryParse(disableBrokerRequirementChecksString, out var disableBrokerRequirementChecks)
            && disableBrokerRequirementChecks)
        {
            transport.DisabledBrokerRequirementChecks =
                BrokerRequirementChecks.Version310OrNewer | BrokerRequirementChecks.StreamsEnabled;
            transport.ValidateDeliveryLimits = false;
        }
    }

    static Dictionary<string, string?>? ParseConnectionString(string connectionString)
    {
        if (connectionString.StartsWith("amqp", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new DbConnectionStringBuilder { ConnectionString = connectionString }
            .OfType<KeyValuePair<string, object>>()
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.OrdinalIgnoreCase);
    }
}
