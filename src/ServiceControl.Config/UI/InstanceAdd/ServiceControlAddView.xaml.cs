namespace ServiceControl.Config.UI.InstanceAdd
{
    using System.Windows.Controls;
    using System.Windows.Shapes;

    public partial class ServiceControlAddView
    {
        public ServiceControlAddView()
        {
            InitializeComponent();
        }

        void Button_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.Source is Expander)
            {
                e.Handled = e.OriginalSource is not (Ellipse or Path);
            }
            else
            {
                e.Handled = e.Source == sender;
            }
        }
    }
}