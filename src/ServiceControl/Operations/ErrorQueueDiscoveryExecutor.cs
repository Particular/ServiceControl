namespace ServiceControl.Operations;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Transport;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Transports;

class ErrorQueueDiscoveryExecutor(Settings settings, IEnumerable<IErrorQueueDiscoveryMethod> methods)
{
    public async Task<Dictionary<string, ReturnQueueResolver>> GetErrorQueueNamesAndReturnQueueResolvers(CancellationToken cancellationToken = default)
    {
        var resolversByErrorQueue = new Dictionary<string, ReturnQueueResolver>();

        foreach (var method in methods.Where(m => settings.ErrorQueueDiscoveryMethods.Contains(m.Name)))
        {
            var queues = await method.GetErrorQueueNames(cancellationToken).ConfigureAwait(false);
            foreach (var queueName in queues)
            {
                if (resolversByErrorQueue.ContainsKey(queueName))
                {
                    throw new Exception($"Duplicate error queue name '{queueName}' found in discovery methods.");
                }

                resolversByErrorQueue[queueName] = new ReturnQueueResolver
                {
                    ResolverName = method.Name,
                    Resolve = method.GetReturnQueueName
                };
            }
        }

        return resolversByErrorQueue;
    }
}

record class ReturnQueueResolver
{
    public string ResolverName { get; init; }
    public Func<MessageContext, string> Resolve { get; init; }
}