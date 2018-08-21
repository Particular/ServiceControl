namespace ServiceControl.Config.UI.MessageBox
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Threading;
    using Framework;
    using Framework.Commands;
    using PropertyChanged;
    using ICommand = System.Windows.Input.ICommand;

    [AddINotifyPropertyChangedInterface]
    public partial class ExceptionMessageBox : IProgressViewModel
    {
        static ExceptionMessageBox()
        {
        }

        public ExceptionMessageBox()
        {
            InitializeComponent();
        }

        public bool IncludeSystemInfo { get; set; }
        Exception Exception { get; set; }

        public ICommand ReportClick => new AwaitableDelegateCommand(CallReportClick);

        public string ProgressTitle { get; set; }
        public bool InProgress { get; set; }
        public string ProgressMessage { get; set; }
        public int ProgressPercent { get; set; }

        public static void Attach()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;
            Dispatcher.CurrentDispatcher.UnhandledException += CurrentDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskSchedulerUnobservedTaskException;
        }

        static void CurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                handleException(ex);
            }
        }

        static void CurrentDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            handleException(e.Exception);
            e.Handled = true;
        }

        static void TaskSchedulerUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            handleException(e.Exception);
        }

        static void ShowExceptionDialog(Exception ex)
        {
            reporter = new RaygunFeedback();

            var dialog = new ExceptionMessageBox
            {
                Exception = ex,
                ErrorDetails =
                {
                    Text = ex.ToString()
                }
            };

            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsLoaded)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            dialog.ShowDialog();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        void CopyClick(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(ErrorDetails.Text);
        }

        async Task CallReportClick(object sender)
        {
            ProgressTitle = "Processing";
            ProgressMessage = "Sending Exception Details...";
            InProgress = true;

            try
            {
                await Task.Factory.StartNew(() => reporter.SendException(Exception, IncludeSystemInfo));
            }
            finally
            {
                ProgressTitle = "Processing";
                ProgressMessage = String.Empty;
                InProgress = false;
            }
        }

        static RaygunFeedback reporter;

        static Action<Exception> handleException = ex =>
        {
            if (ex == null)
            {
                return;
            }

            var rootError = ex.GetBaseException();

            ShowExceptionDialog(rootError);
        };
    }
}