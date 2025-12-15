using System;
using System.Collections.Generic;
using System.Linq;

namespace ModusPractica
{
    /// <summary>
    /// Unified Adaptive Tau Manager - Integrates all adaptive learning systems
    /// Coordinates PersonalizedMemoryCalibration, MemoryStabilityManager, and performance-based adjustments
    /// to provide a single, coherent tau calculation that learns from user behavior.
    /// 
    /// Key improvements:
    /// - Reduces fragmentation between parallel adaptive systems
    /// - Accelerates adaptation for new users (rapid calibration)
    /// - Provides confidence-weighted integration of different data sources
    /// - Maintains scientific baseline while learning individual patterns
    /// </summary>
    public class AdaptiveTauManager
    {
        private static AdaptiveTauManager? _instance;
        public static AdaptiveTauManager Instance => _instance ??= new AdaptiveTauManager();

        // Rapid calibration constants - ENHANCED for age correction convergence
        private const int RAPID_CALIBRATION_SESSIONS = 5;
        private const double RAPID_LEARNING_RATE = 0.35;  // Increased from 0.25 for faster age-bias correction
        private const double CONFIDENCE_BUILDUP_RATE = 0.25; // Increased from 0.2 - faster confidence in individual patterns
        private const double AGE_BIAS_DECAY_RATE = 0.3; // How quickly age-based priors fade in favor of observed behavior

        private AdaptiveTauManager() { }

        /// <summary>
        /// Master tau calculation that integrates all adaptive systems
        /// This replaces the fragmented approach in EbbinghausConstants.CalculateAdjustedTau
        /// </summary>
        public double CalculateIntegratedTau(string difficulty, int repetitionCount,
            Guid? barSectionId = null, List<PracticeHistory>? sectionHistory = null,
            int? userAge = null, string? userExperience = null)
        {
            try
            {
                // Master feature switch: if adaptive systems disabled, defer to demographic only path
                if (!RetentionFeatureFlags.UseAdaptiveSystems)
                {
                    // If demographics disabled, pass blanks so baseline reduces to raw base tau logic
                    double simpleTau = CalculateDemographicBaseline(difficulty, repetitionCount,
                        RetentionFeatureFlags.UseDemographics ? userAge : null,
                        RetentionFeatureFlags.UseDemographics ? (userExperience ?? string.Empty) : string.Empty);
                    double clampedSimple = EbbinghausConstants.ClampTauToSafeBounds(simpleTau);
                    RetentionDiagnostics.LogSimpleTau(barSectionId, difficulty, repetitionCount, simpleTau, clampedSimple);
                    return clampedSimple;
                }

                // Step 1: Calculate demographic baseline (existing logic)
                double demographicTau = CalculateDemographicBaseline(difficulty, repetitionCount,
                    RetentionFeatureFlags.UseDemographics ? userAge : null,
                    RetentionFeatureFlags.UseDemographics ? (userExperience ?? string.Empty) : string.Empty);

                // Step 2: If no section-specific data available, return demographic baseline
                if (!barSectionId.HasValue || sectionHistory == null)
                {
                    MLLogManager.Instance?.Log(
                        $"AdaptiveTauManager: No section data available, using demographic baseline τ={demographicTau:F3}",
                        LogLevel.Debug);
                    RetentionDiagnostics.LogSimpleTau(barSectionId, difficulty, repetitionCount, demographicTau,
                        EbbinghausConstants.ClampTauToSafeBounds(demographicTau));
                    return EbbinghausConstants.ClampTauToSafeBounds(demographicTau);
                }

                // Step 3: Gather adaptive data from all systems
                var adaptiveData = GatherAdaptiveData(barSectionId.Value, sectionHistory, difficulty, repetitionCount);

                // Step 4: Calculate confidence in adaptive vs demographic data
                double adaptiveConfidence = CalculateAdaptiveConfidence(adaptiveData);

                // Step 5: Integrate all sources based on confidence
                double integratedTau = IntegrateTauSources(demographicTau, adaptiveData, adaptiveConfidence);

                MLLogManager.Instance?.Log(
                    $"AdaptiveTauManager: Integrated τ calculation for {difficulty} (reps={repetitionCount}): " +
                    $"demographic={demographicTau:F3}, adaptive={adaptiveData.AdaptiveTau:F3}, " +
                    $"confidence={adaptiveConfidence:F3}, final={integratedTau:F3}",
                    LogLevel.Info);

                // Diagnostics breakdown
                try
                {
                    // Derive component factors for logging
                    double baseTauRaw = EbbinghausConstants.BASE_TAU_DAYS * EbbinghausConstants.MUSIC_MATERIAL_FACTOR;
                    double difficultyModifier = 1.0; // We cannot easily reconstruct exact difficulty modifier here without duplicating logic
                    double repetitionFactor = 1.0; // Repetition acceleration already baked inside demographicTau via CalculateAdjustedTau

                    // If desired we could parse back out, but keep simple: show demographicTau and adaptive contributors.
                    double pmcWeight = adaptiveData.HasPMCData ? adaptiveData.PMCConfidence * 0.4 : 0.0;
                    double stabilityWeight = adaptiveData.HasStabilityData ? adaptiveData.StabilityConfidence * 0.5 : 0.0;
                    double perfWeight = adaptiveData.HasPerformanceData ? adaptiveData.PerformanceConfidence * 0.3 : 0.0;
                    double clamped = EbbinghausConstants.ClampTauToSafeBounds(integratedTau);
                    RetentionDiagnostics.LogTauBreakdown(
                        barSectionId,
                        difficulty,
                        repetitionCount,
                        baseTauRaw,
                        difficultyModifier,
                        repetitionFactor,
                        demographicTau,
                        adaptiveData.PMCTau, pmcWeight,
                        adaptiveData.StabilityBasedTau, stabilityWeight,
                        adaptiveData.HasPerformanceData ? CalculatePerformanceBasedTau(adaptiveData.RecentPerformance) : 0.0, perfWeight,
                        adaptiveConfidence,
                        integratedTau,
                        clamped,
                        null,
                        null,
                        null);
                }
                catch { /* swallow diagnostics errors */ }

                return EbbinghausConstants.ClampTauToSafeBounds(integratedTau);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError("AdaptiveTauManager: Error in integrated tau calculation", ex);
                // Fallback to original demographic calculation
                return EbbinghausConstants.CalculateAdjustedTau(difficulty, repetitionCount);
            }
        }

