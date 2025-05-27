namespace ServiceControl.Operations;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Faults;
using NServiceBus.Transport;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Transports;

public class CentralizedErrorQueueDiscoveryMethod(Settings settings) : IErrorQueueDiscoveryMethod
{
    public string Name => "CentralizedErrorQueue";

    public string ReturnQueueHeaderKey { get; set; } = FaultsHeaderKeys.FailedQ;

    public Func<MessageContext, string> GetReturnQueueName => context => context.Headers[ReturnQueueHeaderKey];

    public Task<IEnumerable<string>> GetErrorQueueNames(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(settings.ErrorQueue))
        {
            return Task.FromResult<IEnumerable<string>>(new string[0]);
        }

        return Task.FromResult<IEnumerable<string>>(new[] { settings.ErrorQueue });
    }
}
