namespace ServiceControl.Transports;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Transport;

public interface IErrorQueueDiscoveryMethod
{
    string Name { get; }

    Func<(MessageContext Context, string ErrorQueueName), string> GetReturnQueueName { get; }

    Task<IEnumerable<string>> GetErrorQueueNames(CancellationToken cancellationToken = default);
}