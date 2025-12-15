using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ModusPractica
{
    public enum ProfileInUseDialogResult
    {
        Ok,
        OpenAnotherProfile
    }

    public partial class ProfileInUseDialog : Window
    {
        public string FolderPath { get; }
        public ProfileInUseDialogResult Result { get; private set; } = ProfileInUseDialogResult.Ok;

        public ProfileInUseDialog(string folderPath)
        {
            InitializeComponent();
            FolderPath = folderPath ?? string.Empty;
            TxtFolder.Text = FolderPath;
        }

        private void HlOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(FolderPath) && Directory.Exists(FolderPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = FolderPath,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
                else if (!string.IsNullOrWhiteSpace(FolderPath))
                {
                    // Open parent if folder doesn't exist
                    var parent = Path.GetDirectoryName(FolderPath);
                    if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = parent,
                            UseShellExecute = true,
                            Verb = "open"
                        });
                    }
                }
            }
            catch { /* ignore */ }
        }

        private void BtnOpenAnother_Click(object sender, RoutedEventArgs e)
        {
            Result = ProfileInUseDialogResult.OpenAnotherProfile;
            DialogResult = true;
            Close();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Result = ProfileInUseDialogResult.Ok;
            DialogResult = true;
            Close();
        }
    }
}
