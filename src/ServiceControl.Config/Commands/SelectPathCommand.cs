using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;
using ServiceControl.Config.Framework.Commands;

namespace ServiceControl.Config.Commands
{
    class SelectPathCommand : AbstractCommand<object>
    {
        private readonly Action<string> setPath;
        private readonly CommonOpenFileDialog dlg;

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
    }
}