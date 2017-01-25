namespace ServiceControl.Infrastructure.OWIN
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Metrics;
    using NServiceBus;
    using Raven.Client;
    using ServiceControl.MessageAuditing;
    using ServiceControl.Operations;
    using ServiceControl.Operations.BodyStorage;

    class WebSocketReceiver
    {
        private readonly string webSocketContextKey = typeof(WebSocketContext).FullName;
        private readonly Metrics.Timer timer = Metric.Context("Processing").Timer("Audit Messages", Unit.Custom("Messages"));
        private readonly Metrics.Timer timer2 = Metric.Context("Saving").Timer("Audit Messages", Unit.Custom("Messages"));
        private readonly BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher;
        private readonly IDocumentStore store;
        private readonly IEnrichImportedMessages[] enrichers;
        private readonly PinnableBufferCache receivePool;

        public WebSocketReceiver(IDocumentStore store, BodyStorageFeature.BodyStorageEnricher bodyStorageEnricher, IEnrichImportedMessages[] enrichers, PinnableBufferCache receivePool)
        {
            this.store = store;
            this.bodyStorageEnricher = bodyStorageEnricher;
            this.enrichers = enrichers;
            this.receivePool = receivePool;
        }

        public async Task Receive(IDictionary<string, object> e)
        {
            var socketContext = (WebSocketContext)e[webSocketContextKey];
            var callCancelled = (CancellationToken)e["websocket.CallCancelled"];

            try
            {
                await Loop(socketContext.WebSocket, callCancelled);

                await Console.Out.WriteLineAsync("websocket.CallCancelled = true");

            }
            catch (OperationCanceledException)
            {
                await Console.Out.WriteLineAsync("websocket.CallCancelled = true with exception");

                if (!callCancelled.IsCancellationRequested)
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync(ex.ToString());
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

                    await Console.Out.WriteLineAsync("closed socket");
                }
            }
        }

        private async Task Loop(WebSocket socketContextWebSocket, CancellationToken callCancelled)
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
                        buffer = receivePool.AllocateBuffer();
                        receiveBuffer = receivePool.AllocateBuffer();

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
                        receivePool.FreeBuffer(buffer);
                        receivePool.FreeBuffer(receiveBuffer);
                    }

                    var intent = MessageIntentEnum.Send;
                    string str;
                    if (headers.TryGetValue(Headers.MessageIntent, out str))
                    {
                        Enum.TryParse(str, true, out intent);
                    }
                    var entity = ConvertToSaveMessage(headers[Headers.MessageId], intent, headers, body);
                    using (timer2.NewContext())
                    {
                        await Save(entity).ConfigureAwait(false);
                    }
                    await Console.Out.WriteLineAsync("Message saved");
                }
            }
        }

        private async Task Save(ProcessedMessage entity)
        {
            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(entity).ConfigureAwait(false);
                await session.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        private ProcessedMessage ConvertToSaveMessage(string messageId, MessageIntentEnum intent, Dictionary<string, string> headers, byte[] body)
        {
            var metadata = new Dictionary<string, object>
            {
                ["MessageId"] = messageId,
                ["MessageIntent"] = intent,
                ["HeadersForSearching"] = string.Join(" ", headers.Values)
            };

            foreach (var enricher in enrichers)
            {
                enricher.Enrich(headers, metadata);
            }

            bodyStorageEnricher.StoreAuditMessageBody(
                body,
                headers,
                metadata);

            var auditMessage = new ProcessedMessage(headers, metadata)
            {
                // We do this so Raven does not spend time assigning a hilo key
                Id = $"ProcessedMessages/{Guid.NewGuid()}"
            };
            return auditMessage;
        }
    }
}