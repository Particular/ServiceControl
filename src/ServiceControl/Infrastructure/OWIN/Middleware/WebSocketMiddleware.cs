namespace ServiceControl.Infrastructure.OWIN
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using Metrics;
    using Microsoft.Owin;
    using NServiceBus;
    using Raven.Client;
    using Roslyn.Utilities;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageAuditing;
    using ServiceControl.Operations;

    internal class WebSocketMiddleware : OwinMiddleware
    {
        private readonly IDocumentStore store;
        private Lazy<IEnrichImportedMessages[]> enrichers;
        ObjectPool<byte[]> bufferPool = new ObjectPool<byte[]>(() => new byte[4 * 1024 * 1024], 30);
        private readonly Metrics.Timer timer = Metric.Context("Processing").Timer("Audit Messages", Unit.Custom("Messages"));
        private readonly string webSocketContextKey = typeof(WebSocketContext).FullName;


        public WebSocketMiddleware(OwinMiddleware next, IContainer container) : base(next)
        {
            store = container.Resolve<IDocumentStore>();
            enrichers = new Lazy<IEnrichImportedMessages[]>(() => container.Resolve<IEnumerable<IEnrichImportedMessages>>().ToArray());
        }

        public override async Task Invoke(IOwinContext context)
        {
            var accept = context.Get<Action<IDictionary<string, object>, Func<IDictionary<string, object>, Task>>>("websocket.Accept");

            if (IsWebSocketRequest(context.Request) && accept != null)
            {
                accept(null, HandleRequest);
            }
            else
            {
                await Console.Out.WriteLineAsync("Bummer!");
                context.Response.StatusCode = 400;
                context.Response.ReasonPhrase = "Not sure what is going on!";
                await Task.FromResult(0);
            }
        }

        private async Task HandleRequest(IDictionary<string, object> e)
        {
            var socketContext = (WebSocketContext) e[webSocketContextKey];
            var callCancelled = (CancellationToken) e["websocket.CallCancelled"];

            try
            {
                await WebSocketLoopAsync(socketContext.WebSocket, callCancelled);
            }
            catch (OperationCanceledException)
            {
                if (!callCancelled.IsCancellationRequested)
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine(ex);
            }
            finally
            {
                if (socketContext.WebSocket != null)
                {
                    if (socketContext.WebSocket.State != WebSocketState.Closed || socketContext.WebSocket.State != WebSocketState.Aborted)
                    {
                        await socketContext.WebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Client has dropped.", callCancelled).ConfigureAwait(false);
                    }
                    socketContext.WebSocket.Dispose();
                }
            }
        }

        private async Task WebSocketLoopAsync(WebSocket socketContextWebSocket, CancellationToken callCancelled)
        {
            while (!callCancelled.IsCancellationRequested && socketContextWebSocket.State == WebSocketState.Open)
            {
                using (timer.NewContext())
                {
                    byte[] buffer = null, receiveBuffer = null;
                    Dictionary<string, string> headers;
                    byte[] body;

                    try
                    {
                        buffer = bufferPool.Allocate();
                        receiveBuffer = bufferPool.Allocate();

                        WebSocketReceiveResult receiveResult;
                        var totalMessageSize = 0;

                        do
                        {
                            receiveResult = await socketContextWebSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), callCancelled).ConfigureAwait(false);

                            if (receiveResult.MessageType == WebSocketMessageType.Close)
                            {
                                return;
                            }

                            Buffer.BlockCopy(receiveBuffer, 0, buffer, totalMessageSize, receiveResult.Count);
                            totalMessageSize += receiveResult.Count;

                        } while (!receiveResult.EndOfMessage);

                        using (var reader = new BinaryReader(new MemoryStream(buffer, 0, totalMessageSize)))
                        {
                            var headersCount = reader.ReadInt32();
                            headers = new Dictionary<string, string>(headersCount);

                            for (var i = 0; i < headersCount; i++)
                            {
                                headers[reader.ReadString()] = reader.ReadString();
                            }

                            body = reader.ReadBytes(reader.ReadInt32());
                        }
                    }
                    finally
                    {
                        bufferPool.Free(buffer);
                        bufferPool.Free(receiveBuffer);
                    }

                    var transportMessage = new TransportMessage(headers[Headers.MessageId], headers)
                    {
                        Body = body
                    };

                    await Save(transportMessage).ConfigureAwait(false);

                    await Console.Out.WriteLineAsync("Message saved");
                }
            }
        }

        private static bool IsWebSocketRequest(IOwinRequest request)
        {
            var isUpgrade = IsHeaderEqual(request, "Connection", "Upgrade,Keep-Alive") || IsHeaderEqual(request, "Connection", "Upgrade");
            var isWebSocket = IsHeaderEqual(request, "Upgrade", "WebSocket");
            return isUpgrade & isWebSocket;
        }

        private static bool IsHeaderEqual(IOwinRequest request, string header, string value)
        {
            string[] headerValues;
            return request.Headers.TryGetValue(header, out headerValues) && headerValues.Length == 1
                   && string.Equals(value, headerValues[0], StringComparison.OrdinalIgnoreCase);
        }

        private async Task Save(TransportMessage message)
        {
            var entity = ConvertToSaveMessage(message);
            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(entity).ConfigureAwait(false);
                await session.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        private ProcessedMessage ConvertToSaveMessage(TransportMessage message)
        {
            var receivedMessage = new ImportSuccessfullyProcessedMessage(message);

            foreach (var enricher in enrichers.Value)
            {
                enricher.Enrich(receivedMessage);
            }

            var auditMessage = new ProcessedMessage(receivedMessage)
            {
                // We do this so Raven does not spend time assigning a hilo key
                Id = $"ProcessedMessages/{Guid.NewGuid()}"
            };
            return auditMessage;
        }
    }
}
