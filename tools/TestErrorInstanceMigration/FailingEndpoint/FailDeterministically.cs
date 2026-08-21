public sealed class FailDeterministically : IMessage
{
    public Guid ErrorId { get; set; }
    public string RandomPayload { get; set; } = string.Empty;
}