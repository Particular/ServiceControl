using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using NServiceBus;

public class FailingHandler : IHandleMessages<PerformSomeTaskThatFails>
{
    public void Handle(PerformSomeTaskThatFails message)
    {
        switch (message.Id%4)
        {
            case 0:
                throw new IOException("The disk is full");
            case 1:
                throw new WebException("The web API isn't responding");
            case 2:
                throw new SerializationException("Cannot deserialize message");
            default:
                throw new Exception("Some business thing happened");
        }
    }
}