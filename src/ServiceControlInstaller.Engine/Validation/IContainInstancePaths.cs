namespace ServiceControlInstaller.Engine.Validation
{
    public interface IContainInstancePaths
    {
        string InstallPath { get; }
        string LogPath { get; set; }
        string DBPath { get; set; }
    }
}