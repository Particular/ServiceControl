namespace ServiceControl.Transports.RabbitMQ;

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using global::RabbitMQ.Client;
using NServiceBus;

static class RabbitMQTransportExtensions
{
    public static void ApplySettingsFromConnectionString(this RabbitMQTransport transport, string connectionString)
    {
        var dictionary = ReadConnectionStringOptions(connectionString);

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

        if (UsesExternalAuthMechanism(dictionary))
        {
            transport.AuthMechanisms = [new ExternalMechanismFactory()];
        }
    }

    public static IEnumerable<TransportEnvironmentDatum> GetEnvironmentData(string connectionString) =>
    [
        new("Transport.RabbitMQ.DeliveryLimitValidation", () =>
        {
            var options = ReadConnectionStringOptions(connectionString);
            var validates = !options.TryGetValue("ValidateDeliveryLimits", out var value) || (bool.TryParse(value, out var parsed) && parsed);
            return validates ? "Enabled" : "Disabled";
        }),
        new("Transport.RabbitMQ.ManagementApi", () => ReadConnectionStringOptions(connectionString).ContainsKey("ManagementApiUrl") ? "Configured" : "Default")
    ];

    public static string GetAuthenticationMode(string connectionString) =>
        UsesExternalAuthMechanism(ReadConnectionStringOptions(connectionString)) ? "ExternalCertificate" : "Password";

    public static bool RelaxesCertificateValidation(string connectionString) =>
        ReadConnectionStringOptions(connectionString).TryGetValue("DisableRemoteCertificateValidation", out var value) && bool.TryParse(value, out var disabled) && disabled;

    static bool UsesExternalAuthMechanism(Dictionary<string, string> options) =>
        options.TryGetValue("UseExternalAuthMechanism", out var value) && bool.TryParse(value, out var useExternalAuthMechanism) && useExternalAuthMechanism;

    static Dictionary<string, string> ReadConnectionStringOptions(string connectionString)
    {
        if (connectionString.StartsWith("amqp", StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return new DbConnectionStringBuilder { ConnectionString = connectionString }
            .OfType<KeyValuePair<string, object>>()
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToString(), StringComparer.OrdinalIgnoreCase);
    }
}
