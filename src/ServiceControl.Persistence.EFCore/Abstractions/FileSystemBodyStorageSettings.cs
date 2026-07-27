namespace ServiceControl.Persistence.EFCore.Abstractions;

public sealed class FileSystemBodyStorageSettings : BodyStorageSettings
{
    public required string StoragePath { get; set; }
}
