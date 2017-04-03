namespace ServiceControl.Config
{
    using System;
    using System.Diagnostics;
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
            Splash.Show();
            if (args.Length == 0)
            {
                var app = new App();
                app.Run();
            }
        }
    }
}