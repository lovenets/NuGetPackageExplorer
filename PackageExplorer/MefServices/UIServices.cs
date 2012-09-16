﻿using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Win32;
using NuGetPackageExplorer.Types;
using Ookii.Dialogs.Wpf;

namespace PackageExplorer
{
    [Export(typeof(IUIServices))]
    internal class UIServices : IUIServices
    {
        [Import]
        public Lazy<MainWindow> Window { get; set; }

        private static bool OSSupportsTaskDialogs
        {
            get { return NativeMethods.IsWindowsVistaOrLater; }
        }

        #region IUIServices Members

        public bool OpenSaveFileDialog(string title, string defaultFileName, string initialDirectory, string filter, bool overwritePrompt,
                                       out string selectedFilePath, out int selectedFilterIndex)
        {
            var dialog = new SaveFileDialog
                         {
                             OverwritePrompt = overwritePrompt,
                             Title = title,
                             Filter = filter,
                             FileName = defaultFileName,
                             ValidateNames = true,
                             InitialDirectory = initialDirectory
                         };

            bool? result = dialog.ShowDialog();
            if (result ?? false)
            {
                selectedFilePath = dialog.FileName;
                selectedFilterIndex = dialog.FilterIndex;
                return true;
            }
            else
            {
                selectedFilePath = null;
                selectedFilterIndex = -1;
                return false;
            }
        }

        public bool OpenFileDialog(string title, string filter, out string selectedFileName)
        {
            var dialog = new OpenFileDialog
                         {
                             Title = title,
                             CheckFileExists = true,
                             CheckPathExists = true,
                             FilterIndex = 0,
                             Multiselect = false,
                             ValidateNames = true,
                             Filter = filter
                         };

            bool? result = dialog.ShowDialog();
            if (result ?? false)
            {
                selectedFileName = dialog.FileName;
                return true;
            }
            else
            {
                selectedFileName = null;
                return false;
            }
        }

        public bool OpenMultipleFilesDialog(string title, string filter, out string[] selectedFileNames)
        {
            var dialog = new OpenFileDialog
                         {
                             Title = title,
                             CheckFileExists = true,
                             CheckPathExists = true,
                             FilterIndex = 0,
                             Multiselect = true,
                             ValidateNames = true,
                             Filter = filter
                         };

            bool? result = dialog.ShowDialog();
            if (result ?? false)
            {
                selectedFileNames = dialog.FileNames;
                return true;
            }
            else
            {
                selectedFileNames = null;
                return false;
            }
        }

        public bool Confirm(string title, string message)
        {
            return Confirm(title, message, isWarning: false);
        }

        public bool Confirm(string title, string message, bool isWarning)
        {
            if (OSSupportsTaskDialogs)
            {
                return ConfirmUsingTaskDialog(message, title, isWarning);
            }
            else
            {
                MessageBoxResult result = MessageBox.Show(
                    Window.Value,
                    message,
                    Resources.Resources.Dialog_Title,
                    MessageBoxButton.YesNo,
                    isWarning ? MessageBoxImage.Warning : MessageBoxImage.Question);
                return result == MessageBoxResult.Yes;
            }
        }

        public bool? ConfirmWithCancel(string title, string message)
        {
            if (OSSupportsTaskDialogs)
            {
                return ConfirmWithCancelUsingTaskDialog(message, title);
            }
            else
            {
                MessageBoxResult result = MessageBox.Show(
                    Window.Value,
                    message,
                    Resources.Resources.Dialog_Title,
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);
                if (result == MessageBoxResult.Cancel)
                {
                    return null;
                }
                else
                {
                    return result == MessageBoxResult.Yes;
                }
            }
        }

        public bool ConfirmCloseEditor(string title, string message)
        {
            if (OSSupportsTaskDialogs)
            {
                return ConfirmCloseEditorUsingTaskDialog(title, message);
            }
            else
            {
                var result = MessageBox.Show(
                    message,
                    Resources.Resources.Dialog_Title,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Error);

                return result == MessageBoxResult.Yes;
            }
        }

        public void Show(string message, MessageLevel messageLevel)
        {
            MessageBoxImage image;
            switch (messageLevel)
            {
                case MessageLevel.Error:
                    image = MessageBoxImage.Error;
                    break;

                case MessageLevel.Information:
                    image = MessageBoxImage.Information;
                    break;

                case MessageLevel.Warning:
                    image = MessageBoxImage.Warning;
                    break;

                default:
                    throw new ArgumentOutOfRangeException("messageLevel");
            }

            MessageBox.Show(
                Window.Value,
                message,
                Resources.Resources.Dialog_Title,
                MessageBoxButton.OK,
                image);
        }

