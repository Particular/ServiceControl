namespace ServiceControl.Config.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Windows;
    using Framework.Commands;
    using Microsoft.WindowsAPICodePack.Dialogs;

    class SelectPathCommand : AbstractCommand<object>
    {
        public SelectPathCommand(Action<string> setPath, string title = null, bool isFolderPicker = false, IEnumerable<CommonFileDialogFilter> filters = null, string defaultPath = "")
        {
            this.setPath = setPath;

            dlg = new CommonOpenFileDialog
            {
                Title = title ?? (isFolderPicker ? "Select Folder" : "Select File"),
                IsFolderPicker = isFolderPicker,
                EnsureValidNames = true,
                EnsureFileExists = true,
                Multiselect = false
            };
            if (isFolderPicker)
            {
                dlg.DefaultDirectory = defaultPath;
            }
            else
            {
                dlg.DefaultFileName = defaultPath;
            }

            if (filters != null)
            {
                foreach (var filter in filters)
                {
                    dlg.Filters.Add(filter);
                }
            }
        }

        public override void Execute(object obj)
        {
            var result = dlg.ShowDialog(Application.Current.MainWindow);

            if (result == CommonFileDialogResult.Ok)
            {
                setPath(dlg.FileName);
            }
        }

        readonly Action<string> setPath;
        readonly CommonOpenFileDialog dlg;
    }
}