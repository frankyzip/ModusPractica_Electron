using System;

namespace ModusPractica
{
    /// <summary>
    /// Central feature flags for retention & adaptive tau system.
    /// Soft simplification path: allows disabling layers without ripping out code.
    /// Flags are runtime-mutable (thread-safe via simple locking) so a future UI can toggle them.
    /// </summary>
    public static class RetentionFeatureFlags
    {
        private static readonly object _lock = new();
        // Core calculation layers
        public static bool UseDemographics { get; private set; } = true;
        public static bool UseRepetitionBonus { get; private set; } = true;
        // Disable ML/adaptive systems by default (safe removal mode)
        public static bool UseAdaptiveSystems { get; private set; } = false; // master switch for AdaptiveTauManager integration
        public static bool UseMemoryStability { get; private set; } = false; // SM-17 based memory stability manager
        public static bool UsePMC { get; private set; } = false; // PersonalizedMemoryCalibration
        public static bool UsePerformanceTrend { get; private set; } = true;

        // Diagnostics
        public static bool EnableDiagnosticLogging { get; private set; } = false;
        public static int LimitDiagnosticPerSession { get; private set; } = 80;

        // Internal counters
        private static int _diagnosticCount = 0;
        private static DateTime _lastResetDate = DateTime.UtcNow.Date;

        /// <summary>
        /// Atomically set multiple flags (any null parameter leaves value unchanged).
        /// </summary>
        public static void Configure(
            bool? useDemographics = null,
            bool? useRepetitionBonus = null,
            bool? useAdaptiveSystems = null,
            bool? useMemoryStability = null,
            bool? usePMC = null,
            bool? usePerformanceTrend = null,
            bool? enableDiagnosticLogging = null,
            int? limitDiagnosticPerSession = null)
        {
            lock (_lock)
            {
                if (useDemographics.HasValue) UseDemographics = useDemographics.Value;
                if (useRepetitionBonus.HasValue) UseRepetitionBonus = useRepetitionBonus.Value;
                if (useAdaptiveSystems.HasValue) UseAdaptiveSystems = useAdaptiveSystems.Value;
                if (useMemoryStability.HasValue) UseMemoryStability = useMemoryStability.Value;
                if (usePMC.HasValue) UsePMC = usePMC.Value;
                if (usePerformanceTrend.HasValue) UsePerformanceTrend = usePerformanceTrend.Value;
                if (enableDiagnosticLogging.HasValue) EnableDiagnosticLogging = enableDiagnosticLogging.Value;
                if (limitDiagnosticPerSession.HasValue && limitDiagnosticPerSession.Value > 0) LimitDiagnosticPerSession = limitDiagnosticPerSession.Value;
            }
        }

        /// <summary>
        /// Returns true if we may emit another diagnostic log line.
        /// Resets daily to avoid unlimited growth.
        /// </summary>
        public static bool ShouldLogDiagnostic()
        {
            lock (_lock)
            {
                var today = DateTime.UtcNow.Date;
                if (today != _lastResetDate)
                {
                    _diagnosticCount = 0;
                    _lastResetDate = today;
                }
                if (!EnableDiagnosticLogging) return false;
                if (_diagnosticCount >= LimitDiagnosticPerSession) return false;
                _diagnosticCount++;
                return true;
            }
        }

        /// <summary>
        /// Manual reset of the diagnostic counter (e.g. start new practice session logic).
        /// </summary>
        public static void ResetDiagnosticCounter()
        {
            lock (_lock)
            {
                _diagnosticCount = 0;
                _lastResetDate = DateTime.UtcNow.Date;
            }
        }
    }
}
