using System.Windows;

namespace ModusPractica
{
    /// <summary>
    /// Feedback dialog for playlist practice sessions following Dr. Gebrian's research methodology.
    /// Captures user's subjective experience of difficulty and practice quality.
    /// </summary>
    public partial class PlaylistFeedbackDialog : Window
    {
        public string ExperiencedDifficulty { get; private set; }
        public string PracticeQuality { get; private set; }
        public string UserNotes { get; private set; }
        public bool FeedbackProvided { get; private set; } = false;

        public PlaylistFeedbackDialog(string musicPieceTitle, string barSectionRange, int durationMinutes)
        {
            InitializeComponent();

            TxtSessionTitle.Text = $"🎵 How was this practice session?";
            TxtSectionInfo.Text = $"{musicPieceTitle} - {barSectionRange} ({durationMinutes} min)";

            // Default values
            ExperiencedDifficulty = "Moderate";
            PracticeQuality = "Good";
            UserNotes = "";
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Get difficulty rating
            if (RbVeryEasy.IsChecked == true)
                ExperiencedDifficulty = "VeryEasy";
            else if (RbEasy.IsChecked == true)
                ExperiencedDifficulty = "Easy";
            else if (RbModerate.IsChecked == true)
                ExperiencedDifficulty = "Moderate";
            else if (RbHard.IsChecked == true)
                ExperiencedDifficulty = "Hard";
            else if (RbVeryHard.IsChecked == true)
                ExperiencedDifficulty = "VeryHard";

            // Get quality rating
            if (RbExcellent.IsChecked == true)
                PracticeQuality = "Excellent";
            else if (RbGood.IsChecked == true)
                PracticeQuality = "Good";
            else if (RbOkay.IsChecked == true)
                PracticeQuality = "Okay";
            else if (RbPoor.IsChecked == true)
                PracticeQuality = "Poor";

            // Get notes
            UserNotes = TxtNotes.Text?.Trim() ?? "";

            FeedbackProvided = true;
            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            // User cancelled - use defaults but mark as not provided
            FeedbackProvided = false;
            this.DialogResult = false;
            this.Close();
        }

        /// <summary>
        /// Convert user feedback to a numeric difficulty score for ML processing
        /// </summary>
        public float GetDifficultyScore()
        {
            return ExperiencedDifficulty switch
            {
                "VeryEasy" => 1.0f,
                "Easy" => 2.5f,
                "Moderate" => 5.0f,
                "Hard" => 7.5f,
                "VeryHard" => 9.0f,
                _ => 5.0f // Default moderate
            };
        }

        /// <summary>
        /// Convert quality rating to session outcome for ML processing
        /// </summary>
        public string GetSessionOutcome()
        {
            return PracticeQuality switch
            {
                "Excellent" => "TargetReached",
                "Good" => "TargetReached",
                "Okay" => "TargetNotReached",
                "Poor" => "Frustration",
                _ => "TargetReached" // Default good
            };
        }

        /// <summary>
        /// Generate estimated repetitions based on difficulty and quality
        /// </summary>
        public int GetEstimatedRepetitions(int durationMinutes)
        {
            float baseRepetitions = Math.Max(1, durationMinutes / 2.0f); // Base estimate

            // Adjust based on difficulty (easier = more repetitions possible)
            float difficultyMultiplier = ExperiencedDifficulty switch
            {
                "VeryEasy" => 1.5f,
                "Easy" => 1.2f,
                "Moderate" => 1.0f,
                "Hard" => 0.8f,
                "VeryHard" => 0.6f,
                _ => 1.0f
            };

            // Adjust based on quality (better quality = more effective repetitions)
            float qualityMultiplier = PracticeQuality switch
            {
                "Excellent" => 1.3f,
                "Good" => 1.1f,
                "Okay" => 1.0f,
                "Poor" => 0.7f,
                _ => 1.0f
            };

            return Math.Max(1, (int)Math.Round(baseRepetitions * difficultyMultiplier * qualityMultiplier));
        }
    }
}
