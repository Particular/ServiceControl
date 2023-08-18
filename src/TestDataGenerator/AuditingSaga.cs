namespace TestDataGenerator
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;

    class AuditingSaga : Saga<AuditingSagaData>,
        IAmStartedByMessages<SagaMessage1>,
        IAmStartedByMessages<SagaMessage2>,
        IHandleTimeouts<MyCustomTimeout>
    {
        protected override void ConfigureHowToFindSaga(SagaPropertyMapper<AuditingSagaData> mapper)
        {
            // https://docs.particular.net/nservicebus/sagas/message-correlation
            mapper.MapSaga(saga => saga.CorrelationId)
                .ToMessage<SagaMessage1>(message => message.CorrelationId)
                .ToMessage<SagaMessage2>(message => message.CorrelationId);
        }

        public async Task Handle(SagaMessage1 message, IMessageHandlerContext context)
        {
            Data.Messages.Add(context.MessageId);
            await RequestTimeout(context, TimeSpan.FromSeconds(5), new MyCustomTimeout { Data = "From SagaMessage1" });

        }

        public async Task Handle(SagaMessage2 message, IMessageHandlerContext context)
        {
            Data.Messages.Add(context.MessageId);
            await RequestTimeout(context, TimeSpan.FromSeconds(5), new MyCustomTimeout { Data = "From SagaMessage2" });
        }

        public Task Timeout(MyCustomTimeout timeout, IMessageHandlerContext context)
        {
            Data.Messages.Add($"{context.MessageId} (Timeout {timeout.Data})");
            if (Data.Messages.Count >= 4)
            {
                MarkAsComplete();
            }
            return Task.CompletedTask;
        }
    }

    class AuditingSagaData : ContainSagaData
    {
        public string CorrelationId { get; set; }
        public List<string> Messages { get; set; } = new List<string>();
    }

    class SagaMessage1 : ICommand
    {
        public string CorrelationId { get; set; }
    }

    class SagaMessage2 : ICommand
    {
        public string CorrelationId { get; set; }
    }

    class MyCustomTimeout
    {
        public string Data { get; set; }
    }
}
