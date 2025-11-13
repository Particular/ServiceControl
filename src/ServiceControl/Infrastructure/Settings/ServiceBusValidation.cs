namespace ServiceBus.Management.Infrastructure.Settings;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class ServiceBusValidation(
    ILogger<Settings> logger // Intentionally using SETTINGS as logger name
) : IValidateOptions<ServiceBusOptions> // TODO: Register
{
    public ValidateOptionsResult Validate(string name, ServiceBusOptions options)
    {
        if (string.IsNullOrEmpty(options.ErrorLogQueue))
        {
            logger.LogInformation("No settings found for error log queue to import, default name will be used");
        }

        return ValidateOptionsResult.Success;
    }
}