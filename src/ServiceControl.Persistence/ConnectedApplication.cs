namespace ServiceControl.Persistence;

using Infrastructure;

public class ConnectedApplication
{
    public string Name { get; set; }
    public bool SupportsHeartbeats { get; set; }

    public const string CollectionName = "ConnectedApplications";

    public static string MakeDocumentId(string name) => $"{CollectionName}/{DeterministicGuid.MakeId(name)}";
}