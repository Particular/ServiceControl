using System.Windows;
using System.Windows.Controls;

namespace ServiceControl.Config.UI.SharedInstanceEditor
{
    public partial class SharedInstanceEditorView : UserControl
    {
        public SharedInstanceEditorView()
        {
            InitializeComponent();
        }

        public string SaveText
        {
            get { return (string)GetValue(SaveTextProperty); }
            set { SetValue(SaveTextProperty, value); }
        }

        public static readonly DependencyProperty SaveTextProperty =
            DependencyProperty.Register("SaveText", typeof(string), typeof(SharedInstanceEditorView), new PropertyMetadata("Save"));

        public object SharedContent
        {
            get { return GetValue(SharedContentProperty); }
            set { SetValue(SharedContentProperty, value); }
        }

        public static readonly DependencyProperty SharedContentProperty =
            DependencyProperty.Register("SharedContent", typeof(object), typeof(SharedInstanceEditorView), new PropertyMetadata());
    }
}