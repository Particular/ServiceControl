namespace ServiceControl.Infrastructure;

using System;
using System.Collections.Generic;
using OpenTelemetry.Resources;

public static class TelemetryResourceBuilderExtensions
{
    /// <summary>
    /// Identifies this process to the exporter, leaving the standard OpenTelemetry environment
    /// variables in charge wherever an operator has set them. Scaled out workers read the same queue
    /// and so share an instance name, so the host and process are reported as well, which tells a
    /// pool apart on a dashboard without anyone having to configure it.
    /// </summary>
    public static ResourceBuilder AddServiceControlInstance(this ResourceBuilder resource, string instanceName, string instanceVersion)
    {
        // Read from the environment rather than IConfiguration, because these are read back by the
        // SDK's own detector, which only looks at the environment. Taking an attribute as declared
        // from a source the detector cannot see would suppress the value here and leave none at all.
        var serviceName = Environment.GetEnvironmentVariable(ServiceNameVariable);
        var resourceAttributes = Environment.GetEnvironmentVariable(ResourceAttributesVariable);

        resource.AddService(
            serviceName: string.IsNullOrWhiteSpace(serviceName) ? instanceName : serviceName,
            serviceVersion: instanceVersion,
            autoGenerateServiceInstanceId: !Declares(resourceAttributes, ServiceInstanceIdAttribute));

        var attributes = new Dictionary<string, object>();

        if (!Declares(resourceAttributes, HostNameAttribute))
        {
            attributes[HostNameAttribute] = Environment.MachineName;
        }

        if (!Declares(resourceAttributes, ProcessIdAttribute))
        {
            attributes[ProcessIdAttribute] = (long)Environment.ProcessId;
        }

        return attributes.Count > 0 ? resource.AddAttributes(attributes) : resource;
    }

    /// <summary>
    /// Whether <c>OTEL_RESOURCE_ATTRIBUTES</c> declares an attribute. Only presence is decided here:
    /// the value stays with the SDK's detector, which owns the percent decoding.
    /// </summary>
    public static bool Declares(string resourceAttributes, string attributeName)
    {
        if (string.IsNullOrWhiteSpace(resourceAttributes))
        {
            return false;
        }

        foreach (var attribute in resourceAttributes.Split(','))
        {
            var separator = attribute.IndexOf('=');

            if (separator > 0 && attribute.AsSpan(0, separator).Trim().SequenceEqual(attributeName))
            {
                return true;
            }
        }

        return false;
    }

    const string ServiceNameVariable = "OTEL_SERVICE_NAME";
    const string ResourceAttributesVariable = "OTEL_RESOURCE_ATTRIBUTES";
    const string ServiceInstanceIdAttribute = "service.instance.id";
    const string HostNameAttribute = "host.name";
    const string ProcessIdAttribute = "process.pid";
}
