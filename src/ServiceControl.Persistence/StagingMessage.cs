namespace ServiceControl.Persistence
{
    using ServiceControl.MessageFailures;

    /// <summary>
    /// A message a batch is about to stage, with the number of times staging it has already failed.
    /// </summary>
    public record StagingMessage(FailedMessage Message, int StageAttempts);
}
