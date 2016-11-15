using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceControl.Pump
{
    class ClientWebSocketCache : IDisposable
    {
        readonly ConcurrentStack<ClientWebSocket> stack = new ConcurrentStack<ClientWebSocket>();

        public async Task<ClientWebSocket> GetClient(CancellationToken token)
        {
            ClientWebSocket client;
            while (stack.TryPop(out client))
            {
                if (client.State == WebSocketState.Open)
                {
                    return client;
                }

                await Cleanup(client, token).ConfigureAwait(false);
            }

            client = new ClientWebSocket();
            await client.ConnectAsync(new Uri("ws://localhost:33333/injest"), token).ConfigureAwait(false);
            return client;
        }

        public Task ReleaseClient(ClientWebSocket client, CancellationToken token)
        {
            if (client.State != WebSocketState.Open)
            {
                return Cleanup(client, token);
            }

            stack.Push(client);

            return Task.FromResult(0);
        }

        private static Task Cleanup(WebSocket client, CancellationToken token)
        {
            return client.CloseAsync(WebSocketCloseStatus.NormalClosure, String.Empty, token)
                .ContinueWith(_ =>
                {
                    _.Exception?.Handle(exception => true);
                    client.Dispose();
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.DenyChildAttach, TaskScheduler.Default);
        }

        public void Dispose()
        {
            ClientWebSocket client;
            while (stack.TryPop(out client))
            {
                Cleanup(client, CancellationToken.None).GetAwaiter().GetResult();
            }
        }
    }
}