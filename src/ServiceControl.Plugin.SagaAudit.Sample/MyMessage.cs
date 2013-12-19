using System;
using NServiceBus;

public class MyMessage : IMessage
{
    public Guid SomeId { get; set; }
}