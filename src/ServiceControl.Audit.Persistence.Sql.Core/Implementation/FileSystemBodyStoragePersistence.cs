namespace ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

using System.IO.Compression;
using Abstractions;

public class FileSystemBodyStoragePersistence(AuditSqlPersisterSettings settings) : IBodyStoragePersistence
{
    const int FormatVersion = 1;

    public async Task WriteBodyAsync(
        string bodyId,
        ReadOnlyMemory<byte> body,
        string contentType,
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        var batchFolder = Path.Combine(settings.MessageBodyStoragePath, batchId.ToString());
        var filePath = Path.Combine(batchFolder, $"{bodyId}.body");

        // Bodies are immutable - skip if file already exists
        if (File.Exists(filePath))
        {
            return;
        }

        Directory.CreateDirectory(batchFolder);

        // Write to temp file first for atomic operation
        var tempFilePath = filePath + ".tmp";

        try
        {
            var fileStream = new FileStream(
                tempFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);

            await using (fileStream.ConfigureAwait(false))
            {
                using var writer = new BinaryWriter(fileStream, System.Text.Encoding.UTF8, leaveOpen: true);

                var shouldCompress = body.Length >= settings.MinBodySizeForCompression;

                // Write header
                writer.Write(FormatVersion);
                writer.Write(contentType);
                writer.Write(body.Length); // Original uncompressed size
                writer.Write(shouldCompress);
                writer.Write(Guid.NewGuid().ToString()); // Generate ETag

                // Flush the header before writing body
                writer.Flush();

                // Write body (compressed or not)
                if (shouldCompress)
                {
                    var brotliStream = new BrotliStream(fileStream, CompressionLevel.Fastest, leaveOpen: true);
                    await using (brotliStream.ConfigureAwait(false))
                    {
                        await brotliStream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    await fileStream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
                }

                await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            // Atomic rename
            File.Move(tempFilePath, filePath, overwrite: false);
        }
        catch
        {
            // Clean up temp file if it exists
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            throw;
        }
    }

    public Task<MessageBodyFileResult?> ReadBodyAsync(string bodyId, Guid batchId, CancellationToken cancellationToken = default)
    {
        var batchFolder = Path.Combine(settings.MessageBodyStoragePath, batchId.ToString());
        var filePath = Path.Combine(batchFolder, $"{bodyId}.body");

        if (!File.Exists(filePath))
        {
            return Task.FromResult<MessageBodyFileResult?>(null);
        }

        try
        {
            var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            var reader = new BinaryReader(fileStream, System.Text.Encoding.UTF8, leaveOpen: true);

            // Read header
            var formatVersion = reader.ReadInt32();
            if (formatVersion != FormatVersion)
            {
                fileStream.Dispose();
                throw new InvalidOperationException($"Unsupported body file format version: {formatVersion}");
            }

            var contentType = reader.ReadString();
            var bodySize = reader.ReadInt32();
            var isCompressed = reader.ReadBoolean();
            var etag = reader.ReadString();

            // Create appropriate stream wrapper for body data
            Stream bodyStream = fileStream;
            if (isCompressed)
            {
                bodyStream = new BrotliStream(fileStream, CompressionMode.Decompress, leaveOpen: false);
            }

            var result = new MessageBodyFileResult
            {
                Stream = bodyStream,
                ContentType = contentType,
                BodySize = bodySize,
                Etag = etag
            };

            return Task.FromResult<MessageBodyFileResult?>(result);
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult<MessageBodyFileResult?>(null);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Failed to read body file for {bodyId}", ex);
        }
    }

    public Task DeleteBatches(IEnumerable<Guid> batchIds, CancellationToken cancellationToken = default)
    {
        foreach (var batchId in batchIds)
        {
            DeleteBatchFolder(batchId);
        }

        return Task.CompletedTask;
    }

    void DeleteBatchFolder(Guid batchId)
    {
        var batchFolder = Path.Combine(settings.MessageBodyStoragePath, batchId.ToString());

        try
        {
            if (Directory.Exists(batchFolder))
            {
                Directory.Delete(batchFolder, recursive: true);
            }
        }
        catch (DirectoryNotFoundException)
        {
            // Already deleted, ignore
        }
        catch (FileNotFoundException)
        {
            // Already deleted, ignore
        }
    }

}
