using NServiceBus;
using NServiceBus.Logging;

public class MyHandler1: IHandleMessages<Message1>
{
    static ILog logger = LogManager.GetLogger(typeof(MyHandler1));

    public void Handle(Message1 message)
    {
        logger.Info("Hello from MyHandler1");
    }

}