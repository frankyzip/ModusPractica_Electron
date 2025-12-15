using System.IO;
using System.Text.Json;
using ModusPractica.Infrastructure;

namespace ModusPractica
{
    /// <summary>
    /// Scheduling is the source of truth for NextDueDate.
    /// BarSection.NextDueDate is synchronized one-way (Schedule → Section) for UI convenience.
    /// </summary>
    public class ScheduledPracticeSessionManager
    {
        private static ScheduledPracticeSessionManager _instance;
        public static ScheduledPracticeSessionManager Instance => _instance ??= new ScheduledPracticeSessionManager();

        // SCOPED: Context flag now only for ExtraPractice same-day scenarios
        private static bool _preserveDueDateSkipContext = false;
        private static string _preserveCallSite = "";
        private static Guid _preserveSectionId = Guid.Empty;

        // NEW: Central event to notify schedule changes
        public event EventHandler? ScheduleChanged;

        private string _scheduledSessionsFilePath;
        private List<ScheduledPracticeSession> _scheduledSessions;
        private readonly object _lock = new object();

        // Maximum aantal records dat we bewaren in scheduled_sessions.json
        private const int MaxScheduledSessions = 1_000;

        private ScheduledPracticeSessionManager()
        {
            _scheduledSessions = new List<ScheduledPracticeSession>();
        }

        /// <summary>
        /// SCOPED: Sets the preserve context strictly for ExtraPractice same-day scenarios
        /// </summary>
        public static void SetExtraPracticePreserveContext(bool enabled, Guid sectionId, string callsite)
        {
            _preserveDueDateSkipContext = enabled;
            _preserveSectionId = sectionId;
            _preserveCallSite = callsite;

            if (enabled)
            {
                MLLogManager.Instance.Log($"[PRESERVE CONTEXT] Enabled for ExtraPractice - SectionId: {sectionId}, CallSite: {callsite}", LogLevel.Debug);
            }
            else
            {
                MLLogManager.Instance.Log($"[PRESERVE CONTEXT] Disabled - was for SectionId: {_preserveSectionId}, CallSite: {_preserveCallSite}", LogLevel.Debug);
            }
        }

        /// <summary>
        /// Check if preserve context should block scheduling operations
        /// </summary>
        private bool ShouldSkipDueToPreserveContext(string operation, Guid? sectionId = null)
        {
            if (!_preserveDueDateSkipContext) return false;

            // Log the attempted operation
            MLLogManager.Instance.Log($"{operation}: skip due to preserve context (SectionId: {sectionId}, PreserveSectionId: {_preserveSectionId}, CallSite: {_preserveCallSite})", LogLevel.Debug);
            return true;
        }

        /// <summary>
        /// BYPASS: Force scheduling for management actions (merge/new section)
        /// </summary>
        public void AddScheduledSessionWithBypass(ScheduledPracticeSession session, string bypassReason)
        {
            MLLogManager.Instance.Log($"[BYPASS PRESERVE] {bypassReason} - Adding session for {session.BarSectionId}", LogLevel.Info);

            lock (_lock)
            {
                if (session != null)
                {
                    var existingSession = _scheduledSessions.FirstOrDefault(s => s.Id == session.Id);
                    if (existingSession != null)
                    {
                        existingSession.Status = session.Status;
                    }
                    else
                    {
                        // Safety net: do NOT remove "today's" non-completed session for this section.
                        // If marking as Completed failed (e.g., due to outcome/criteria), we still want today's
                        // entry to remain visible on the calendar instead of disappearing.
                        var today = DateHelper.GetCurrentSessionDate();
                        _scheduledSessions.RemoveAll(s =>
                            s.BarSectionId == session.BarSectionId &&
                            s.Status != "Completed" &&
                            !DateHelper.IsToday(s.ScheduledDate)
                        );

                        _scheduledSessions.Add(session);
                    }
                    SaveScheduledSessions(); // Event fired inside
                }
            }
        }

        /// <summary>
        /// BYPASS: Force removal for management actions
        /// </summary>
        public void RemoveSessionsForBarSectionWithBypass(Guid barSectionId, string bypassReason)
        {
            MLLogManager.Instance.Log($"[BYPASS PRESERVE] {bypassReason} - Removing sessions for {barSectionId}", LogLevel.Info);

            lock (_lock)
            {
                try
                {
                    if (_scheduledSessions == null)
                    {
                        MLLogManager.Instance.Log("RemoveSessionsForBarSectionWithBypass: _scheduledSessions is null", LogLevel.Warning);
                        return;
                    }

                    if (barSectionId == Guid.Empty)
                    {
                        MLLogManager.Instance.Log("RemoveSessionsForBarSectionWithBypass: barSectionId is empty", LogLevel.Warning);
                        return;
                    }

                    int removedCount = _scheduledSessions.RemoveAll(s => s != null && s.BarSectionId == barSectionId);

                    if (removedCount > 0)
                    {
                        MLLogManager.Instance.Log($"[BYPASS] Removed {removedCount} scheduled sessions for bar section {barSectionId}", LogLevel.Debug);
                        // Intentional: no save here for batch operations
                    }
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance.LogError($"RemoveSessionsForBarSectionWithBypass: Error removing sessions for barSectionId {barSectionId}", ex);
                }
            }
        }

        private void OnScheduleChanged()
        {
            try
            {
                ScheduleChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error notifying ScheduleChanged", ex);
            }
        }

        public void InitializeForUser(string profileName)
        {
            string scheduledSessionsFolder = DataPathProvider.GetScheduledFolder(profileName);
            _scheduledSessionsFilePath = Path.Combine(scheduledSessionsFolder, "scheduled_sessions.json");
            LoadScheduledSessions();
        }

