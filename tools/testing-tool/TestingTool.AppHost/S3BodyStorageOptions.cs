namespace TestingTool.AppHost;

/// <summary>
/// S3 body storage settings from the --s3-* command-line options. Without keys, the container uses the AWS default credential chain.
/// </summary>
public record S3BodyStorageOptions(string Bucket, string? Region, string? ServiceUrl, string? AccessKeyId, string? SecretAccessKey)
{
    /// <returns>null when --s3-body-storage is not set.</returns>
    public static S3BodyStorageOptions? Parse(CliOptions options, bool azureBlobSelected)
    {
        var bucket = Read(options, "s3-body-storage");
        var region = Read(options, "s3-region");
        var serviceUrl = Read(options, "s3-service-url");
        var accessKeyId = Read(options, "s3-access-key-id");
        var secretAccessKey = Read(options, "s3-secret-access-key");

        if (bucket is null)
        {
            if (region is not null || serviceUrl is not null || accessKeyId is not null || secretAccessKey is not null)
            {
                throw new ArgumentException("--s3-region, --s3-service-url, --s3-access-key-id, and --s3-secret-access-key require --s3-body-storage.");
            }

            return null;
        }

        if (azureBlobSelected)
        {
            throw new ArgumentException("--azure-blob-body-storage and --s3-body-storage are mutually exclusive.");
        }

        if ((accessKeyId is null) != (secretAccessKey is null))
        {
            throw new ArgumentException("--s3-access-key-id and --s3-secret-access-key must be set together.");
        }

        if (serviceUrl is not null && !(Uri.TryCreate(serviceUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
        {
            throw new ArgumentException("--s3-service-url must be an absolute http or https URL.");
        }

        return new S3BodyStorageOptions(bucket, region, serviceUrl, accessKeyId, secretAccessKey);
    }

    static string? Read(CliOptions options, string key)
    {
        if (!options.TryGetValue(key, out var value))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"--{key} requires a non-blank value.") : value;
    }
}
