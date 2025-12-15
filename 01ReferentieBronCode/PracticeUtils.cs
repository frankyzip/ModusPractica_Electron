using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ModusPractica
{
    /// <summary>
    /// Utility class for practice-related calculations.
    /// Replaces ML-based methods with simple heuristic implementations.
    /// </summary>
    public static class PracticeUtils
    {
        /// <summary>
        /// Converts a numeric performance score to a rating string.
        /// </summary>
        public static string ConvertScoreToRatingString(float score)
        {
            if (score >= 8.0f) return "Excellent";
            if (score >= 6.5f) return "Good";
            if (score >= 5.0f) return "Average";
            if (score >= 3.5f) return "Below Average";
            return "Poor";
        }

        /// <summary>
        /// Calculates a heuristic performance rating for a practice session.
        /// Uses the basic session properties that remain after removing ML outcomes.
        /// </summary>
        public static float CalculatePerformanceRating(PracticeSession session, bool ignoreTempoForPlanner = false)
        {
            if (session == null)
            {
                return 5.0f;
            }

            // Interpret progress as either 0-1 or 0-100 and map to a 1-10 scale.
            double progressValue = double.IsFinite(session.Progress) ? session.Progress : 0.0;
            if (progressValue > 1.0)
            {
                progressValue /= 100.0;
            }
            progressValue = Math.Clamp(progressValue, 0.0, 1.0);

            float baseScore = (float)(1.0 + progressValue * 9.0); // 0% => 1.0, 100% => 10.0

            // Give a small bonus for longer focused work (capped to avoid runaway scores).
            int durationMinutes = Math.Max(session.Duration, 0);
            if (durationMinutes > 0)
            {
                float durationBoost = Math.Clamp(durationMinutes / 30f, 0f, 3f); // ~30 minutes yields +1, capped at +3
                baseScore = Math.Clamp(baseScore + durationBoost, 1f, 10f);
            }

            return baseScore;
        }

        /// <summary>
        /// Calculates performance rating based on practice history outcome and quality metrics.
        /// Overload for PracticeHistory type.
        /// </summary>
        public static float CalculatePerformanceRating(PracticeHistory history, bool ignoreTempoForPlanner = false)
        {
            if (history == null) return 5.0f;

            float baseScore = 5.0f;

            // Use PerformanceScore if available (post-migration)
            if (history.PerformanceScore > 0)
            {
                return history.PerformanceScore;
            }

            // 🔥 PRIORITY: Check difficulty level FIRST (Advanced Mode)
            // This is the user's primary assessment of their performance
            if (!string.IsNullOrEmpty(history.Difficulty))
            {
                string difficulty = history.Difficulty.ToLower();
                if (difficulty == "mastered")
                {
                    baseScore = 10.0f; // Mastered = perfect performance (100%)
                    MLLogManager.Instance?.Log($"[Performance] Difficulty-based score: Mastered → {baseScore:F1}", LogLevel.Debug);
                }
                else if (difficulty == "easy")
                {
                    baseScore = 8.5f; // Easy = very good performance
                    MLLogManager.Instance?.Log($"[Performance] Difficulty-based score: Easy → {baseScore:F1}", LogLevel.Debug);
                }
                else if (difficulty == "average")
                {
                    baseScore = 7.0f; // Average = good performance
                    MLLogManager.Instance?.Log($"[Performance] Difficulty-based score: Average → {baseScore:F1}", LogLevel.Debug);
                }
                else if (difficulty == "difficult")
                {
                    baseScore = 5.0f; // Difficult = needs more practice
                    MLLogManager.Instance?.Log($"[Performance] Difficulty-based score: Difficult → {baseScore:F1}", LogLevel.Debug);
                }
            }
            // Fallback: Adjust based on outcome (for older sessions or Simple Mode)
            else if (!string.IsNullOrEmpty(history.SessionOutcome))
            {
                string outcome = history.SessionOutcome.ToLower();
                if (outcome.Contains("targetreached")) baseScore = 8.0f;
                else if (outcome.Contains("frustration")) baseScore = 3.0f;
                else if (outcome.Contains("partial")) baseScore = 6.0f;
                else if (outcome.Contains("satisfactory")) baseScore = 7.0f;
            }

            // 🔥 ADJUSTED: Leerproces-factor - alleen toepassen voor NIET-mastered sessies
            // Mastered secties zijn al efficiënt, geen extra penalty/bonus nodig
            if (history.PreparatoryPhaseDuration.TotalMinutes > 0 &&
                (string.IsNullOrEmpty(history.Difficulty) || history.Difficulty.ToLower() != "mastered"))
            {
                double prepMinutes = history.PreparatoryPhaseDuration.TotalMinutes;
                double targetMinutes = history.Duration.TotalMinutes * 0.3; // Verwacht 30% prep tijd

                if (prepMinutes > targetMinutes * 2) // Veel meer prep dan verwacht
                {
                    baseScore *= 0.85f; // Gematigde penalty (was 0.8)
                    MLLogManager.Instance?.Log($"[Performance] Preparatory phase penalty: {prepMinutes:F1}min prep vs {targetMinutes:F1}min expected → score {baseScore:F1}", LogLevel.Debug);
                }
                else if (prepMinutes < targetMinutes * 0.5) // Efficiënt leren
                {
                    baseScore *= 1.05f; // Kleine bonus (was 1.1)
                    MLLogManager.Instance?.Log($"[Performance] Preparatory phase bonus: {prepMinutes:F1}min prep vs {targetMinutes:F1}min expected → score {baseScore:F1}", LogLevel.Debug);
                }
            }

            return Math.Clamp(baseScore, 1.0f, 10.0f);
        }

        /// <summary>
        /// Parses bar count from a bar section range string (e.g., "1-8" returns 8).
        /// </summary>
        public static int ParseBarCount(string barRange)
        {
            if (string.IsNullOrWhiteSpace(barRange)) return 0;

            // Match patterns like "1-8", "5-12", etc.
            var match = Regex.Match(barRange, @"(\d+)\s*-\s*(\d+)");
            if (match.Success && int.TryParse(match.Groups[2].Value, out int endBar))
            {
                if (int.TryParse(match.Groups[1].Value, out int startBar))
                {
                    return endBar - startBar + 1;
                }
                return endBar;
            }

            // Try to parse a single number
            if (int.TryParse(barRange.Trim(), out int singleBar))
            {
                return singleBar;
            }

            return 0;
        }

        /// <summary>
        /// Returns estimated duration in minutes for a section based on bar count.
        /// Simple heuristic: average 30 seconds per bar.
        /// </summary>
        public static double GetEstimatedDurationForSection(Guid sectionId)
        {
            // Default estimate: 5 minutes
            return 5.0;
        }

        /// <summary>
        /// Returns estimated duration in minutes for a section based on bar count.
        /// Simple heuristic: average 30 seconds per bar.
        /// </summary>
        public static double GetEstimatedDurationForSection(string sectionId)
        {
            // Default estimate: 5 minutes
            return 5.0;
        }

        /// <summary>
        /// Returns estimated duration as TimeSpan for a section.
        /// </summary>
        public static TimeSpan GetEstimatedDurationAsTimeSpan(Guid sectionId)
        {
            return TimeSpan.FromMinutes(5.0);
        }

        /// <summary>
        /// Returns estimated duration based on bar section data.
        /// </summary>
        public static double GetEstimatedDurationForSection(BarSection section)
        {
            if (section == null) return 5.0;

            int barCount = ParseBarCount(section.BarRange);
            if (barCount > 0)
            {
                // Estimate 30 seconds per bar on average
                return (barCount * 0.5);
            }

            return 5.0; // Default 5 minutes
        }

        /// <summary>
        /// Generates a simple practice schedule without ML.
        /// Returns empty list - scheduling is now handled by ScheduledPracticeSessionManager.
        /// </summary>
        public static List<ScheduledPracticeSession> GeneratePracticeSchedule(
            List<MusicPieceItem> musicPieces,
            List<ScheduledPracticeSession>? currentSessions = null)
        {
            // No longer generate ML-based schedules
            // Use ScheduledPracticeSessionManager for manual scheduling
            return new List<ScheduledPracticeSession>();
        }
    }
}