        // Beperk het aantal sessies tot MaxScheduledSessions (meest recente eerst op basis van CompletionDate of ScheduledDate)
        private int EnforceScheduledSessionsLimit()
        {
            if (_scheduledSessions == null) return 0;

            int count = _scheduledSessions.Count;
            if (count <= MaxScheduledSessions) return 0;

            // Sorteer op "effectieve datum": eerst CompletionDate (als beschikbaar), anders ScheduledDate
            DateTime EffectiveDate(ScheduledPracticeSession s)
                => s?.CompletionDate ?? s?.ScheduledDate ?? DateTime.MinValue;

            var pruned = _scheduledSessions
                .Where(s => s != null)
                .OrderByDescending(EffectiveDate)
                .Take(MaxScheduledSessions)
                .ToList();

            int removed = count - pruned.Count;
            DateTime? earliestKept = pruned.Count > 0 ? pruned.Min(EffectiveDate) : null;

            _scheduledSessions = pruned;

            MLLogManager.Instance.Log($"ScheduledPracticeSessionManager: Pruned {removed} oude geplande/afgeronde sessie-records. Oudste bewaarde datum: {earliestKept:yyyy-MM-dd}.", LogLevel.Info);
            return removed;
        }



        // Replace SaveScheduledSessions with this:
        public void SaveScheduledSessions()
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(_scheduledSessionsFilePath)) return;

