namespace ServiceControl.LearningTransport
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using Janitor;

    static class FileOps
    {
        public static void WriteBytes(string filePath, byte[] bytes, bool allowNullBytes = false)
        {
            if (bytes == null)
            {
                if (!allowNullBytes)
                {
                    throw new Exception("bytes array is null");
                }

                using (new WriteLock(filePath))
                {
                    File.Create(filePath).Dispose();
                }
                return;
            }

            using (new WriteLock(filePath))
            {
                using (var stream = CreateWriteStream(filePath, FileMode.Create))
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
            }
        }

        //write to temp file first so we can do a atomic move
        public static void WriteTextAtomic(string targetPath, string text)
        {
            var tempFile = Path.GetTempFileName();
            var bytes = Encoding.UTF8.GetBytes(text);

            try
            {
                using (var stream = CreateWriteStream(tempFile, FileMode.Open))
                {
                    stream.Write(bytes, 0, bytes.Length);
                }
            }
            catch
            {
                File.Delete(tempFile);
                throw;
            }

            using (new WriteLock(targetPath))
            {
                File.Move(tempFile, targetPath);
            }
        }

        public static string ReadText(string filePath)
        {
            using (new ReadLock(filePath))
            {
                using (var stream = new StreamReader(CreateReadStream(filePath), Encoding.UTF8))
                {
                    var result = stream.ReadToEnd();

                    return result;
                }
            }
        }

        public static byte[] ReadBytes(string filePath)
        {
            using (new ReadLock(filePath))
            {
                using (var stream = CreateReadStream(filePath))
                {
                    var length = (int) stream.Length;
                    var body = new byte[length];
                    stream.Read(body, 0, length);

                    return body;
                }
            }
        }

        public static void Move(string sourcePath, string targetPath)
        {
            using (new WriteLock(sourcePath))
            {
                using (new WriteLock(targetPath))
                {
                    File.Move(sourcePath, targetPath);
                }
            }
        }

        public static void Delete(string filePath)
        {
            using (new WriteLock(filePath))
            {
                File.Delete(filePath);
            }
        }

        public static void WriteText(string filePath, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);

            WriteBytes(filePath, bytes);
        }

        static FileStream CreateReadStream(string filePath)
        {
            return new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096);
        }

        static FileStream CreateWriteStream(string filePath, FileMode fileMode)
        {
            return new FileStream(filePath, fileMode, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        }

        [SkipWeaving]
        class ReadLock : IDisposable
        {
            public ReadLock(string filePath)
            {
                readHandle = new EventWaitHandle(true, EventResetMode.AutoReset, CalculateDeterministicId(filePath, "READ"));
                readHandle.WaitOne();
            }

            protected static string CalculateDeterministicId(params object[] data)
            {
                // use MD5 hash to get a 16-byte hash of the string
                using (var provider = new MD5CryptoServiceProvider())
                {
                    var inputBytes = Encoding.Default.GetBytes(String.Concat(data));
                    var hashBytes = provider.ComputeHash(inputBytes);
                    // generate a guid from the hash:
                    return new Guid(hashBytes).ToString();
                }
            }

            public virtual void Dispose()
            {
                readHandle?.Set();
                readHandle?.Dispose();
            }

            EventWaitHandle readHandle;
        }

        [SkipWeaving]
        class WriteLock : ReadLock, IDisposable
        {
            public WriteLock(string filePath) : base(filePath)
            {
                writeHandle = new EventWaitHandle(true, EventResetMode.AutoReset, CalculateDeterministicId(filePath, "WRITE"));
                writeHandle.WaitOne();
            }

            public new void Dispose()
            {
                base.Dispose();
                writeHandle?.Set();
                writeHandle?.Dispose();
            }

            EventWaitHandle writeHandle;
        }

    }
}