        public bool OpenRenameDialog(string currentName, string description, out string newName)
        {
            var dialog = new RenameWindow
                         {
                             NewName = currentName,
                             Description = description,
                             Owner = Window.Value
                         };

            bool? result = dialog.ShowDialog();
            if (result ?? false)
            {
                newName = dialog.NewName;
                return true;
            }
            else
            {
                newName = null;
                return false;
            }
        }

        public bool OpenPublishDialog(object viewModel)
        {
            var dialog = new PublishPackageWindow
                         {
                             Owner = Window.Value,
                             DataContext = viewModel
                         };

            var disposable = viewModel as IDisposable;
            if (disposable != null)
            {
                dialog.Closed += OnDialogClosed;
            }

            bool? result = dialog.ShowDialog();
            return result ?? false;
        }

        private void OnDialogClosed(object sender, EventArgs e)
        {
            var window = (Window)sender;
            var disposable = window.DataContext as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
            window.Closed -= OnDialogClosed;
        }

        public bool OpenFolderDialog(string title, string initialPath, out string selectedPath)
        {
            var dialog = new VistaFolderBrowserDialog
                         {
                             ShowNewFolderButton = true,
                             SelectedPath = initialPath,
                             Description = title,
                             UseDescriptionForTitle = true
                         };

            bool? result = dialog.ShowDialog(Window.Value);
            if (result ?? false)
            {
                selectedPath = dialog.SelectedPath;
                return true;
            }
            else
            {
                selectedPath = null;
                return false;
            }
        }

        public void BeginInvoke(Action action)
        {
            Window.Value.Dispatcher.BeginInvoke(action);
        }

        public Tuple<bool?, bool> ConfirmMoveFile(string fileName, string targetFolder, int numberOfItemsLeft)
        {
            if (numberOfItemsLeft < 0)
            {
                throw new ArgumentOutOfRangeException("numberOfItemsLeft");
            }

            string mainInstruction = String.Format(
                CultureInfo.CurrentCulture,
                Resources.Resources.MoveContentFileToFolder,
                fileName,
                targetFolder);

            if (OSSupportsTaskDialogs)
            {
                return ConfirmMoveFileUsingTaskDialog(fileName, targetFolder, numberOfItemsLeft, mainInstruction);
            }
            else
            {
                bool? answer = ConfirmWithCancel(Resources.Resources.Dialog_Title, mainInstruction);
                return Tuple.Create(answer, false);
            }
        }

        public bool? AskToInstallNpeOnWindows8()
        {
            using (var dialog = new TaskDialog())
            {
                dialog.WindowTitle = Resources.Resources.Dialog_Title;
                dialog.MainInstruction = "Great! You are running on Windows 8";
                dialog.Content = "There is also a Windows Store app of NuGet Package Explorer that is designed to be touch friendly, fast and fluid. Do you want to install it now?";
                dialog.AllowDialogCancellation = true;
                dialog.CenterParent = true;
                dialog.ButtonStyle = TaskDialogButtonStyle.CommandLinks;

                var yesButton = new TaskDialogButton
                {
                    Text = "Yes",
                    CommandLinkNote = "Go to the Store and install it now."
                };

                var noButton = new TaskDialogButton
                {
                    Text = "No",
                    CommandLinkNote = "Don't bother."
                };

                var remindButton = new TaskDialogButton("Remind me next time");

                dialog.Buttons.Add(yesButton);
                dialog.Buttons.Add(noButton);
                dialog.Buttons.Add(remindButton);

                TaskDialogButton result = dialog.ShowDialog();
                if (result == yesButton)
                {
                    return true;
                }
                else if (result == noButton) 
                {
                    return false;
                }
                else 
                {
                    return null;
                }
            }
        }

