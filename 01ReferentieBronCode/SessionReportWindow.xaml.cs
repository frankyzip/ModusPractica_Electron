using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace ModusPractica
{
    /// <summary>
    /// Interaction logic for SessionReportWindow.xaml
    /// </summary>
    public partial class SessionReportWindow : Window
    {
        // Reference to the PracticeSessionWindow that opened this report
        private PracticeSessionWindow? _practiceSessionWindow;

        // NEW: Override Properties
        private double _originalAlgorithmInterval;
        private BarSection? _currentSection;
        private MusicPieceItem? _currentPiece;

        // Properties for override data (accessible by calling code)
        public bool IsOverrideEnabled { get; private set; } = false;
        public double? OverrideInterval { get; private set; }
        public string OverrideReason { get; private set; } = string.Empty;
        public Dictionary<string, object>? OverrideData { get; private set; }

        // Constructor for use with PracticeSessionWindow
        public SessionReportWindow(PracticeSessionWindow practiceSessionWindow, PracticeHistory session, float totalPerformanceScore, DateTime nextDueDate, float barsPerMinute, float plannerPerformanceScore, double algorithmInterval = 0, BarSection? currentSection = null, MusicPieceItem? currentPiece = null, double currentSessionRatio = 0, double rollingRatio = 0, string learningZone = "")
        {
            InitializeComponent();
            _practiceSessionWindow = practiceSessionWindow;
            _originalAlgorithmInterval = algorithmInterval;
            _currentSection = currentSection;
            _currentPiece = currentPiece;
            PopulateReport(session, totalPerformanceScore, nextDueDate, barsPerMinute, plannerPerformanceScore, currentSessionRatio, rollingRatio, learningZone);
            InitializeOverrideFeatures();
        }

        // Constructor for use without PracticeSessionWindow (e.g., PlaylistPracticeWindow)
        public SessionReportWindow(PracticeHistory session, float totalPerformanceScore, DateTime nextDueDate, float barsPerMinute, float plannerPerformanceScore, double algorithmInterval = 0, BarSection? currentSection = null, MusicPieceItem? currentPiece = null, double currentSessionRatio = 0, double rollingRatio = 0, string learningZone = "")
        {
            InitializeComponent();
            _practiceSessionWindow = null;
            _originalAlgorithmInterval = algorithmInterval;
            _currentSection = currentSection;
            _currentPiece = currentPiece;
            PopulateReport(session, totalPerformanceScore, nextDueDate, barsPerMinute, plannerPerformanceScore, currentSessionRatio, rollingRatio, learningZone);
            InitializeOverrideFeatures();
        }

        private void PopulateReport(PracticeHistory session, float totalPerformanceScore, DateTime nextDueDate, float barsPerMinute, float plannerPerformanceScore, double currentSessionRatio = 0, double rollingRatio = 0, string learningZone = "")
        {
            // DEBUG: Log the scores being passed in
            MLLogManager.Instance?.Log($"SessionReportWindow: totalPerformanceScore={totalPerformanceScore:F2}, plannerPerformanceScore={plannerPerformanceScore:F2}, stored PerformanceScore={session.PerformanceScore:F2}", LogLevel.Info);

            // Use settings to determine which score to show as primary
            bool preferPlannerScore = SettingsManager.Instance?.CurrentSettings?.PreferPlannerScoreInReport ?? false;
            float primaryScore = preferPlannerScore ? plannerPerformanceScore : totalPerformanceScore;

            // DEBUG: Log which score is being used
            MLLogManager.Instance?.Log($"SessionReportWindow: preferPlannerScore={preferPlannerScore}, primaryScore={primaryScore:F2}", LogLevel.Info);

            // Set the main score and rating
            TxtScore.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, $"{primaryScore:F1}");
            string rating = PracticeUtils.ConvertScoreToRatingString(primaryScore);
            TxtRating.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, rating.ToUpper());

            // Set the color of the score circle based on the rating
            SolidColorBrush scoreBrush = rating switch
            {
                "Excellent" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),     // Green
                "Good" => new SolidColorBrush(Color.FromRgb(255, 193, 7)),         // Amber
                "Average" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),      // Orange
                "Below Average" => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // Red-Orange
                _ => new SolidColorBrush(Color.FromRgb(211, 47, 47))               // Red
            };
            ScoreBorder.SetCurrentValue(System.Windows.Controls.Border.BackgroundProperty, scoreBrush);

            // Build the analysis string with the correct efficiency metric
            var analysisText = new StringBuilder();
            analysisText.AppendLine($"• Efficiency: {barsPerMinute:F1} bars per minute");
            analysisText.AppendLine($"• Initial Attempts: {session.AttemptsTillSuccess} before first success");
            analysisText.AppendLine($"• Consistency: {session.RepetitionStreakAttempts} streak resets");
            if (session.SessionOutcome != "TargetReached")
            {
                analysisText.AppendLine($"• Outcome: Session ended due to '{session.SessionOutcome}'");
            }
            TxtAnalysis.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, analysisText.ToString());

            // --- NEW: Success Ratio Information ---
            ShowSuccessRatioInfo(currentSessionRatio, rollingRatio, learningZone);
            // --- END NEW ---

            // Set the result and the dynamic scheduling reason
            TxtNextPracticeDate.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, nextDueDate.ToString("D", CultureHelper.Current));

            if (primaryScore >= 8.0)
            {
                TxtSchedulingReason.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, "Excellent work! Your mastery is strong. The next session is scheduled to anchor this in your long-term memory.");
            }
            else if (primaryScore >= 5.0)
            {
                TxtSchedulingReason.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, "Good progress. The next session is scheduled relatively soon to build on the momentum and solidify what you've learned.");
            }
            else
            {
                TxtSchedulingReason.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, "This passage needs more attention. The next session is scheduled soon to strengthen the foundation and prevent forgetting.");
            }

            // --- NIEUW: Coaching-boodschap voor de 'ontdekkingsfase' ---
            // Toon deze boodschap als er geen herhalingen zijn, maar er wel tijd is besteed.
            if (session.Repetitions == 0 && session.Duration.TotalMinutes >= 1)
            {
                TxtCoachingMessage.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, "Good effort! The time you invested in this 'discovery phase' is valuable. The planner recognizes this effort and has used it to schedule your next session.");
                TxtCoachingMessage.SetCurrentValue(VisibilityProperty, Visibility.Visible);
            }
            // --- EINDE NIEUW ---

            // --- NIEUW: Overlearning Recommendations ---
            ShowOverlearningRecommendations(session);
            // --- EINDE NIEUW ---

            // --- NIEUW: Success Ratio Trend Chart (via reusable control) ---
            if (_currentSection != null)
            {
                try
                {
                    SuccessRatioChartGroupBox.SetCurrentValue(VisibilityProperty, Visibility.Visible);
                    SuccessRatioChart.BarSection = _currentSection;
                    SuccessRatioChart.MaxSessions = 7;
                    SuccessRatioChart.IncludeDeleted = true; // behoud huidig gedrag
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance?.LogError("Error initializing SuccessRatioTrendChartControl in SessionReport", ex);
                    SuccessRatioChartGroupBox.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
                }
            }
            else
            {
                SuccessRatioChartGroupBox.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
            }
            // --- EINDE NIEUW ---
        }

        private void ShowSuccessRatioInfo(double currentSessionRatio, double rollingRatio, string learningZone)
        {
            try
            {
                // Only show success ratio info if we have meaningful data
                if (string.IsNullOrEmpty(learningZone) || (currentSessionRatio == 0 && rollingRatio == 0))
                {
                    SuccessRatioPanel.SetCurrentValue(VisibilityProperty, System.Windows.Visibility.Collapsed);
                    return;
                }

                SuccessRatioPanel.SetCurrentValue(VisibilityProperty, System.Windows.Visibility.Visible);

                // Format success ratio information in single line
                var ratioText = $"Current: {currentSessionRatio:P0}, 7-Session Avg: {rollingRatio:P0}";
                TxtSuccessRatioInfo.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, ratioText);

                // Extract zone name and determine color
                string displayText;
                System.Windows.Media.SolidColorBrush brush;

                // Use consistent zone naming and coloring
                displayText = learningZone switch
                {
                    "TooHard" => "Too Hard Zone",
                    "Exploration" => "Exploration Zone",
                    "Consolidation" => "Consolidation Zone",
                    "Polish" => "Polish Zone",
                    "Mastered" => "Mastered Zone",
                    _ => learningZone + " Zone"
                };

                brush = learningZone switch
                {
                    "TooHard" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red),
                    "Exploration" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange),
                    "Consolidation" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green),
                    "Polish" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkBlue),
                    "Mastered" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGreen),
                    _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray)
                };

                TxtLearningZoneInfo.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, displayText);

                // Set both the indicator ellipse and text color
                LearningZoneIndicator.SetCurrentValue(System.Windows.Shapes.Shape.FillProperty, brush);
                TxtLearningZoneInfo.SetCurrentValue(System.Windows.Controls.TextBlock.ForegroundProperty, brush);
            }
            catch (Exception ex)
            {
                ExceptionHelper.HandleException(ex, "Failed to display success ratio information");
                SuccessRatioPanel.SetCurrentValue(VisibilityProperty, System.Windows.Visibility.Collapsed);
            }
        }

        #region Override Feature Methods

        private void InitializeOverrideFeatures()
        {
            // Check if user has advanced features enabled
            var settings = SettingsManager.Instance?.CurrentSettings;
            bool enabledInSettings = settings?.EnableIntervalOverrides == true;
            bool correctExperienceLevel = settings?.MusicalExperience?.StartsWith("Intermediate") == true ||
                                        settings?.MusicalExperience?.StartsWith("Advanced") == true ||
                                        settings?.MusicalExperience?.StartsWith("Professional") == true;
            bool hasValidInterval = _originalAlgorithmInterval > 0;
            bool showOverride = enabledInSettings && correctExperienceLevel;

            if (showOverride && hasValidInterval)
            {
                try
                {
                    IntervalOverrideGroupBox.SetCurrentValue(VisibilityProperty, Visibility.Visible);
                    AlgorithmSuggestionText.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, $"Algorithm suggests: {_originalAlgorithmInterval:F1} days");

                    // Smart default: suggest same as algorithm initially
                    OverrideIntervalTextBox.SetCurrentValue(System.Windows.Controls.TextBox.TextProperty, Math.Round(_originalAlgorithmInterval).ToString());

                    // Increase window height to accommodate override section
                    this.Height += 120;
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance?.Log($"❌ Error showing override section: {ex.Message}", LogLevel.Error);
                }
            }
            else
            {
                try
                {
                    IntervalOverrideGroupBox.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance?.Log($"❌ Error hiding override section: {ex.Message}", LogLevel.Error);
                }
            }
        }

        private void EnableOverrideCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            OverrideInputPanel.SetCurrentValue(IsEnabledProperty, true);
            OverrideReasonTextBox.SetCurrentValue(IsEnabledProperty, true);
            OverrideIntervalTextBox.Focus();
            IsOverrideEnabled = true;
        }

        private void EnableOverrideCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            OverrideInputPanel.SetCurrentValue(IsEnabledProperty, false);
            OverrideReasonTextBox.SetCurrentValue(IsEnabledProperty, false);
            OverrideWarningText.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
            IsOverrideEnabled = false;
        }

        private void OverrideIntervalTextBox_TextChanged(object sender, RoutedEventArgs e)
        {
            if (!IsOverrideEnabled) return;

            if (double.TryParse(OverrideIntervalTextBox.Text, out double userInterval))
            {
                // Validate range
                if (userInterval < 1 || userInterval > 365)
                {
                    OverrideWarningText.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, "Interval must be between 1 and 365 days.");
                    OverrideWarningText.SetCurrentValue(VisibilityProperty, Visibility.Visible);
                    return;
                }

                // Check for extreme values
                if (_originalAlgorithmInterval > 0)
                {
                    double ratio = userInterval / _originalAlgorithmInterval;
                    if (ratio < 0.3)
                    {
                        OverrideWarningText.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, $"Much shorter than algorithm suggestion ({_originalAlgorithmInterval:F1}d). This may result in over-practicing.");
                        OverrideWarningText.SetCurrentValue(VisibilityProperty, Visibility.Visible);
                    }
                    else if (ratio > 3.0)
                    {
                        OverrideWarningText.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, $"Much longer than algorithm suggestion ({_originalAlgorithmInterval:F1}d). This may result in forgetting.");
                        OverrideWarningText.SetCurrentValue(VisibilityProperty, Visibility.Visible);
                    }
                    else
                    {
                        OverrideWarningText.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
                    }
                }
                else
                {
                    OverrideWarningText.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
                }
            }
            else if (!string.IsNullOrEmpty(OverrideIntervalTextBox.Text))
            {
                OverrideWarningText.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, "Please enter a valid number.");
                OverrideWarningText.SetCurrentValue(VisibilityProperty, Visibility.Visible);
            }
            else
            {
                OverrideWarningText.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
            }
        }

        private bool ValidateOverrideInput()
        {
            if (!IsOverrideEnabled) return true;

            if (!double.TryParse(OverrideIntervalTextBox.Text, out double userInterval))
            {
                MessageBox.Show("Please enter a valid number for the override interval.", "Invalid Input",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                OverrideIntervalTextBox.Focus();
                return false;
            }

            if (userInterval < 1 || userInterval > 365)
            {
                MessageBox.Show("Override interval must be between 1 and 365 days.", "Invalid Input",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                OverrideIntervalTextBox.Focus();
                return false;
            }

            // Warn for extreme overrides
            if (_originalAlgorithmInterval > 0)
            {
                double ratio = userInterval / _originalAlgorithmInterval;
                if (ratio < 0.3 || ratio > 3.0)
                {
                    var result = MessageBox.Show(
                        $"Your override ({userInterval:F1} days) is very different from the algorithm suggestion ({_originalAlgorithmInterval:F1} days).\n\n" +
                        "Are you sure you want to continue?",
                        "Extreme Override",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.No)
                    {
                        OverrideIntervalTextBox.Focus();
                        return false;
                    }
                }
            }

            return true;
        }

        private void PrepareOverrideData()
        {
            if (IsOverrideEnabled && double.TryParse(OverrideIntervalTextBox.Text, out double userInterval))
            {
                OverrideInterval = userInterval;
                OverrideReason = OverrideReasonTextBox.Text?.Trim() ?? string.Empty;

                // Create override data dictionary for the calling code
                OverrideData = new Dictionary<string, object>
                {
                    ["UserOverrideInterval"] = userInterval,
                    ["OriginalAlgorithmInterval"] = _originalAlgorithmInterval,
                    ["OverrideReason"] = OverrideReason
                };

                MLLogManager.Instance.Log(
                    $"User override prepared: Algorithm={_originalAlgorithmInterval:F1}d → User={userInterval:F1}d. " +
                    $"Reason: '{OverrideReason}'",
                    LogLevel.Info);
            }
            else
            {
                OverrideInterval = null;
                OverrideReason = string.Empty;
                OverrideData = null;
            }
        }

        #endregion

        // AANPASSING: Hernoemd van BtnSave_Click naar BtnOk_Click
        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            // NEW: Validate Override Input
            if (!ValidateOverrideInput())
            {
                return; // Don't close if validation fails
            }

            // NEW: Prepare Override Data
            PrepareOverrideData();

            // This window is now purely informational.
            // This button just closes the dialog.
            this.DialogResult = true;
            this.Close();
        }

        /// <summary>
        /// Cancel button - closes the window without saving any changes
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            // Set DialogResult to false to indicate cancellation
            this.DialogResult = false;
            this.Close();
        }



        /// <summary>
        /// Shows overlearning recommendations based on session attempts
        /// </summary>
        private void ShowOverlearningRecommendations(PracticeHistory session)
        {
            try
            {
                // Only show if there were attempts before success
                if (session.AttemptsTillSuccess <= 0)
                {
                    OverlearningRecommendationsGroupBox.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
                    return;
                }

                var overlearningTracker = new OverlearningTracker();
                int recommended50 = overlearningTracker.CalculateRequiredRepetitions(session.AttemptsTillSuccess, false);
                int recommended100 = overlearningTracker.CalculateRequiredRepetitions(session.AttemptsTillSuccess, true);

                // Update the text blocks
                TxtOverlearningInfo.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty,
                    $"Based on {session.AttemptsTillSuccess} attempts before first success:");

                Txt50OverlearningRecommendation.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty,
                    $"{recommended50} repetitions (moderate reinforcement)");

                Txt100OverlearningRecommendation.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty,
                    $"{recommended100} repetitions (maximum consolidation)");

                // Show the group box
                OverlearningRecommendationsGroupBox.SetCurrentValue(VisibilityProperty, Visibility.Visible);

                MLLogManager.Instance?.Log($"SessionReport: Showed overlearning recommendations - 50%={recommended50}, 100%={recommended100} (based on {session.AttemptsTillSuccess} attempts)", LogLevel.Info);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError("Error showing overlearning recommendations in SessionReport", ex);
                OverlearningRecommendationsGroupBox.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
            }
        }


        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // If the window is closed (not just hidden) and there is a PracticeSessionWindow,
            // make sure to show the PracticeSessionWindow
            if (_practiceSessionWindow != null && _practiceSessionWindow.IsVisible == false)
            {
                _practiceSessionWindow.Show();
            }
        }
    }
}