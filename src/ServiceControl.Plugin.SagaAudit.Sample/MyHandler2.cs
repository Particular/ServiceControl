using NServiceBus;
using NServiceBus.Logging;

public class MyHandler2: IHandleMessages<Message2>
{
    static ILog logger = LogManager.GetLogger(typeof(MyHandler2));

    public void Handle(Message2 message)
    {
        logger.Info("Hello from MyHandler2");
    }

}