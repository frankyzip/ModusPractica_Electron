// File: BarSection.cs

using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace ModusPractica
{
    /// <summary>
    /// Lifecycle state of a bar section determining scheduling behavior.
    /// </summary>
    public enum LifecycleState
    {
        /// <summary>Normal active practice with standard scheduling.</summary>
        Active = 0,
        /// <summary>Long-term maintenance with minimum 60-day intervals.</summary>
        Maintenance = 1,
        /// <summary>Inactive sections are never scheduled.</summary>
        Inactive = 2
    }

    /// <summary>
    /// Difficulty impacts target retention (R*) via the central mapping.
    /// NextDueDate here is not authoritative; scheduling JSON is the SSOT. This value is updated by one-way sync.
    /// </summary>
    public class BarSection : INotifyPropertyChanged
    {
        private Guid _id;
        private string _barRange = string.Empty;
        private string _description = string.Empty;
        private int _targetRepetitions;
        private int _completedRepetitions;
        private DateTime? _lastPracticeDate;
        private DateTime? _nextDueDate;
        private int _interval;
        private string _status;
        private string _difficulty;
        private DateTime? _startDate;
        private int _attemptsTillSuccess;
        private int _targetTempo;

        // --- ADDED PROPERTY ---
        private int _practiceScheduleStage;

        // --- NEW PROPERTY FOR FIX 3 ---
        private DateTime? _lastFrustrationDate;

        // --- ADAPTIVE TAU ENHANCEMENT ---
        /// <summary>
        /// Per-section adaptive tau multiplier for personalized interval adjustments.
        /// Range: 0.5 - 2.0, Default: 1.0 (neutral)
        /// Applied in addition to global demographic and calibration adjustments.
        /// </summary>
        private double _adaptiveTauMultiplier = 1.0;

        // --- LIFECYCLE STATE ---
        /// <summary>
        /// Tracks the lifecycle state of this section: Active, Maintenance, or Inactive.
        /// Determines scheduling behavior and minimum intervals.
        /// </summary>
        private LifecycleState _lifecycleState = LifecycleState.Active;

        // --- PARENT MUSIC PIECE REFERENCE ---
        /// <summary>
        /// Reference to the parent MusicPiece ID for lifecycle persistence.
        /// Used to find the correct JSON file when saving lifecycle changes.
        /// </summary>
        private Guid _parentMusicPieceId;


        public BarSection()
        {
            _id = Guid.NewGuid();
            // STANDARDIZED: Use DateHelper for consistent date initialization
            _startDate = DateHelper.GetCurrentSessionDate();
            _targetRepetitions = 10;
            _completedRepetitions = 0;
            _interval = 1;
            _status = "New";
            _difficulty = "Difficult"; // ← CHANGED DEFAULT FROM "Average" TO "Difficult"
            _attemptsTillSuccess = 0;
            _targetTempo = 0;
            _practiceScheduleStage = 0; // Start at stage 0 (brand new)

            // NextDueDate is no longer automatically set during construction
            // because scheduled_sessions.json is the single source of truth
        }


        /// <summary>
        /// Tracks the current stage of the section within the multi-phase
        /// Gebrian/Ebbinghaus hybrid learning schedule.
        /// </summary>
        public int PracticeScheduleStage
        {
            get { return _practiceScheduleStage; }
            set
            {
                if (_practiceScheduleStage != value)
                {
                    _practiceScheduleStage = value;
                    OnPropertyChanged(nameof(PracticeScheduleStage));
                }
            }
        }

        // --- NEW PUBLIC PROPERTY FOR FIX 3 ---
        /// <summary>
        /// Stores the date of the last session that was marked with frustration.
        /// This is used to prevent applying a frustration penalty multiple times.
        /// </summary>
        public DateTime? LastFrustrationDate
        {
            get { return _lastFrustrationDate; }
            set
            {
                if (_lastFrustrationDate != value)
                {
                    _lastFrustrationDate = value;
                    OnPropertyChanged(nameof(LastFrustrationDate));
                }
            }
        }
        // --- END OF NEW PROPERTY ---

        /// <summary>
        /// Per-section adaptive tau multiplier for personalized interval adjustments.
        /// Automatically adjusted based on practice session performance.
        /// Range: 0.5 - 2.0, Default: 1.0 (neutral)
        /// </summary>
        public double AdaptiveTauMultiplier
        {
            get { return _adaptiveTauMultiplier; }
            set
            {
                // Clamp to safe bounds
                double clampedValue = Math.Max(0.5, Math.Min(2.0, value));
                if (Math.Abs(_adaptiveTauMultiplier - clampedValue) > 0.001)
                {
                    _adaptiveTauMultiplier = clampedValue;
                    OnPropertyChanged(nameof(AdaptiveTauMultiplier));

                    // Log significant changes for debugging
                    if (Math.Abs(clampedValue - 1.0) > 0.1)
                    {
                        MLLogManager.Instance?.Log(
                            $"BarSection {BarRange}: Adaptive tau multiplier updated to {clampedValue:F3}x",
                            LogLevel.Debug);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the lifecycle state of this section.
        /// Active: Normal scheduling behavior.
        /// Maintenance: Minimum 60-day intervals, reset overdue status.
        /// Inactive: Never scheduled, NextDueDate cleared.
        /// </summary>
        public LifecycleState LifecycleState
        {
            get { return _lifecycleState; }
            set
            {
                if (_lifecycleState != value)
                {
                    var oldState = _lifecycleState;
                    _lifecycleState = value;
                    OnPropertyChanged(nameof(LifecycleState));

                    // Apply side-effects through centralized service
                    SectionLifecycleService.Apply(this, oldState, value);
                }
            }
        }

        public int TargetTempo
        {
            get { return _targetTempo; }
            set
            {
                if (_targetTempo != value)
                {
                    _targetTempo = value;
                    OnPropertyChanged(nameof(TargetTempo));
                }
            }
        }

        public int AttemptsTillSuccess
        {
            get { return _attemptsTillSuccess; }
            set
            {
                if (_attemptsTillSuccess != value)
                {
                    _attemptsTillSuccess = value;
                    OnPropertyChanged(nameof(AttemptsTillSuccess));
                }
            }
        }

        public Guid Id
        {
            get { return _id; }
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged(nameof(Id));
                }
            }
        }

        public string BarRange
        {
            get { return _barRange; }
            set
            {
                if (_barRange != value)
                {
                    _barRange = value;
                    OnPropertyChanged(nameof(BarRange));
                }
            }
        }

        public string Description
        {
            // Ensure consumers never see null; treat as empty string
            get { return _description ?? string.Empty; }
            set
            {
                string newValue = value?.Trim() ?? string.Empty;
                if ((_description ?? string.Empty) != newValue)
                {
                    _description = newValue;
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        public int TargetRepetitions
        {
            get { return _targetRepetitions; }
            set
            {
                if (_targetRepetitions != value)
                {
                    _targetRepetitions = value;
                    OnPropertyChanged(nameof(TargetRepetitions));
                    // Progress depends on target repetitions as a cap
                    OnPropertyChanged(nameof(ProgressPercentage));
                    // NextDueDate is no longer automatically adjusted
                    // because scheduled_sessions.json is the single source of truth
                }
            }
        }

        public int CompletedRepetitions
        {
            get { return _completedRepetitions; }
            set
            {
                if (_completedRepetitions != value)
                {
                    _completedRepetitions = value;
                    OnPropertyChanged(nameof(CompletedRepetitions));
                    UpdateStatus();
                    // Progress depends on completed repetitions
                    OnPropertyChanged(nameof(ProgressPercentage));
                    // NextDueDate is no longer automatically adjusted
                    // because scheduled_sessions.json is the single source of truth
                }
            }
        }

        public DateTime? LastPracticeDate
        {
            get { return _lastPracticeDate; }
            set
            {
                // STANDARDIZED: Always normalize dates using DateHelper
                var normalizedValue = DateHelper.NormalizeToDateOnly(value);
                if (_lastPracticeDate != normalizedValue)
                {
                    _lastPracticeDate = normalizedValue;
                    OnPropertyChanged(nameof(LastPracticeDate));
                }
            }
        }

        /// <summary>
        /// Gets the average success ratio of the last 7 practice sessions for this bar section.
        /// Returns null if no session data is available.
        /// Uses the same calculation method as PracticeSessionWindow for consistency.
        /// </summary>
        public double? AverageSuccessRatio
        {
            get
            {
                var recentSessions = PracticeHistoryManager.Instance.GetHistoryForBarSection(Id)
                    .Where(h => !h.IsDeleted)
                    .OrderByDescending(h => h.Date)
                    .Take(7)
                    .ToList();

                if (recentSessions.Count == 0) return null;

                // Corrected logic: Calculate the average of the individual session SuccessRatio values.
                return recentSessions.Average(s => s.SuccessRatio);
            }
        }

        /// <summary>
        /// Gets the learning zone based on the average success ratio of the last 7 sessions.
        /// Returns null if no session data is available.
        /// </summary>
        public string? AverageLearningZone
        {
            get
            {
                var avgRatio = AverageSuccessRatio;
                if (avgRatio == null) return null;

                double ratio = avgRatio.Value;
                if (ratio < 0.60) return "TooHard";
                if (ratio < 0.80) return "Exploration";
                if (ratio < 0.90) return "Consolidation";
                if (ratio < 0.95) return "Polish";
                return "Mastered";
            }
        }

        public DateTime? NextDueDate
        {
            get { return _nextDueDate; }
            set
            {
                // STANDARDIZED: Always normalize dates using DateHelper
                var normalizedValue = DateHelper.NormalizeToDateOnly(value);
                if (_nextDueDate != normalizedValue)
                {
                    _nextDueDate = normalizedValue;
                    OnPropertyChanged(nameof(NextDueDate));
                }
            }
        }

        public DateTime? StartDate
        {
            get { return _startDate; }
            set
            {
                // STANDARDIZED: Always normalize dates using DateHelper
                var normalizedValue = DateHelper.NormalizeToDateOnly(value);
                if (_startDate != normalizedValue)
                {
                    _startDate = normalizedValue;
                    OnPropertyChanged(nameof(StartDate));
                    // NextDueDate is no longer automatically adjusted
                    // because scheduled_sessions.json is the single source of truth
                }
            }
        }

        public int Interval
        {
            get { return _interval; }
            set
            {
                if (_interval != value)
                {
                    _interval = value;
                    OnPropertyChanged(nameof(Interval));
                }
            }
        }

        public string Status
        {
            get { return _status; }
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        public string Difficulty
        {
            get { return _difficulty; }
            set
            {
                if (_difficulty != value)
                {
                    // Normalize null/empty and map legacy "Normal" -> "Average"
                    string newValue = string.IsNullOrWhiteSpace(value) ? "Average" : value;

                    if (string.Equals(newValue, "Normal", StringComparison.OrdinalIgnoreCase))
                    {
                        MLLogManager.Instance?.Log(
                            "BarSection.Difficulty received legacy value 'Normal'; normalizing to 'Average' for consistency.",
                            LogLevel.Warning
                        );
                        newValue = "Average";
                    }

                    _difficulty = newValue;
                    OnPropertyChanged(nameof(Difficulty));
                    // Difficulty caps the progress ceiling
                    OnPropertyChanged(nameof(ProgressPercentage));
                }
            }
        }


        // --- ADJUSTMENT: Property refactored for clarity ---
        /// <summary>
        /// Calculates the overall progress percentage for this section.
        /// The progress is determined by completed repetitions, capped by the section's difficulty level.
        /// For example, a section marked "Difficult" can only reach 60% progress, reflecting that
        /// more work (or re-evaluation of difficulty) is needed for true mastery.
        /// </summary>
        public double ProgressPercentage
        {
            get
            {
                if (TargetRepetitions <= 0) return 0;

                // 1. Calculate base progress based on repetitions completed, capped at 100%.
                double repetitionsProgress = (double)CompletedRepetitions / TargetRepetitions;
                repetitionsProgress = Math.Min(1.0, repetitionsProgress);

                // 2. Determine the maximum achievable progress based on difficulty level.
                // This acts as a ceiling on the progress percentage.
                double maxProgressBasedOnDifficulty;
                switch (Difficulty.ToLower())
                {
                    case "difficult":
                        maxProgressBasedOnDifficulty = 0.6;
                        break;
                    case "average":
                        maxProgressBasedOnDifficulty = 0.8;
                        break;
                    case "easy":
                        maxProgressBasedOnDifficulty = 0.9;
                        break;
                    case "mastered":
                        maxProgressBasedOnDifficulty = 1.0;
                        break;
                    default:
                        maxProgressBasedOnDifficulty = 0.8; // Default to average
                        break;
                }

                // 3. Final progress is the repetition progress scaled by the difficulty ceiling.
                // Example: 100% repetitions done * 60% difficulty cap = 60% total progress.
                // If difficulty is "Mastered" (cap = 1.0) and repetitions are done (progress = 1.0), result is 1.0.
                double totalProgress = repetitionsProgress * maxProgressBasedOnDifficulty;
                return totalProgress;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Triggers property change notifications for calculated properties that depend on practice history.
        /// Call this method after a new practice session is added to update the UI.
        /// </summary>
        public void RefreshCalculatedProperties()
        {
            OnPropertyChanged(nameof(AverageSuccessRatio));
            OnPropertyChanged(nameof(AverageLearningZone));

            // Notificeer ook de parent MusicPieceItem om retentie UI bij te werken
            // Dit zorgt ervoor dat de kleurcode in de muziekstukken-lijst correct wordt bijgewerkt
            NotifyParentMusicPieceRetentionChange();
        }

        /// <summary>
        /// Helper om de parent MusicPieceItem te notificeren dat retentie is gewijzigd.
        /// Dit triggert een update van de UI-indicatoren (kleur, icoon) in de hoofdlijst.
        /// </summary>
        private void NotifyParentMusicPieceRetentionChange()
        {
            // Zoek de parent MusicPieceItem in de hoofdlijst (MusicPieces is static)
            if (MainWindow.MusicPieces != null)
            {
                var parentPiece = MainWindow.MusicPieces.FirstOrDefault(mp =>
                    mp.BarSections?.Any(bs => bs.Id == this.Id) == true);

                if (parentPiece != null)
                {
                    parentPiece.RefreshRetentionProperties();
                }
            }
        }

        private void UpdateStatus()
        {
            if (CompletedRepetitions == 0)
            {
                Status = "New";
            }
            else if (CompletedRepetitions < TargetRepetitions)
            {
                Status = "In Progress";
            }
            else
            {
                Status = "Completed";
            }
        }

        /// <summary>
        /// Recalculates the next due date using standardized date logic.
        /// 
        /// CHANGED: This method now uses DateHelper for consistent date handling
        /// and only returns a proposed date without directly adjusting NextDueDate.
        /// </summary>
        /// <returns>The next practice date based on local logic</returns>
        public DateTime CalculateNextDueDate()
        {
            try
            {
                // STANDARDIZED: Use DateHelper for consistent date handling
                var start = StartDate ?? DateHelper.GetCurrentSessionDate();

                if (TargetRepetitions <= 0)
                {
                    return DateHelper.NormalizeToDateOnly(start);
                }

                if (CompletedRepetitions >= TargetRepetitions)
                {
                    return DateHelper.NormalizeToDateOnly(start);
                }
                else
                {
                    // STANDARDIZED: Use DateHelper for date calculation
                    return DateHelper.CalculateNextPracticeDate(start, 1);
                }
            }
            catch
            {
                // Fail-safe: never throw from model property logic
                return DateHelper.GetCurrentSessionDate();
            }
        }

        /// <summary>
        /// Updates the adaptive tau multiplier based on recent practice session performance.
        /// Called automatically after each practice session to enable rapid adaptation.
        /// </summary>
        /// <param name="performanceScore">Session performance score (0-10)</param>
        /// <param name="isRapidCalibrationPhase">If true, applies more aggressive adjustments</param>
        public void UpdateAdaptiveTauMultiplier(double performanceScore, bool isRapidCalibrationPhase = false)
        {
            try
            {
                double expectedPerformance = 6.0; // Baseline expectation
                double deviation = performanceScore - expectedPerformance;

                // Calculate adjustment based on performance deviation
                double adjustment = 1.0;

                if (Math.Abs(deviation) > 1.0) // Significant deviation
                {
                    if (deviation < 0) // Poor performance - shorter intervals
                    {
                        adjustment = isRapidCalibrationPhase ? 0.85 : 0.92;
                    }
                    else // Good performance - longer intervals
                    {
                        adjustment = isRapidCalibrationPhase ? 1.18 : 1.08;
                    }

                    // Apply gradual adjustment to prevent oscillation
                    double learningRate = isRapidCalibrationPhase ? 0.3 : 0.15;
                    double newMultiplier = _adaptiveTauMultiplier + (adjustment - 1.0) * learningRate;

                    // Update through property for validation and logging
                    AdaptiveTauMultiplier = newMultiplier;

                    MLLogManager.Instance?.Log(
                        $"BarSection {BarRange}: Tau multiplier updated from {_adaptiveTauMultiplier:F3}x to {newMultiplier:F3}x " +
                        $"(performance: {performanceScore:F1}, deviation: {deviation:F1}, rapid: {isRapidCalibrationPhase})",
                        LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError($"BarSection {BarRange}: Error updating adaptive tau multiplier", ex);
            }
        }

        /// <summary>
        /// Gets or sets the ID of the parent MusicPiece containing this section.
        /// Used for lifecycle persistence when the parent cannot be found via collection search.
        /// </summary>
        public Guid ParentMusicPieceId
        {
            get { return _parentMusicPieceId; }
            set
            {
                if (_parentMusicPieceId != value)
                {
                    _parentMusicPieceId = value;
                    OnPropertyChanged(nameof(ParentMusicPieceId));
                }
            }
        }
    }
}