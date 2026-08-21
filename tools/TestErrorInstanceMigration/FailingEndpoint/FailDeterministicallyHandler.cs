public sealed class FailDeterministicallyHandler : IHandleMessages<FailDeterministically>
{
    public Task Handle(FailDeterministically message, IMessageHandlerContext context) =>
        throw new SimulatedDeterministicFailure($"Error {message.ErrorId} is expected to fail on every attempt.");
}