using System.Windows;

namespace ModusPractica
{
    /// <summary>
    /// Interaction logic for EditMusicPieceWindow.xaml
    /// </summary>
    public partial class EditMusicPieceWindow : Window
    {
        // Het muziekstuk dat we aan het bewerken zijn
        private MusicPieceItem _musicPiece;

        // Resultaat van het dialoogvenster
        public bool IsSaved { get; private set; }

        public EditMusicPieceWindow(MusicPieceItem musicPiece)
        {
            // Initialize field to avoid CS8618 warning
            _musicPiece = musicPiece ?? new MusicPieceItem();

            InitializeComponent();

            // Controleer of er een geldig muziekstuk is doorgegeven
            if (musicPiece == null)
            {
                MessageBox.Show("No music piece provided for editing.", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }

            // Sla het muziekstuk op
            _musicPiece = musicPiece;

            // Vul de velden in met de huidige waarden
            LoadMusicPieceData();
        }

        private void LoadMusicPieceData()
        {
            // Fill the fields with the data from the music piece.
            TxtTitle.Text = _musicPiece.Title;
            TxtComposer.Text = _musicPiece.Composer;

            // --- PROPOSED CHANGE ---
            // Use the CultureHelper and the standard "D" (Long Date) format specifier
            // to display the creation date according to the user's selected region.
            TxtCreationDate.Text = _musicPiece.CreationDate.ToString("D", CultureHelper.Current);

            // Update the progress of the music piece based on completed bar sections.
            _musicPiece.UpdateProgress();

            // Set the progress bar.
            PbProgress.Value = _musicPiece.Progress;
            TxtProgress.Text = $"{_musicPiece.Progress:0}%";
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Valideer de invoer
            if (string.IsNullOrWhiteSpace(TxtTitle.Text))
            {
                MessageBox.Show("Please enter a title for the music piece.", "Missing Title",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtTitle.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtComposer.Text))
            {
                MessageBox.Show("Please enter a composer for the music piece.", "Missing Composer",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtComposer.Focus();
                return;
            }

            // Update het muziekstuk met de nieuwe waarden
            _musicPiece.Title = TxtTitle.Text.Trim();
            _musicPiece.Composer = TxtComposer.Text.Trim();

            // Markeer als opgeslagen
            IsSaved = true;

            // Sluit het venster
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            // Sluit het venster zonder op te slaan
            DialogResult = false;
            Close();
        }
    }
}