        /// <summary>
        /// Rapid calibration for new users - applies after each session in first 5 sessions
        /// </summary>
        public void ApplyRapidCalibration(Guid barSectionId, PracticeHistory newSession, BarSection section)
        {
            try
            {
                if (!RetentionFeatureFlags.UseAdaptiveSystems)
                {
                    return; // disabled
                }
                if (newSession == null || section == null) return;

                // Check if we're in rapid calibration phase
                var allHistory = PracticeHistoryManager.Instance?.GetAllHistory();
                if (allHistory == null) return;

                int totalUserSessions = allHistory.Count;
                int sectionSessions = allHistory.Count(h => h.BarSectionId == barSectionId);

                bool isInRapidPhase = totalUserSessions <= RAPID_CALIBRATION_SESSIONS || sectionSessions <= 3;

                if (isInRapidPhase)
                {
                    double performanceDeviation = newSession.PerformanceScore - 6.0; // Expected baseline
                    double adjustment = 1.0;

                    if (Math.Abs(performanceDeviation) > 1.5) // Significant deviation
                    {
                        if (performanceDeviation < 0) // Poor performance
                        {
                            adjustment = 0.8; // Shorter intervals
                        }
                        else // Good performance  
                        {
                            adjustment = 1.25; // Longer intervals
                        }

                        // Apply rapid adjustment through PersonalizedMemoryCalibration
                        PersonalizedMemoryCalibration.Instance?.UpdateCalibrationFromSession(newSession, section);

                        MLLogManager.Instance?.Log(
                            $"AdaptiveTauManager: Rapid calibration applied for section {barSectionId}: " +
                            $"performance={newSession.PerformanceScore:F1}, adjustment={adjustment:F3}x " +
                            $"(session {sectionSessions} of section, {totalUserSessions} total user sessions)",
                            LogLevel.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError("AdaptiveTauManager: Error in rapid calibration", ex);
            }
        }

        /// <summary>
        /// Detects if immediate tau adjustment is needed based on performance deviation
        /// </summary>
        public bool RequiresImmediateAdjustment(PracticeHistory session, List<PracticeHistory> recentHistory)
        {
            if (session == null || recentHistory == null || recentHistory.Count < 2) return false;

            try
            {
                double avgRecentPerformance = recentHistory.TakeLast(3).Average(h => h.PerformanceScore);
                double expectedPerformance = 6.0;
                double deviation = Math.Abs(avgRecentPerformance - expectedPerformance);

                return deviation > 2.5; // Significant deviation requiring immediate attention
            }
            catch
            {
                return false;
            }
        }

        #region Private Helper Methods

        private double CalculateDemographicBaseline(string difficulty, int repetitionCount,
            int? userAge, string userExperience)
        {
            // Leeftijd verwijderd - altijd de basis overload gebruiken zonder leeftijdsaanpassing
            return EbbinghausConstants.CalculateAdjustedTau(difficulty, repetitionCount);
        }

        private AdaptiveDataSet GatherAdaptiveData(Guid barSectionId, List<PracticeHistory> sectionHistory,
            string difficulty, int repetitionCount)
        {
            var dataSet = new AdaptiveDataSet();

            try
            {
                // 1. PersonalizedMemoryCalibration data
                if (RetentionFeatureFlags.UsePMC && PersonalizedMemoryCalibration.Instance != null)
                {
                    double pmcTau = PersonalizedMemoryCalibration.Instance.GetPersonalizedTau(difficulty, repetitionCount);
                    if (!double.IsNaN(pmcTau) && pmcTau > 0)
                    {
                        dataSet.PMCTau = pmcTau;
                        dataSet.HasPMCData = true;

                        var pmcStats = PersonalizedMemoryCalibration.Instance.GetCalibrationStats();
                        dataSet.PMCConfidence = pmcStats?.TotalSessions >= 3 ?
                            Math.Min(1.0, pmcStats.TotalSessions / 10.0) : 0.0;
                    }
                }

                // 2. MemoryStabilityManager data
                if (RetentionFeatureFlags.UseMemoryStability && MemoryStabilityManager.Instance != null)
                {
                    var memoryStats = MemoryStabilityManager.Instance.GetMemoryStats(barSectionId);
                    if (memoryStats != null && !memoryStats.IsNew && memoryStats.ReviewCount >= 2)
                    {
                        dataSet.StabilityDays = memoryStats.Stability;
                        dataSet.MemoryDifficulty = memoryStats.Difficulty;
                        dataSet.HasStabilityData = true;
                        dataSet.StabilityConfidence = Math.Min(1.0, memoryStats.ReviewCount / 5.0);

                        // Convert stability to tau equivalent
                        dataSet.StabilityBasedTau = ConvertStabilityToTau(memoryStats.Stability, memoryStats.Difficulty);
                    }
                }

                // 3. Recent performance trend
                if (sectionHistory?.Count >= 2)
                {
                    var recentSessions = sectionHistory.OrderByDescending(h => h.Date).Take(3).ToList();
                    double avgPerformance = recentSessions.Average(h => h.PerformanceScore);
                    dataSet.RecentPerformance = avgPerformance;
                    dataSet.HasPerformanceData = true;
                    dataSet.PerformanceConfidence = Math.Min(1.0, recentSessions.Count / 3.0);
                }

                // 4. Calculate integrated adaptive tau
                dataSet.AdaptiveTau = CalculateWeightedAdaptiveTau(dataSet);

            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError("AdaptiveTauManager: Error gathering adaptive data", ex);
            }

            return dataSet;
        }

        private double CalculateWeightedAdaptiveTau(AdaptiveDataSet dataSet)
        {
            double weightedSum = 0.0;
            double totalWeight = 0.0;

            // Weight PMC data
            if (dataSet.HasPMCData && dataSet.PMCConfidence > 0)
            {
                double weight = dataSet.PMCConfidence * 0.4; // 40% max weight for PMC
                weightedSum += dataSet.PMCTau * weight;
                totalWeight += weight;
            }

            // Weight stability data (most reliable for established sections)
            if (dataSet.HasStabilityData && dataSet.StabilityConfidence > 0)
            {
                double weight = dataSet.StabilityConfidence * 0.5; // 50% max weight for stability
                weightedSum += dataSet.StabilityBasedTau * weight;
                totalWeight += weight;
            }

            // Weight performance data (immediate feedback)
            if (dataSet.HasPerformanceData && dataSet.PerformanceConfidence > 0)
            {
                double performanceAdjustment = CalculatePerformanceBasedTau(dataSet.RecentPerformance);
                double weight = dataSet.PerformanceConfidence * 0.3; // 30% max weight for performance
                weightedSum += performanceAdjustment * weight;
                totalWeight += weight;
            }

            if (totalWeight > 0)
            {
                return weightedSum / totalWeight;
            }

            // Fallback to demographic baseline if no adaptive data
            return EbbinghausConstants.BASE_TAU_DAYS * EbbinghausConstants.MUSIC_MATERIAL_FACTOR;
        }

        private double ConvertStabilityToTau(double stabilityDays, double difficulty)
        {
            // Stability represents 50% retention point, tau represents 37% retention point
            // Approximate conversion based on forgetting curve mathematics
            double conversionFactor = 0.7; // Empirically derived
            return stabilityDays * conversionFactor * (1.0 + difficulty * 0.3);
        }

        private double CalculatePerformanceBasedTau(double avgPerformance)
        {
            // Convert performance to tau multiplier
            double baseline = EbbinghausConstants.BASE_TAU_DAYS * EbbinghausConstants.MUSIC_MATERIAL_FACTOR;

            if (avgPerformance < 4.0) // Poor performance
                return baseline * 0.7; // Shorter intervals
            else if (avgPerformance > 7.5) // Good performance
                return baseline * 1.4; // Longer intervals
            else
                return baseline; // Neutral
        }

        private double CalculateAdaptiveConfidence(AdaptiveDataSet dataSet)
        {
            // Calculate overall confidence in adaptive data vs demographic baseline
            double totalConfidence = 0.0;
            int dataSourceCount = 0;

            if (dataSet.HasPMCData)
            {
                totalConfidence += dataSet.PMCConfidence;
                dataSourceCount++;
            }

            if (dataSet.HasStabilityData)
            {
                totalConfidence += dataSet.StabilityConfidence;
                dataSourceCount++;
            }

            if (dataSet.HasPerformanceData)
            {
                totalConfidence += dataSet.PerformanceConfidence;
                dataSourceCount++;
            }

            if (dataSourceCount == 0) return 0.0;

            // Average confidence across available sources
            double avgConfidence = totalConfidence / dataSourceCount;

            // Boost confidence if multiple sources agree
            if (dataSourceCount >= 2) avgConfidence *= 1.2;
            if (dataSourceCount >= 3) avgConfidence *= 1.1;

            return Math.Min(1.0, avgConfidence);
        }

        private double IntegrateTauSources(double demographicTau, AdaptiveDataSet adaptiveData, double adaptiveConfidence)
        {
            // Weighted blend based on confidence in adaptive data
            if (adaptiveConfidence < 0.1) // Very low confidence
            {
                return demographicTau; // Use demographic baseline
            }
            else if (adaptiveConfidence > 0.8) // High confidence
            {
                // Mostly adaptive with small demographic influence
                return adaptiveData.AdaptiveTau * 0.9 + demographicTau * 0.1;
            }
            else // Medium confidence
            {
                // Blend proportionally
                return adaptiveData.AdaptiveTau * adaptiveConfidence +
                       demographicTau * (1.0 - adaptiveConfidence);
            }
        }

        #endregion

        #region Data Classes

        /// <summary>
        /// NEW: Calculates age-bias decay factor based on individual learning data
        /// Age demographics fade quickly as personal patterns emerge
        /// </summary>
        private double CalculateAgeBiasDecay(List<PracticeHistory> sectionHistory, int totalUserSessions)
        {
            if (sectionHistory == null || sectionHistory.Count == 0) return 1.0; // Full age bias

            // Decay age bias as we gather individual data
            double sectionDataWeight = Math.Min(sectionHistory.Count / 10.0, 1.0); // 10 sessions = full confidence
            double globalDataWeight = Math.Min(totalUserSessions / 20.0, 1.0); // 20 sessions = full confidence

            // Combined decay: age bias becomes less relevant with experience
            double combinedDecay = Math.Max(sectionDataWeight, globalDataWeight * 0.7);

            // Apply decay rate from constants
            return Math.Pow(1.0 - combinedDecay, AGE_BIAS_DECAY_RATE);
        }

        /// <summary>
        /// NEW: Enhanced integration that considers age-bias decay
        /// </summary>
        private double IntegrateTauSourcesWithAgeBias(double demographicTau, AdaptiveDataSet adaptiveData,
            double adaptiveConfidence, double ageBiasDecay)
        {
            // Separate age bias from other demographic factors (gender, experience)
            double neutralDemographicTau = 3.0 * 3.0; // Baseline without age adjustment
            double ageBiasComponent = demographicTau - neutralDemographicTau;

            // Apply decay to age bias only
            double adjustedDemographicTau = neutralDemographicTau + (ageBiasComponent * ageBiasDecay);

            // Standard integration with adjusted demographic component
            if (adaptiveConfidence < 0.3)
            {
                return adjustedDemographicTau; // Mostly demographic (but age-decayed)
            }
            else if (adaptiveConfidence > 0.8)
            {
                return adaptiveData.AdaptiveTau; // Mostly adaptive
            }
            else
            {
                // Weighted blend with reduced age influence
                double weight = adaptiveConfidence;
                return (adjustedDemographicTau * (1 - weight)) + (adaptiveData.AdaptiveTau * weight);
            }
        }

        #endregion

        #region Data Classes

        private class AdaptiveDataSet
        {
            public double PMCTau { get; set; } = 0.0;
            public double PMCConfidence { get; set; } = 0.0;
            public bool HasPMCData { get; set; } = false;

            public double StabilityDays { get; set; } = 0.0;
            public double MemoryDifficulty { get; set; } = 0.0;
            public double StabilityBasedTau { get; set; } = 0.0;
            public double StabilityConfidence { get; set; } = 0.0;
            public bool HasStabilityData { get; set; } = false;

            public double RecentPerformance { get; set; } = 0.0;
            public double PerformanceConfidence { get; set; } = 0.0;
            public bool HasPerformanceData { get; set; } = false;

            public double AdaptiveTau { get; set; } = 0.0;
        }

        #endregion
    }
}