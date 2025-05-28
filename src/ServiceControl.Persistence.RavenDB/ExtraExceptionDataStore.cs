using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Transport;

namespace ServiceControl.Persistence.RavenDB;

// public class ExtraExceptionDataStore(IRavenSessionProvider sessionProvider) : IExtraExceptionDataStore
// {
//     public async Task storeExtraExceptionInformation(ExtraExceptionInfo extraExceptionInfo,
//         CancellationToken cancellationToken)
//     {
//         using (var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken))
//         {
//             await session.StoreAsync(extraExceptionInfo, cancellationToken);
//             await session.SaveChangesAsync(cancellationToken);
//         }
//     }
//
//     public async Task<ExtraExceptionInfo> GetExtraExceptionInformation(string Key)
//     {
//         using (var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken))
//         {
//             
//         }
//     }
//
//     public async Task storePartialMessage(MessageContext message, CancellationToken cancellationToken)
//     {
//         using (var session = await sessionProvider.OpenSession(cancellationToken: cancellationToken))
//         {
//             await session.StoreAsync(message, cancellationToken);
//             await session.SaveChangesAsync(cancellationToken);
//         }
//     }
//
//     public async Task<MessageContext> GetPartialMessage(string Key)
//     {
//         
//     }
// }