namespace ServiceControl.Config.UI.SharedInstanceEditor
{
    using System.Windows;

    public partial class SharedServiceControlEditorView
    {
        public SharedServiceControlEditorView()
        {
            InitializeComponent();
        }

        public string SaveText
        {
            get { return (string)GetValue(SaveTextProperty); }
            set { SetValue(SaveTextProperty, value); }
        }

        public object SharedContent
        {
            get { return GetValue(SharedContentProperty); }
            set { SetValue(SharedContentProperty, value); }
        }

        public static readonly DependencyProperty SaveTextProperty =
            DependencyProperty.Register("SaveText", typeof(string), typeof(SharedServiceControlEditorView), new PropertyMetadata("Save"));

        public static readonly DependencyProperty SharedContentProperty =
            DependencyProperty.Register("SharedContent", typeof(object), typeof(SharedServiceControlEditorView), new PropertyMetadata());
    }
}