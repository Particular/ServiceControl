namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    interface IStateMachineContext<TState>
    {
        TState State { get; set; }
    }
}