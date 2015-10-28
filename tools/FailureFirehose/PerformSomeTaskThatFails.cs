using NServiceBus;

public class PerformSomeTaskThatFails : ICommand
{
    public int Id { get; set; }
}