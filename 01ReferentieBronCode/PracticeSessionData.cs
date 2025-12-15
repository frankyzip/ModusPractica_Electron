namespace ModusPractica
{
    #region Spaced Repetition Algorithm

    public class SpacedRepetitionAlgorithm
    {
        public (DateTime NextDate, double Tau) CalculateNextPracticeDate(
            MusicPieceItem piece,
            BarSection section,
            List<PracticeHistory> history,
            float performanceScore,
            DateTime currentSessionDate,
            int completedRepetitionsThisSession,
            Dictionary<string, object>? userOverrideData = null)
        {
            DateTime calculationBaseDate = DateHelper.NormalizeToDateOnly(currentSessionDate);
            // Removed unused locals
            var safeMethodInputs = ApplyMethodInputSafetyRails(performanceScore, completedRepetitionsThisSession, currentSessionDate);
            performanceScore = safeMethodInputs.performanceScore;
            int safeCompletedReps = safeMethodInputs.completedRepetitions;
            calculationBaseDate = safeMethodInputs.sessionDate;
            var filteredHistory = (history ?? new List<PracticeHistory>()).ToList();
            var lastSession = filteredHistory?.LastOrDefault();
            string sectionIdentifier = $"{piece.Title} - {section.BarRange}";

            if (ShouldUseMemoryStability(section, filteredHistory))
            {
                return CalculateStabilityBasedInterval(section, filteredHistory ?? new List<PracticeHistory>(), calculationBaseDate, sectionIdentifier);
            }

            bool isRecentFrustration = (lastSession?.SessionOutcome?.ToLower() == "frustration" || lastSession?.SessionOutcome?.ToLower() == "manualfrustration")
                                       && (!section.LastFrustrationDate.HasValue || DateHelper.CalculateIntervalDays(section.LastFrustrationDate.Value, calculationBaseDate) > 1);
            if (isRecentFrustration)
            {
                // Foundation/Frustration fixed intervals also pass through ClampIntervalToScientificBounds.
                // Rationale: uniform logging and single source of truth (no local caps).
                double fixedIntervalDays = (lastSession?.SessionOutcome?.ToLower() == "manualfrustration") ? 2.0 : 3.0;
                section.LastFrustrationDate = calculationBaseDate;
                MLLogManager.Instance.Log(
                    $"Frustration detected for '{sectionIdentifier}'. Applying {fixedIntervalDays}-day stabilization break. Cooldown marker set for {DateHelper.FormatDisplayDate(calculationBaseDate)}.",
                    LogLevel.Info);

                // Kies een τ-referentie (gebruik de reeds berekende tauUsed als die er is; anders een veilige referentie)
                // NEW: Gebruik stage-aware overload voor graduele MASTERED groei
                double tauRef = EbbinghausConstants.CalculateAdjustedTau(section.Difficulty, section.CompletedRepetitions, section.PracticeScheduleStage);
                tauRef = EbbinghausConstants.ClampTauToSafeBounds(tauRef);

                // CENTRALE CLAMP
                var (clampedDays, reason) = EbbinghausConstants.ClampIntervalToScientificBounds(
                    fixedIntervalDays,
                    tau: tauRef,
                    stability: null
                );

                // Optionele detail-log bij ingreep
                if (reason != "none")
                {
                    // Routine clamp: Debug (behoud Warning voor extreme cases)
                    var level = reason.Contains("extreme") || reason.Contains("invalid") ? LogLevel.Warning : LogLevel.Debug;
                    MLLogManager.Instance.Log(
                        $"[Frustration] Clamp reason={reason} interval {fixedIntervalDays:F2}d → {clampedDays:F2}d (τ={tauRef:F3})",
                        level
                    );
                }

                // Samenvatting per berekening
                double rStar = EbbinghausConstants.GetRetentionTargetForDifficulty(section.Difficulty);
                MLLogManager.Instance.Log(
                    $"[Summary/Frustration] section={section.Id} R*={rStar:P0} tau_used={tauRef:F3} t_raw={fixedIntervalDays:F2}d t_final={clampedDays:F2}d clamp_reason={reason}",
                    LogLevel.Info
                );

                // Planner contract (frustration break maakt deel uit van planning)
                System.Diagnostics.Debug.Assert(clampedDays >= 1.0, "Planner must never schedule < 1 day");

                // Gebruik ALLEEN de geclampte waarde voor due date
                var nextDue = DateHelper.CalculateNextPracticeDate(calculationBaseDate, clampedDays, isRegistrationPath: false);
                return (nextDue, tauRef);
            }

            // FOUNDATION STAGE ADVANCEMENT: Moet gebeuren VOOR de stage check
            // Twee triggers: 1) echte practice sessions (completedRepetitionsThisSession > 0)
            //                2) totale practice history (voor planner herplanning)
            if (section.PracticeScheduleStage < 3)
            {
                bool shouldAdvance = false;

                // Trigger 1: Echte practice session
                if (completedRepetitionsThisSession > 0)
                {
                    shouldAdvance = true;
                }
                // Trigger 2: Planner herplanning - gebruik practice history count
                else if (completedRepetitionsThisSession == 0)
                {
                    int practiceCount = history?.Count ?? 0;
                    int expectedStage = Math.Min(practiceCount, 3); // Max stage 3
                    if (section.PracticeScheduleStage < expectedStage)
                    {
                        // Auto-advance naar de correcte stage op basis van practice history
                        section.PracticeScheduleStage = expectedStage;
                        MLLogManager.Instance.Log(
                            $"[Stage] Section {section.Id} auto-advanced to Stage {section.PracticeScheduleStage} (history: {practiceCount} sessions)",
                            LogLevel.Info);
                        shouldAdvance = false; // Al gedaan
                    }
                }

                // Normale single-step advancement voor echte practice sessions
                if (shouldAdvance)
                {
                    section.PracticeScheduleStage++;
                    MLLogManager.Instance.Log(
                        $"[Stage] Section {section.Id} advanced to Stage {section.PracticeScheduleStage}",
                        LogLevel.Info);
                }
            }

            if (section.PracticeScheduleStage < 3)
            {
                // Foundation/Frustration fixed intervals also pass through ClampIntervalToScientificBounds.
                // Rationale: uniform logging and single source of truth (no local caps).
                // Foundation phase ... (Stage: 0/1/2)

                // SAFETY: Check for invalid negative stages
                if (section.PracticeScheduleStage < 0)
                {
                    MLLogManager.Instance.Log($"Invalid negative PracticeScheduleStage ({section.PracticeScheduleStage}) corrected to 0 for '{sectionIdentifier}'", LogLevel.Warning);
                    section.PracticeScheduleStage = 0;
                }

                double fixedIntervalDays;
                switch (section.PracticeScheduleStage)
                {
                    case 0: fixedIntervalDays = 1.0; break;
                    case 1: fixedIntervalDays = 1.0; break;
                    case 2: fixedIntervalDays = 1.0; break; // FIX: Changed from 2.0 to 1.0 to ensure 3 consecutive days for new sections
                    default:
                        fixedIntervalDays = 1.0;
                        MLLogManager.Instance.Log($"Unexpected PracticeScheduleStage ({section.PracticeScheduleStage}) in foundation phase for '{sectionIdentifier}'. Defaulting to 1 day.", LogLevel.Warning);
                        break;
                }

                // Kies een τ-referentie (gebruik de reeds berekende tauUsed als die er is; anders een veilige referentie)
                // NEW: Gebruik stage-aware overload voor graduele MASTERED groei
                double tauRef = EbbinghausConstants.CalculateAdjustedTau(section.Difficulty, section.CompletedRepetitions, section.PracticeScheduleStage);
                tauRef = EbbinghausConstants.ClampTauToSafeBounds(tauRef);

                // CENTRALE CLAMP
                var (clampedDays, reason) = EbbinghausConstants.ClampIntervalToScientificBounds(
                    fixedIntervalDays,
                    tau: tauRef,
                    stability: null
                );

                // Optionele detail-log bij ingreep
                if (reason != "none")
                {
                    // Routine clamp: Debug (behoud Warning voor extreme cases)
                    var level = reason.Contains("extreme") || reason.Contains("invalid") ? LogLevel.Warning : LogLevel.Debug;
                    MLLogManager.Instance.Log(
                        $"[Foundation] Clamp reason={reason} interval {fixedIntervalDays:F2}d → {clampedDays:F2}d (τ={tauRef:F3})",
                        level
                    );
                }

                // Samenvatting per berekening
                double rStar = EbbinghausConstants.GetRetentionTargetForDifficulty(section.Difficulty);
                MLLogManager.Instance.Log(
                    $"[Summary/Foundation] section={section.Id} R*={rStar:P0} tau_used={tauRef:F3} t_raw={fixedIntervalDays:F2}d t_final={clampedDays:F2}d clamp_reason={reason}",
                    LogLevel.Info
                );

                // Planner contract (foundation maakt deel uit van planning)
                System.Diagnostics.Debug.Assert(clampedDays >= 1.0, "Planner must never schedule < 1 day");

                // NEW: Per-item τ update integratie voor Foundation phase
                MLLogManager.Instance.Log($"[DEBUG] Starting per-item τ update for FOUNDATION section={section.Id}, perfScore={performanceScore:F2}", LogLevel.Info);
                try
                {
                    // Definieer een itemId (kan later verfijnd worden; nu section.Id voldoende)
                    string itemId = section.Id.ToString();

                    // Bepaal intervalDays gebruikt voor deze planning (clamped waarde)
                    double intervalDays = clampedDays;

                    // Evalueer of de huidige performance een 'correct' geheugen-event impliceert.
                    // Gebruik SessionOutcome als echte recall metric (TargetReached = success)
                    bool correct = (lastSession?.SessionOutcome?.ToLower() == "targetreached");

                    // Doelretentie vanuit difficulty
                    double targetRetention = EbbinghausConstants.GetRetentionTargetForDifficulty(section.Difficulty);

                    // Initialisatie τ-prior indien item nieuw: gebruik tauRef als startpunt
                    Func<double> initTauFactory = () => tauRef;

                    MLLogManager.Instance.Log($"[DEBUG] Foundation Pre-update: itemId={itemId}, interval={intervalDays:F2}d, correct={correct}, targetR={targetRetention:F3}, initTau={tauRef:F3}", LogLevel.Info);

                    var updated = ItemMemoryModel.Update(
                        itemId: itemId,
                        intervalDays: intervalDays,
                        correct: correct,
                        targetRetention: targetRetention,
                        initTauFactory: initTauFactory);

                    MLLogManager.Instance.Log($"[DEBUG] Foundation Post-update: τ_old={tauRef:F3} τ_new={updated.TauDays:F3} diff={Math.Abs(updated.TauDays - tauRef):F6}", LogLevel.Info);

                    // ALTIJD loggen (niet alleen bij verschillen > 0.001)
                    MLLogManager.Instance.Log(
                        $"[PerItemTau] FOUNDATION section={section.Id} τ_session={tauRef:F3} → τ_item={updated.TauDays:F3} nextPlanned≈{updated.LastPlannedIntervalDays:F2}d (correct={correct})",
                        LogLevel.Info);
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance.LogError($"Per-item tau update failed for FOUNDATION section {section.Id}", ex);
                }

                // Gebruik ALLEEN de geclampte waarde voor due date
                var nextDue = DateHelper.CalculateNextPracticeDate(calculationBaseDate, clampedDays, isRegistrationPath: false);
                MLLogManager.Instance.Log($"Foundation phase for '{sectionIdentifier}' (Stage: {section.PracticeScheduleStage}). Fixed interval: {fixedIntervalDays:F2} days → clamped: {clampedDays:F2} days. τ(ref)={tauRef:F3}", LogLevel.Info);
                return (nextDue, tauRef);
            }

            /// <remarks>
            /// Flow:
            /// 1) Compute raw interval with CalculateDynamicInterval (no local caps).
            /// 2) Central clamp: [1..365] and ≤ 5×τ via ClampIntervalToScientificBounds (single source of truth).
            /// 3) Planner contract: schedule with clampedDays (≥ 1 day).
            /// Logs per calc: R*, tau_used, t_raw, t_final, clamp_reason ([Summary]).
            /// </remarks>
            string difficulty = string.IsNullOrWhiteSpace(section.Difficulty) ? "Average" : section.Difficulty;
            int completedReps = Math.Max(0, section.CompletedRepetitions);

            // AUDIT FIX: Gebruik geïntegreerde τ-berekening i.p.v. basis EbbinghausConstants
            double baseTau;
            try
            {
                var settings = SettingsManager.Instance?.CurrentSettings;
                baseTau = EbbinghausExtensions.CalculateEnhancedTau(
                    difficulty,
                    completedReps,
                    barSectionId: section.Id,
                    sectionHistory: filteredHistory,
                    userAge: settings?.Age,
                    userExperience: settings?.MusicalExperience ?? string.Empty
                );
                MLLogManager.Instance.Log($"[NextDate] Using integrated τ={baseTau:F3} for section {section.Id}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                // Fallback naar originele methode bij problemen
                // NEW: Gebruik stage-aware overload voor graduele MASTERED groei
                baseTau = EbbinghausConstants.CalculateAdjustedTau(difficulty, completedReps, section.PracticeScheduleStage);
                MLLogManager.Instance.Log($"[NextDate] Fallback to basic τ={baseTau:F3} due to error: {ex.Message}", LogLevel.Warning);
            }

            // AUDIT FIX: De geïntegreerde τ-berekening heeft al PMC integatie, dus skip de legacy PMC code
            double tauUsed = baseTau;
            MLLogManager.Instance.Log($"[NextDate] Using integrated τ={tauUsed:F3} (includes PMC, stability, and performance adaptations)", LogLevel.Info);

            double rawInterval;
            try
            {
                rawInterval = CalculateDynamicInterval(piece, section, filteredHistory ?? new List<PracticeHistory>(), performanceScore, tauUsed);
                MLLogManager.Instance.Log($"[NextDate] Dynamic Ebbinghaus rawInterval={rawInterval:F2}d τ={tauUsed:F3} perf={performanceScore:F2}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.Log($"[NextDate] Dynamic interval failed: {ex.Message}. Fallback to 1 day.", LogLevel.Error);
                rawInterval = 1.0;
            }

            // FIX 2.1: Add central clamp with comprehensive logging for NextDate path
            double targetRetentionMain = EbbinghausConstants.GetRetentionTargetForDifficulty(difficulty);
            double t_raw_main = rawInterval;
            var (clampedMainInterval, mainClampReason) = EbbinghausConstants.ClampIntervalToScientificBounds(rawInterval, tau: tauUsed, stability: null);
            double t_final_main = clampedMainInterval;

            if (mainClampReason != "none")
            {
                // Routine clamp: Debug (behoud Warning voor extreme cases)
                var level = mainClampReason.Contains("extreme") || mainClampReason.Contains("invalid") ? LogLevel.Warning : LogLevel.Debug;
                MLLogManager.Instance.Log($"[NextDate] Central clamp reason={mainClampReason} interval {rawInterval:F2}d → {clampedMainInterval:F2}d", level);
            }

            // Samenvatting per berekening (voldoet aan acceptatiecriteria)
            double retentionTargetForSummary = EbbinghausConstants.GetRetentionTargetForDifficulty(section.Difficulty);
            MLLogManager.Instance.Log(
                $"[Summary] section={section.Id} R*={retentionTargetForSummary:P0} tau_used={tauUsed:F3} t_raw={rawInterval:F2}d t_final={clampedMainInterval:F2}d clamp_reason={mainClampReason}",
                LogLevel.Info
            );

            // FIX 2: Complete "per berekening" logging with summary line for NextDate path
            MLLogManager.Instance.Log($"[Summary-NextDate] tau_before={baseTau:F3} tau_after={tauUsed:F3} R*={targetRetentionMain:F3} t_raw={t_raw_main:F2} t_final={t_final_main:F2} clamp_reason={mainClampReason}", LogLevel.Info);

            DateTime nextDate = DateHelper.CalculateNextPracticeDate(calculationBaseDate, clampedMainInterval, isRegistrationPath: false);

            // NEW: Per-item τ update integratie (eerste eenvoudige heuristic)
            MLLogManager.Instance.Log($"[DEBUG] Starting per-item τ update for section={section.Id}, perfScore={performanceScore:F2}", LogLevel.Info);
            try
            {
                // Definieer een itemId (kan later verfijnd worden; nu section.Id voldoende)
                string itemId = section.Id.ToString();

                // Bepaal intervalDays gebruikt voor deze planning (clamped waarde)
                double intervalDays = clampedMainInterval;

                // Evalueer of de huidige performance een 'correct' geheugen-event impliceert.
                // Gebruik SessionOutcome als echte recall metric (TargetReached = success)
                bool correct = (lastSession?.SessionOutcome?.ToLower() == "targetreached");

                // Doelretentie vanuit difficulty
                double targetRetention = EbbinghausConstants.GetRetentionTargetForDifficulty(section.Difficulty);

                // Initialisatie τ-prior indien item nieuw: gebruik tauUsed als startpunt
                Func<double> initTauFactory = () => tauUsed;

                MLLogManager.Instance.Log($"[DEBUG] Pre-update: itemId={itemId}, interval={intervalDays:F2}d, correct={correct}, targetR={targetRetention:F3}, initTau={tauUsed:F3}", LogLevel.Info);

                var updated = ItemMemoryModel.Update(
                    itemId: itemId,
                    intervalDays: intervalDays,
                    correct: correct,
                    targetRetention: targetRetention,
                    initTauFactory: initTauFactory);

                MLLogManager.Instance.Log($"[DEBUG] Post-update: τ_old={tauUsed:F3} τ_new={updated.TauDays:F3} diff={Math.Abs(updated.TauDays - tauUsed):F6}", LogLevel.Info);

                // ALTIJD loggen (niet alleen bij verschillen > 0.001)
                MLLogManager.Instance.Log(
                    $"[PerItemTau] section={section.Id} τ_session={tauUsed:F3} → τ_item={updated.TauDays:F3} nextPlanned≈{updated.LastPlannedIntervalDays:F2}d (correct={correct})",
                    LogLevel.Info);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"Per-item tau update failed for section {section.Id}", ex);
            }

            // NEW: Apply user override if provided
            if (userOverrideData != null && userOverrideData.ContainsKey("UserOverrideInterval"))
            {
                double userInterval = (double)userOverrideData["UserOverrideInterval"];
                double originalInterval = (double)userOverrideData["OriginalAlgorithmInterval"];
                string reason = userOverrideData.GetValueOrDefault("OverrideReason", "")?.ToString() ?? "";

                MLLogManager.Instance.Log(
                    $"[UserOverride] Section {section.Id}: Algorithm={originalInterval:F1}d → User={userInterval:F1}d. " +
                    $"Reason: '{reason}'",
                    LogLevel.Info);

                // Use user override but still apply safety clamps
                var (clampedUserInterval, clampReason) = EbbinghausConstants.ClampIntervalToScientificBounds(
                    userInterval, tau: tauUsed, stability: null);

                if (clampReason != "none")
                {
                    // Routine clamp: Debug (behoud Warning voor extreme cases)
                    var level = clampReason.Contains("extreme") || clampReason.Contains("invalid") ? LogLevel.Warning : LogLevel.Debug;
                    MLLogManager.Instance.Log(
                        $"[UserOverride] Safety clamp applied: {userInterval:F1}d → {clampedUserInterval:F1}d (reason: {clampReason})",
                        level);
                }

                DateTime userNextDate = DateHelper.CalculateNextPracticeDate(calculationBaseDate, clampedUserInterval, isRegistrationPath: false);

                MLLogManager.Instance.Log($"[NextDate] OVERRIDE RESULT section={section.Id} next={userNextDate:yyyy-MM-dd} interval={clampedUserInterval:F2}d τUsed={tauUsed:F3}", LogLevel.Info);
                return (userNextDate, tauUsed);
            }

            MLLogManager.Instance.Log($"[NextDate] RESULT section={section.Id} next={nextDate:yyyy-MM-dd} interval={clampedMainInterval:F2}d τUsed={tauUsed:F3}", LogLevel.Info);
            return (nextDate, tauUsed);
        }

        private bool ShouldUseMemoryStability(BarSection section, List<PracticeHistory>? history)
        {
            if (section.PracticeScheduleStage < 3) return false;
            if (history == null || history.Count < 5) return false;
            var recentScores = history.TakeLast(5).Select(h => h.PerformanceScore).ToList();
            if (recentScores.Count < 3) return false;
            double scoreVariance = CalculateVariance(recentScores);
            return scoreVariance > 1.0;
        }

        /// <summary>
        /// Stability-based interval: converts stability S (tau-like) to raw days and applies central clamp.
        /// Logs per calc: R*, S, t_raw, t_final, clamp_reason ([Summary/Stability]).
        /// </summary>
        private (DateTime NextDate, double Tau) CalculateStabilityBasedInterval(BarSection section, List<PracticeHistory> history, DateTime calculationBaseDate, string sectionIdentifier)
        {
            try
            {
                var memoryStats = MemoryStabilityManager.Instance.GetMemoryStats(section.Id);
                DateTime stabilityBasedDate = MemoryStabilityManager.Instance.CalculateOptimalReviewDate(section.Id);
                double stabilityTau = memoryStats.Stability;
                double rawIntervalDays = DateHelper.CalculateIntervalDays(calculationBaseDate, stabilityBasedDate);
                MLLogManager.Instance.Log($"[Stability] section={section.Id} S={stabilityTau:F3} rawInterval={rawIntervalDays:F2}d base={calculationBaseDate:yyyy-MM-dd}→{stabilityBasedDate:yyyy-MM-dd}", LogLevel.Info);

                // FIX 2: Add comprehensive logging for Stability path with all required fields
                double targetRetention = EbbinghausConstants.GetRetentionTargetForDifficulty(section.Difficulty);
                double t_raw_stability = rawIntervalDays;
                var (clampedDays, reason) = EbbinghausConstants.ClampIntervalToScientificBounds(rawIntervalDays, tau: null, stability: stabilityTau);
                double t_final_stability = clampedDays;

                if (reason != "none")
                {
                    // Routine clamp: Debug (behoud Warning voor extreme cases)
                    var level = reason.Contains("extreme") || reason.Contains("invalid") ? LogLevel.Warning : LogLevel.Debug;
                    MLLogManager.Instance.Log($"[Stability] Clamp applied reason={reason} interval {rawIntervalDays:F2}d → {clampedDays:F2}d", level);
                }

                // Samenvatting per berekening (stability)
                double retentionTargetForSummary = EbbinghausConstants.GetRetentionTargetForDifficulty(section.Difficulty);
                MLLogManager.Instance.Log(
                    $"[Summary/Stability] section={section.Id} R*={retentionTargetForSummary:P0} S={stabilityTau:F3} t_raw={rawIntervalDays:F2}d t_final={clampedDays:F2}d clamp_reason={reason}",
                    LogLevel.Info
                );

                // FIX 2: Complete "per berekening" logging with summary line for Stability path
                MLLogManager.Instance.Log($"[Summary-Stability] tau_before={stabilityTau:F3} tau_after={stabilityTau:F3} R*={targetRetention:F3} t_raw={t_raw_stability:F2} t_final={t_final_stability:F2} clamp_reason={reason}", LogLevel.Info);

                DateTime finalDate = DateHelper.CalculateNextPracticeDate(calculationBaseDate, clampedDays, isRegistrationPath: false);
                MLLogManager.Instance.Log($"[Stability] RESULT section={section.Id} next={finalDate:yyyy-MM-dd} interval={clampedDays:F2}d (S={stabilityTau:F3})", LogLevel.Info);
                return (finalDate, stabilityTau);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"Error in stability-based calculation for {sectionIdentifier}", ex);
                // NEW: Gebruik stage-aware overload voor graduele MASTERED groei
                double fallbackTau = EbbinghausConstants.CalculateAdjustedTau(section.Difficulty, section.CompletedRepetitions, section.PracticeScheduleStage);
                double fallbackInterval = CalculateDynamicInterval(null, section, history, 5.0f, fallbackTau);

                // FIX 2: Add comprehensive logging for Stability fallback path
                double targetRetentionFallback = EbbinghausConstants.GetRetentionTargetForDifficulty(section.Difficulty);
                double t_raw_fallback = fallbackInterval;
                var (clampedFallback, reason) = EbbinghausConstants.ClampIntervalToScientificBounds(fallbackInterval, tau: fallbackTau, stability: null);
                double t_final_fallback = clampedFallback;

                if (reason != "none")
                {
                    // Routine clamp: Debug (behoud Warning voor extreme cases)
                    var level = reason.Contains("extreme") || reason.Contains("invalid") ? LogLevel.Warning : LogLevel.Debug;
                    MLLogManager.Instance.Log($"[Stability→Fallback] Clamp reason={reason} interval {fallbackInterval:F2}d → {clampedFallback:F2}d", level);
                }

                MLLogManager.Instance.Log(
                    $"[Summary/Stability→Fallback] section={section.Id} R*={targetRetentionFallback:P0} tau_used={fallbackTau:F3} t_raw={fallbackInterval:F2}d t_final={clampedFallback:F2}d clamp_reason={reason}",
                    LogLevel.Info
                );

                // Samenvatting per berekening (stability fallback)
                MLLogManager.Instance.Log(
                    $"[Summary/StabilityFallback] section={section.Id} R*={targetRetentionFallback:P0} tau={fallbackTau:F3} t_raw={fallbackInterval:F2}d t_final={clampedFallback:F2}d clamp_reason={reason}",
                    LogLevel.Info
                );

                // FIX 2: Complete "per berekening" logging with summary line for Stability fallback path
                MLLogManager.Instance.Log($"[Summary-StabilityFallback] tau_before={fallbackTau:F3} tau_after={fallbackTau:F3} R*={targetRetentionFallback:F3} t_raw={t_raw_fallback:F2} t_final={t_final_fallback:F2} clamp_reason={reason}", LogLevel.Info);

                DateTime fallbackDate = DateHelper.CalculateNextPracticeDate(calculationBaseDate, clampedFallback, isRegistrationPath: false);
                return (fallbackDate, fallbackTau);
            }
        }

        private double CalculateVariance(List<float> values)
        {
            if (values.Count < 2) return 0;
            double mean = values.Average();
            double sumSquaredDiffs = values.Sum(v => Math.Pow(v - mean, 2));
            return sumSquaredDiffs / values.Count;
        }

        private double CalculateDynamicInterval(MusicPieceItem? piece, BarSection section, List<PracticeHistory> history, float performanceScore, double adjustedTau)
        {
            string sectionIdentifier = piece != null ? $"{piece.Title} - {section.BarRange}" : $"BarSectionId: {section.Id}";
            MLLogManager.Instance.Log($"--- Dynamic Interval Calculation for {sectionIdentifier} ---", LogLevel.Debug);
            var safeInputs = ApplyInputSafetyRails(section, performanceScore, adjustedTau, history);
            performanceScore = safeInputs.performanceScore;
            adjustedTau = safeInputs.tau;
            int safeReps = safeInputs.repetitions;
            TimeSpan safeDuration = safeInputs.duration;
            double tau_before = adjustedTau;
            MLLogManager.Instance.Log($"[Input] Difficulty: '{section.Difficulty}', Reps: {safeReps} -> Adjusted Tau: {adjustedTau:F2}", LogLevel.Debug);
            double targetRetention = EbbinghausConstants.GetRetentionTargetForDifficulty(section.Difficulty);
            // NEW: Tiny R* pre-inversion nudge based on recent performance trend (±0.02–0.05)
            var (nudgedRStar, nudgeReason, nudgeDelta) = ApplyRStarTrendNudge(history, section.Difficulty, targetRetention);
            if (Math.Abs(nudgeDelta) > 1e-6)
            {
                MLLogManager.Instance.Log(
                    $"[R* Nudge] slope/avgPerf-driven: {targetRetention:F3} → {nudgedRStar:F3} (Δ={nudgeDelta:+0.000;-0.000}) reason={nudgeReason}",
                    LogLevel.Info);
            }
            targetRetention = nudgedRStar;
            MLLogManager.Instance.Log($"[Input] Difficulty: '{section.Difficulty}' -> Target Retention (after nudge): {targetRetention:P0}", LogLevel.Debug);
            double tau_after = adjustedTau;
            double optimalInterval = CalculateOptimalInterval(adjustedTau, targetRetention, safeReps);
            MLLogManager.Instance.Log($"[Calc] Base Ebbinghaus Interval (raw, pre-performance, pre-pattern): {optimalInterval:F6} days", LogLevel.Debug);
            double intervalAfterPerformance = AdjustForPerformanceScientific(optimalInterval, performanceScore);
            MLLogManager.Instance.Log($"[Adjust] Performance Score: '{performanceScore:F2}' -> Interval is nu: {intervalAfterPerformance:F2} days", LogLevel.Debug);
            double finalInterval = AdjustForPracticePattern(intervalAfterPerformance, section, history);
            MLLogManager.Instance.Log($"[Adjust] Practice Pattern -> Final Interval: {finalInterval:F2} days", LogLevel.Debug);
            double t_raw = finalInterval;

            // SAFETY: Debug assertions for critical values
            System.Diagnostics.Debug.Assert(!double.IsNaN(finalInterval) && !double.IsInfinity(finalInterval),
                "Final interval must be a valid number");
            System.Diagnostics.Debug.Assert(finalInterval > 0,
                "Final interval must be positive");
            System.Diagnostics.Debug.Assert(adjustedTau >= 1.0 && adjustedTau <= 180.0,
                $"Tau must be within safe bounds [1, 180], got {adjustedTau}");

            MLLogManager.Instance.Log($"[PreClamp] t_raw={t_raw:F2}d before central clamping", LogLevel.Info);
            var (clampedInterval, clampReason) = EbbinghausConstants.ClampIntervalToScientificBounds(finalInterval, tau: adjustedTau, stability: null);
            double t_final = clampedInterval;

            // SAFETY: Post-clamp assertion
            System.Diagnostics.Debug.Assert(t_final >= 1.0,
                $"Clamped interval must be at least 1 day, got {t_final}");

            MLLogManager.Instance.Log($"[PostClamp] t_final={t_final:F2}d clamp_reason={clampReason}", LogLevel.Info);

            // FIX 2: The summary line here is already present in the existing code - this remains as is
            MLLogManager.Instance.Log($"[Summary] tau_before={tau_before:F3} tau_after={tau_after:F3} R*={targetRetention:F3} t_raw={t_raw:F2} t_final={t_final:F2} clamp_reason={clampReason}", LogLevel.Info);
            return clampedInterval;
        }

        /// <summary>
        /// Applies a small pre-inversion adjustment to the retention target (R*) based on recent performance trend.
        /// Direction: better and improving → slightly LOWER R* (longer wait); struggling or declining → HIGHER R* (shorter wait).
        /// Magnitude: 0.00–0.05 with conservative bounds, then clamped into [0.60, 0.90].
        /// </summary>
        private (double rStar, string reason, double delta) ApplyRStarTrendNudge(List<PracticeHistory> history, string difficulty, double currentRStar)
        {
            try
            {
                if (history == null || history.Count == 0)
                {
                    return (currentRStar, "no_history", 0.0);
                }

                // Analyze up to last 5 sessions for this section
                var validHistory = history
                    .Where(h => h != null && !double.IsNaN(h.PerformanceScore) && !double.IsInfinity(h.PerformanceScore))
                    .OrderByDescending(h => h.Date)
                    .Take(5)
                    .OrderBy(h => h.Date)
                    .ToList();

                if (validHistory.Count == 0)
                    return (currentRStar, "no_valid_history", 0.0);

                double avgPerf = validHistory.Average(h => h.PerformanceScore);
                double slope;
                try
                {
                    (slope, var _avgPerfCheck) = CalculateSafeLinearRegression(validHistory);
                    // Use avgPerf from our own computation for consistency
                }
                catch
                {
                    slope = 0.0;
                }

                // Normalize signals
                double p = Math.Max(0.0, Math.Min(1.0, avgPerf / 10.0));
                double slopeNorm = Math.Max(-0.5, Math.Min(0.5, slope / 10.0)); // compress slope impact

                // Base magnitude from confidence in signals
                double magnitude = 0.0;
                string category;
                if (p >= 0.8 && slope > 0) // strong performance and improving
                {
                    magnitude = 0.03 + Math.Min(0.02, slopeNorm * 0.5);
                    category = "high_perf_positive_trend";
                }
                else if (p <= 0.4 && slope < 0) // weak performance and declining
                {
                    magnitude = 0.03 + Math.Min(0.02, Math.Abs(slopeNorm) * 0.5);
                    category = "low_perf_negative_trend";
                }
                else if (slope > 0.2) // modest positive trend
                {
                    magnitude = 0.01;
                    category = "positive_trend";
                }
                else if (slope < -0.2) // modest negative trend
                {
                    magnitude = 0.01;
                    category = "negative_trend";
                }
                else
                {
                    return (currentRStar, "neutral", 0.0);
                }

                magnitude = Math.Max(0.0, Math.Min(0.05, magnitude));

                // Direction: better → lower R* (longer); worse → higher R* (shorter)
                double delta = 0.0;
                if (category is "high_perf_positive_trend" or "positive_trend")
                {
                    delta = -magnitude;
                }
                else // negative trend categories
                {
                    delta = magnitude;
                }

                double nudged = currentRStar + delta;
                // Conservative bounds; CalculateOptimalInterval will also clamp
                nudged = Math.Max(0.60, Math.Min(0.90, nudged));

                return (nudged, category, delta);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("ApplyRStarTrendNudge failed", ex);
                return (currentRStar, "error", 0.0);
            }
        }

        private (float performanceScore, double tau, int repetitions, TimeSpan duration) ApplyInputSafetyRails(BarSection section, float performanceScore, double tau, List<PracticeHistory> history)
        {
            var clampedValues = (performanceScore: performanceScore, tau: tau, repetitions: Math.Max(0, section.CompletedRepetitions), duration: TimeSpan.Zero);
            var logParts = new List<string>();
            float originalPerfForAccuracy = performanceScore;
            double originalAccuracyNorm = float.IsNaN(originalPerfForAccuracy) || float.IsInfinity(originalPerfForAccuracy) ? double.NaN : originalPerfForAccuracy / 10.0;
            const int MAX_SAFE_REPS = 200;
            int originalReps = Math.Max(0, section.CompletedRepetitions);
            if (originalReps > MAX_SAFE_REPS) { clampedValues.repetitions = MAX_SAFE_REPS; logParts.Add($"reps={originalReps}→{MAX_SAFE_REPS}"); }
            else if (originalReps < 0) { clampedValues.repetitions = 0; logParts.Add($"reps={originalReps}→0"); }
            bool perfClamped = false; float originalPerf = performanceScore;
            if (float.IsNaN(originalPerf) || float.IsInfinity(originalPerf)) { clampedValues.performanceScore = 5.0f; perfClamped = true; }
            else if (originalPerf < 0.0f) { clampedValues.performanceScore = 0.0f; perfClamped = true; }
            else if (originalPerf > 10.0f) { clampedValues.performanceScore = 10.0f; perfClamped = true; }
            else { clampedValues.performanceScore = originalPerf; }
            double originalTau = tau; double clampedTau = EbbinghausConstants.ClampTauToSafeBounds(tau); if (Math.Abs(clampedTau - originalTau) > 0.001) { logParts.Add($"tau={originalTau:F3}→{clampedTau:F3}"); }
            clampedValues.tau = clampedTau;
            const double MAX_SAFE_HOURS = 8.0; if (history != null && history.Any()) { var totalDuration = TimeSpan.FromTicks(history.Sum(h => h.Duration.Ticks)); if (totalDuration.TotalHours > MAX_SAFE_HOURS) { var clampedDuration = TimeSpan.FromHours(MAX_SAFE_HOURS); clampedValues.duration = clampedDuration; logParts.Add($"duration={totalDuration.TotalHours:F2}h→{MAX_SAFE_HOURS}h"); } else if (totalDuration.TotalSeconds < 0) { clampedValues.duration = TimeSpan.Zero; logParts.Add("duration=negative→0h"); } else { clampedValues.duration = totalDuration; } }
            double originalAccuracy = originalAccuracyNorm; double newAccuracy = clampedValues.performanceScore / 10.0; bool accuracyClamped = false; if (double.IsNaN(originalAccuracy) || double.IsInfinity(originalAccuracy)) { accuracyClamped = true; logParts.Add($"accuracy=NaN→{newAccuracy:F2}"); } else if (originalAccuracy < 0.0 || originalAccuracy > 1.0 || perfClamped) { accuracyClamped = true; logParts.Add($"accuracy={originalAccuracy:F2}→{newAccuracy:F2}"); }
            bool repsChanged = logParts.Any(p => p.StartsWith("reps=")); bool durationChanged = logParts.Any(p => p.StartsWith("duration=")); bool accuracyChanged = accuracyClamped; if (repsChanged || durationChanged || accuracyChanged) { var clampTokens = new List<string>(); if (repsChanged) clampTokens.Add(logParts.First(p => p.StartsWith("reps="))); if (durationChanged) clampTokens.Add(logParts.First(p => p.StartsWith("duration="))); if (accuracyChanged) clampTokens.Add(logParts.First(p => p.StartsWith("accuracy="))); MLLogManager.Instance.Log($"InputClamp {string.Join(" ", clampTokens)}", LogLevel.Info); } else if (logParts.Any()) { MLLogManager.Instance.Log($"InputClamp {string.Join(" ", logParts)}", LogLevel.Info); }
            return clampedValues;
        }

        private (float performanceScore, int completedRepetitions, DateTime sessionDate) ApplyMethodInputSafetyRails(float performanceScore, int completedRepetitions, DateTime sessionDate)
        {
            var logParts = new List<string>(); float safePerformance = performanceScore; int safeRepetitions = completedRepetitions; DateTime safeDate = sessionDate; double originalAccuracy = (float.IsNaN(performanceScore) || float.IsInfinity(performanceScore)) ? double.NaN : performanceScore / 10.0; bool perfChanged = false; if (float.IsNaN(performanceScore) || float.IsInfinity(performanceScore)) { safePerformance = 5.0f; perfChanged = true; } else if (performanceScore < 0.0f) { safePerformance = 0.0f; perfChanged = true; } else if (performanceScore > 10.0f) { safePerformance = 10.0f; perfChanged = true; }
            const int MAX_SAFE_REPS = 200; if (completedRepetitions < 0) { safeRepetitions = 0; logParts.Add($"reps={completedRepetitions}→0"); } else if (completedRepetitions > MAX_SAFE_REPS) { safeRepetitions = MAX_SAFE_REPS; logParts.Add($"reps={completedRepetitions}→{MAX_SAFE_REPS}"); }
            var minDate = new DateTime(2020, 1, 1); var maxDate = DateTime.Today.AddYears(1); if (sessionDate < minDate) { safeDate = DateTime.Today; logParts.Add($"date={sessionDate:yyyy-MM-dd}→{DateTime.Today:yyyy-MM-dd}"); } else if (sessionDate > maxDate) { safeDate = DateTime.Today; logParts.Add($"date={sessionDate:yyyy-MM-dd}→{DateTime.Today:yyyy-MM-dd}"); }
            double newAccuracy = safePerformance / 10.0; bool accuracyChanged = perfChanged || double.IsNaN(originalAccuracy) || originalAccuracy < 0.0 || originalAccuracy > 1.0; if (accuracyChanged) { if (double.IsNaN(originalAccuracy)) logParts.Add($"accuracy=NaN→{newAccuracy:F2}"); else logParts.Add($"accuracy={originalAccuracy:F2}→{newAccuracy:F2}"); }
            bool repsChanged = logParts.Any(p => p.StartsWith("reps=")); if (repsChanged || accuracyChanged) { var clampTokens = new List<string>(); if (repsChanged) clampTokens.Add(logParts.First(p => p.StartsWith("reps="))); if (accuracyChanged) clampTokens.Add(logParts.First(p => p.StartsWith("accuracy="))); MLLogManager.Instance.Log($"InputClamp {string.Join(" ", clampTokens)}", LogLevel.Info); } else if (logParts.Any()) { MLLogManager.Instance.Log($"InputClamp {string.Join(" ", logParts)}", LogLevel.Info); }
            return (safePerformance, safeRepetitions, safeDate);
        }

        /// <summary>
        /// Returns the raw interval (in days) from the Ebbinghaus equation using clamped τ and validated R*.
        /// No interval clamping here. Central clamping is applied later via EbbinghausConstants.ClampIntervalToScientificBounds.
        /// Logs: tau_before/after, targetRetention, rawInterval.
        /// </summary>
        private double CalculateOptimalInterval(double tau, double targetRetention, int completedRepetitions)
        {
            try
            {
                // Input sanity
                if (double.IsNaN(tau) || double.IsInfinity(tau) || tau <= 0)
                {
                    MLLogManager.Instance.Log($"CalculateOptimalInterval: Invalid tau value ({tau}), using default", LogLevel.Warning);
                    tau = EbbinghausConstants.BASE_TAU_DAYS;
                }
                if (double.IsNaN(targetRetention) || double.IsInfinity(targetRetention) || targetRetention <= 0 || targetRetention >= 1)
                {
                    MLLogManager.Instance.Log($"CalculateOptimalInterval: Invalid targetRetention value ({targetRetention}), using default", LogLevel.Warning);
                    targetRetention = EbbinghausConstants.RETENTION_THRESHOLD;
                }
                if (completedRepetitions < 0)
                {
                    MLLogManager.Instance.Log($"CalculateOptimalInterval: Negative completedRepetitions ({completedRepetitions}), correcting to 0", LogLevel.Warning);
                    completedRepetitions = 0;
                }

                // Tau clamp (centraal)
                double tau_before = tau;
                tau = EbbinghausConstants.ClampTauToSafeBounds(tau);
                if (Math.Abs(tau - tau_before) > 0.001)
                {
                    MLLogManager.Instance.Log($"CalculateOptimalInterval: Tau clamped from {tau_before:F3} to {tau:F3}", LogLevel.Debug);
                }

                // SAFETY: Debug assertion for tau bounds
                System.Diagnostics.Debug.Assert(tau >= 1.0 && tau <= 180.0,
                    $"Tau must be within [1, 180] after clamping, got {tau}");

                // Retentie binnen modelgrenzen
                const double MIN_SAFE_RETENTION = 0.01;
                const double MAX_SAFE_RETENTION = 0.99;
                targetRetention = Math.Max(MIN_SAFE_RETENTION, Math.Min(MAX_SAFE_RETENTION, targetRetention));

                double baseline = EbbinghausConstants.ASYMPTOTIC_RETENTION_BASELINE;
                double learningStrength = EbbinghausConstants.INITIAL_LEARNING_STRENGTH;
                if (learningStrength <= 0)
                {
                    MLLogManager.Instance.Log("CalculateOptimalInterval: Invalid INITIAL_LEARNING_STRENGTH, using fallback", LogLevel.Warning);
                    learningStrength = 0.01;
                }

                double maxModelRetention = Math.Min(0.999, baseline + learningStrength - 0.001);
                if (targetRetention >= maxModelRetention)
                {
                    MLLogManager.Instance.Log($"CalculateOptimalInterval: targetRetention {targetRetention:F3} clamped to model maximum {maxModelRetention:F3}", LogLevel.Warning);
                    targetRetention = maxModelRetention;
                }

                double minModelRetention = Math.Max(baseline + 0.001, baseline);
                if (targetRetention <= minModelRetention)
                {
                    MLLogManager.Instance.Log($"CalculateOptimalInterval: targetRetention {targetRetention:F3} raised above baseline {minModelRetention:F3}", LogLevel.Warning);
                    targetRetention = minModelRetention;
                }

                double ratio = (targetRetention - baseline) / learningStrength;
                if (double.IsNaN(ratio) || double.IsInfinity(ratio)) { MLLogManager.Instance.Log($"CalculateOptimalInterval: Invalid ratio ({ratio}), defaulting", LogLevel.Warning); ratio = 0.5; }
                ratio = Math.Max(1e-6, Math.Min(0.999999, ratio));

                double logResult;
                try
                {
                    logResult = Math.Log(ratio);
                    if (double.IsNaN(logResult) || double.IsInfinity(logResult))
                    {
                        MLLogManager.Instance.Log($"CalculateOptimalInterval: Invalid logarithmic result, using safe approximation", LogLevel.Warning);
                        logResult = Math.Log(Math.Max(1e-6, ratio));
                    }
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance.LogError($"CalculateOptimalInterval: Logarithm calculation failed", ex);
                    logResult = Math.Log(Math.Max(1e-6, ratio));
                }

                double rawInterval = -tau * logResult;
                if (double.IsNaN(rawInterval) || double.IsInfinity(rawInterval) || rawInterval <= 0)
                {
                    MLLogManager.Instance.Log($"CalculateOptimalInterval: Invalid raw interval calculation, using fallback", LogLevel.Warning);
                    double safeRatio = Math.Max(1e-3, Math.Min(0.999, ratio));
                    rawInterval = Math.Max(1.0, tau * (1.0 - safeRatio));
                }
                if (double.IsNaN(rawInterval) || double.IsInfinity(rawInterval) || rawInterval <= 0)
                {
                    MLLogManager.Instance.Log($"CalculateOptimalInterval: Final validation failed, using emergency fallback", LogLevel.Error);
                    return 1.0;
                }

                MLLogManager.Instance.Log($"CalculateOptimalInterval: tau_before={tau_before:F3}, tau_afterClamp={tau:F3}, targetRetention={targetRetention:F2}, completedRepetitions={completedRepetitions} -> rawInterval={rawInterval:F2} days", LogLevel.Debug);
                return rawInterval;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"CalculateOptimalInterval: Critical error - tau={tau}, targetRetention={targetRetention}, completedRepetitions={completedRepetitions}", ex);
                try
                {
                    double safeTau = EbbinghausConstants.ClampTauToSafeBounds(tau);
                    double safeRetention = Math.Max(0.5, Math.Min(0.9, targetRetention));
                    return Math.Max(1.0, safeTau * (1.0 - safeRetention));
                }
                catch
                {
                    return 1.0;
                }
            }
        }

        private double AdjustForPerformanceScientific(double interval, float performanceScore)
        {
            try
            {
                if (double.IsNaN(interval) || double.IsInfinity(interval) || interval <= 0) { MLLogManager.Instance.Log($"AdjustForPerformanceScientific: Invalid interval ({interval}), using fallback", LogLevel.Warning); interval = 1.0; }
                if (double.IsNaN(performanceScore) || double.IsInfinity(performanceScore)) { MLLogManager.Instance.Log($"AdjustForPerformanceScientific: Invalid performanceScore ({performanceScore}), using neutral", LogLevel.Warning); performanceScore = 5.0f; }
                performanceScore = Math.Max(0.0f, Math.Min(10.0f, performanceScore));
                double adjustmentFactor = CalculateCognitivePerformanceAdjustment(performanceScore);
                if (double.IsNaN(adjustmentFactor) || double.IsInfinity(adjustmentFactor) || adjustmentFactor <= 0) { MLLogManager.Instance.Log($"AdjustForPerformanceScientific: Invalid adjustment factor, using neutral", LogLevel.Warning); adjustmentFactor = 1.0; }
                const double MIN_ADJUSTMENT = 0.3; const double MAX_ADJUSTMENT = 2.5; adjustmentFactor = Math.Max(MIN_ADJUSTMENT, Math.Min(MAX_ADJUSTMENT, adjustmentFactor));
                double adjustedInterval = interval * adjustmentFactor; if (double.IsNaN(adjustedInterval) || double.IsInfinity(adjustedInterval) || adjustedInterval <= 0) { MLLogManager.Instance.Log($"AdjustForPerformanceScientific: Final result invalid, using original interval", LogLevel.Warning); return interval; }
                MLLogManager.Instance.Log($"[Adjust] Performance {performanceScore:F1} -> Factor {adjustmentFactor:F3} -> Interval {interval:F2}d → {adjustedInterval:F2}d", LogLevel.Debug);
                return adjustedInterval;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"AdjustForPerformanceScientific: Critical error with interval={interval}, performanceScore={performanceScore}", ex);
                return Math.Max(1.0, interval);
            }
        }

        private double CalculateCognitivePerformanceAdjustment(float performanceScore)
        {
            try
            {
                double normalizedScore = Math.Max(0.0, Math.Min(1.0, performanceScore / 10.0));
                double sigmoidAdjustment = CalculateSigmoidPerformanceMapping(normalizedScore);
                double confidenceModifier = CalculateConfidenceModifier(normalizedScore);
                double cognitiveLoadFactor = CalculateCognitiveLoadFactor(normalizedScore);
                double combinedFactor = (sigmoidAdjustment * 0.5) + (confidenceModifier * 0.3) + (cognitiveLoadFactor * 0.2);
                return Math.Max(0.3, Math.Min(2.5, combinedFactor));
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"CalculateCognitivePerformanceAdjustment: Error with performanceScore={performanceScore}", ex);
                return 1.0;
            }
        }

        private double CalculateSigmoidPerformanceMapping(double normalizedScore)
        {
            try
            {
                const double SIGMOID_STEEPNESS = 6.0; const double SIGMOID_MIDPOINT = 0.5; double x = (normalizedScore - SIGMOID_MIDPOINT) * SIGMOID_STEEPNESS; double sigmoidResult; if (x > 50) sigmoidResult = 1.0; else if (x < -50) sigmoidResult = 0.0; else sigmoidResult = 1.0 / (1.0 + Math.Exp(-x)); return 0.4 + (sigmoidResult * 1.6);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"CalculateSigmoidPerformanceMapping: Error with normalizedScore={normalizedScore}", ex);
                return 1.0;
            }
        }

        private double CalculateConfidenceModifier(double normalizedScore)
        {
            try
            {
                if (normalizedScore <= 0.5) { return 0.6 + (normalizedScore * 0.8); }
                double exponentFactor = (normalizedScore - 0.5) * 2.0; double exponentialGain = Math.Pow(exponentFactor, 1.5); return 1.0 + (exponentialGain * 0.8);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"CalculateConfidenceModifier: Error with normalizedScore={normalizedScore}", ex);
                return 1.0;
            }
        }

        private double CalculateCognitiveLoadFactor(double normalizedScore)
        {
            try
            {
                if (normalizedScore < 0.3) { double overloadSeverity = (0.3 - normalizedScore) / 0.3; return 0.3 + (0.4 * (1.0 - overloadSeverity)); }
                if (normalizedScore < 0.7) { return 0.8 + (normalizedScore * 0.4); }
                return 1.0 + ((normalizedScore - 0.7) * 0.5);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"CalculateCognitiveLoadFactor: Error with normalizedScore={normalizedScore}", ex);
                return 1.0;
            }
        }

        private double AdjustForPracticePattern(double interval, BarSection section, List<PracticeHistory> history)
        {
            try
            {
                if (double.IsNaN(interval) || double.IsInfinity(interval) || interval <= 0) { MLLogManager.Instance.Log($"AdjustForPracticePattern: Invalid interval ({interval}), using fallback", LogLevel.Warning); interval = 1.0; }
                if (section == null) { MLLogManager.Instance.Log("AdjustForPracticePattern: Section is null, returning original interval", LogLevel.Warning); return interval; }
                if (history == null || history.Count == 0) { MLLogManager.Instance.Log("AdjustForPracticePattern: No history available, returning original interval", LogLevel.Debug); return interval; }
                var validHistory = history.Where(h => h != null && !double.IsNaN(h.PerformanceScore) && !double.IsInfinity(h.PerformanceScore) && h.PerformanceScore >= 0 && h.PerformanceScore <= 10).OrderBy(h => h.Date).ToList();
                if (validHistory.Count == 0) { MLLogManager.Instance.Log("AdjustForPracticePattern: No valid history found, returning original interval", LogLevel.Debug); return interval; }
                int sessionsToAnalyze = Math.Min(5, validHistory.Count); var recentSessions = validHistory.OrderByDescending(h => h.Date).Take(sessionsToAnalyze).ToList();
                if (recentSessions.Count < 2)
                {
                    float avgPerf = (float)recentSessions.Average(h => h.PerformanceScore);
                    MLLogManager.Instance.Log($"[Adjust] Insufficient sessions for regression ({recentSessions.Count}), using average performance: {avgPerf:F2}", LogLevel.Debug);
                    double adjFactor = CalculateNonLinearPerformanceAdjustment(avgPerf);
                    return interval * adjFactor;
                }
                recentSessions = recentSessions.OrderBy(h => h.Date).ToList();
                var (slope, avgPerformance) = CalculateSafeLinearRegression(recentSessions);
                MLLogManager.Instance.Log($"[Adjust] Linear regression analysis: Slope={slope:F4}, AvgPerformance={avgPerformance:F2}, Sessions={recentSessions.Count}", LogLevel.Debug);
                double normalizedSlope = Math.Max(-0.3, Math.Min(0.3, slope * 0.1));
                double baseFactor = CalculateNonLinearPerformanceAdjustment(avgPerformance);
                double finalAdjustmentFactor = baseFactor + normalizedSlope;
                finalAdjustmentFactor = Math.Max(0.7, Math.Min(1.3, finalAdjustmentFactor));
                double adjustedInterval = interval * finalAdjustmentFactor; if (double.IsNaN(adjustedInterval) || double.IsInfinity(adjustedInterval) || adjustedInterval <= 0) { MLLogManager.Instance.Log("AdjustForPracticePattern: Final result invalid, using original interval", LogLevel.Warning); return interval; }
                MLLogManager.Instance.Log($"[Adjust] Final adjustment factor: {finalAdjustmentFactor:F3} (Base: {baseFactor:F3}, Slope contribution: {normalizedSlope:F3})", LogLevel.Debug);
                return adjustedInterval;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"AdjustForPracticePattern: Critical error", ex);
                return Math.Max(1.0, interval);
            }
        }

        private double CalculateNonLinearPerformanceAdjustment(double averagePerformance)
        {
            try
            {
                double normalizedScore = Math.Max(0.0, Math.Min(1.0, averagePerformance / 10.0));
                if (normalizedScore < 0.3) { double severity = (0.3 - normalizedScore) / 0.3; return 0.7 + (0.1 * (1.0 - Math.Pow(severity, 2.0))); }
                else if (normalizedScore < 0.5) { return 0.8 + (normalizedScore - 0.3) * 0.4; }
                else if (normalizedScore < 0.7) { return 0.88 + (normalizedScore - 0.5) * 0.6; }
                else { double excessPerformance = normalizedScore - 0.7; double logBonus = Math.Log(1.0 + excessPerformance * 3.0) * 0.1; return Math.Min(1.3, 1.0 + logBonus); }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"CalculateNonLinearPerformanceAdjustment: Error with averagePerformance={averagePerformance}", ex);
                return 1.0;
            }
        }

        private (double slope, double avgPerformance) CalculateSafeLinearRegression(List<PracticeHistory> sessions)
        {
            try
            {
                int n = sessions.Count; if (n < 2) return (0.0, 5.0); double sumX = 0, sumY = 0, sumXY = 0, sumXX = 0; for (int i = 0; i < n; i++) { double x = i; double y = sessions[i].PerformanceScore; if (double.IsNaN(y) || double.IsInfinity(y)) y = 5.0; sumX += x; sumY += y; sumXY += x * y; sumXX += x * x; }
                double denominator = n * sumXX - sumX * sumX; double slope = Math.Abs(denominator) > 0.0001 ? (n * sumXY - sumX * sumY) / denominator : 0.0; slope = Math.Max(-5.0, Math.Min(5.0, slope)); double avgPerformance = sumY / n; if (double.IsNaN(slope) || double.IsInfinity(slope)) slope = 0.0; if (double.IsNaN(avgPerformance) || double.IsInfinity(avgPerformance)) avgPerformance = 5.0; return (slope, avgPerformance);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("CalculateSafeLinearRegression: Error in regression calculation", ex);
                return (0.0, 5.0);
            }
        }

        public List<(DateTime Date, double RetentionPercentage)> CalculateRetentionCurve(BarSection section, int daysAhead = 365)
        {
            try
            {
                if (section == null) { MLLogManager.Instance.Log("CalculateRetentionCurve: section is null", LogLevel.Warning); return new List<(DateTime, double)>(); }
                daysAhead = Math.Max(1, Math.Min(365, daysAhead));
                DateTime startDate = section.LastPracticeDate ?? DateTime.Today;
                var retentionCurve = new List<(DateTime, double)>();
                // NEW: Gebruik stage-aware overload voor graduele MASTERED groei
                double adjustedTau = EbbinghausConstants.CalculateAdjustedTau(section.Difficulty, section.CompletedRepetitions, section.PracticeScheduleStage);
                MLLogManager.Instance.Log($"CalculateRetentionCurve: Calculating enhanced curve for {daysAhead} days with tau={adjustedTau:F2}", LogLevel.Debug);

                // Get user experience level for individual variability
                string experience = "intermediate"; // Default
                try
                {
                    var userSettings = SettingsManager.Instance.CurrentSettings;
                    if (userSettings != null && !string.IsNullOrEmpty(userSettings.MusicalExperience))
                    {
                        experience = userSettings.MusicalExperience;
                    }
                }
                catch
                {
                    // Use default if settings unavailable
                }

                for (int i = 0; i <= daysAhead; i++)
                {
                    DateTime currentDate = startDate.AddDays(i);
                    // Use enhanced retention calculation with all improvements
                    double retentionDecimal = EbbinghausConstants.CalculateRetention(i, adjustedTau, section.CompletedRepetitions, section.Difficulty, experience);
                    double retentionPercentage = retentionDecimal * 100.0;
                    retentionPercentage = Math.Max(0.0, Math.Min(100.0, retentionPercentage));
                    retentionCurve.Add((currentDate, retentionPercentage));
                }
                MLLogManager.Instance.Log($"CalculateRetentionCurve: Generated enhanced retention curve with {retentionCurve.Count} data points", LogLevel.Debug);
                return retentionCurve;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"CalculateRetentionCurve: Error calculating retention curve for section {section?.Id ?? Guid.Empty}", ex);
                var fallbackCurve = new List<(DateTime, double)>(); DateTime startDate = section?.LastPracticeDate ?? DateTime.Today; for (int i = 0; i <= Math.Min(365, daysAhead); i++) { double fallbackRetention = Math.Max(15.0, 90.0 - (i * 2.5)); fallbackCurve.Add((startDate.AddDays(i), fallbackRetention)); }
                return fallbackCurve;
            }
        }
    }

    #endregion Spaced Repetition Algorithm

    #region Overlearning Tracker

    public class OverlearningTracker
    {
        public int CalculateRequiredRepetitions(int attemptsBeforeSuccess, bool useFullOverlearning = true)
        {
            if (attemptsBeforeSuccess <= 0)
            {
                return 5;
            }
            const int baseTarget = 5;
            int extraRepetitions = useFullOverlearning ? attemptsBeforeSuccess : (int)Math.Ceiling(attemptsBeforeSuccess / 2.0);
            return baseTarget + extraRepetitions;
        }
    }

    #endregion Overlearning Tracker

    #region Practice Scheduler

    public class PracticeScheduler
    {
        private readonly SpacedRepetitionAlgorithm _algorithm = new();

        internal List<ScheduledPracticeSession> ScheduleFutureSessions(
            List<MusicPieceItem> allMusicPieces,
            List<PracticeHistory> allHistory,
            List<ScheduledPracticeSession> currentSessions)
        {
            var futureSessions = new List<ScheduledPracticeSession>();

            foreach (var piece in allMusicPieces.Where(p => !MusicPieceUtils.IsMusicPiecePaused(p.Id)))
            {
                foreach (var section in piece.BarSections)
                {
                    if (section.LifecycleState == LifecycleState.Inactive)
                    {
                        continue;
                    }

                    var historyForSection = allHistory
                        .Where(h => h.BarSectionId == section.Id)
                        .OrderBy(h => h.Date)
                        .ToList();

                    DateTime lastPracticeDate = DateHelper.NormalizeToDateOnly(
                        section.LastPracticeDate ?? section.StartDate ?? DateHelper.GetCurrentSessionDate());

                    float lastPerformance = section.CompletedRepetitions > 0 ? 5.0f : 1.0f;

                    var scheduleResult = _algorithm.CalculateNextPracticeDate(
                        piece,
                        section,
                        historyForSection,
                        lastPerformance,
                        DateHelper.GetCurrentSessionDate(),
                        completedRepetitionsThisSession: 0);

                    DateTime algorithmicDate = scheduleResult.NextDate;
                    double algorithmicTau = scheduleResult.Tau;

                    double algorithmicInterval = DateHelper.CalculateIntervalDays(lastPracticeDate, algorithmicDate);
                    DateTime nextDate = DateHelper.CalculateNextPracticeDate(lastPracticeDate, algorithmicInterval, isRegistrationPath: false);

                    section.NextDueDate = nextDate;

                    futureSessions.Add(new ScheduledPracticeSession
                    {
                        Id = Guid.NewGuid(),
                        MusicPieceId = piece.Id,
                        MusicPieceTitle = piece.Title,
                        BarSectionId = section.Id,
                        BarSectionRange = section.BarRange,
                        ScheduledDate = nextDate,
                        Difficulty = section.Difficulty,
                        Status = "Planned",
                        EstimatedDuration = TimeSpan.FromMinutes(5),
                        TauValue = algorithmicTau,
                    });
                }
            }

            return futureSessions;
        }
    }

    #endregion Practice Scheduler

}

