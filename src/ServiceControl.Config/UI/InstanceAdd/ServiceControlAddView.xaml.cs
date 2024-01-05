﻿namespace ServiceControl.Config.UI.InstanceAdd
{
    using System.Windows;
    using System.Windows.Controls;

    public partial class ServiceControlAddView
    {
        public ServiceControlAddView()
        {
            InitializeComponent();
        }
        public void CheckBoxError_Indeterminate(object sender, RoutedEventArgs e)
        {
            var chk = sender as CheckBox;
            chk.Indeterminate += (sender, e) => chk.IsChecked = false;
        }
        public void CheckBoxAudit_Indeterminate(object sender, RoutedEventArgs e)
        {
            var chk = sender as CheckBox;
            chk.Indeterminate += (sender, e) => chk.IsChecked = false;
        }
    }
}