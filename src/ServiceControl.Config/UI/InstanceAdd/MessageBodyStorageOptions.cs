namespace ServiceControl.Config.UI.InstanceAdd;

using System.Linq;
using System.Windows.Input;
using Commands;
using Extensions;
using ServiceControlInstaller.Engine.Instances;

// Instances added from the wizard always store message bodies on the file system.
public class MessageBodyStorageOptions
{
    public MessageBodyStorageOptions() => SelectPath = new SelectPathCommand(p => Path = p, isFolderPicker: true, defaultPath: Path);

    public static bool IsSupportedBy(PersistenceManifest manifest) => manifest?.Settings.Any(s => s.Name.EndsWith("/MessageBody/StorageType")) == true;

    public string Path
    {
        get => path;
        set => path = value.SanitizeFilePath();
    }

    public ICommand SelectPath { get; }

    // Returns null for storage types that keep message bodies in the database.
    public MessageBodyStorage Create(PersistenceManifest manifest) => IsSupportedBy(manifest)
        ? new MessageBodyStorage { StorageType = MessageBodyStorageType.FileSystem, FileSystemStoragePath = Path }
        : null;

    string path;
}
