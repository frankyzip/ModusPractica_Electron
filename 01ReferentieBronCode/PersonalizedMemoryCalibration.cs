using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ModusPractica
{
    /// <summary>
    /// Persoonlijke geheugen kalibratie gebaseerd op Bayesiaanse parameter aanpassing
    /// Leert van individuele oefenpatronen om τ-parameters te personaliseren
    /// </summary>
    public class PersonalizedMemoryCalibration
    {
        private static PersonalizedMemoryCalibration _instance;
        private PersonalCalibrationData _calibrationData;
        private string _calibrationFilePath;

        // Singleton pattern
        public static PersonalizedMemoryCalibration Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PersonalizedMemoryCalibration();
                }
                return _instance;
            }
        }

        private PersonalizedMemoryCalibration()
        {
            InitializeCalibrationSystem();
        }

        public void InitializeCalibrationSystem()
        {
            if (!RetentionFeatureFlags.UsePMC)
            {
                // Disabled: do not initialize or touch the filesystem
                return;
            }
            try
            {
                // Profiel: gebruik het actieve profiel en centrale padprovider
                string calibrationFolder = Path.Combine(
                    DataPathProvider.GetProfileFolder(ActiveUserSession.ProfileName),
                    "Calibration");

                if (!Directory.Exists(calibrationFolder))
                {
                    Directory.CreateDirectory(calibrationFolder);
                }

                _calibrationFilePath = Path.Combine(
                    calibrationFolder,
                    "personal_memory_calibration.json"
                );

                // Handige logging: waar wordt PMC opgeslagen/gelezen?
                MLLogManager.Instance?.Log(
                    $"PMC.InitializeCalibrationSystem: Calibration file path set to '{_calibrationFilePath}'.",
                    LogLevel.Info
                );

                // Laad bestaande kalibratie of initialiseer nieuwe
                LoadCalibrationData();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError("PMC.InitializeCalibrationSystem: Failed to initialize calibration system.", ex);
                // Fail closed: laat _calibrationFilePath in de laatste bekende staat;
                // verdere calls (Save/Load) loggen hun eigen fouten.
            }
        }



        /// <summary>
        /// Berekent gepersonaliseerde τ-waarde op basis van individuele geschiedenis
        /// </summary>
        public double GetPersonalizedTau(string difficulty, int repetitions)
        {
            // If disabled, return standard calculation
            if (!RetentionFeatureFlags.UsePMC)
            {
                return EbbinghausConstants.CalculateAdjustedTau(difficulty, repetitions);
            }
            // Start met wetenschappelijke basis
            double baseTau = EbbinghausConstants.CalculateAdjustedTau(difficulty, repetitions);

            // Pas aan op basis van persoonlijke kalibratie
            double personalAdjustment = CalculatePersonalAdjustment(difficulty, repetitions);

            double personalizedTau = baseTau * personalAdjustment;

            // Log voor debugging
            MLLogManager.Instance.Log($"Personal Tau Calculation: Base Tau: {baseTau:F2} days, Personal Adjustment: {personalAdjustment:F2}x, Personalized Tau: {personalizedTau:F2} days", LogLevel.Info);

            return personalizedTau;
        }

        public void UpdateCalibrationFromSession(PracticeHistory session, BarSection section)
        {
            if (!RetentionFeatureFlags.UsePMC)
            {
                return; // disabled
            }
            if (session == null)
            {
                MLLogManager.Instance?.Log("PMC.UpdateCalibrationFromSession: session is null; skipping.", LogLevel.Warning);
                return;
            }

            // Difficulty bepalen (fallback naar Average)
            string difficulty = "Average";
            try
            {
                if (section != null && !string.IsNullOrWhiteSpace(section.Difficulty))
                    difficulty = section.Difficulty;
            }
            catch
            {
                // ignore – difficulty blijft "Average"
            }

            try
            {
                // 1) Bereken voorspelfout/accuracy (interne methode gebruikt o.a. τ-verwachting)
                double predictionAccuracy = section != null ? CalculatePredictionAccuracy(session, section) : 1.0;

                // 2) Pas persoonlijke parameters aan voor de betreffende moeilijkheid
                UpdatePersonalParameters(difficulty, predictionAccuracy);

                // 3) Persist + logging
                SaveCalibrationData();

                MLLogManager.Instance?.Log(
                    $"PMC.UpdateCalibrationFromSession: Updated calibration. Diff={difficulty}, " +
                    $"Outcome='{session.SessionOutcome}', Reps={session.Repetitions}, " +
                    $"PredAccuracy={predictionAccuracy:F3}",
                    LogLevel.Info
                );
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError("PMC.UpdateCalibrationFromSession: Error during calibration update.", ex);
            }
        }



        /// <summary>
        /// Berekent persoonlijke aanpassingsfactor op basis van opgebouwde data
        /// UPDATED: Supports rapid calibration with lower session requirements
        /// </summary>
        private double CalculatePersonalAdjustment(string difficulty, int repetitions)
        {
            // NEW: Check for rapid calibration phase (first 5 sessions get immediate but conservative adjustments)
            bool isRapidPhase = _calibrationData.TotalSessions <= 5;

            if (!HasSufficientDataForCalibration() && !isRapidPhase)
            {
                return 1.0; // Gebruik standaard waarden als onvoldoende data
            }

            // Zoek relevante kalibratie data voor deze moeilijkheidsgraad
            if (_calibrationData.DifficultyAdjustments.TryGetValue(difficulty.ToLower(), out var adjustment))
            {
                double confidenceFactor;

                if (isRapidPhase)
                {
                    // Rapid calibration: lower confidence but still apply adjustments
                    confidenceFactor = Math.Min(0.6, adjustment.SessionCount / 3.0); // Max 60% confidence in rapid phase
                    MLLogManager.Instance?.Log(
                        $"PMC: Rapid calibration active for {difficulty} (sessions={adjustment.SessionCount}, confidence={confidenceFactor:F2})",
                        LogLevel.Debug);
                }
                else
                {
                    // Normal calibration: gradual confidence buildup
                    confidenceFactor = Math.Min(1.0, _calibrationData.TotalSessions / 25.0); // Reduced from 50 to 25
                }

                double personalizedFactor = 1.0 + (adjustment.AdjustmentFactor - 1.0) * confidenceFactor;

                return Math.Max(0.3, Math.Min(3.0, personalizedFactor)); // Veilige grenzen
            }

            return 1.0;
        }

        /// <summary>
        /// Berekent hoe accuraat onze interval voorspelling was
        /// </summary>
        private double CalculatePredictionAccuracy(PracticeHistory session, BarSection section)
        {
            // Bereken verwachte retentie op basis van interval
            double daysSincePractice = (session.Date - (section.LastPracticeDate ?? session.Date)).TotalDays;
            if (daysSincePractice <= 0) return 1.0;

            double expectedTau = EbbinghausConstants.CalculateAdjustedTau(section.Difficulty, section.CompletedRepetitions);
            double expectedRetention = EbbinghausConstants.CalculateRetention(daysSincePractice, expectedTau);

            // Schat werkelijke retentie op basis van sessie prestatie
            double actualRetention = EstimateActualRetention(session);

            // Bereken accuraatheid (1.0 = perfect, 0.0 = volledig verkeerd)
            double accuracy = 1.0 - Math.Abs(expectedRetention - actualRetention);
            return Math.Max(0.0, Math.Min(1.0, accuracy));
        }

        /// <summary>
        /// Schat werkelijke retentie op basis van sessie prestaties
        /// </summary>
        private double EstimateActualRetention(PracticeHistory session)
        {
            // Eenvoudige schatting op basis van herhalingen en tijd
            // Meer herhalingen in minder tijd = betere retentie
            if (session.Duration.TotalMinutes <= 0) return 0.5;

            double repetitionsPerMinute = session.Repetitions / session.Duration.TotalMinutes;

            // Normaliseer naar 0-1 schaal (0.5-2.0 rep/min = normale range)
            double normalizedEfficiency = Math.Max(0.1, Math.Min(1.0, repetitionsPerMinute / 2.0));

            // Aanpassing op basis van attempts till success
            if (session.AttemptsTillSuccess > 0)
            {
                double attemptsAdjustment = 1.0 / Math.Sqrt(session.AttemptsTillSuccess);
                normalizedEfficiency *= attemptsAdjustment;
            }

            return Math.Max(0.1, Math.Min(1.0, normalizedEfficiency));
        }

        /// <summary>
        /// Update persoonlijke parameters met Bayesiaanse learning
        /// </summary>
        private void UpdatePersonalParameters(string difficulty, double accuracy)
        {
            string difficultyKey = difficulty.ToLower();

            if (!_calibrationData.DifficultyAdjustments.ContainsKey(difficultyKey))
            {
                _calibrationData.DifficultyAdjustments[difficultyKey] = new DifficultyAdjustment
                {
                    AdjustmentFactor = 1.0,
                    Confidence = 0.0,
                    SessionCount = 0
                };
            }

            var adjustment = _calibrationData.DifficultyAdjustments[difficultyKey];

            // Bayesiaanse update met learning rate
            double learningRate = EbbinghausConstants.BAYESIAN_LEARNING_RATE;
            double targetAdjustment = accuracy < 0.5 ?
                (accuracy < 0.3 ? 0.7 : 0.85) :  // Te snel vergeten -> korter tau
                (accuracy > 0.8 ? 1.3 : 1.15);   // Te goed onthouden -> langer tau

            // Geleidelijke aanpassing naar doel
            adjustment.AdjustmentFactor += learningRate * (targetAdjustment - adjustment.AdjustmentFactor);
            adjustment.SessionCount++;
            adjustment.Confidence = Math.Min(1.0, adjustment.SessionCount / 20.0);

            _calibrationData.TotalSessions++;
        }

        /// <summary>
        /// Controleert of er voldoende data is voor betrouwbare kalibratie
        /// UPDATED: Reduced minimum sessions for faster adaptation
        /// </summary>
        private bool HasSufficientDataForCalibration()
        {
            // Reduced from 10 to 5 sessions for faster user adaptation
            const int ACCELERATED_MIN_SESSIONS = 5;
            return _calibrationData.TotalSessions >= ACCELERATED_MIN_SESSIONS;
        }

        /// <summary>
        /// Laadt kalibratie data uit bestand
        /// </summary>
        private void LoadCalibrationData()
        {
            if (!RetentionFeatureFlags.UsePMC)
            {
                _calibrationData = new PersonalCalibrationData();
                return;
            }
            try
            {
                if (File.Exists(_calibrationFilePath))
                {
                    string jsonContent = File.ReadAllText(_calibrationFilePath);
                    _calibrationData = JsonSerializer.Deserialize<PersonalCalibrationData>(jsonContent)
                                     ?? new PersonalCalibrationData();
                }
                else
                {
                    _calibrationData = new PersonalCalibrationData();
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"Error loading calibration data from {_calibrationFilePath}: {ex.Message}", ex);
                _calibrationData = new PersonalCalibrationData();
            }
        }

        /// <summary>
        /// Slaat kalibratie data op naar bestand
        /// </summary>
        private void SaveCalibrationData()
        {
            if (!RetentionFeatureFlags.UsePMC) return;
            try
            {
                string jsonContent = JsonSerializer.Serialize(_calibrationData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(_calibrationFilePath, jsonContent);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"Error saving calibration data to {_calibrationFilePath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Geeft calibratie statistieken voor debugging
        /// </summary>
        public CalibrationStats GetCalibrationStats()
        {
            if (!RetentionFeatureFlags.UsePMC)
            {
                return new CalibrationStats
                {
                    TotalSessions = 0,
                    IsCalibrated = false,
                    DifficultyAdjustments = new Dictionary<string, object>()
                };
            }
            return new CalibrationStats
            {
                TotalSessions = _calibrationData.TotalSessions,
                IsCalibrated = HasSufficientDataForCalibration(),
                DifficultyAdjustments = _calibrationData.DifficultyAdjustments.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object)new
                    {
                        Factor = kvp.Value.AdjustmentFactor,
                        Confidence = kvp.Value.Confidence,
                        Sessions = kvp.Value.SessionCount
                    })
            };
        }
    }

    #region Data Classes

    /// <summary>
    /// Data klasse voor persoonlijke kalibratie informatie
    /// </summary>
    public class PersonalCalibrationData
    {
        public int TotalSessions { get; set; } = 0;
        public Dictionary<string, DifficultyAdjustment> DifficultyAdjustments { get; set; } =
            new Dictionary<string, DifficultyAdjustment>();
        public DateTime LastCalibrationUpdate { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Aanpassingsdata per moeilijkheidsgraad
    /// </summary>
    public class DifficultyAdjustment
    {
        public double AdjustmentFactor { get; set; } = 1.0;
        public double Confidence { get; set; } = 0.0;
        public int SessionCount { get; set; } = 0;
    }

    /// <summary>
    /// Kalibratie statistieken voor UI weergave
    /// </summary>
    public class CalibrationStats
    {
        public int TotalSessions { get; set; }
        public bool IsCalibrated { get; set; }
        public Dictionary<string, object> DifficultyAdjustments { get; set; } = new Dictionary<string, object>();
    }

    #endregion Data Classes
}