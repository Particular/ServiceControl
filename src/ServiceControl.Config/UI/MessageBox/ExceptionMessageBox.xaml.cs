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
    using ServiceControl.Config.Framework;
    using ServiceControl.Config.Extensions;
    using ServiceControl.Config.Framework.Commands;
    using ICommand = System.Windows.Input.ICommand;

    [ImplementPropertyChanged]
    public partial class ExceptionMessageBox: IProgressViewModel
    {
        List<string> ExceptionInformationList = new List<string>();

        static RaygunReporter reporter;

        static ExceptionMessageBox()
        {
      
        }

        public static void Attach()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Dispatcher.CurrentDispatcher.UnhandledException += CurrentDispatcher_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            if (ex != null)
                HandleException(ex);
        }

        static void CurrentDispatcher_UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            HandleException(e.Exception);
            e.Handled = true;
        }

        static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            HandleException(e.Exception);
        }

        static Action<Exception> HandleException = ex =>
        {
            if (ex == null)
                return;

            var rootError = ex.GetBaseException();

         
            ShowExceptionDialog(rootError);
        };

        public static void ShowExceptionDialog(Exception ex)
        {

            reporter = new RaygunReporter();

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
                    Text = ex.Message.Truncate(60)
                } ,
                Exception = ex
            };

            dialog.buildTreeLayer(ex, treeViewItem);
            dialog.TraceTree.Items.Add(treeViewItem);

            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsLoaded)
            {
                dialog.Owner = Application.Current.MainWindow;
            }
            dialog.ShowDialog();

        }

        void buildTreeLayer(Exception e, TreeViewItem parent)
        {
            var exceptionInformation = "\n\r\n\r" + e.GetType() + "\n\r\n\r";
            parent.DisplayMemberPath = "Header";
            parent.Items.Add(new TreeViewStringSet() { Header = "Type", Content = e.GetType().ToString() });
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
                            Header = info.Name
                        };
                        buildTreeLayer(e.InnerException, treeViewItem);
                        parent.Items.Add(treeViewItem);
                    }
                    else
                    {
                        var treeViewStringSet = new TreeViewStringSet() { Header = info.Name, Content = value.ToString() };
                        parent.Items.Add(treeViewStringSet);
                        exceptionInformation += treeViewStringSet.Header + "\n\r\n\r" + treeViewStringSet.Content + "\n\r\n\r";
                    }
                }
            }
            ExceptionInformationList.Add(exceptionInformation);
        }

        private class TreeViewStringSet
        {
            public string Header { get; set; }
            public string Content { get; set; }

            public override string ToString()
            {
                return Content;
            }
        }

        public ExceptionMessageBox()
        {
            InitializeComponent();
        }

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

     

        public ICommand ReportClick
        {
            get
            {
                return new AwaitableDelegateCommand(CallReportClick);
            }
        }

        async Task CallReportClick(object sender)
        {
         
            ProgressTitle = "Processing";
            ProgressMessage = "Sending Exception Details...";
            InProgress = true;

            try
            {
                await Task.Factory.StartNew(() => reporter.SendReport(this.Exception));
            }
            finally
            {
                ProgressTitle = "Processing";
                ProgressMessage = "";
                InProgress = false;
            }
        }

        private void TraceTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue.GetType() == typeof(TreeViewItem)) ErrorDetails.Text = "Exception";
            else ErrorDetails.Text = e.NewValue.ToString();
        }


        public string ProgressTitle { get; set; }
        public bool InProgress { get; set; }
        public string ProgressMessage { get; set; }
        public int ProgressPercent { get; set; }
    }
}
