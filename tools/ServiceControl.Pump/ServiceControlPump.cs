namespace ServiceControl.Pump
{
    using System;
    using System.IO;
    using System.Net.WebSockets;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Transport;

    public class ServiceControlPump : Feature
    {
        readonly Transmit transmit = new Transmit();

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.RegisterSingleton(transmit); // This is a hack so the instance is disposed aspart of the container dispose

            var pushRuntimeSettings = new PushRuntimeSettings(30);
            var qualifiedAddress = context.Settings.LogicalAddress().CreateQualifiedAddress("ServiceControl");
            var transportAddress = context.Settings.GetTransportAddress(qualifiedAddress);

            context.AddSatelliteReceiver("ServiceControl pump master", transportAddress, TransportTransactionMode.ReceiveOnly, pushRuntimeSettings,
                (_, __) => RecoverabilityAction.ImmediateRetry(), (_, c) => transmit.Send(c));
        }

        class Transmit : IDisposable
        {
            readonly PinnableBufferCache bufferPool;
            readonly ClientWebSocketCache socketPool = new ClientWebSocketCache();

            public Transmit()
            {
                bufferPool = new PinnableBufferCache("Send pool", 4 * 1024 * 1024);
            }

            public async Task Send(MessageContext message)
            {
                var headers = message.Headers;
                byte[] buffer = null;
                ClientWebSocket socket = null;

                try
                {
                    buffer = bufferPool.AllocateBuffer();
                    socket = await socketPool.GetClient(message.ReceiveCancellationTokenSource.Token).ConfigureAwait(false);

                    using (var stream = new MemoryStream(buffer))
                    using (var writer = new BinaryWriter(stream))
                    {
                        writer.Write(headers.Count);

                        foreach (var header in headers)
                        {
                            writer.Write(header.Key);
                            writer.Write(header.Value ?? string.Empty);
                        }

                        writer.Write(message.Body.Length);
                        writer.Write(message.Body);

                        await socket.SendAsync(new ArraySegment<byte>(buffer, 0, (int) stream.Position),
                            WebSocketMessageType.Binary, true, message.ReceiveCancellationTokenSource.Token).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                finally
                {
                    if (buffer != null)
                    {
                        bufferPool.FreeBuffer(buffer);
                    }

                    if (socket != null)
                    {
                        await socketPool.ReleaseClient(socket, message.ReceiveCancellationTokenSource.Token).ConfigureAwait(false);
                    }
                }
            }

            public void Dispose()
            {
                socketPool.Dispose();
            }
        }
    }
}
