namespace ServiceControl.Config
{
    using System;
    using System.Diagnostics;
    using UI.MessageBox;

    public partial class App
    {
        public App()
        {
            InitializeComponent();

            if (!Debugger.IsAttached)
            {
                ExceptionMessageBox.Attach();
            }

            StandardPopup.ApplyDefaultAlignment();
        }

        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Splash.Show();
                var app = new App();
                app.Run();
            }
        }
    }
}