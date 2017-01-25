namespace ServiceControl.Infrastructure.OWIN
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using Microsoft.Owin;
    using Raven.Client;
    using ServiceControl.Operations;
    using ServiceControl.Operations.BodyStorage;

    internal class WebSocketMiddleware : OwinMiddleware
    {
        private readonly IDocumentStore store;
        private readonly Lazy<IEnrichImportedMessages[]> enrichers;
        private readonly PinnableBufferCache receivePool = new PinnableBufferCache("Receive pool", 4 * 1024 * 1024);
        private readonly PinnableBufferCache upgradePool = new PinnableBufferCache("Upgrade pool", 3 * 1024);
        private readonly Lazy<BodyStorageFeature.BodyStorageEnricher> bodyStorageEnricher;

        public WebSocketMiddleware(OwinMiddleware next, IContainer container) : base(next)
        {
            bodyStorageEnricher = new Lazy<BodyStorageFeature.BodyStorageEnricher> (() => container.Resolve<BodyStorageFeature.BodyStorageEnricher>());
            store = container.Resolve<IDocumentStore>();
            enrichers = new Lazy<IEnrichImportedMessages[]>(() => container.Resolve<IEnumerable<IEnrichImportedMessages>>().ToArray());
        }

        public override Task Invoke(IOwinContext context)
        {
            var accept = context.Get<Action<IDictionary<string, object>, Func<IDictionary<string, object>, Task>>>("websocket.Accept");

            if (accept != null)
            {
                try
                {
                    var buffer = upgradePool.AllocateBuffer();
                    accept(new Dictionary<string, object>
                    {
                        {"websocket.ReceiveBufferSize", 1024},
                        {"websocket.Buffer", new ArraySegment<byte>(buffer)}
                    }, e  =>
                    {
                        return new WebSocketReceiver(store, bodyStorageEnricher.Value, enrichers.Value, receivePool)
                            .Receive(e)
                            .ContinueWith(_ => upgradePool.FreeBuffer(buffer), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.DenyChildAttach, TaskScheduler.Default);
                    });
                }
                catch (Exception ex)
                {
                    Console.Out.WriteLine(ex);
                }
            }
            else
            {
                context.Response.StatusCode = 400;
            }

            return Task.FromResult(0);
        }
    }
}
