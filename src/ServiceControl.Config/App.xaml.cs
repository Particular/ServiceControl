namespace ServiceControl.Config
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using ServiceControl.Config.UI.MessageBox;

    public partial class App
    {
        public App()
        {
            InitializeComponent();

            if (!Debugger.IsAttached)
                ExceptionMessageBox.Attach();
        }

        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) => ResolveAssembly(e.Name);

            Splash.Show();
            if (args.Length == 0)
            {
                var app = new App();
                app.Run();
            }
        }

        static Assembly ResolveAssembly(string name)
        {
            var assemblyLocation = Assembly.GetEntryAssembly().Location;
            var appDirectory = Path.GetDirectoryName(assemblyLocation);
            var requestingName = new AssemblyName(name).Name;

            // ReSharper disable once AssignNullToNotNullAttribute
            var combine = Path.Combine(appDirectory, requestingName + ".dll");
            if (!File.Exists(combine))
            {
                return null;
            }
            return Assembly.LoadFrom(combine);
        }
    }
}