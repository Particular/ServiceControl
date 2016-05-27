namespace ServiceControl.Config.UI.MessageBox
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Threading;
    using PropertyChanged;
    using ServiceControl.Config.Extensions;
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Framework.Commands;
    using ICommand = System.Windows.Input.ICommand;

    [ImplementPropertyChanged]
    public partial class ExceptionMessageBox: IProgressViewModel
    {
        List<string> ExceptionInformationList = new List<string>();

        static RaygunFeedback reporter;

        static ExceptionMessageBox()
        {
      
        }

        public static void Attach()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;
            Dispatcher.CurrentDispatcher.UnhandledException += CurrentDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskSchedulerUnobservedTaskException;
        }

        static void CurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            if (ex != null)
                handleException(ex);
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

        static Action<Exception> handleException = ex =>
        {
            if (ex == null)
                return;

            var rootError = ex.GetBaseException();

         
            ShowExceptionDialog(rootError);
        };

        static void ShowExceptionDialog(Exception ex)
        {

            reporter = new RaygunFeedback();

            var treeViewItem = new TreeViewItem
            {
                Header = "Exception",
                BorderThickness = new Thickness(0),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)) // transparent

            };

            treeViewItem.ExpandSubtree();

            var dialog = new ExceptionMessageBox
            {
                TraceTree =
                {
                    BorderThickness = new Thickness(0),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255))
                },
               
                Message =
                {
                    Text = ex.Message.TruncateSentence(200)
                } ,
                Exception = ex
            };

            dialog.BuildTreeLayer(ex, treeViewItem);
            dialog.TraceTree.Items.Add(treeViewItem);

            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsLoaded)
            {
                dialog.Owner = Application.Current.MainWindow;
            }
            dialog.ShowDialog();

        }

        void BuildTreeLayer(Exception e, TreeViewItem parent)
        {
            var exceptionInformation = "\n\r\n\r" + e.GetType() + "\n\r\n\r";
            parent.DisplayMemberPath = "Header";
            parent.Items.Add(new TreeViewItem { Header = "Type", Tag = e.GetType().ToString() });
            var memberList = e.GetType().GetProperties();
            foreach (var info in memberList)
            {
                var value = info.GetValue(e, null);
                if (value != null)
                {
                    if (info.Name == "InnerException")
                    {
                        var treeViewItem = new TreeViewItem
                        {
                            Header = info.Name,
                            Tag = e.InnerException.GetType().ToString()
                        };
                        BuildTreeLayer(e.InnerException, treeViewItem);
                        parent.Items.Add(treeViewItem);
                    }
                    else
                    {
                        parent.Items.Add(new TreeViewItem { Header = info.Name, Tag = value.ToString()} );
                        exceptionInformation += info.Name + "\n\r\n\r" + value + "\n\r\n\r";
                    }
                }
            }
            ExceptionInformationList.Add(exceptionInformation);
        }

        public ExceptionMessageBox()
        {
            InitializeComponent();
        }

        public bool IncludeSystemInfo { get; set; }
        Exception Exception { get; set; }

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

        public ICommand ReportClick => new AwaitableDelegateCommand(CallReportClick);

        async Task CallReportClick(object sender)
        {
         
            ProgressTitle = "Processing";
            ProgressMessage = "Sending Exception Details...";
            InProgress = true;

            try
            {
                await Task.Factory.StartNew(() => reporter.SendException(Exception,  IncludeSystemInfo));
            }
            finally
            {
                ProgressTitle = "Processing";
                ProgressMessage = String.Empty;
                InProgress = false;
            }
        }

        private void TraceTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var item = e.NewValue as TreeViewItem;
            ErrorDetails.Text = (item != null ) ? item.Tag.ToString() :  "Exception";
        }


        public string ProgressTitle { get; set; }
        public bool InProgress { get; set; }
        public string ProgressMessage { get; set; }
        public int ProgressPercent { get; set; }
    }
}
