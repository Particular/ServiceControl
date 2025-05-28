using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Transport;

namespace ServiceControl.Persistence;

public interface IExtraExceptionDataStore
{
    Task storeExtraExceptionInformation(ExtraExceptionInfo extraExceptionInfo , CancellationToken cancellationToken);
    Task<ExtraExceptionInfo?> GetExtraExceptionInformation(string Key, CancellationToken cancellationToken);
    Task storePartialMessage(MessageContext message, CancellationToken cancellationToken);
    Task<MessageContext?> GetPartialMessage(string Key, CancellationToken cancellationToken);
}

public class ExtraExceptionInfo
{
    public string ID { get; set; }
    public Dictionary<string, string> Headers { get; set; }
}

public class PartialMessageInfo
{
    
}
