namespace ServiceControl.Persistence.EFCore.Abstractions;

public sealed class S3BodyStorageSettings : BodyStorageSettings
{
    public required string BucketName { get; set; }
    public string KeyPrefix { get; set; } = "error-bodies/";
    public string? Region { get; set; }
    public string? ServiceUrl { get; set; }

    // Null resolves the ambient IAM role through the SDK's default credential chain.
    public S3StaticCredentials? Credentials { get; set; }
}

public sealed class S3StaticCredentials
{
    public required string AccessKeyId { get; set; }
    public required string SecretAccessKey { get; set; }
}
