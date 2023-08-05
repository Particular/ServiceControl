using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using ServiceControl.PersistenceTests;

#if DEBUG
static class RavenStudioHelper // TODO: Must be removed when done
{
    public static void LaunchAndBlock()
    {
        var url = $"http://localhost:{TestPersistenceImpl.DatabaseMaintenancePort}/studio/index.html#databases/documents?&database=%3Csystem%3E";

        if (!Debugger.IsAttached)
        {
            return;
        }

        OpenUrl(url);

        while (true)
        {
            Thread.Sleep(5000);
            Trace.Write("Waiting for debugger pause");
        }
    }

    static void OpenUrl(string url)
    {
        try
        {
            Process.Start(url);
        }
        catch
        {
            // hack because of this: https://github.com/dotnet/corefx/issues/10361
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                throw;
            }
        }
    }
}
#endif