namespace Particular.ServiceControl
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Reflection;
    using Commands;
    using Hosting;
    using Raven.Abstractions;

    public class Program
    {
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) => ResolveAssembly(e.Name);

            var arguments = new HostArguments(args);

            if (arguments.Help)
            {
                arguments.PrintUsage();
                return;
            }

            new CommandRunner(arguments.Commands).Execute(arguments);
        }

        static Assembly ResolveAssembly(string name)
        {
            var assemblyLocation = Assembly.GetEntryAssembly().Location;
            var appDirectory = Path.GetDirectoryName(assemblyLocation);
            var requestingName = new AssemblyName(name).Name;

            // ReSharper disable once AssignNullToNotNullAttribute
            var combine = Path.Combine(appDirectory, $"{requestingName}.dll");
            if (!File.Exists(combine))
            {
                return null;
            }

            if (name == "metrics, Version=1.0.0.0, Culture=neutral, PublicKeyToken=ca6c6ef570198eba")
            {
                using (var stream = LoadStream())
                {
                    var rawAssembly = ReadStream(stream);
                    return Assembly.Load(rawAssembly);
                }
            }

            return Assembly.LoadFrom(combine);
        }

        private static Stream LoadStream()
        {
            var assembly = typeof(SystemTime).Assembly;

            using (var manifestResourceStream = assembly.GetManifestResourceStream("costura.metrics.dll.zip"))
            {
                using (DeflateStream deflateStream = new DeflateStream(manifestResourceStream, CompressionMode.Decompress))
                {
                    var memoryStream = new MemoryStream();
                    CopyTo(deflateStream, memoryStream);
                    memoryStream.Position = 0L;
                    return memoryStream;
                }
            }
        }

        private static void CopyTo(Stream source, Stream destination)
        {
            var buffer = new byte[81920];
            int count;
            while ((count = source.Read(buffer, 0, buffer.Length)) != 0)
            {
                destination.Write(buffer, 0, count);
            }
        }

        private static byte[] ReadStream(Stream stream)
        {
            var buffer = new byte[stream.Length];
            stream.Read(buffer, 0, buffer.Length);
            return buffer;
        }
    }
}