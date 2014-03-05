using System;
using NServiceBus.Saga;

public class MySagaData : IContainSagaData
{
    public Guid Id { get; set; }
    public string Originator { get; set; }
    public string OriginalMessageId { get; set; }
    public Guid SomeId { get; set; }
}