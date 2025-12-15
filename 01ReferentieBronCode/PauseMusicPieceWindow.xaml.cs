using System;
using System.Linq;
using System.Windows;

namespace ModusPractica
{
    /// <summary>
    /// Interaction logic for PauseMusicPieceWindow.xaml
    /// </summary>
    public partial class PauseMusicPieceWindow : Window
    {
        private MusicPieceItem _musicPiece;

        public PauseMusicPieceWindow(MusicPieceItem musicPiece)
        {
            InitializeComponent();

            // Controleer of er een geldig muziekstuk is doorgegeven
            if (musicPiece == null)
            {
                MessageBox.Show("No music piece provided for pausing.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            // Sla het muziekstuk op
            _musicPiece = musicPiece;

            // Vul de velden in
            TxtMusicPieceTitle.Text = _musicPiece.Title;

            // Stel standaard pauze datum in (2 weken vanaf nu)
            DpPauseUntilDate.SelectedDate = DateTime.Today.AddDays(14);
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            // Controleer of er een datum is geselecteerd
            if (!DpPauseUntilDate.SelectedDate.HasValue)
            {
                MessageBox.Show("Please select a date until which the music piece should be paused.",
                    "Missing Date", MessageBoxButton.OK, MessageBoxImage.Warning);
                DpPauseUntilDate.Focus();
                return;
            }

            // Controleer of de datum in de toekomst ligt
            if (DpPauseUntilDate.SelectedDate.Value <= DateTime.Today)
            {
                MessageBox.Show("The pause date must be in the future.",
                    "Invalid Date", MessageBoxButton.OK, MessageBoxImage.Warning);
                DpPauseUntilDate.Focus();
                return;
            }

            // Zet het muziekstuk op pauze
            _musicPiece.IsPaused = true;
            _musicPiece.PauseUntilDate = DpPauseUntilDate.SelectedDate.Value;

            // Annuleer geplande oefensessies voor dit muziekstuk
            CancelScheduledSessions();

            // Sluit het venster
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            // Sluit het venster zonder wijzigingen door te voeren
            DialogResult = false;
            Close();
        }

        private void CancelScheduledSessions()
        {
            try
            {
                // Haal alle geplande sessies op
                var allSessions = ScheduledPracticeSessionManager.Instance.GetAllRegularScheduledSessions();

                // Filter sessies voor dit muziekstuk die nog niet voltooid zijn
                var sessionsToCancel = allSessions.Where(s =>
                    s.MusicPieceId == _musicPiece.Id &&
                    s.Status.ToLower() != "completed").ToList();

                // Markeer deze sessies als geannuleerd
                foreach (var session in sessionsToCancel)
                {
                    session.Status = "Canceled";
                }

                // Sla de wijzigingen op
                ScheduledPracticeSessionManager.Instance.SaveScheduledSessions();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"Error canceling scheduled sessions for music piece {_musicPiece.Id}: {ex.Message}", ex);
                // Log de fout, maar laat het proces doorgaan
            }
        }
    }
}