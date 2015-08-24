namespace Particular.ServiceControl
{
    using System;
    using System.IO;
    using System.Reflection;
    using Commands;
    using Hosting;
    using Raven.Database;
    using System.IO.Compression;

    public class Program
    {
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) => ResolveAssembly(e);

            var arguments = new HostArguments(args);

            if (arguments.Help)
            {
                arguments.PrintUsage();
                return;
            }

            new CommandRunner(arguments.Commands).Execute(arguments);
        }

        static Assembly ResolveAssembly( ResolveEventArgs args )
        {
            var assemblyLocation = Assembly.GetEntryAssembly().Location;
            var appDirectory = Path.GetDirectoryName( assemblyLocation );
            var requestingName = new AssemblyName( args.Name ).Name;

            //if( args.Name.StartsWith( "Metrics" ) )
            //{
            //    requestingName = requestingName.Replace("Metrics", "Metrics.net");
            //}

            if( args.Name.StartsWith( "metrics" ) )
            {
                using( var resourceStream = typeof( DocumentDatabase ).Assembly.GetManifestResourceStream( "costura.metrics.dll.zip" ) )
                using( var compressStream = new DeflateStream( resourceStream, CompressionMode.Decompress ) )
                {
                    using (var memStream = new MemoryStream())
                    {
                        compressStream.CopyTo(memStream);
                        memStream.Position = 0;
                        var a= Assembly.Load(memStream.ToArray());

                        return a;
                    }
                }
            }

            // ReSharper disable once AssignNullToNotNullAttribute
            var combine = Path.Combine( appDirectory, requestingName + ".dll" );
            if( !File.Exists( combine ) )
            {
                return null;
            }
            return Assembly.LoadFrom( combine );
        }
    }
}