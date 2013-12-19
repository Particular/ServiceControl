using System.Threading;
using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.Saga;

public class MySaga : Saga<MySagaData>, IAmStartedByMessages<MyMessage>
{
    static ILog logger = LogManager.GetLogger(typeof(MySaga));


    public override void ConfigureHowToFindSaga()
    {
        ConfigureMapping<MyMessage>(m => m.SomeId)
            .ToSaga(s => s.SomeId);
    }

    public void Handle(MyMessage message)
    {
        logger.Info("Hello from MySaga");
    }

}
public class MyHandler: IHandleMessages<MyMessage>
{
    static ILog logger = LogManager.GetLogger(typeof(MyHandler));

    public void Handle(MyMessage message)
    {
        logger.Info("Hello from MyHandler");
        Thread.Sleep(10000);
    }

}