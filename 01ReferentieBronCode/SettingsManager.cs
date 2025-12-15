using System;
using System.IO;
using System.Text.Json;

namespace ModusPractica
{
    // ADJUSTMENT
    public class UserSettings
    {
        public string SelectedCultureName { get; set; } = string.Empty;
        public bool ShowPostSessionTips { get; set; } = true;
        // This setting is no longer configurable from UI, but kept for compatibility with existing code
        // We're setting a high value to effectively make it unlimited by default
        public int MaxDailyPracticeTimeMinutes { get; set; } = 9999;
        // NEW: Flag to track if the performance score migration has been completed.
        public bool HasMigratedPerformanceScores { get; set; } = false;
        public bool ShowSessionReport { get; set; } = true; // Default is true to maintain current behavior
                                                            // NEW: If true, show the planner score (tempo-agnostic) as the primary score in the session report
        public bool PreferPlannerScoreInReport { get; set; } = false;

        // NEW: Demografische personalisatie parameters
        /// <summary>
        /// Leeftijd van de gebruiker voor personalisatie van de vergeetcurve.
        /// Default 25 (midden van optimale range voor baseline tau).
        /// </summary>
        public int Age { get; set; } = 25;

        /// <summary>
        /// Geslacht van de gebruiker. Opties: "Man", "Vrouw", of leeg voor geen personalisatie.
        /// Gebruikt voor geslacht-specifieke geheugen consolidatie aanpassingen.
        /// </summary>
        public string Gender { get; set; } = string.Empty;

        /// <summary>
        /// Muzikale ervaring niveau. Opties: "Beginner", "Intermediate", "Advanced", "Professional".
        /// Default "Intermediate" voor brede toegankelijkheid, van beginners tot gevorderden.
        /// </summary>
        public string MusicalExperience { get; set; } = "Intermediate";

        // NEW: Spaced Repetition Parameters
        /// <summary>
        /// Globale multiplier voor alle intervallen. 1.0 = standaard, >1.0 = langere intervallen, <1.0 = kortere intervallen.
        /// </summary>
        public double GlobalIntervalMultiplier { get; set; } = 1.0;

        /// <summary>
        /// Tau multiplier voor beginners. Lagere waarde (<1.0) betekent kortere intervallen.
        /// Gebaseerd op Ebbinghaus: beginners hebben kortere retentie, dus snellere herhalingen.
        /// </summary>
        public double BeginnerTauMultiplier { get; set; } = 0.8;

        /// <summary>
        /// Tau multiplier voor intermediate spelers. Referentiewaarde.
        /// </summary>
        public double IntermediateTauMultiplier { get; set; } = 1.0;

        /// <summary>
        /// Tau multiplier voor gevorderde spelers. Hogere waarde (>1.0) betekent langere intervallen.
        /// Gebaseerd op onderzoek: experts hebben sterkere encoding, dus langere desirable difficulty.
        /// </summary>
        public double AdvancedTauMultiplier { get; set; } = 1.3;

        /// <summary>
        /// Tau multiplier voor professionele muzikanten. Hoogste waarde voor maximale "desirable difficulty".
        /// Gebaseerd op encoding strength hypothesis: experts kunnen langere intervallen aan.
        /// </summary>
        public double ProfessionalTauMultiplier { get; set; } = 1.3;

        /// <summary>
        /// Gewenste retentie percentage voor makkelijke stukken. Lager = langere intervallen.
        /// </summary>
        public double EasyRetentionTarget { get; set; } = 0.75;

        /// <summary>
        /// Gewenste retentie percentage voor gemiddelde stukken.
        /// </summary>
        public double AverageRetentionTarget { get; set; } = 0.80;

        /// <summary>
        /// Gewenste retentie percentage voor moeilijke stukken. Hoger = kortere intervallen.
        /// </summary>
        public double DifficultRetentionTarget { get; set; } = 0.85;

        /// <summary>
        /// Gewenste retentie percentage voor beheerste stukken.
        /// </summary>
        public double MasteredRetentionTarget { get; set; } = 0.90;

        /// <summary>
        /// Performance score drempel voor straf. Scores onder deze waarde verkorten het volgende interval.
        /// </summary>
        public double PerformancePenaltyThreshold { get; set; } = 5.0;

