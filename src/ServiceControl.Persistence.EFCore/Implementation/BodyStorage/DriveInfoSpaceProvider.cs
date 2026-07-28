namespace ServiceControl.Persistence.EFCore.Implementation.BodyStorage;

public class DriveInfoSpaceProvider : IDriveSpaceProvider
{
    public DriveSpace GetDriveSpace(string pathRoot)
    {
        var driveInfo = new DriveInfo(pathRoot);

        return new DriveSpace(driveInfo.Name, driveInfo.AvailableFreeSpace, driveInfo.TotalSize);
    }
}
