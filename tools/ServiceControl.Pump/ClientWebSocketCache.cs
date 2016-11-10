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

        public async Task ReleaseClient(ClientWebSocket client, CancellationToken token)
        {
            if (client.State != WebSocketState.Open)
            {
                await Cleanup(client, token).ConfigureAwait(false);
                return;
            }

            stack.Push(client);
        }

        private static async Task Cleanup(ClientWebSocket client, CancellationToken token)
        {
            try
            {
                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, String.Empty, token);
            }
            catch (Exception)
            {
                // We tried our best!
            }
            client.Dispose();
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