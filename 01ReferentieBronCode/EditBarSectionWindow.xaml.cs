using System.Windows;

namespace ModusPractica
{
    public partial class EditBarSectionWindow : Window
    {
        private readonly BarSection _barSection;
        private readonly MusicPieceItem _parentMusicPiece;

        public EditBarSectionWindow(BarSection barSection, MusicPieceItem parentMusicPiece)
        {
            InitializeComponent();
            _barSection = barSection;
            _parentMusicPiece = parentMusicPiece;

            // Populate the ComboBox for target repetitions (1..12)
            for (int i = 1; i <= 12; i++)
            {
                CbTargetRepetitions.Items.Add(i.ToString());
            }

            // Load existing data into the fields
            TxtBarRange.Text = _barSection.BarRange;
            TxtDescription.Text = _barSection.Description;
            CbTargetRepetitions.SelectedItem = _barSection.TargetRepetitions.ToString();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // --- Update the BarSection object ---
            // Because this object was passed by reference, these changes will be reflected in MainWindow.
            // Note: BarRange is no longer editable, so we only update Description and TargetRepetitions
            _barSection.Description = TxtDescription.Text.Trim();

            if (int.TryParse(CbTargetRepetitions.SelectedItem.ToString(), out int newRepetitions))
            {
                _barSection.TargetRepetitions = newRepetitions;
            }

            // Close the dialog and indicate success
            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}