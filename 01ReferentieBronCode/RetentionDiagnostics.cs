using System;
using System.Text;

namespace ModusPractica
{
    /// <summary>
    /// Structured diagnostic logging for retention & tau calculations.
    /// Emits compact single-line records with a stable prefix so users can filter/share.
    /// All methods are fail-safe: they never throw.
    /// </summary>
    public static class RetentionDiagnostics
    {
        private static bool _headerEmitted = false;
        private static readonly object _lock = new();
        private const string PREFIX = "[RETENTION_DIAG]";
        private const string HEADER_PREFIX = "[RETENTION_DIAG_HEADER]";

        private static void EmitHeaderIfNeeded()
        {
            if (!RetentionFeatureFlags.EnableDiagnosticLogging) return;
            lock (_lock)
            {
                if (_headerEmitted) return;
                _headerEmitted = true;
                try
                {
                    MLLogManager.Instance?.Log(
                        $"{HEADER_PREFIX} Columns=Context,Section,Difficulty,Reps,BaseTauRaw,DiffMod,RepFactor,DemographicTau,PMC(τ|w),StabilityTau(w),PerfTau(w),AdaptiveConfidence,IntegratedTau,ClampedTau,NextInterval,TargetR*,PredictedR", LogLevel.Info);
                }
                catch { /* ignore */ }
            }
        }

        public static void LogTauBreakdown(
            Guid? sectionId,
            string difficulty,
            int repetitionCount,
            double baseTauRaw,
            double difficultyModifier,
            double repetitionFactor,
            double demographicTau,
            double pmcTau, double pmcWeight,
            double stabilityTau, double stabilityWeight,
            double perfTau, double perfWeight,
            double adaptiveConfidence,
            double integratedTau,
            double clampedTau,
            double? nextIntervalDays = null,
            double? targetRetention = null,
            double? predictedRetention = null)
        {
            try
            {
                if (!RetentionFeatureFlags.ShouldLogDiagnostic()) return;
                EmitHeaderIfNeeded();
                var sb = new StringBuilder();
                sb.Append(PREFIX).Append(' ');
                sb.Append("TauCalc,");
                sb.Append(sectionId.HasValue ? sectionId.Value.ToString("D") : "-").Append(',');
                sb.Append(difficulty ?? "-").Append(',');
                sb.Append(repetitionCount).Append(',');
                sb.Append(baseTauRaw.ToString("F3")).Append(',');
                sb.Append(difficultyModifier.ToString("F3")).Append(',');
                sb.Append(repetitionFactor.ToString("F3")).Append(',');
                sb.Append(demographicTau.ToString("F3")).Append(',');
                sb.Append(pmcTau > 0 ? pmcTau.ToString("F3") + "|" + pmcWeight.ToString("F3") : "-").Append(',');
                sb.Append(stabilityTau > 0 ? stabilityTau.ToString("F3") + "|" + stabilityWeight.ToString("F3") : "-").Append(',');
                sb.Append(perfWeight > 0 ? perfTau.ToString("F3") + "|" + perfWeight.ToString("F3") : "-").Append(',');
                sb.Append(adaptiveConfidence.ToString("F3")).Append(',');
                sb.Append(integratedTau.ToString("F3")).Append(',');
                sb.Append(clampedTau.ToString("F3")).Append(',');
                sb.Append(nextIntervalDays.HasValue ? nextIntervalDays.Value.ToString("F2") : "-").Append(',');
                sb.Append(targetRetention.HasValue ? targetRetention.Value.ToString("F3") : "-").Append(',');
                sb.Append(predictedRetention.HasValue ? predictedRetention.Value.ToString("F3") : "-");

                MLLogManager.Instance?.Log(sb.ToString(), LogLevel.Info);
            }
            catch { /* swallow */ }
        }

        public static void LogSimpleTau(Guid? sectionId, string difficulty, int reps, double tau, double clampedTau,
            double? nextIntervalDays = null, double? targetRetention = null, double? predictedRetention = null)
        {
            try
            {
                if (!RetentionFeatureFlags.ShouldLogDiagnostic()) return;
                EmitHeaderIfNeeded();
                MLLogManager.Instance?.Log(
                    $"{PREFIX} SimpleTau,{(sectionId.HasValue ? sectionId.Value.ToString("D") : "-")},{difficulty},{reps},{tau:F3},{clampedTau:F3},-,-,-,-,-,-,-,-,{nextIntervalDays?.ToString("F2") ?? "-"},{targetRetention?.ToString("F3") ?? "-"},{predictedRetention?.ToString("F3") ?? "-"}",
                    LogLevel.Info);
            }
            catch { }
        }

        public static void LogAdaptationUpdate(Guid sectionId, double perf, double tauMultiplier, double? stability = null, double? difficulty = null, int? reviewCount = null)
        {
            try
            {
                if (!RetentionFeatureFlags.ShouldLogDiagnostic()) return;
                EmitHeaderIfNeeded();
                MLLogManager.Instance?.Log(
                    $"{PREFIX} AdaptUpdate,{sectionId:D},-,-,-,-,-,-,-,-,-,-,-,-,-,-,- Perf={perf:F1} TauMult={tauMultiplier:F3} Stability={stability?.ToString("F2") ?? "-"} Diff={difficulty?.ToString("F3") ?? "-"} Reviews={reviewCount?.ToString() ?? "-"}",
                    LogLevel.Debug);
            }
            catch { }
        }
    }
}
