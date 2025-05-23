namespace ServiceControl.Operations;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Transport;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Transports;

public class ErrorQueueDiscoveryExecutor(Settings settings, IEnumerable<IErrorQueueDiscoveryMethod> methods)
{
    public async Task<Dictionary<string, Func<(MessageContext Context, string ErrorQueueName), string>> GetErrorQueueNames(CancellationToken cancellationToken = default)
    {
        var errorQueueNames = new List<string>();

        foreach (var method in methods.Where(m => settings.ErrorQueueDiscoveryMethods.Contains(m.Name)))
        {
            var queues = await method.GetErrorQueueNames(cancellationToken).ConfigureAwait(false);
            errorQueueNames.AddRange(queues);
        }

        return errorQueueNames.ToArray();
    }
}