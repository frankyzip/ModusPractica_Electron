using System;
using System.Collections.Generic;

namespace ModusPractica
{
    /// <summary>
    /// Integration extension for EbbinghausConstants to connect with AdaptiveTauManager.
    /// Provides enhanced tau calculation methods while maintaining backward compatibility.
    /// </summary>
    public static class EbbinghausExtensions
    {
        /// <summary>
        /// Enhanced tau calculation using AdaptiveTauManager integration.
        /// This method should be used in place of EbbinghausConstants.CalculateAdjustedTau
        /// when section-specific data is available.
        /// </summary>
        public static double CalculateEnhancedTau(string difficulty, int repetitionCount,
            Guid? barSectionId = null, List<PracticeHistory>? sectionHistory = null,
            int? userAge = null, string? userExperience = null)
        {
            try
            {
                // If adaptive systems are disabled, immediately fallback to standard calculation
                if (!RetentionFeatureFlags.UseAdaptiveSystems)
                {
                    if (userAge.HasValue && !string.IsNullOrEmpty(userExperience))
                    {
                        return EbbinghausConstants.CalculateAdjustedTau(difficulty, repetitionCount, userAge.Value, userExperience);
                    }
                    else
                    {
                        return EbbinghausConstants.CalculateAdjustedTau(difficulty, repetitionCount);
                    }
                }

                // Try enhanced calculation first
                if (barSectionId.HasValue)
                {
                    return AdaptiveTauManager.Instance.CalculateIntegratedTau(
                        difficulty,
                        repetitionCount,
                        barSectionId,
                        sectionHistory ?? new List<PracticeHistory>(),
                        userAge,
                        userExperience ?? string.Empty);
                }
                else
                {
                    // Fallback to enhanced but non-section specific calculation
                    return AdaptiveTauManager.Instance.CalculateIntegratedTau(
                        difficulty,
                        repetitionCount,
                        null,
                        new List<PracticeHistory>(),
                        userAge,
                        userExperience ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError("EbbinghausExtensions: Error in enhanced tau calculation, falling back to standard", ex);

                // Ultimate fallback to standard calculation (v3.0: gender removed)
                if (userAge.HasValue && !string.IsNullOrEmpty(userExperience))
                {
                    return EbbinghausConstants.CalculateAdjustedTau(difficulty, repetitionCount, userAge.Value, userExperience);
                }
                else
                {
                    return EbbinghausConstants.CalculateAdjustedTau(difficulty, repetitionCount);
                }
            }
        }

        /// <summary>
        /// Updates section-specific adaptive parameters after a practice session.
        /// This should be called after each practice session to enable continuous adaptation.
        /// </summary>
        public static void UpdateSectionAdaptation(BarSection section, PracticeHistory session)
        {
            try
            {
                if (section == null || session == null) return;

                // Respect feature flags: allow complete disable of adaptation/ML
                if (!RetentionFeatureFlags.UseAdaptiveSystems)
                {
                    return; // No adaptation when disabled
                }

                // 1. Update per-section tau multiplier for immediate adaptation
                var allHistory = PracticeHistoryManager.Instance?.GetAllHistory();
                bool isRapidPhase = allHistory?.Count <= 5;
                section.UpdateAdaptiveTauMultiplier(session.PerformanceScore, isRapidPhase);

                // 2. Apply rapid calibration if applicable
                AdaptiveTauManager.Instance.ApplyRapidCalibration(section.Id, session, section);

                // 3. Update PersonalizedMemoryCalibration
                if (RetentionFeatureFlags.UsePMC)
                {
                    PersonalizedMemoryCalibration.Instance?.UpdateCalibrationFromSession(session, section);
                }

                // 4. Update MemoryStabilityManager
                if (RetentionFeatureFlags.UseMemoryStability)
                {
                    MemoryStabilityManager.Instance?.UpdateMemoryStability(section.Id, session);
                }

                MLLogManager.Instance?.Log(
                    $"EbbinghausExtensions: Updated adaptation for section {section.BarRange} " +
                    $"(performance: {session.PerformanceScore:F1}, tau multiplier: {section.AdaptiveTauMultiplier:F3}x)",
                    LogLevel.Debug);

                // Structured diagnostics (safe & guarded by feature flags)
                try
                {
                    if (RetentionFeatureFlags.EnableDiagnosticLogging)
                    {
                        var memoryStats = MemoryStabilityManager.Instance?.GetMemoryStats(section.Id);
                        RetentionDiagnostics.LogAdaptationUpdate(section.Id, session.PerformanceScore,
                            section.AdaptiveTauMultiplier,
                            memoryStats?.Stability,
                            memoryStats?.Difficulty,
                            memoryStats?.ReviewCount);
                    }
                }
                catch { /* swallow diagnostics errors */ }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError("EbbinghausExtensions: Error updating section adaptation", ex);
            }
        }

        /// <summary>
        /// Gets the current adaptive state for a section for debugging/monitoring.
        /// </summary>
        public static AdaptiveState GetAdaptiveState(BarSection section)
        {
            try
            {
                // If disabled, return neutral state immediately
                if (!RetentionFeatureFlags.UseAdaptiveSystems)
                {
                    return new AdaptiveState
                    {
                        SectionId = section.Id,
                        SectionTauMultiplier = section.AdaptiveTauMultiplier,
                        PMCTotalSessions = 0,
                        PMCIsCalibrated = false,
                        MemoryStability = 0.0,
                        MemoryDifficulty = 0.0,
                        ReviewCount = 0,
                        IsNewSection = true
                    };
                }

                var pmcStats = RetentionFeatureFlags.UsePMC ? PersonalizedMemoryCalibration.Instance?.GetCalibrationStats() : null;
                var memoryStats = RetentionFeatureFlags.UseMemoryStability ? MemoryStabilityManager.Instance?.GetMemoryStats(section.Id) : null;

                return new AdaptiveState
                {
                    SectionId = section.Id,
                    SectionTauMultiplier = section.AdaptiveTauMultiplier,
                    PMCTotalSessions = pmcStats?.TotalSessions ?? 0,
                    PMCIsCalibrated = pmcStats?.IsCalibrated ?? false,
                    MemoryStability = memoryStats?.Stability ?? 0.0,
                    MemoryDifficulty = memoryStats?.Difficulty ?? 0.0,
                    ReviewCount = memoryStats?.ReviewCount ?? 0,
                    IsNewSection = memoryStats?.IsNew ?? true
                };
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError("EbbinghausExtensions: Error getting adaptive state", ex);
                return new AdaptiveState { SectionId = section.Id };
            }
        }
    }

    /// <summary>
    /// Data class representing the current adaptive learning state for a section.
    /// </summary>
    public class AdaptiveState
    {
        public Guid SectionId { get; set; }
        public double SectionTauMultiplier { get; set; } = 1.0;
        public int PMCTotalSessions { get; set; } = 0;
        public bool PMCIsCalibrated { get; set; } = false;
        public double MemoryStability { get; set; } = 0.0;
        public double MemoryDifficulty { get; set; } = 0.0;
        public int ReviewCount { get; set; } = 0;
        public bool IsNewSection { get; set; } = true;

        public override string ToString()
        {
            return $"AdaptiveState[{SectionId:D}]: TauMult={SectionTauMultiplier:F3}x, " +
                   $"PMC={PMCTotalSessions}sessions/{(PMCIsCalibrated ? "calibrated" : "learning")}, " +
                   $"Memory=S{MemoryStability:F1}d/D{MemoryDifficulty:F3}/{ReviewCount}reviews, " +
                   $"New={IsNewSection}";
        }
    }
}