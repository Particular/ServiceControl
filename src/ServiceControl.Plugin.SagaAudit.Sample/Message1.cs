using System;
using NServiceBus;

public class Message1 : IMessage
{
    public Guid SomeId { get; set; }
}
public class Message2 : IMessage
{
}