        #endregion

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool ConfirmUsingTaskDialog(string message, string title, bool isWarning)
        {
            using (var dialog = new TaskDialog())
            {
                dialog.WindowTitle = Resources.Resources.Dialog_Title;
                dialog.MainInstruction = title;
                dialog.Content = message;
                dialog.AllowDialogCancellation = true;
                dialog.CenterParent = true;
                //dialog.ButtonStyle = TaskDialogButtonStyle.CommandLinks;
                if (isWarning)
                {
                    dialog.MainIcon = TaskDialogIcon.Warning;
                }

                var yesButton = new TaskDialogButton("Yes");
                var noButton = new TaskDialogButton("No");

                dialog.Buttons.Add(yesButton);
                dialog.Buttons.Add(noButton);

                TaskDialogButton result = dialog.ShowDialog();
                return result == yesButton;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool? ConfirmWithCancelUsingTaskDialog(string message, string title)
        {
            using (var dialog = new TaskDialog())
            {
                dialog.WindowTitle = Resources.Resources.Dialog_Title;
                dialog.MainInstruction = title;
                dialog.AllowDialogCancellation = true;
                dialog.Content = message;
                dialog.CenterParent = true;
                dialog.MainIcon = TaskDialogIcon.Warning;
                //dialog.ButtonStyle = TaskDialogButtonStyle.CommandLinks;

                var yesButton = new TaskDialogButton("Yes");
                var noButton = new TaskDialogButton("No");
                var cancelButton = new TaskDialogButton("Cancel");

                dialog.Buttons.Add(yesButton);
                dialog.Buttons.Add(noButton);
                dialog.Buttons.Add(cancelButton);

                TaskDialogButton result = dialog.ShowDialog();
                if (result == yesButton)
                {
                    return true;
                }
                else if (result == noButton)
                {
                    return false;
                }

                return null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private Tuple<bool?, bool> ConfirmMoveFileUsingTaskDialog(string fileName, string targetFolder,
                                                                  int numberOfItemsLeft, string mainInstruction)
        {
            string content = String.Format(
                CultureInfo.CurrentCulture,
                Resources.Resources.MoveContentFileToFolderExplanation,
                targetFolder);

            var dialog = new TaskDialog
                         {
                             MainInstruction = mainInstruction,
                             Content = content,
                             WindowTitle = Resources.Resources.Dialog_Title,
                             ButtonStyle = TaskDialogButtonStyle.CommandLinks
                         };

            if (numberOfItemsLeft > 0)
            {
                dialog.VerificationText = "Do this for the next " + numberOfItemsLeft + " file(s).";
            }

            var moveButton = new TaskDialogButton
                             {
                                 Text = "Yes",
                                 CommandLinkNote =
                                     "'" + fileName + "' will be added to '" + targetFolder +
                                     "' folder."
                             };

            var noMoveButton = new TaskDialogButton
                               {
                                   Text = "No",
                                   CommandLinkNote =
                                       "'" + fileName + "' will be added to the package root."
                               };

            dialog.Buttons.Add(moveButton);
            dialog.Buttons.Add(noMoveButton);
            dialog.Buttons.Add(new TaskDialogButton(ButtonType.Cancel));

            TaskDialogButton result = dialog.ShowDialog(Window.Value);

            bool? movingFile;
            if (result == moveButton)
            {
                movingFile = true;
            }
            else if (result == noMoveButton)
            {
                movingFile = false;
            }
            else
            {
                // Cancel button clicked
                movingFile = null;
            }

            bool remember = dialog.IsVerificationChecked;
            return Tuple.Create(movingFile, remember);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool ConfirmCloseEditorUsingTaskDialog(string title, string message)
        {
            using (var dialog = new TaskDialog())
            {
                dialog.WindowTitle = Resources.Resources.Dialog_Title;
                dialog.MainInstruction = title;
                dialog.Content = message;
                dialog.AllowDialogCancellation = false;
                dialog.CenterParent = true;
                dialog.ButtonStyle = TaskDialogButtonStyle.CommandLinks;

                var yesButton = new TaskDialogButton
                                {
                                    Text = "Yes",
                                    CommandLinkNote = "Return to package view and lose all your changes."
                                };

                var noButton = new TaskDialogButton
                               {
                                   Text = "No",
                                   CommandLinkNote = "Stay at the metadata editor and fix the error."
                               };

                dialog.Buttons.Add(yesButton);
                dialog.Buttons.Add(noButton);

                TaskDialogButton result = dialog.ShowDialog();
                return result == yesButton;
            }
        }

        public bool TrySelectPortableFramework(out string portableFramework)
        {
            var dialog = new PortableLibraryDialog
            {
                Owner = Window.Value
            };

            bool? result = dialog.ShowDialog();
            if (result ?? false)
            {
                portableFramework = dialog.GetSelectedFrameworkName();
                return true;
            }
            else
            {
                portableFramework = null;
                return false;
            }
        }
    }
}