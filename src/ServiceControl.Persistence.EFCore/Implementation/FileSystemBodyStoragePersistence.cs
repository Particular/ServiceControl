namespace ServiceControl.Persistence.EFCore.Implementation;

using System.IO.Compression;
using System.Text;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Infrastructure;

// Bodies are immutable and keyed by bodyId alone, so a re-failure resolves to the same file and an
// existing one is left untouched. 
public class FileSystemBodyStoragePersistence(EFPersisterSettings settings) : IBodyStoragePersistence
{
    const int FormatVersion = 1;

    string StoragePath => settings.MessageBodyStoragePath!;

    public async Task WriteBody(string bodyId, ReadOnlyMemory<byte> body, string contentType, CancellationToken cancellationToken = default)
    {
        var filePath = GetBodyFilePath(bodyId);

        if (File.Exists(filePath))
        {
            return;
        }

        // A unique temp name lets concurrent writers of the same body race without clobbering.
        var tempFilePath = $"{filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            var fileStream = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
            await using (fileStream.ConfigureAwait(false))
            {
                var shouldCompress = body.Length >= settings.MinBodySizeForCompression;

                using (var writer = new BinaryWriter(fileStream, Encoding.UTF8, leaveOpen: true))
                {
                    writer.Write(FormatVersion);
                    writer.Write(contentType);
                    writer.Write(body.Length);
                    writer.Write(shouldCompress);
                    writer.Flush();
                }

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

            try
            {
                File.Move(tempFilePath, filePath, overwrite: false);
            }
            catch (IOException) when (File.Exists(filePath))
            {
                // A concurrent writer already produced the (immutable) body; discard our copy.
                TryDelete(tempFilePath);
            }
        }
        catch
        {
            TryDelete(tempFilePath);
            throw;
        }
    }

    public Task<MessageBodyFileResult?> ReadBody(string bodyId, CancellationToken cancellationToken = default)
    {
        var filePath = GetBodyFilePath(bodyId);

        if (!File.Exists(filePath))
        {
            return Task.FromResult<MessageBodyFileResult?>(null);
        }

        FileStream? fileStream = null;
        try
        {
            fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);

            var reader = new BinaryReader(fileStream, Encoding.UTF8, leaveOpen: true);

            var formatVersion = reader.ReadInt32();
            if (formatVersion != FormatVersion)
            {
                throw new InvalidOperationException($"Unsupported body file format version {formatVersion} for {bodyId}.");
            }

            var contentType = reader.ReadString();
            var bodySize = reader.ReadInt32();
            var isCompressed = reader.ReadBoolean();

            // The returned stream owns fileStream and is disposed by the caller.
            Stream bodyStream = isCompressed
                ? new BrotliStream(fileStream, CompressionMode.Decompress, leaveOpen: false)
                : fileStream;

            return Task.FromResult<MessageBodyFileResult?>(new MessageBodyFileResult
            {
                Stream = bodyStream,
                ContentType = contentType,
                BodySize = bodySize
            });
        }
        catch (FileNotFoundException)
        {
            fileStream?.Dispose();
            return Task.FromResult<MessageBodyFileResult?>(null);
        }
        catch
        {
            fileStream?.Dispose();
            throw;
        }
    }

    public Task DeleteBody(string bodyId, CancellationToken cancellationToken = default)
    {
        TryDelete(GetBodyFilePath(bodyId));
        return Task.CompletedTask;
    }

    string GetBodyFilePath(string bodyId) => Path.Combine(StoragePath, $"{bodyId}.body");

    static void TryDelete(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch (DirectoryNotFoundException)
        {
            // Nothing to delete.
        }
    }
}
