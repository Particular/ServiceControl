namespace ServiceControl.Persistence.EFCore.Implementation.BodyStorage;

public interface IDriveSpaceProvider
{
    DriveSpace GetDriveSpace(string pathRoot);
}

public readonly record struct DriveSpace(string Name, long AvailableFreeSpace, long TotalSize);
