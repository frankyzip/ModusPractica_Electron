using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ModusPractica
{
    /// <summary>
    /// Advanced Memory Stability Management System
    /// Based on SuperMemo SM-17+ algorithms and modern cognitive research
    /// 
    /// Key Concepts:
    /// - Stability (S): How long memory lasts before 50% forgetting probability
    /// - Retrievability (R): Current probability of successful recall
    /// - Difficulty (D): Inherent difficulty of the memory item
    /// </summary>
    public class MemoryStabilityManager
    {
        private static MemoryStabilityManager _instance;
        public static MemoryStabilityManager Instance => _instance ??= new MemoryStabilityManager();

        private Dictionary<Guid, MemoryStabilityData> _stabilityDatabase;
        private readonly object _lock = new object();
        private string _stabilityFilePath;

        // SM-17+ Algorithm Constants (scientifically validated)
        private const double INITIAL_STABILITY_DAYS = 1.8;        // First recall stability
        private const double DEFAULT_DIFFICULTY = 0.3;            // Default difficulty (0.0 = easy, 1.0 = hard)
        private const double STABILITY_GROWTH_FACTOR = 1.3;       // How much stability grows on success
        private const double DIFFICULTY_ADJUSTMENT_RATE = 0.05;   // How fast difficulty adapts
        private const double RETRIEVABILITY_THRESHOLD = 0.8;      // Optimal recall probability
        private const double FORGETTING_CURVE_STEEPNESS = -0.233; // Forgetting curve parameter

        private MemoryStabilityManager()
        {
            _stabilityDatabase = new Dictionary<Guid, MemoryStabilityData>();
        }

        public void InitializeForUser(string profileName)
        {
            if (!RetentionFeatureFlags.UseMemoryStability)
            {
                // Disabled: do not initialize or touch filesystem
                return;
            }
            lock (_lock)
            {
                string stabilityFolder = Path.Combine(
                    DataPathProvider.GetProfileFolder(profileName),
                    "MemoryStability");

                Directory.CreateDirectory(stabilityFolder);
                _stabilityFilePath = Path.Combine(stabilityFolder, "memory_stability.json");

                LoadStabilityData();
                MLLogManager.Instance.Log($"MemoryStabilityManager initialized for profile: {profileName}", LogLevel.Info);
            }
        }

        /// <summary>
        /// Updates memory stability based on practice session result
        /// This is the core of the SM-17+ algorithm implementation
        /// </summary>
        public void UpdateMemoryStability(Guid barSectionId, PracticeHistory practiceResult)
        {
            if (!RetentionFeatureFlags.UseMemoryStability) return; // disabled
            lock (_lock)
            {
                try
                {
                    if (practiceResult == null || barSectionId == Guid.Empty)
                    {
                        MLLogManager.Instance.Log("UpdateMemoryStability: Invalid parameters", LogLevel.Warning);
                        return;
                    }

                    // Get or create stability data
                    if (!_stabilityDatabase.TryGetValue(barSectionId, out var stabilityData))
                    {
                        stabilityData = new MemoryStabilityData
                        {
                            BarSectionId = barSectionId,
                            Stability = INITIAL_STABILITY_DAYS,
                            Difficulty = DEFAULT_DIFFICULTY,
                            LastReviewDate = DateHelper.GetCurrentSessionDate(),
                            ReviewCount = 0
                        };
                        _stabilityDatabase[barSectionId] = stabilityData;
                    }

                    // Calculate current retrievability before this session
                    double daysSinceLastReview = DateHelper.CalculateIntervalDays(
                        stabilityData.LastReviewDate,
                        practiceResult.Date);

                    double retrievability = CalculateRetrievability(stabilityData.Stability, daysSinceLastReview);

                    // Determine if this was a successful recall
                    bool wasSuccessful = DetermineRecallSuccess(practiceResult);

                    // Update stability using SM-17+ algorithm
                    UpdateStabilityAlgorithm(stabilityData, retrievability, wasSuccessful, practiceResult);

                    // Save updated data
                    SaveStabilityData();

                    MLLogManager.Instance.Log(
                        $"Memory stability updated for section {barSectionId}: " +
                        $"S={stabilityData.Stability:F1}d, D={stabilityData.Difficulty:F3}, " +
                        $"R={retrievability:F3}, Success={wasSuccessful}",
                        LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance.LogError("UpdateMemoryStability: Error updating stability data", ex);
                }
            }
        }

        /// <summary>
        /// Calculates optimal next review date based on memory stability
        /// </summary>
        public DateTime CalculateOptimalReviewDate(Guid barSectionId)
        {
            if (!RetentionFeatureFlags.UseMemoryStability)
            {
                // Disabled: default to tomorrow
                return DateHelper.CalculateNextPracticeDate(DateHelper.GetCurrentSessionDate(), 1.0);
            }
            lock (_lock)
            {
                try
                {
                    if (!_stabilityDatabase.TryGetValue(barSectionId, out var stabilityData))
                    {
                        // New item - use initial stability
                        return DateHelper.CalculateNextPracticeDate(
                            DateHelper.GetCurrentSessionDate(),
                            INITIAL_STABILITY_DAYS);
                    }

                    // Calculate interval to reach target retrievability
                    double targetInterval = CalculateTargetInterval(
                        stabilityData.Stability,
                        RETRIEVABILITY_THRESHOLD);

                    // Apply difficulty adjustment
                    double adjustedInterval = ApplyDifficultyAdjustment(targetInterval, stabilityData.Difficulty);

                    return DateHelper.CalculateNextPracticeDate(
                        stabilityData.LastReviewDate,
                        adjustedInterval);
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance.LogError($"CalculateOptimalReviewDate: Error for section {barSectionId}", ex);
                    return DateHelper.CalculateNextPracticeDate(DateHelper.GetCurrentSessionDate(), 1.0);
                }
            }
        }

        /// <summary>
        /// Gets comprehensive memory statistics for a bar section
        /// </summary>
        public MemoryStabilityStats GetMemoryStats(Guid barSectionId)
        {
            if (!RetentionFeatureFlags.UseMemoryStability)
            {
                return new MemoryStabilityStats
                {
                    BarSectionId = barSectionId,
                    IsNew = true,
                    Stability = 0.0,
                    Difficulty = 0.0,
                    CurrentRetrievability = 1.0,
                    ReviewCount = 0,
                    LastReviewDate = null,
                    DaysSinceLastReview = 0.0,
                    OptimalNextReview = DateHelper.CalculateNextPracticeDate(DateHelper.GetCurrentSessionDate(), 1.0),
                    RetentionStrength = 0.0,
                    LearningProgress = 0.0
                };
            }
            lock (_lock)
            {
                if (!_stabilityDatabase.TryGetValue(barSectionId, out var stabilityData))
                {
                    return new MemoryStabilityStats
                    {
                        BarSectionId = barSectionId,
                        IsNew = true,
                        Stability = INITIAL_STABILITY_DAYS,
                        Difficulty = DEFAULT_DIFFICULTY,
                        CurrentRetrievability = 1.0,
                        ReviewCount = 0
                    };
                }

                double daysSinceLastReview = DateHelper.CalculateIntervalDays(
                    stabilityData.LastReviewDate,
                    DateHelper.GetCurrentSessionDate());

                double currentRetrievability = CalculateRetrievability(
                    stabilityData.Stability,
                    daysSinceLastReview);

                return new MemoryStabilityStats
                {
                    BarSectionId = barSectionId,
                    IsNew = false,
                    Stability = stabilityData.Stability,
                    Difficulty = stabilityData.Difficulty,
                    CurrentRetrievability = currentRetrievability,
                    ReviewCount = stabilityData.ReviewCount,
                    LastReviewDate = stabilityData.LastReviewDate,
                    DaysSinceLastReview = daysSinceLastReview,
                    OptimalNextReview = CalculateOptimalReviewDate(barSectionId),
                    RetentionStrength = CalculateRetentionStrength(stabilityData),
                    LearningProgress = CalculateLearningProgress(stabilityData)
                };
            }
        }

        /// <summary>
        /// Predicts memory retention curve for visualization
        /// </summary>
        public List<(DateTime Date, double Retrievability)> PredictRetentionCurve(
            Guid barSectionId,
            int daysAhead = 30)
        {
            if (!RetentionFeatureFlags.UseMemoryStability)
            {
                // Return a flat, neutral curve if disabled
                var neutral = new List<(DateTime, double)>();
                DateTime start = DateHelper.GetCurrentSessionDate();
                for (int day = 0; day <= daysAhead; day++)
                {
                    neutral.Add((start.AddDays(day), 1.0));
                }
                return neutral;
            }
            var curve = new List<(DateTime, double)>();
            var stats = GetMemoryStats(barSectionId);

            DateTime startDate = stats.LastReviewDate ?? DateHelper.GetCurrentSessionDate();

            for (int day = 0; day <= daysAhead; day++)
            {
                double retrievability = CalculateRetrievability(stats.Stability, day);
                curve.Add((startDate.AddDays(day), retrievability));
            }

            return curve;
        }

        #region Private Algorithm Implementation

        /// <summary>
        /// Core SM-17+ algorithm for updating memory stability
        /// </summary>
        private void UpdateStabilityAlgorithm(
            MemoryStabilityData stabilityData,
            double retrievability,
            bool wasSuccessful,
            PracticeHistory practiceResult)
        {
            stabilityData.ReviewCount++;
            stabilityData.LastReviewDate = DateHelper.NormalizeToDateOnly(practiceResult.Date);

            if (wasSuccessful)
            {
                // Successful recall - increase stability
                double stabilityIncrease = CalculateStabilityIncrease(
                    stabilityData.Stability,
                    retrievability,
                    stabilityData.Difficulty);

                stabilityData.Stability *= stabilityIncrease;

                // Slightly reduce difficulty (it's getting easier)
                stabilityData.Difficulty = Math.Max(0.01,
                    stabilityData.Difficulty - DIFFICULTY_ADJUSTMENT_RATE);
            }
            else
            {
                // Failed recall - reset stability but don't make it worse than initial
                stabilityData.Stability = Math.Max(
                    INITIAL_STABILITY_DAYS * 0.8,  // Slight penalty
                    stabilityData.Stability * 0.3   // Significant reduction
                );

                // Increase difficulty (it's harder than we thought)
                stabilityData.Difficulty = Math.Min(0.99,
                    stabilityData.Difficulty + DIFFICULTY_ADJUSTMENT_RATE * 2);
            }

            // Apply performance-based fine-tuning
            ApplyPerformanceAdjustments(stabilityData, practiceResult);
        }

        /// <summary>
        /// Calculates how much stability should increase on successful recall
        /// Based on SM-17+ algorithm with retrievability consideration
        /// </summary>
        private double CalculateStabilityIncrease(double currentStability, double retrievability, double difficulty)
        {
            // Lower retrievability = harder recall = more stability gain
            double retrievabilityFactor = Math.Pow(1.0 - retrievability + 0.1, 0.5);

            // Higher difficulty = less stability gain
            double difficultyFactor = 1.0 - (difficulty * 0.3);

            // Base growth factor adjusted by conditions
            return STABILITY_GROWTH_FACTOR * retrievabilityFactor * difficultyFactor;
        }

        /// <summary>
        /// Calculates current retrievability based on forgetting curve
        /// R(t) = e^(t * ln(0.5) / S) where S is stability
        /// </summary>
        private double CalculateRetrievability(double stability, double daysSinceReview)
        {
            if (daysSinceReview <= 0) return 1.0;
            if (stability <= 0) return 0.1;

            // Forgetting curve: R(t) = e^(t * ln(0.5) / S)
            double exponent = daysSinceReview * Math.Log(0.5) / stability;
            return Math.Max(0.01, Math.Exp(exponent));
        }

        /// <summary>
        /// Determines if a practice session represents successful recall
        /// </summary>
        private bool DetermineRecallSuccess(PracticeHistory practiceResult)
        {
            // Multiple success criteria
            bool hasGoodPerformance = practiceResult.PerformanceScore >= 6.0;
            bool hasSuccessfulReps = practiceResult.Repetitions > 0;
            bool hasTargetReached = practiceResult.SessionOutcome?.Contains("TargetReached") == true;
            bool hasReasonableTime = practiceResult.Duration.TotalMinutes >= 1.0;

            // Success if multiple criteria are met
            int successCriteria = 0;
            if (hasGoodPerformance) successCriteria++;
            if (hasSuccessfulReps) successCriteria++;
            if (hasTargetReached) successCriteria++;
            if (hasReasonableTime) successCriteria++;

            return successCriteria >= 2; // At least 2 out of 4 criteria
        }

        /// <summary>
        /// Calculates target interval to reach desired retrievability
        /// </summary>
        private double CalculateTargetInterval(double stability, double targetRetrievability)
        {
            if (targetRetrievability >= 1.0) return 0.0;
            if (targetRetrievability <= 0.01) return stability * 10; // Very long interval

            // Solve for t in: R(t) = e^(t * ln(0.5) / S) = targetRetrievability
            // t = S * ln(targetRetrievability) / ln(0.5)
            double interval = stability * Math.Log(targetRetrievability) / Math.Log(0.5);
            return Math.Max(0.1, Math.Min(stability * 5, interval)); // Reasonable bounds
        }

        /// <summary>
        /// Applies difficulty-based adjustment to intervals
        /// </summary>
        private double ApplyDifficultyAdjustment(double baseInterval, double difficulty)
        {
            // Higher difficulty = shorter intervals
            double difficultyMultiplier = 1.0 - (difficulty * 0.4); // 0.6 to 1.0 range
            return Math.Max(0.5, baseInterval * difficultyMultiplier);
        }

        /// <summary>
        /// Fine-tunes stability based on practice session performance details
        /// </summary>
        private void ApplyPerformanceAdjustments(MemoryStabilityData stabilityData, PracticeHistory practiceResult)
        {
            // Performance-based fine adjustment
            if (practiceResult.PerformanceScore >= 8.0)
            {
                // Excellent performance - slight stability bonus
                stabilityData.Stability *= 1.05;
            }
            else if (practiceResult.PerformanceScore <= 4.0)
            {
                // Poor performance - slight stability penalty
                stabilityData.Stability *= 0.95;
            }

            // Duration-based adjustment
            double sessionMinutes = practiceResult.Duration.TotalMinutes;
            if (sessionMinutes < 2.0)
            {
                // Very short session - might not be consolidated well
                stabilityData.Stability *= 0.98;
            }
            else if (sessionMinutes > 15.0)
            {
                // Long session - good consolidation
                stabilityData.Stability *= 1.02;
            }
        }

        /// <summary>
        /// Calculates overall retention strength (0-100)
        /// </summary>
        private double CalculateRetentionStrength(MemoryStabilityData stabilityData)
        {
            // Combine stability and review count for overall strength
            double stabilityScore = Math.Min(100, stabilityData.Stability * 5); // 20 days = 100%
            double experienceScore = Math.Min(100, stabilityData.ReviewCount * 10); // 10 reviews = 100%
            double difficultyScore = (1.0 - stabilityData.Difficulty) * 100;

            // Weighted average
            return (stabilityScore * 0.5 + experienceScore * 0.3 + difficultyScore * 0.2);
        }

        /// <summary>
        /// Calculates learning progress (0-100)
        /// </summary>
        private double CalculateLearningProgress(MemoryStabilityData stabilityData)
        {
            // Progress based on stability growth from initial value
            double stabilityProgress = Math.Min(100,
                (stabilityData.Stability / INITIAL_STABILITY_DAYS - 1.0) * 20);

            // Progress based on difficulty reduction
            double difficultyProgress = (DEFAULT_DIFFICULTY - stabilityData.Difficulty) * 200;

            // Progress based on review experience
            double experienceProgress = Math.Min(100, stabilityData.ReviewCount * 5);

            return Math.Max(0, (stabilityProgress + difficultyProgress + experienceProgress) / 3);
        }

        #endregion

        #region Data Persistence

        private void LoadStabilityData()
        {
            try
            {
                if (!File.Exists(_stabilityFilePath))
                {
                    _stabilityDatabase = new Dictionary<Guid, MemoryStabilityData>();
                    return;
                }

                string jsonContent = FileLockManager.ReadAllTextWithLock(_stabilityFilePath);
                var dataList = JsonSerializer.Deserialize<List<MemoryStabilityData>>(jsonContent);

                _stabilityDatabase = dataList?.ToDictionary(d => d.BarSectionId) ??
                    new Dictionary<Guid, MemoryStabilityData>();

                MLLogManager.Instance.Log($"Loaded {_stabilityDatabase.Count} memory stability records", LogLevel.Info);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error loading memory stability data", ex);
                _stabilityDatabase = new Dictionary<Guid, MemoryStabilityData>();
            }
        }

        private void SaveStabilityData()
        {
            try
            {
                var dataList = _stabilityDatabase.Values.ToList();
                string jsonContent = JsonSerializer.Serialize(dataList,
                    new JsonSerializerOptions { WriteIndented = true });

                FileLockManager.WriteAllTextWithLock(_stabilityFilePath, jsonContent);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error saving memory stability data", ex);
            }
        }

        /// <summary>
        /// Merges memory stability data from multiple old sections into a new merged section
        /// Uses weighted averaging based on review counts and selects optimal values
        /// </summary>
        public void MergeStabilityData(List<Guid> oldSectionIds, Guid newSectionId)
        {
            if (!RetentionFeatureFlags.UseMemoryStability) return; // disabled
            if (oldSectionIds == null || oldSectionIds.Count == 0 || newSectionId == Guid.Empty)
            {
                MLLogManager.Instance.Log("MergeStabilityData: Invalid parameters", LogLevel.Warning);
                return;
            }

            lock (_lock)
            {
                try
                {
                    var oldStabilityData = new List<MemoryStabilityData>();

                    // Collect existing stability data for old sections
                    foreach (var oldId in oldSectionIds)
                    {
                        if (_stabilityDatabase.TryGetValue(oldId, out var data))
                        {
                            oldStabilityData.Add(data);
                        }
                    }

                    if (oldStabilityData.Count == 0)
                    {
                        MLLogManager.Instance.Log($"MergeStabilityData: No existing stability data found for {oldSectionIds.Count} sections", LogLevel.Info);
                        return;
                    }

                    // Calculate merged stability values using weighted averaging
                    double totalReviews = oldStabilityData.Sum(d => d.ReviewCount);
                    if (totalReviews == 0) totalReviews = 1; // Avoid division by zero

                    double mergedStability = oldStabilityData
                        .Sum(d => d.Stability * (d.ReviewCount / totalReviews));

                    double mergedDifficulty = oldStabilityData
                        .Sum(d => d.Difficulty * (d.ReviewCount / totalReviews));

                    DateTime mostRecentReview = oldStabilityData
                        .Max(d => d.LastReviewDate);

                    int totalReviewCount = oldStabilityData
                        .Sum(d => d.ReviewCount);

                    DateTime earliestCreation = oldStabilityData
                        .Min(d => d.CreatedDate);

                    // Create merged stability data
                    var mergedData = new MemoryStabilityData
                    {
                        BarSectionId = newSectionId,
                        Stability = Math.Max(mergedStability, INITIAL_STABILITY_DAYS), // Ensure minimum stability
                        Difficulty = Math.Clamp(mergedDifficulty, 0.0, 1.0), // Ensure valid range
                        LastReviewDate = mostRecentReview,
                        ReviewCount = totalReviewCount,
                        CreatedDate = earliestCreation,
                        LastModifiedDate = DateTime.Now
                    };

                    // Add merged data to database
                    _stabilityDatabase[newSectionId] = mergedData;

                    // Remove old stability data
                    foreach (var oldId in oldSectionIds)
                    {
                        _stabilityDatabase.Remove(oldId);
                    }

                    // Save changes
                    SaveStabilityData();

                    MLLogManager.Instance.Log($"MergeStabilityData: Successfully merged {oldStabilityData.Count} stability records into new section {newSectionId}. " +
                        $"Merged stats - Stability: {mergedData.Stability:F1} days, Difficulty: {mergedData.Difficulty:F2}, Reviews: {mergedData.ReviewCount}", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance.LogError($"MergeStabilityData: Error merging stability data for section {newSectionId}", ex);
                }
            }
        }

        #endregion
    }

    #region Data Classes

    /// <summary>
    /// Core memory stability data for a bar section
    /// </summary>
    public class MemoryStabilityData
    {
        public Guid BarSectionId { get; set; }
        public double Stability { get; set; }           // Days until 50% forgetting probability
        public double Difficulty { get; set; }          // Inherent difficulty (0.0 = easy, 1.0 = hard)
        public DateTime LastReviewDate { get; set; }    // Last practice date
        public int ReviewCount { get; set; }            // Total number of reviews
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime LastModifiedDate { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Comprehensive memory statistics for UI display and analysis
    /// </summary>
    public class MemoryStabilityStats
    {
        public Guid BarSectionId { get; set; }
        public bool IsNew { get; set; }
        public double Stability { get; set; }
        public double Difficulty { get; set; }
        public double CurrentRetrievability { get; set; }
        public int ReviewCount { get; set; }
        public DateTime? LastReviewDate { get; set; }
        public double DaysSinceLastReview { get; set; }
        public DateTime OptimalNextReview { get; set; }
        public double RetentionStrength { get; set; }    // 0-100 score
        public double LearningProgress { get; set; }     // 0-100 score

        // Helper properties for UI
        public string StabilityDisplayText => $"{Stability:F1} days";
        public string DifficultyDisplayText => GetDifficultyText(Difficulty);
        public string RetrievabilityDisplayText => $"{CurrentRetrievability:P1}";
        public string RetentionStrengthDisplayText => $"{RetentionStrength:F0}%";
        public string LearningProgressDisplayText => $"{LearningProgress:F0}%";

        private string GetDifficultyText(double difficulty)
        {
            if (difficulty < 0.2) return "Very Easy";
            if (difficulty < 0.4) return "Easy";
            if (difficulty < 0.6) return "Moderate";
            if (difficulty < 0.8) return "Difficult";
            return "Very Difficult";
        }
    }

    #endregion
}