namespace ServiceControl.Transports
{
    using System;

    public class QueueLengthStoreDto
    {
        readonly Action<EntryDto[], EndpointInputQueueDto> store;

        public QueueLengthStoreDto(Action<EntryDto[], EndpointInputQueueDto> store)
        {
            this.store = store;
        }
        public void Store(EntryDto[] entry, EndpointInputQueueDto instance)
        {
            store(entry, instance);
        }
    }
}