        /// <summary>
        /// Aantal dagen cooldown na automatische frustratie detectie.
        /// </summary>
        public double FrustrationCooldownDays { get; set; } = 3.0;

        /// <summary>
        /// Aantal dagen cooldown na handmatige frustratie markering.
        /// </summary>
        public double ManualFrustrationCooldownDays { get; set; } = 2.0;

        /// <summary>
        /// Bepaalt of playlist resultaten de spaced repetition planning beïnvloeden.
        /// ALTIJD false voor data-integriteit. Playlist sessies zijn alleen voor familiariteit.
        /// </summary>
        public bool PlaylistAffectsSpacedRepetition { get; set; } = false;

        // NEW: Advanced Feature Settings for Interval Overrides
        /// <summary>
        /// Enables advanced users to override interval suggestions in session reports.
        /// Default false - this is an advanced feature not suitable for beginners.
        /// </summary>
        public bool EnableIntervalOverrides { get; set; } = false;

        /// <summary>
        /// Shows algorithm suggestions when override is enabled.
        /// Helps users make informed override decisions.
        /// </summary>
        public bool ShowAlgorithmSuggestions { get; set; } = true;

        /// <summary>
        /// Shows statistics about user override patterns.
        /// Helps users understand their override behavior and effectiveness.
        /// </summary>
        public bool ShowOverrideStatistics { get; set; } = false;

        /// <summary>
        /// Minimum user experience level required for advanced features.
        /// Prevents beginners from accidentally enabling complex features.
        /// </summary>
        public string MinimumExperienceForAdvancedFeatures { get; set; } = "Intermediate";

        /// <summary>
        /// Enables adaptive tau adjustments based on individual section performance.
        /// When enabled, intervals are automatically adjusted per section based on SuccessRatio.
        /// </summary>
        public bool UseAdaptiveSystems { get; set; } = true;

        /// <summary>
        /// Practice session interface mode. 
        /// "Simple" = streamlined UI with post-session evaluation.
        /// "Advanced" = detailed real-time tracking with all metrics.
        /// </summary>
        public string PracticeSessionMode { get; set; } = "Advanced";

        // Custom quick link to open YouTube (per profile)
        public string YouTubeQuickLink { get; set; } = string.Empty;
    }

    public class SettingsManager
    {
        private static SettingsManager? _instance;
        public static SettingsManager Instance => _instance ??= new SettingsManager();

        private string _filePath = string.Empty;

        public UserSettings CurrentSettings { get; private set; }

        private SettingsManager()
        {
            // Constructor is nu leeg, initialisatie gebeurt later.
            CurrentSettings = new UserSettings(); // Zorg dat er altijd een default object is.
        }

        // --- NIEUWE METHODE ---
        public void InitializeForUser(string profileName)
        {
            string settingsFolder = DataPathProvider.GetProfileFolder(profileName);
            _filePath = Path.Combine(settingsFolder, "usersettings.json");

            // Herlaad de instellingen voor de geselecteerde gebruiker.
            LoadSettings();
        }

        public void LoadSettings()
        {
            // If the path isn't set yet, keep defaults.
            if (string.IsNullOrEmpty(_filePath))
            {
                CurrentSettings = new UserSettings();
                return;
            }

            try
            {
                if (File.Exists(_filePath))
                {
                    // Locked, consistent read
                    string json = FileLockManager.ReadAllTextWithLock(_filePath);
                    CurrentSettings = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();

                    // Migration: if BeginnerTauMultiplier is legacy 0.8, bump to 1.0
                    if (Math.Abs(CurrentSettings.BeginnerTauMultiplier - 0.8) < 0.0001)
                    {
                        CurrentSettings.BeginnerTauMultiplier = 1.0;
                        SaveSettings();
                    }
                }
                else
                {
                    // No settings file yet: create defaults and persist
                    CurrentSettings = new UserSettings();
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"Error loading settings from {_filePath}: {ex.Message}. Using default settings.", ex);
                CurrentSettings = new UserSettings();
            }
        }


        public void SaveSettings()
        {
            if (string.IsNullOrEmpty(_filePath)) return;
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(CurrentSettings, options);

                // Use the same atomic write pattern as the rest of the app
                FileLockManager.WriteAllTextWithLock(_filePath, json);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"Error saving settings to {_filePath}: {ex.Message}", ex);
            }
        }

    }
}