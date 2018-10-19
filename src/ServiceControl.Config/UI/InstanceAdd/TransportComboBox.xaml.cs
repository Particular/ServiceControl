using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ServiceControl.Config.UI.InstanceAdd
{
    using System.Linq;
    using ServiceControlInstaller.Engine.Instances;

    public partial class TransportComboBox : UserControl
    {
        public TransportComboBox()
        {
            InitializeComponent();
        }

        public IEnumerable<TransportInfo> Transports
        {
            get => (IEnumerable<TransportInfo>)GetValue(TransportsProperty);
            set => SetValue(TransportsProperty, value);
        }

        public TransportInfo SelectedTransport
        {
            get => (TransportInfo)GetValue(SelectedTransportProperty);
            set => SetValue(SelectedTransportProperty, value);
        }

        public static DependencyProperty TransportsProperty =
            DependencyProperty.Register("Transports", typeof(IEnumerable<TransportInfo>), typeof(TransportComboBox), new PropertyMetadata(OnTransportsChanged));

        public static DependencyProperty SelectedTransportProperty =
            DependencyProperty.Register("SelectedTransport", typeof(TransportInfo), typeof(TransportComboBox), new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        static void OnTransportsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (TransportComboBox)d;

            var sortedTransprots = control.Transports.Where(t => t.IsLegacy == false)
                .Concat(new[] { (TransportInfo)null })
                .Concat(control.Transports.Where(t => t.IsLegacy));

            control.SetValue(CategorizedTransportsProperty, sortedTransprots);
        }

        static DependencyProperty CategorizedTransportsProperty =
            DependencyProperty.Register("CategorizedTransports", typeof(IEnumerable<TransportInfo>), typeof(TransportComboBox));
    }
}
