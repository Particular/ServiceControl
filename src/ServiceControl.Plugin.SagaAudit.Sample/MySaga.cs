using NServiceBus.Logging;
using NServiceBus.Saga;

public class MySaga : Saga<MySagaData>, IAmStartedByMessages<Message1>
{
    static ILog logger = LogManager.GetLogger(typeof(MySaga));


    public override void ConfigureHowToFindSaga()
    {
        ConfigureMapping<Message1>(m => m.SomeId)
            .ToSaga(s => s.SomeId);
    }

    public void Handle(Message1 message)
    {
        logger.Info("Hello from MySaga");
        Bus.SendLocal(new Message2());
    }

}