                try
                {
                    // Enforce limiet voor het wegschrijven
                    EnforceScheduledSessionsLimit();

                    string jsonContent = JsonSerializer.Serialize(_scheduledSessions,
                        new JsonSerializerOptions { WriteIndented = true });

                    // Use atomic write operation
                    FileLockManager.WriteAllTextWithLock(_scheduledSessionsFilePath, jsonContent);

                    MLLogManager.Instance.Log($"Saved {_scheduledSessions.Count} scheduled sessions to file.", LogLevel.Info);

                    // FIRE EVENT (Anchor A equivalent)
                    OnScheduleChanged();
                    // PATCH: App-breed event
                    AppEvents.RaiseScheduledSessionsChanged();
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance.LogError("Error saving scheduled sessions to file", ex);
                }
            }
        }

        private void LoadScheduledSessions()
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(_scheduledSessionsFilePath) || !File.Exists(_scheduledSessionsFilePath))
                {
                    _scheduledSessions = new List<ScheduledPracticeSession>();
                    return;
                }

                try
                {
                    // Use atomic read operation
                    string jsonContent = FileLockManager.ReadAllTextWithLock(_scheduledSessionsFilePath);
                    _scheduledSessions = JsonSerializer.Deserialize<List<ScheduledPracticeSession>>(jsonContent)
                        ?? new List<ScheduledPracticeSession>();

                    // Prune na laden om bestandsgrootte te beperken
                    int removed = EnforceScheduledSessionsLimit();
                    if (removed > 0)
                    {
                        // Sla meteen op zodat het bestand ook gepruned wordt
                        SaveScheduledSessions();
                    }

                    // Geen automatische normalisatie meer
                }
                catch (Exception ex)
                {
                    _scheduledSessions = new List<ScheduledPracticeSession>();
                    MLLogManager.Instance.LogError("Error loading scheduled sessions from file", ex);
                }
            }
        }


        public void AddScheduledSession(ScheduledPracticeSession session)
        {
            if (ShouldSkipDueToPreserveContext("AddScheduledSession", session?.BarSectionId))
            {
                return;
            }
            lock (_lock)
            {
                if (session != null)
                {
                    var existingSession = _scheduledSessions.FirstOrDefault(s => s.Id == session.Id);
                    if (existingSession != null)
                    {
                        existingSession.Status = session.Status;
                    }
                    else
                    {
                        _scheduledSessions.RemoveAll(s => s.BarSectionId == session.BarSectionId && s.Status != "Completed");
                        _scheduledSessions.Add(session);
                    }
                    SaveScheduledSessions(); // Event fired inside
                }
            }
        }

        public void ReplaceAllNonCompletedSessions(List<ScheduledPracticeSession> newSessions)
        {
            if (ShouldSkipDueToPreserveContext("ReplaceAllNonCompletedSessions"))
            {
                return;
            }
            lock (_lock)
            {
                if (newSessions == null) return;
                var completedSessions = _scheduledSessions.Where(s => s.Status == "Completed").ToList();
                _scheduledSessions = completedSessions.Concat(newSessions).ToList();
                SaveScheduledSessions(); // Event fired inside (Anchor D)
            }
        }

        public void RemoveScheduledSession(Guid sessionId)
        {
            lock (_lock)
            {
                int removedCount = _scheduledSessions.RemoveAll(s => s.Id == sessionId);
                if (removedCount > 0)
                {
                    MLLogManager.Instance.Log($"Removed scheduled session: {sessionId}", LogLevel.Debug);
                    SaveScheduledSessions(); // Event fired inside
                }
            }
        }

        /// <summary>
        /// When difficulty is refined in-session, propagate to the next non-completed scheduled session.
        /// Does not change due date; adheres to same-day policy.
        /// </summary>
        public void UpdateFutureSessionDifficulty(Guid barSectionId, string newDifficulty)
        {
            lock (_lock)
            {
                try
                {
                    if (barSectionId == Guid.Empty || string.IsNullOrWhiteSpace(newDifficulty))
                    {
                        MLLogManager.Instance.Log("UpdateFutureSessionDifficulty: invalid input", LogLevel.Warning);
                        return;
                    }

                    var session = GetScheduledSessionForBarSection(barSectionId);
                    if (session == null)
                    {
                        MLLogManager.Instance.Log(
                            $"UpdateFutureSessionDifficulty: no future session found for BarSectionId {barSectionId}",
                            LogLevel.Debug);
                        return;
                    }

                    if (string.Equals(session.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                    {
                        MLLogManager.Instance.Log(
                            $"UpdateFutureSessionDifficulty: next session is already completed (Id={session.Id})",
                            LogLevel.Debug);
                        return;
                    }

                    string prev = session.Difficulty;
                    session.Difficulty = newDifficulty;
                    SaveScheduledSessions(); // triggert event

                    MLLogManager.Instance.Log(
                        $"UpdateFutureSessionDifficulty: session {session.Id} difficulty {prev} → {newDifficulty}",
                        LogLevel.Info);
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance.LogError("UpdateFutureSessionDifficulty: failed", ex);
                }
            }
        }

        /// <summary>
        /// Propagate a refined BarSection difficulty to ALL upcoming (non-completed) scheduled sessions for that section.
        /// </summary>
        public void UpdateDifficultyForUpcomingSessions(Guid barSectionId, string newDifficulty)
        {
            if (barSectionId == Guid.Empty || string.IsNullOrWhiteSpace(newDifficulty))
            {
                MLLogManager.Instance.Log("UpdateDifficultyForUpcomingSessions: invalid input", LogLevel.Warning);
                return;
            }
            lock (_lock)
            {
                try
                {
                    bool changed = false;
                    foreach (var s in _scheduledSessions.Where(s => s != null && s.BarSectionId == barSectionId && !string.Equals(s.Status, "Completed", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!string.Equals(s.Difficulty, newDifficulty, StringComparison.OrdinalIgnoreCase))
                        {
                            string prev = s.Difficulty;
                            s.Difficulty = newDifficulty;
                            changed = true;
                            MLLogManager.Instance.Log($"UpdateDifficultyForUpcomingSessions: session {s.Id} {prev} → {newDifficulty}", LogLevel.Debug);
                        }
                    }
                    if (changed)
                    {
                        SaveScheduledSessions();
                        MLLogManager.Instance.Log($"UpdateDifficultyForUpcomingSessions: propagated '{newDifficulty}' to upcoming sessions for BarSection {barSectionId}", LogLevel.Info);
                    }
                    else
                    {
                        MLLogManager.Instance.Log($"UpdateDifficultyForUpcomingSessions: no changes needed for BarSection {barSectionId}", LogLevel.Debug);
                    }
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance.LogError("UpdateDifficultyForUpcomingSessions: failed", ex);
                }
            }
        }

        public void CompleteTodaysSessionFor(Guid barSectionId)
        {
            lock (_lock)
            {
                try
                {
                    // BEVEILIGING 1: Input validatie
                    if (barSectionId == Guid.Empty)
                    {
                        MLLogManager.Instance.Log("CompleteTodaysSessionFor: Invalid barSectionId (Empty GUID)", LogLevel.Warning);
                        return;
                    }

                    // STANDARDIZED: Use DateHelper for consistent "today" reference
                    var today = DateHelper.GetCurrentSessionDate();

                    // BEVEILIGING 2: Veilige ophaling van praktijkgeschiedenis
                    var todaysHistory = GetTodaysPracticeHistorySafely(barSectionId, today);
                    if (todaysHistory == null)
                    {
                        MLLogManager.Instance.Log($"CompleteTodaysSessionFor: No practice history retrieved for barSectionId {barSectionId}", LogLevel.Debug);
                        return;
                    }

                    string completionReason = null;

                    // BEVEILIGING 3: Robuuste evaluatie van betekenisvolle voorbereiding
                    bool hasMeaningfulPrep = EvaluateMeaningfulPreparation(todaysHistory);

                    // BEVEILIGING 4: Multi-criterium doelbereik evaluatie
                    bool reachedTarget = EvaluateTargetAchievement(todaysHistory, hasMeaningfulPrep, out completionReason);

                    if (!reachedTarget)
                    {
                        MLLogManager.Instance.Log(
                            $"Skip marking Completed for BarSection {barSectionId}: no verified successful practice today (outcome='{todaysHistory?.SessionOutcome}', reps={todaysHistory?.Repetitions}, duration={todaysHistory?.Duration}).",
                            LogLevel.Warning);
                        return;
                    }

                    // BEVEILIGING 5: Veilige sessie ophaling en validatie
                    var sessionToComplete = FindTodaysPlannedSessionSafely(barSectionId, today);

                    if (sessionToComplete != null)
                    {
                        // BEVEILIGING 6: Atomische status update
                        UpdateSessionStatusSafely(sessionToComplete, barSectionId, completionReason);
                    }
                    else
                    {
                        MLLogManager.Instance.Log(
                            $"No 'Planned' session found for today for BarSection {barSectionId} to complete. This is expected during extra practice.",
                            LogLevel.Debug);
                    }
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance.LogError($"CompleteTodaysSessionFor: Critical error for barSectionId {barSectionId}", ex);
                    // Continue operation - don't let individual session completion failures break the system
                }
            }
        }

        /// <summary>
        /// BEVEILIGING: Veilige ophaling van vandaag's praktijkgeschiedenis
        /// </summary>
        private PracticeHistory GetTodaysPracticeHistorySafely(Guid barSectionId, DateTime today)
        {
            try
            {
                var historyManager = PracticeHistoryManager.Instance;
                if (historyManager == null)
                {
                    MLLogManager.Instance.Log("GetTodaysPracticeHistorySafely: PracticeHistoryManager.Instance is null", LogLevel.Warning);
                    return null;
                }

                var todaysHistory = historyManager
                    .GetHistoryForBarSection(barSectionId)
                    .Where(h => h != null && DateHelper.IsToday(h.Date)) // STANDARDIZED: Use DateHelper for date comparison
                    .OrderByDescending(h => h.Date)
                    .FirstOrDefault();

                return todaysHistory;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"GetTodaysPracticeHistorySafely: Error retrieving history for barSectionId {barSectionId}", ex);
                return null;
            }
        }

        /// <summary>
        /// BEVEILIGING: Robuuste evaluatie van betekenisvolle voorbereiding
        /// </summary>
        private bool EvaluateMeaningfulPreparation(PracticeHistory todaysHistory)
        {
            try
            {
                if (todaysHistory == null) return false;

                // Valideer dat we geen null/corrupte data hebben
                if (todaysHistory.Duration.TotalSeconds < 0)
                {
                    MLLogManager.Instance.Log($"EvaluateMeaningfulPreparation: Invalid negative duration {todaysHistory.Duration}", LogLevel.Warning);
                    return false;
                }

                if (todaysHistory.Repetitions < 0)
                {
                    MLLogManager.Instance.Log($"EvaluateMeaningfulPreparation: Invalid negative repetitions {todaysHistory.Repetitions}", LogLevel.Warning);
                    return false;
                }

                // Originele logica met extra validatie
                bool isZeroRepetitions = todaysHistory.Repetitions == 0;
                bool hasMinimumDuration = todaysHistory.Duration.TotalSeconds >= 15;
                bool isNotTargetNotReached = !string.Equals(todaysHistory.SessionOutcome, "TargetNotReached", StringComparison.OrdinalIgnoreCase);

                bool hasMeaningfulPrep = isZeroRepetitions && hasMinimumDuration && isNotTargetNotReached;

                if (hasMeaningfulPrep)
                {
                    MLLogManager.Instance.Log($"EvaluateMeaningfulPreparation: Meaningful prep detected - Duration: {todaysHistory.Duration:hh\\:mm\\:ss}, Outcome: {todaysHistory.SessionOutcome}", LogLevel.Debug);
                }

                return hasMeaningfulPrep;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"EvaluateMeaningfulPreparation: Error evaluating preparation", ex);
                return false; // Conservative fallback
            }
        }

        private bool EvaluateTargetAchievement(PracticeHistory todaysHistory, bool hasMeaningfulPrep, out string completionReason)
        {
            completionReason = null;

            try
            {
                if (todaysHistory == null)
                {
                    completionReason = "No history available";
                    MLLogManager.Instance.Log("EvaluateTargetAchievement: No history for today.", LogLevel.Warning);
                    return false;
                }

                // Normaliseer outcome (alleen SessionOutcome bestaat)
                string outcome = (todaysHistory.SessionOutcome ?? string.Empty).Trim();
                string outcomeLower = outcome.ToLowerInvariant();

                // 1) Expliciete success-status (TargetReached)
                if (outcomeLower.Contains("targetreached"))
                {
                    completionReason = $"Outcome={outcome}";
                    MLLogManager.Instance.Log($"EvaluateTargetAchievement: Explicit success via outcome '{outcome}'.", LogLevel.Info);
                    return true;
                }

                // 2) Meaningful prep telt NIET als 'Completed' (stap 6-regel)
                if (hasMeaningfulPrep)
                {
                    completionReason = $"PrepOnly>=15s (no reps) - Outcome={outcome}";
                    MLLogManager.Instance.Log("EvaluateTargetAchievement: Prep-only session detected; NOT marking as Completed.", LogLevel.Info);
                    return false;
                }

                // 3) Echte oefening: ten minste 1 herhaling
                if (todaysHistory.Repetitions > 0)
                {
                    if (todaysHistory.Repetitions > 1000)
                    {
                        MLLogManager.Instance.Log($"EvaluateTargetAchievement: Suspiciously high repetition count {todaysHistory.Repetitions}.", LogLevel.Warning);
                    }

                    completionReason = $"Repetitions={todaysHistory.Repetitions}, Duration={todaysHistory.Duration:hh\\:mm\\:ss}";
                    MLLogManager.Instance.Log("EvaluateTargetAchievement: Marking as Completed based on reps > 0.", LogLevel.Info);
                    return true;
                }

                // 4) Geen criterium gehaald → niet voltooien
                completionReason = $"No criteria met - Outcome={outcome}, Reps={todaysHistory.Repetitions}, Duration={todaysHistory.Duration:hh\\:mm\\:ss}";
                MLLogManager.Instance.Log($"EvaluateTargetAchievement: Not completed. {completionReason}", LogLevel.Warning);
                return false;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("EvaluateTargetAchievement: Error during evaluation", ex);
                completionReason = $"Error during evaluation: {ex.Message}";
                return false;
            }
        }



        private ScheduledPracticeSession FindTodaysPlannedSessionSafely(Guid barSectionId, DateTime today)
        {
            try
            {
                if (_scheduledSessions == null)
                {
                    MLLogManager.Instance.Log("FindTodaysPlannedSessionSafely: _scheduledSessions is null", LogLevel.Warning);
                    return null;
                }

                // Verzamel alle niet-voltooide sessies voor vandaag voor deze BarSection
                var todaysCandidates = _scheduledSessions
                    .Where(s => s != null)
                    .Where(s => s.BarSectionId == barSectionId)
                    .Where(s => !string.Equals(s.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                    .Where(s => DateHelper.IsToday(s.ScheduledDate))
                    .OrderBy(s => s.ScheduledDate)
                    .ThenBy(s => s.Id)
                    .ToList();

                if (todaysCandidates.Count == 0)
                {
                    MLLogManager.Instance.Log(
                        $"FindTodaysPlannedSessionSafely: No planned session found for BarSectionId {barSectionId} on {DateHelper.FormatDisplayDate(today)}.",
                        LogLevel.Debug);
                    return null;
                }

                // Houd de vroegste, ruim duplicaten op
                var keep = todaysCandidates.First();
                var remove = todaysCandidates.Skip(1).Select(s => s.Id).ToHashSet();

                if (remove.Count > 0)
                {
                    MLLogManager.Instance.Log(
                        $"FindTodaysPlannedSessionSafely: Found {remove.Count + 1} sessions for BarSectionId {barSectionId} today. Keeping earliest {keep.Id}, removing {remove.Count} duplicate(s).",
                        LogLevel.Warning);

                    _scheduledSessions.RemoveAll(s => s != null && remove.Contains(s.Id));
                    try
                    {
                        SaveScheduledSessions();
                        MLLogManager.Instance.Log("FindTodaysPlannedSessionSafely: Duplicates removed and schedule saved.", LogLevel.Info);
                    }
                    catch (Exception saveEx)
                    {
                        MLLogManager.Instance.LogError("FindTodaysPlannedSessionSafely: Failed to save after duplicate cleanup", saveEx);
                    }
                }
                else
                {
                    MLLogManager.Instance.Log(
                        $"FindTodaysPlannedSessionSafely: Single planned session {keep.Id} found for BarSectionId {barSectionId} today.",
                        LogLevel.Debug);
                }

                // Extra sanity checks
                if (keep.Id == Guid.Empty)
                {
                    MLLogManager.Instance.Log("FindTodaysPlannedSessionSafely: Found session with empty ID", LogLevel.Warning);
                    return null;
                }

                if (keep.ScheduledDate.Date > today.Date)
                {
                    MLLogManager.Instance.Log(
                        $"FindTodaysPlannedSessionSafely: Session {keep.Id} is scheduled in the future ({DateHelper.FormatDisplayDate(keep.ScheduledDate)}), not today.",
                        LogLevel.Warning);
                    return null;
                }

                return keep;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"FindTodaysPlannedSessionSafely: Error while finding planned session for barSectionId {barSectionId}", ex);
                return null;
            }
        }


        private void UpdateSessionStatusSafely(ScheduledPracticeSession sessionToComplete, Guid barSectionId, string completionReason)
        {
            if (sessionToComplete == null)
            {
                MLLogManager.Instance.Log("UpdateSessionStatusSafely: sessionToComplete is null", LogLevel.Warning);
                return;
            }

            try
            {
                // Normaliseer completion-datum naar sessiedatum (date-only) i.p.v. DateTime.Now
                var completionDate = DateHelper.GetCurrentSessionDate();

                // Status & meta bijwerken
                sessionToComplete.Status = "Completed";
                sessionToComplete.CompletionDate = completionDate;
                sessionToComplete.CompletionReason = completionReason;

                // Persist
                SaveScheduledSessions();

                MLLogManager.Instance.Log(
                    $"UpdateSessionStatusSafely: Marked session {sessionToComplete.Id} (BarSectionId={barSectionId}) as Completed on {DateHelper.FormatDisplayDate(completionDate)}. Reason: {completionReason}",
                    LogLevel.Info);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError(
                    $"UpdateSessionStatusSafely: Failed to update status for session {sessionToComplete.Id} (BarSectionId={barSectionId})",
                    ex);
            }
        }


        public void RescheduleOverdueSessions(List<MusicPieceItem> allMusicPieces)
        {
            if (ShouldSkipDueToPreserveContext("RescheduleOverdueSessions"))
            {
                return;
            }
            lock (_lock)
            {
                try
                {
                    MLLogManager.Instance.Log("Checking for overdue practice sessions to reschedule...", LogLevel.Info);

                    // BEVEILIGING 1: Input validatie
                    if (allMusicPieces == null)
                    {
                        MLLogManager.Instance.Log("RescheduleOverdueSessions: allMusicPieces parameter is null", LogLevel.Warning);
                        return;
                    }

                    if (_scheduledSessions == null)
                    {
                        MLLogManager.Instance.Log("RescheduleOverdueSessions: _scheduledSessions is null, initializing empty list", LogLevel.Warning);
                        _scheduledSessions = new List<ScheduledPracticeSession>();
                        return;
                    }

                    // BEVEILIGING 2: Veilige overdue sessie detectie
                    var overdueSessions = FindOverdueSessionsSafely();

                    if (!overdueSessions.Any())
                    {
                        MLLogManager.Instance.Log("No overdue sessions found. Schedule is up to date.", LogLevel.Info);
                        return;
                    }

                    MLLogManager.Instance.Log($"Found {overdueSessions.Count} overdue session(s). Rescheduling now.", LogLevel.Warning);

                    var algorithm = new SpacedRepetitionAlgorithm();
                    bool changesMade = false;
                    var today = DateHelper.GetCurrentSessionDate();

                    // BEVEILIGING 3: Veilige rescheduling met validatie
                    foreach (var session in overdueSessions)
                    {
                        try
                        {
                            if (session == null) continue;



                            if (!ShouldRescheduleSession(session, allMusicPieces))
                                continue;

                            // Resolve piece + section
                            var piece = allMusicPieces.FirstOrDefault(p => p != null && p.Id == session.MusicPieceId);
                            if (piece == null)
                            {
                                MLLogManager.Instance.Log($"RescheduleOverdueSessions: Music piece {session.MusicPieceId} not found, skipping.", LogLevel.Debug);
                                continue;
                            }

                            var section = piece.BarSections?.FirstOrDefault(s => s != null && s.Id == session.BarSectionId);
                            if (section == null)
                            {
                                MLLogManager.Instance.Log(
                                    $"RescheduleOverdueSessions: BarSection {session.BarSectionId} for '{piece.Title}' not found, skipping.",
                                    LogLevel.Debug);
                                continue;
                            }

                            // Historiek laden, chronologisch oplopend
                            var history = PracticeHistoryManager.Instance
                                .GetHistoryForBarSection(session.BarSectionId)
                                .Where(h => h != null)
                                .OrderBy(h => h.Date)
                                .ToList();

                            // Laatste performance als input; fallback neutraal
                            float lastPerformance = history.Count > 0
                                ? history.Last().PerformanceScore
                                : (section.CompletedRepetitions > 0 ? 5.0f : 1.0f);

                            DateTime originalDate = session.ScheduledDate;

                            // Berekening met Ebbinghaus/stability (6 parameters)
                            (DateTime NextDate, double Tau) result;
                            try
                            {
                                result = algorithm.CalculateNextPracticeDate(
                                    piece,
                                    section,
                                    history,
                                    lastPerformance,
                                    today,
                                    0 // geen nieuwe herhalingen tijdens "herplannen"
                                );
                            }
                            catch (Exception algEx)
                            {
                                MLLogManager.Instance.LogError(
                                    $"RescheduleOverdueSessions: Algorithm failed for '{piece.Title} - {section.BarRange}'. Using safe fallback (tomorrow).",
                                    algEx);

                                double fallbackTau = section.CompletedRepetitions > 0
                                    ? EbbinghausConstants.CalculateAdjustedTau(section.Difficulty, section.CompletedRepetitions)
                                    : EbbinghausConstants.BASE_TAU_DAYS;

                                result = (DateHelper.CalculateNextPracticeDate(today, 1.0), fallbackTau);
                            }

                            DateTime proposed = DateHelper.NormalizeToDateOnly(result.NextDate);

                            // FIXED: Never schedule for today - minimum tomorrow
                            if (proposed <= today)
                            {
                                proposed = today.AddDays(1);
                                MLLogManager.Instance.Log(
                                    $"RescheduleOverdueSessions: Proposed date was today or past - corrected to tomorrow ({DateHelper.FormatDisplayDate(proposed)}).",
                                    LogLevel.Debug);
                            }

                            session.ScheduledDate = proposed;
                            session.TauValue = result.Tau;
                            changesMade = true;

                            int overdueDays = (int)Math.Max(0, DateHelper.CalculateIntervalDays(originalDate, today));
                            MLLogManager.Instance.Log(
                                $"Rescheduled '{piece.Title} - {section.BarRange}' ({overdueDays} overdue day(s)) from {originalDate:yyyy-MM-dd} → {proposed:yyyy-MM-dd} (τ={result.Tau:F2}).",
                                LogLevel.Info);
                        }
                        catch (Exception sessionEx)
                        {
                            MLLogManager.Instance.LogError($"RescheduleOverdueSessions: Error processing session {session?.Id ?? Guid.Empty}", sessionEx);
                            // Continue met volgende sessie
                        }
                    }

                    // BEVEILIGING 4: Atomische save operatie
                    if (changesMade)
                    {
                        try
                        {
                            SaveScheduledSessions(); // Event fired inside
                            MLLogManager.Instance.Log("Finished rescheduling overdue sessions and saved changes.", LogLevel.Info);
                        }
                        catch (Exception saveEx)
                        {
                            MLLogManager.Instance.LogError("RescheduleOverdueSessions: Failed to save rescheduled sessions", saveEx);
                            throw; // Critical error - re-throw
                        }
                    }
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance.LogError("RescheduleOverdueSessions: Critical error during rescheduling operation", ex);
                    // Don't re-throw - keep system stable
                }
            }
        }


        /// <summary>
        /// BEVEILIGING: Veilige detectie van overdue sessies
        /// </summary>
        private List<ScheduledPracticeSession> FindOverdueSessionsSafely()
        {
            try
            {
                var today = DateTime.Today;
                var overdueSessions = new List<ScheduledPracticeSession>();

                foreach (var session in _scheduledSessions.Where(s => s != null))
                {
                    try
                    {
                        // Valideer sessie data
                        if (session.Id == Guid.Empty)
                        {
                            MLLogManager.Instance.Log("FindOverdueSessionsSafely: Skipping session with empty ID", LogLevel.Debug);
                            continue;
                        }

                        // Check if overdue
                        if (session.ScheduledDate.Date < today &&
                            !string.Equals(session.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                        {
                            overdueSessions.Add(session);
                        }
                    }
                    catch (Exception sessionEx)
                    {
                        MLLogManager.Instance.LogError($"FindOverdueSessionsSafely: Error checking session {session?.Id ?? Guid.Empty}", sessionEx);
                        // Continue met volgende sessie
                    }
                }

                return overdueSessions;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("FindOverdueSessionsSafely: Error finding overdue sessions", ex);
                return new List<ScheduledPracticeSession>(); // Empty list fallback
            }
        }

        /// <summary>
        /// BEVEILIGING: Bepaal of een sessie herplant moet worden
        /// </summary>
        private bool ShouldRescheduleSession(ScheduledPracticeSession session, List<MusicPieceItem> allMusicPieces)
        {
            try
            {
                if (session == null) return false;

                var musicPiece = allMusicPieces.FirstOrDefault(mp => mp != null && mp.Id == session.MusicPieceId);
                if (musicPiece == null)
                {
                    MLLogManager.Instance.Log($"ShouldRescheduleSession: Music piece {session.MusicPieceId} not found", LogLevel.Debug);
                    return false;
                }

                // Archiveren bestaat niet meer: geen check meer op IsArchived

                // Check if piece is paused
                if (musicPiece.IsPaused && musicPiece.PauseUntilDate.HasValue && musicPiece.PauseUntilDate.Value >= DateTime.Today)
                {
                    MLLogManager.Instance.Log($"ShouldRescheduleSession: Music piece '{musicPiece.Title}' is paused until {musicPiece.PauseUntilDate.Value:yyyy-MM-dd}, not rescheduling", LogLevel.Debug);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"ShouldRescheduleSession: Error evaluating session {session?.Id ?? Guid.Empty}", ex);
                return false; // Conservative fallback
            }
        }

        public void CleanupOrphanedSessions(List<MusicPieceItem> allMusicPieces)
        {
            lock (_lock)
            {
                try
                {
                    // BEVEILIGING 1: Input validatie
                    if (allMusicPieces == null)
                    {
                        MLLogManager.Instance.Log("CleanupOrphanedSessions: allMusicPieces parameter is null", LogLevel.Warning);
                        return;
                    }

                    if (_scheduledSessions == null)
                    {
                        MLLogManager.Instance.Log("CleanupOrphanedSessions: _scheduledSessions is null", LogLevel.Warning);
                        return;
                    }

                    // BEVEILIGING 2: Veilige ID extractie met error handling
                    var (validBarSectionIds, validMusicPieceIds) = ExtractValidIdsSafely(allMusicPieces);

                    // BEVEILIGING 3: Veilige orphan detectie
                    var orphanedSessions = FindOrphanedSessionsSafely(validBarSectionIds, validMusicPieceIds);

                    if (orphanedSessions.Any())
                    {
                        MLLogManager.Instance.Log($"Data Integrity Check: Found {orphanedSessions.Count} orphaned scheduled sessions. Cleaning up now...", LogLevel.Warning);

                        // BEVEILIGING 4: Atomische cleanup operatie
                        int removedCount = PerformOrphanCleanupSafely(validBarSectionIds, validMusicPieceIds);

                        if (removedCount > 0)
                        {
                            MLLogManager.Instance.Log($"Data Integrity Check: Successfully removed {removedCount} orphaned sessions.", LogLevel.Info);

                            try
                            {
                                SaveScheduledSessions(); // Event fired inside
                            }
                            catch (Exception saveEx)
                            {
                                MLLogManager.Instance.LogError("CleanupOrphanedSessions: Failed to save after cleanup", saveEx);
                                throw; // Critical - re-throw
                            }
                        }
                    }
                    else
                    {
                        MLLogManager.Instance.Log("Data Integrity Check: No orphaned scheduled sessions found. Data is consistent.", LogLevel.Info);
                    }
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance.LogError("CleanupOrphanedSessions: Critical error during cleanup operation", ex);
                    // Don't re-throw - keep system stable
                }
            }
        }

        /// <summary>
        /// BEVEILIGING: Veilige extractie van geldige IDs
        /// </summary>
        private (HashSet<Guid> barSectionIds, HashSet<Guid> musicPieceIds) ExtractValidIdsSafely(List<MusicPieceItem> allMusicPieces)
        {
            var validBarSectionIds = new HashSet<Guid>();
            var validMusicPieceIds = new HashSet<Guid>();

            try
            {
                foreach (var piece in allMusicPieces.Where(p => p != null))
                {
                    try
                    {
                        // Valideer music piece ID
                        if (piece.Id != Guid.Empty)
                        {
                            validMusicPieceIds.Add(piece.Id);
                        }

                        // Extracteer bar section IDs veilig
                        if (piece.BarSections != null)
                        {
                            foreach (var section in piece.BarSections.Where(s => s != null))
                            {
                                if (section.Id != Guid.Empty)
                                {
                                    validBarSectionIds.Add(section.Id);
                                }
                            }
                        }
                    }
                    catch (Exception pieceEx)
                    {
                        MLLogManager.Instance.LogError($"ExtractValidIdsSafely: Error processing music piece {piece.Id}", pieceEx);
                        // Continue met volgende piece
                    }
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("ExtractValidIdsSafely: Error extracting valid IDs", ex);
            }

            return (validBarSectionIds, validMusicPieceIds);
        }

        /// <summary>
        /// BEVEILIGING: Veilige detectie van orphaned sessies
        /// </summary>
        private List<ScheduledPracticeSession> FindOrphanedSessionsSafely(HashSet<Guid> validBarSectionIds, HashSet<Guid> validMusicPieceIds)
        {
            var orphanedSessions = new List<ScheduledPracticeSession>();

            try
            {
                foreach (var session in _scheduledSessions.Where(s => s != null))
                {
                    try
                    {
                        bool isOrphaned = !validBarSectionIds.Contains(session.BarSectionId) ||
                                         !validMusicPieceIds.Contains(session.MusicPieceId);

                        if (isOrphaned)
                        {
                            orphanedSessions.Add(session);
                        }
                    }
                    catch (Exception sessionEx)
                    {
                        MLLogManager.Instance.LogError($"FindOrphanedSessionsSafely: Error checking session {session?.Id ?? Guid.Empty}", sessionEx);
                        // Conservative: behandel als orphaned als we niet zeker zijn
                        orphanedSessions.Add(session);
                    }
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("FindOrphanedSessionsSafely: Error finding orphaned sessions", ex);
            }

            return orphanedSessions;
        }

        /// <summary>
        /// BEVEILIGING: Atomische orphan cleanup
        /// </summary>
        private int PerformOrphanCleanupSafely(HashSet<Guid> validBarSectionIds, HashSet<Guid> validMusicPieceIds)
        {
            try
            {
                int removedCount = _scheduledSessions.RemoveAll(session =>
                {
                    try
                    {
                        if (session == null) return true; // Remove null sessions

                        return !validBarSectionIds.Contains(session.BarSectionId) ||
                               !validMusicPieceIds.Contains(session.MusicPieceId);
                    }
                    catch (Exception sessionEx)
                    {
                        MLLogManager.Instance.LogError($"PerformOrphanCleanupSafely: Error checking session {session?.Id ?? Guid.Empty}", sessionEx);
                        return true; // Conservative: remove if we can't validate
                    }
                });

                return removedCount;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("PerformOrphanCleanupSafely: Error during cleanup removal", ex);
                return 0; // No changes if error
            }
        }

        /// <summary>
        /// Gets all scheduled sessions.
        /// </summary>
        /// <returns>A list of all scheduled sessions</returns>
        public List<ScheduledPracticeSession> GetAllRegularScheduledSessions()
        {
            lock (_lock)
            {
                try
                {
                    if (_scheduledSessions == null)
                    {
                        MLLogManager.Instance.Log("GetAllRegularScheduledSessions: _scheduledSessions is null, returning empty list", LogLevel.Warning);
                        return new List<ScheduledPracticeSession>();
                    }

                    return new List<ScheduledPracticeSession>(
                        _scheduledSessions
                            .Where(s => s != null)
                            .OrderBy(s => s.ScheduledDate)
                    );
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance.LogError("GetAllRegularScheduledSessions: Error retrieving regular scheduled sessions", ex);
                    return new List<ScheduledPracticeSession>(); // Return empty list on error
                }
            }
        }

        /// <summary>
        /// Reloads scheduled sessions from the file system.
        /// Useful for refreshing data after external changes.
        /// </summary>
        public void ReloadScheduledSessions()
        {
            lock (_lock)
            {
                try
                {
                    MLLogManager.Instance.Log("ReloadScheduledSessions: Reloading scheduled sessions from file", LogLevel.Debug);
                    LoadScheduledSessions();

                    // Trigger schedule changed event
                    OnScheduleChanged();

                    MLLogManager.Instance.Log($"ReloadScheduledSessions: Successfully reloaded {_scheduledSessions?.Count ?? 0} scheduled sessions", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance.LogError("ReloadScheduledSessions: Error reloading scheduled sessions", ex);

                    // Ensure we have a valid list even on error
                    if (_scheduledSessions == null)
                    {
                        _scheduledSessions = new List<ScheduledPracticeSession>();
                    }
                }
            }
        }

        /// <summary>
        /// Gets a scheduled session for a specific bar section.
        /// Returns the next non-completed session, or null if no session is scheduled.
        /// </summary>
        /// <param name="barSectionId">The bar section ID to find a session for</param>
        /// <returns>The scheduled session, or null if none found</returns>
        public ScheduledPracticeSession GetScheduledSessionForBarSection(Guid barSectionId)
        {
            lock (_lock)
            {
                try
                {
                    if (_scheduledSessions == null)
                    {
                        MLLogManager.Instance.Log("GetScheduledSessionForBarSection: _scheduledSessions is null", LogLevel.Warning);
                        return null;
                    }

                    if (barSectionId == Guid.Empty)
                    {
                        MLLogManager.Instance.Log("GetScheduledSessionForBarSection: barSectionId is empty", LogLevel.Warning);
                        return null;
                    }

                    return _scheduledSessions
                        .Where(s => s != null &&
                                   s.BarSectionId == barSectionId &&
                                   !string.Equals(s.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(s => s.ScheduledDate)
                        .FirstOrDefault();
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance.LogError($"GetScheduledSessionForBarSection: Error retrieving session for barSectionId {barSectionId}", ex);
                    return null;
                }
            }
        }

        /// <summary>
        /// Removes scheduled sessions for a specific bar section without saving to file.
        /// This is useful for batch operations where you want to save once at the end.
        /// </summary>
        /// <param name="barSectionId">The bar section ID to remove sessions for</param>
        public void RemoveSessionsForBarSectionWithoutSaving(Guid barSectionId)
        {
            lock (_lock)
            {
                try
                {
                    if (_scheduledSessions == null)
                    {
                        MLLogManager.Instance.Log("RemoveSessionsForBarSectionWithoutSaving: _scheduledSessions is null", LogLevel.Warning);
                        return;
                    }

                    if (barSectionId == Guid.Empty)
                    {
                        MLLogManager.Instance.Log("RemoveSessionsForBarSectionWithoutSaving: barSectionId is empty", LogLevel.Warning);
                        return;
                    }

                    int removedCount = _scheduledSessions.RemoveAll(s => s != null && s.BarSectionId == barSectionId);

                    if (removedCount > 0)
                    {
                        MLLogManager.Instance.Log($"Queued deletion of {removedCount} scheduled sessions for bar section {barSectionId}", LogLevel.Debug);
                        // Intentional: no save -> no event yet
                    }
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance.LogError($"RemoveSessionsForBarSectionWithoutSaving: Error removing sessions for barSectionId {barSectionId}", ex);
                }
            }
        }

        /// <summary>
        /// AUTO-REPAIR: Detect and fix sections that have been practiced today but have no scheduled session
        /// </summary>
        public void AutoRepairMissingSessions(List<MusicPieceItem> allMusicPieces)
        {
            try
            {
                var today = DateHelper.GetCurrentSessionDate();
                var tomorrow = today.AddDays(1);
                int repairedCount = 0;

                MLLogManager.Instance.Log("AutoRepair: Checking for sections with today's practice but no scheduled session...", LogLevel.Info);

                foreach (var piece in allMusicPieces.Where(p => p != null && !p.IsPaused))
                {
                    if (piece.BarSections == null) continue;

                    foreach (var section in piece.BarSections)
                    {
                        try
                        {
                            // Skip Inactive sections - they should never be scheduled
                            if (section.LifecycleState == LifecycleState.Inactive)
                                continue;

                            // Check if practiced today
                            bool practicedToday = section.LastPracticeDate.HasValue &&
                                                DateHelper.IsToday(section.LastPracticeDate.Value);

                            if (!practicedToday) continue;

                            // Check if NextDueDate is today or null/past
                            bool needsRepair = !section.NextDueDate.HasValue ||
                                             section.NextDueDate.Value <= today;

                            if (!needsRepair) continue;

                            // Check if there's no planned session
                            var existingSession = GetScheduledSessionForBarSection(section.Id);
                            if (existingSession != null) continue;

                            // AUTO-REPAIR: Add session for tomorrow
                            var repairSession = new ScheduledPracticeSession
                            {
                                Id = Guid.NewGuid(),
                                MusicPieceId = piece.Id,
                                MusicPieceTitle = piece.Title,
                                BarSectionId = section.Id,
                                BarSectionRange = section.BarRange,
                                ScheduledDate = tomorrow,
                                Difficulty = section.Difficulty,
                                Status = "Planned",
                                EstimatedDuration = PracticeUtils.GetEstimatedDurationAsTimeSpan(section.Id),
                                TauValue = EbbinghausConstants.CalculateAdjustedTau(section.Difficulty, section.CompletedRepetitions)
                            };

                            // Use bypass to avoid preserve context blocking
                            AddScheduledSessionWithBypass(repairSession, "AutoRepair");

                            // Update section's NextDueDate
                            section.NextDueDate = tomorrow;

                            repairedCount++;
                            MLLogManager.Instance.Log($"AutoRepair: Created scheduled session for tomorrow for '{piece.Title} - {section.BarRange}' (practiced today but had no planning)", LogLevel.Info);
                        }
                        catch (Exception sectionEx)
                        {
                            MLLogManager.Instance.LogError($"AutoRepair: Error processing section {section?.Id} of piece {piece.Title}", sectionEx);
                        }
                    }
                }

                if (repairedCount > 0)
                {
                    MLLogManager.Instance.Log($"AutoRepair: Successfully repaired {repairedCount} section(s) with missing scheduled sessions", LogLevel.Info);
                }
                else
                {
                    MLLogManager.Instance.Log("AutoRepair: No sections needed repair", LogLevel.Debug);
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("AutoRepair: Critical error during auto-repair operation", ex);
            }
        }
    }
}
