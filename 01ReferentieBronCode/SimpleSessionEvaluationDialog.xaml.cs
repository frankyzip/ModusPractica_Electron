using System.Windows;
using System.Windows.Controls;

namespace ModusPractica
{
    public partial class SimpleSessionEvaluationDialog : Window
    {
        public class EvaluationResult
        {
            public string OverallFeeling { get; set; } = "Okay";
            public int EstimatedRepetitions { get; set; } = 6;
            public string DifficultyExpectation { get; set; } = "AsExpected";
            public string Notes { get; set; } = "";
            public bool WasSaved { get; set; } = false;

            // Derived properties for practice session data
            public string DifficultyLevel
            {
                get
                {
                    return OverallFeeling switch
                    {
                        "VeryHard" => "Difficult",
                        "Hard" => "Difficult",
                        "Okay" => "Average",
                        "Easy" => "Easy",
                        "VeryEasy" => "Easy",
                        _ => "Average"
                    };
                }
            }

            public float PerformanceScore
            {
                get
                {
                    var baseScore = OverallFeeling switch
                    {
                        "VeryHard" => 3.0f,
                        "Hard" => 5.0f,
                        "Okay" => 7.0f,
                        "Easy" => 8.5f,
                        "VeryEasy" => 9.5f,
                        _ => 7.0f
                    };

                    // Adjust based on expectation
                    var adjustment = DifficultyExpectation switch
                    {
                        "HarderThanExpected" => -1.0f,
                        "SlightlyHarder" => -0.5f,
                        "AsExpected" => 0.0f,
                        "Easier" => +0.5f,
                        _ => 0.0f
                    };

                    return Math.Max(1.0f, Math.Min(10.0f, baseScore + adjustment));
                }
            }

            public int EstimatedAttempts
            {
                get
                {
                    return OverallFeeling switch
                    {
                        "VeryHard" => EstimatedRepetitions * 3,
                        "Hard" => EstimatedRepetitions * 2,
                        "Okay" => EstimatedRepetitions,
                        "Easy" => Math.Max(1, EstimatedRepetitions / 2),
                        "VeryEasy" => Math.Max(1, EstimatedRepetitions / 3),
                        _ => EstimatedRepetitions
                    };
                }
            }
        }

        public EvaluationResult Result { get; private set; }

        public SimpleSessionEvaluationDialog(string sectionInfo, bool isFrustrationSession = false)
        {
            InitializeComponent();
            Result = new EvaluationResult();
            TxtSessionInfo.SetCurrentValue(TextBlock.TextProperty, sectionInfo);

            if (isFrustrationSession)
            {
                this.SetCurrentValue(TitleProperty, "That was frustrating - tell us more");
                RbVeryHard.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
                RbOkay.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, false);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Get overall feeling
            if (RbVeryHard.IsChecked == true) Result.OverallFeeling = "VeryHard";
            else if (RbHard.IsChecked == true) Result.OverallFeeling = "Hard";
            else if (RbOkay.IsChecked == true) Result.OverallFeeling = "Okay";
            else if (RbEasy.IsChecked == true) Result.OverallFeeling = "Easy";
            else if (RbVeryEasy.IsChecked == true) Result.OverallFeeling = "VeryEasy";

            // Get estimated repetitions
            if (CbRepetitions.SelectedItem is ComboBoxItem repItem && repItem.Tag != null)
            {
                if (int.TryParse(repItem.Tag.ToString(), out int reps))
                {
                    Result.EstimatedRepetitions = reps;
                }
            }

            // Get difficulty expectation
            if (CbExpectation.SelectedItem is ComboBoxItem expItem && expItem.Tag != null)
            {
                Result.DifficultyExpectation = expItem.Tag.ToString() ?? "AsExpected";
            }

            // Notes field removed from UI; keep default empty string

            Result.WasSaved = true;

            MLLogManager.Instance.Log(
                $"Simple evaluation: {Result.OverallFeeling}, {Result.EstimatedRepetitions} reps, " +
                $"difficulty={Result.DifficultyLevel}, performance={Result.PerformanceScore:F1}",
                LogLevel.Info);

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result.WasSaved = false;
            DialogResult = false;
            Close();
        }
    }
}