namespace ServiceControl.Persistence.EFCore.Abstractions;

public sealed class AzureBlobBodyStorageSettings : BodyStorageSettings
{
    public required AzureBlobAuthentication Authentication { get; set; }
    public string ContainerName { get; set; } = "error-bodies";
}

// Shared-key and managed-identity auth are mutually exclusive, and the managed identity options are
// meaningless alongside a connection string.
public abstract class AzureBlobAuthentication;

public sealed class AzureBlobSharedKeyAuthentication : AzureBlobAuthentication
{
    public required string ConnectionString { get; set; }
}

public sealed class AzureBlobManagedIdentityAuthentication : AzureBlobAuthentication
{
    public required Uri ServiceUri { get; set; }
    public string? ClientId { get; set; }

    // Steers the login endpoint for sovereign clouds; when unset the SDK honours the
    // AZURE_AUTHORITY_HOST environment variable.
    public Uri? AuthorityHost { get; set; }
}
