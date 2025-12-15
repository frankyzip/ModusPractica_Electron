using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ModusPractica
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            // Attach event handlers for the buttons
            BtnSave.Click += BtnSave_Click;
            BtnCancel.Click += BtnCancel_Click;
            BtnResetSpacedRepetition.Click += BtnResetSpacedRepetition_Click;

            // Load the current settings into the UI when the window opens
            LoadSettingsToUI();
        }
        // AANPASSING
        /// <summary>
        /// Populates the UI controls with the currently saved settings.
        /// </summary>
        private void LoadSettingsToUI()
        {
            // --- GEDEELTE 1: CULTUUR-INSTELLINGEN ---
            var availableCultures = new List<object>
    {
        new { DisplayName = "(Use System Default)", Name = string.Empty }
    };
            var cultureNames = new[] { "en-US", "en-GB", "nl-BE", "nl-NL", "de-DE", "fr-FR" };
            foreach (var name in cultureNames)
            {
                try
                {
                    var cultureInfo = new CultureInfo(name);
                    availableCultures.Add(new { DisplayName = cultureInfo.NativeName, Name = cultureInfo.Name });
                }
                catch (CultureNotFoundException ex)
                {
                    MLLogManager.Instance.LogError($"Culture not found on this system: {name}", ex);
                }
            }
            CmbCulture.ItemsSource = availableCultures;
            CmbCulture.DisplayMemberPath = "DisplayName";
            string savedCultureName = SettingsManager.Instance.CurrentSettings.SelectedCultureName;
            var selectedCultureItem = availableCultures
                .FirstOrDefault(c => ((dynamic)c).Name == savedCultureName);
            CmbCulture.SelectedItem = selectedCultureItem ?? availableCultures[0];

            // --- GEDEELTE 2: CHECKBOXES ---
            ChkShowTips.IsChecked = SettingsManager.Instance.CurrentSettings.ShowPostSessionTips;
            ChkShowSessionReport.IsChecked = SettingsManager.Instance.CurrentSettings.ShowSessionReport; // NIEUWE REGEL
            ChkPreferPlannerScore.IsChecked = SettingsManager.Instance.CurrentSettings.PreferPlannerScoreInReport;
            ChkEnableIntervalOverrides.IsChecked = SettingsManager.Instance.CurrentSettings.EnableIntervalOverrides; // NEW: Override feature

            // --- GEDEELTE 3: PERSONALIZATION PROFILE ---
            // Load actual profile data from SettingsManager

            // Set musical experience ComboBox
            if (!string.IsNullOrEmpty(SettingsManager.Instance.CurrentSettings.MusicalExperience))
            {
                foreach (ComboBoxItem item in CmbMusicalExperience.Items)
                {
                    if (item.Content?.ToString()?.Equals(SettingsManager.Instance.CurrentSettings.MusicalExperience, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        CmbMusicalExperience.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, item);
                        break;
                    }
                }
            }
            else
            {
                CmbMusicalExperience.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedIndexProperty, 1); // Default to "Intermediate"
            }

            // --- GEDEELTE 4: PRACTICE SESSION MODE ---
            foreach (ComboBoxItem item in CmbPracticeSessionMode.Items)
            {
                if (item.Tag?.ToString() == SettingsManager.Instance.CurrentSettings.PracticeSessionMode)
                {
                    CmbPracticeSessionMode.SelectedItem = item;
                    break;
                }
            }
            if (CmbPracticeSessionMode.SelectedItem == null)
            {
                CmbPracticeSessionMode.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedIndexProperty, 1); // Default to Advanced
            }

            // --- GEDEELTE 5: SPACED REPETITION PARAMETERS ---
            LoadSpacedRepetitionSettingsToUI();

            // --- GEDEELTE 6: MAXIMALE OEFENTIJD --- (Verwijderd)
        }

        // AANPASSING
        /// <summary>
        /// Handles the click event for the Save button.
        /// </summary>
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // 1. Get the selected values from the UI
            string selectedCultureName = string.Empty;
            if (CmbCulture.SelectedItem != null)
            {
                selectedCultureName = ((dynamic)CmbCulture.SelectedItem).Name;
            }
            bool showTips = ChkShowTips.IsChecked ?? true;
            bool showReport = ChkShowSessionReport.IsChecked ?? true; // NIEUWE REGEL
            bool preferPlanner = ChkPreferPlannerScore.IsChecked ?? false;
            bool enableOverrides = ChkEnableIntervalOverrides.IsChecked ?? false; // NEW: Override feature

            // Extract values from personalization profile
            string experience = ((ComboBoxItem)CmbMusicalExperience.SelectedItem)?.Content?.ToString() ?? "Intermediate";
            string practiceMode = ((ComboBoxItem)CmbPracticeSessionMode.SelectedItem)?.Tag?.ToString() ?? "Advanced";

            // 2. Update the settings object
            SettingsManager.Instance.CurrentSettings.SelectedCultureName = selectedCultureName;
            SettingsManager.Instance.CurrentSettings.ShowPostSessionTips = showTips;
            SettingsManager.Instance.CurrentSettings.ShowSessionReport = showReport; // NIEUWE REGEL
            SettingsManager.Instance.CurrentSettings.PreferPlannerScoreInReport = preferPlanner;
            SettingsManager.Instance.CurrentSettings.EnableIntervalOverrides = enableOverrides; // NEW: Override feature
            SettingsManager.Instance.CurrentSettings.PracticeSessionMode = practiceMode; // NEW: Practice mode

            // Update personalization profile
            // Age verwijderd in v3.0 (niet meer gebruikt in berekeningen)
            SettingsManager.Instance.CurrentSettings.MusicalExperience = experience;

            // Update spaced repetition settings
            SaveSpacedRepetitionSettingsFromUI();

            // Apply adaptive systems setting
            RetentionFeatureFlags.Configure(useAdaptiveSystems: SettingsManager.Instance.CurrentSettings.UseAdaptiveSystems);

            // Log the values being saved
            MLLogManager.Instance.Log($"SettingsWindow: Saving ShowPostSessionTips as: {showTips}", LogLevel.Info);
            MLLogManager.Instance.Log($"SettingsWindow: Saving ShowSessionReport as: {showReport}", LogLevel.Info); // NIEUWE REGEL
            MLLogManager.Instance.Log($"SettingsWindow: Saving PreferPlannerScoreInReport as: {preferPlanner}", LogLevel.Info);
            MLLogManager.Instance.Log($"SettingsWindow: Saving PracticeSessionMode as: {practiceMode}", LogLevel.Info);
            MLLogManager.Instance.Log($"SettingsWindow: Saving personalization - Experience: '{experience}' (Age verwijderd in v3.0)", LogLevel.Info);

            // 3. Save the settings to the file
            SettingsManager.Instance.SaveSettings();

            // 4. Inform the user and close the window
            MessageBox.Show(
                "Settings have been saved. Some changes may require a restart to take full effect.",
                "Settings Saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            this.Close();
        }

        /// <summary>
        /// Handles the click event for the Cancel button.
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Loads spaced repetition settings into the UI controls.
        /// </summary>
        private void LoadSpacedRepetitionSettingsToUI()
        {
            var settings = SettingsManager.Instance.CurrentSettings;

            // Legacy migration: verhoog een oude default 0.8 naar 1.0 bij laden
            if (Math.Abs(settings.BeginnerTauMultiplier - 0.8) < 0.0001)
            {
                settings.BeginnerTauMultiplier = 1.0;
                SettingsManager.Instance.SaveSettings();
            }

            TxtGlobalIntervalMultiplier.SetCurrentValue(TextBox.TextProperty, settings.GlobalIntervalMultiplier.ToString("F1"));
            TxtBeginnerTauMultiplier.SetCurrentValue(TextBox.TextProperty, settings.BeginnerTauMultiplier.ToString("F1"));
            TxtIntermediateTauMultiplier.SetCurrentValue(TextBox.TextProperty, settings.IntermediateTauMultiplier.ToString("F1"));
            TxtAdvancedTauMultiplier.SetCurrentValue(TextBox.TextProperty, settings.AdvancedTauMultiplier.ToString("F1"));

            TxtEasyRetentionTarget.SetCurrentValue(TextBox.TextProperty, (settings.EasyRetentionTarget * 100).ToString("F0") + "%");
            TxtAverageRetentionTarget.SetCurrentValue(TextBox.TextProperty, (settings.AverageRetentionTarget * 100).ToString("F0") + "%");
            TxtDifficultRetentionTarget.SetCurrentValue(TextBox.TextProperty, (settings.DifficultRetentionTarget * 100).ToString("F0") + "%");
            TxtMasteredRetentionTarget.SetCurrentValue(TextBox.TextProperty, (settings.MasteredRetentionTarget * 100).ToString("F0") + "%");

            TxtPerformancePenaltyThreshold.SetCurrentValue(TextBox.TextProperty, settings.PerformancePenaltyThreshold.ToString("F1"));
            TxtFrustrationCooldownDays.SetCurrentValue(TextBox.TextProperty, settings.FrustrationCooldownDays.ToString("F1"));
            TxtManualFrustrationCooldownDays.SetCurrentValue(TextBox.TextProperty, settings.ManualFrustrationCooldownDays.ToString("F1"));

            // NEW: Adaptive systems
            ChkUseAdaptiveSystems.IsChecked = settings.UseAdaptiveSystems;
        }

        /// <summary>
        /// Saves spaced repetition settings from the UI controls to the settings object.
        /// </summary>
        private void SaveSpacedRepetitionSettingsFromUI()
        {
            var settings = SettingsManager.Instance.CurrentSettings;

            // Global multiplier
            if (double.TryParse(TxtGlobalIntervalMultiplier.Text?.Trim(), out double globalMultiplier) && globalMultiplier > 0)
            {
                settings.GlobalIntervalMultiplier = globalMultiplier;
            }

            // Tau multipliers
            if (double.TryParse(TxtBeginnerTauMultiplier.Text?.Trim(), out double beginnerMultiplier) && beginnerMultiplier > 0)
            {
                settings.BeginnerTauMultiplier = beginnerMultiplier;
            }

            if (double.TryParse(TxtIntermediateTauMultiplier.Text?.Trim(), out double intermediateMultiplier) && intermediateMultiplier > 0)
            {
                settings.IntermediateTauMultiplier = intermediateMultiplier;
            }

            if (double.TryParse(TxtAdvancedTauMultiplier.Text?.Trim(), out double advancedMultiplier) && advancedMultiplier > 0)
            {
                settings.AdvancedTauMultiplier = advancedMultiplier;
            }

            // Retention targets (parse percentages)
            settings.EasyRetentionTarget = ParsePercentage(TxtEasyRetentionTarget.Text?.Trim(), 0.75);
            settings.AverageRetentionTarget = ParsePercentage(TxtAverageRetentionTarget.Text?.Trim(), 0.80);
            settings.DifficultRetentionTarget = ParsePercentage(TxtDifficultRetentionTarget.Text?.Trim(), 0.85);
            settings.MasteredRetentionTarget = ParsePercentage(TxtMasteredRetentionTarget.Text?.Trim(), 0.90);

            // Performance and frustration settings
            if (double.TryParse(TxtPerformancePenaltyThreshold.Text?.Trim(), out double penaltyThreshold) && penaltyThreshold >= 0)
            {
                settings.PerformancePenaltyThreshold = penaltyThreshold;
            }

            if (double.TryParse(TxtFrustrationCooldownDays.Text?.Trim(), out double frustrationCooldown) && frustrationCooldown >= 0)
            {
                settings.FrustrationCooldownDays = frustrationCooldown;
            }

            if (double.TryParse(TxtManualFrustrationCooldownDays.Text?.Trim(), out double manualFrustrationCooldown) && manualFrustrationCooldown >= 0)
            {
                settings.ManualFrustrationCooldownDays = manualFrustrationCooldown;
            }

            // NEW: Adaptive systems
            settings.UseAdaptiveSystems = ChkUseAdaptiveSystems.IsChecked ?? false;
        }

        /// <summary>
        /// Parses a percentage string (e.g., "75%" or "75") to a decimal value (e.g., 0.75).
        /// </summary>
        private double ParsePercentage(string? input, double defaultValue)
        {
            if (string.IsNullOrWhiteSpace(input))
                return defaultValue;

            string cleanInput = input.Replace("%", "").Trim();

            if (double.TryParse(cleanInput, out double value))
            {
                // If value is > 1, assume it's a percentage (e.g., 75 -> 0.75)
                if (value > 1)
                    return Math.Max(0.1, Math.Min(1.0, value / 100.0));
                // If value is <= 1, assume it's already a decimal (e.g., 0.75)
                else
                    return Math.Max(0.1, Math.Min(1.0, value));
            }

            return defaultValue;
        }

        /// <summary>
        /// Handles the click event for the Reset Spaced Repetition button.
        /// </summary>
        private void BtnResetSpacedRepetition_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all spaced repetition settings to default values?",
                "Reset to Defaults",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Reset to scientific defaults
                TxtGlobalIntervalMultiplier.SetCurrentValue(TextBox.TextProperty, "1.0");
                TxtBeginnerTauMultiplier.SetCurrentValue(TextBox.TextProperty, "0.8");
                TxtIntermediateTauMultiplier.SetCurrentValue(TextBox.TextProperty, "1.0");
                TxtAdvancedTauMultiplier.SetCurrentValue(TextBox.TextProperty, "1.2");
                TxtEasyRetentionTarget.SetCurrentValue(TextBox.TextProperty, "75%");
                TxtAverageRetentionTarget.SetCurrentValue(TextBox.TextProperty, "80%");
                TxtDifficultRetentionTarget.SetCurrentValue(TextBox.TextProperty, "85%");
                TxtMasteredRetentionTarget.SetCurrentValue(TextBox.TextProperty, "90%");
                TxtPerformancePenaltyThreshold.SetCurrentValue(TextBox.TextProperty, "5.0");
                TxtFrustrationCooldownDays.SetCurrentValue(TextBox.TextProperty, "3.0");
                TxtManualFrustrationCooldownDays.SetCurrentValue(TextBox.TextProperty, "2.0");

                MessageBox.Show("Spaced repetition settings reset to default values.",
                               "Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}