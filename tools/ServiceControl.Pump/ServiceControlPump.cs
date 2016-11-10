namespace ServiceControl.Pump
{
    using System;
    using System.IO;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Transport;
    using Roslyn.Utilities;

    public class ServiceControlPump : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            var pushRuntimeSettings = new PushRuntimeSettings(150);
            context.Container.RegisterSingleton(new Transmit(pushRuntimeSettings.MaxConcurrency));

            var qualifiedAddress = context.Settings.LogicalAddress().CreateQualifiedAddress("ServiceControl");
            var transportAddress = context.Settings.GetTransportAddress(qualifiedAddress);
            context.AddSatelliteReceiver("ServiceControl pump master", transportAddress, TransportTransactionMode.ReceiveOnly, pushRuntimeSettings, RecoverabilityPolicy, OnMessage);
        }

        RecoverabilityAction RecoverabilityPolicy(RecoverabilityConfig recoverabilityConfig, ErrorContext errorContext)
        {
            return RecoverabilityAction.ImmediateRetry();
        }

        Task OnMessage(IBuilder builder, MessageContext messageContext)
        {
            return builder.Build<Transmit>().Send(messageContext);
        }

        class Transmit : IDisposable
        {
            readonly ObjectPool<byte[]> bufferPool;
            readonly ClientWebSocketCache socketPool = new ClientWebSocketCache();

            public Transmit(int maxConcurrency)
            {
                bufferPool = new ObjectPool<byte[]>(() => new byte[4 * 1024 * 1024], maxConcurrency);
            }

            public async Task Send(MessageContext message)
            {
                var headers = message.Headers;
                byte[] buffer = null;
                ClientWebSocket socket = null;

                try
                {
                    buffer = bufferPool.Allocate();
                    socket = await socketPool.GetClient(message.ReceiveCancellationTokenSource.Token);

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
                        bufferPool.Free(buffer);
                    }

                    if (socket != null)
                    {
                        await socketPool.ReleaseClient(socket, message.ReceiveCancellationTokenSource.Token);
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
