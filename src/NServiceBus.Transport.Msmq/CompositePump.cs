namespace NServiceBus.Transport.Msmq
{
    using System;
    using System.Threading.Tasks;

    class CompositePump : IPushMessages
    {
        readonly IPushMessages mainPump;
        readonly IPushMessages delayedDeliveryPump;

        public CompositePump(IPushMessages mainPump, IPushMessages delayedDeliveryPump)
        {
            this.mainPump = mainPump;
            this.delayedDeliveryPump = delayedDeliveryPump;
        }

        public async Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            await mainPump.Init(onMessage, onError, criticalError, settings).ConfigureAwait(false);
            if (delayedDeliveryPump != null)
            {
                await delayedDeliveryPump.Init(onMessage, onError, criticalError, settings).ConfigureAwait(false);
            }
        }

        public void Start(PushRuntimeSettings limitations)
        {
            mainPump.Start(limitations);
            //Delayed delivery pump always uses default concurrency settings
            delayedDeliveryPump?.Start(PushRuntimeSettings.Default);
        }

        public async Task Stop()
        {
            await mainPump.Stop().ConfigureAwait(false);
            if (delayedDeliveryPump != null)
            {
                await delayedDeliveryPump.Stop().ConfigureAwait(false);
            }
        }
    }
}