using System.Windows;

namespace ModusPractica
{
    /// <summary>
    /// Concretie van de dialoogservice met standaard WPF-dialogen.
    /// </summary>
    public sealed class PracticeSessionDialogService : IPracticeSessionDialogService
    {
        public void ShowInformation(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowError(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public bool ConfirmDeletion(string message, string title)
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        public bool? ShowSessionEditor(MusicPieceItem musicPiece, BarSection barSection, PracticeHistory session)
        {
            var owner = Application.Current?.MainWindow;
            var window = new PracticeSessionWindow(musicPiece, barSection, session)
            {
                Owner = owner
            };

            return window.ShowDialog();
        }
    }
}
