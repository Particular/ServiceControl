namespace ServiceControl.Persistence.BodyStorage;

public sealed class FileSystemBodyStorageSettings : BodyStorageSettings
{
    public const int DefaultDataSpaceRemainingThreshold = 15;

    public required string StoragePath { get; set; }

    public int DataSpaceRemainingThreshold { get; set; } = DefaultDataSpaceRemainingThreshold;
}
