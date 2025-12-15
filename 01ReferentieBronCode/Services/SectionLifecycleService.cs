// File: Services/SectionLifecycleService.cs

using System;
using System.Linq;

namespace ModusPractica
{
    /// <summary>
    /// Centralized service for applying side-effects when a BarSection's lifecycle state changes.
    /// Enforces business rules for Active, Maintenance, and Inactive states.
    /// </summary>
    public static class SectionLifecycleService
    {
        /// <summary>
        /// Minimum interval (in days) required for sections in Maintenance state.
        /// UPDATED: Lowered to 7 days to respect graduated MASTERED interval growth (7→14→30→60).
        /// Previously forced 60 days immediately, which contradicted motor learning research.
        /// </summary>
        private const int MaintenanceMinDays = 7;

        /// <summary>
        /// Applies appropriate side-effects when a section transitions between lifecycle states.
        /// </summary>
        /// <param name="section">The bar section being updated</param>
        /// <param name="oldState">Previous lifecycle state</param>
        /// <param name="newState">New lifecycle state</param>
        public static void Apply(BarSection section, LifecycleState oldState, LifecycleState newState)
        {
            if (section == null)
                throw new ArgumentNullException(nameof(section));

            // Suppress side-effects and verbose logs during bulk loads
            if (AppState.SuppressLifecycleSideEffects)
            {
                // Minimal debug line for traceability, avoid empty BarRange spam
                string label = string.IsNullOrWhiteSpace(section.BarRange) ? section.Id.ToString() : section.BarRange;
                MLLogManager.Instance?.Log($"[Lifecycle] (suppressed) {oldState} → {newState} for '{label}'", LogLevel.Debug);
                return; // Do not apply any side-effects while loading
            }

            // Log the transition for debugging (use a safe label)
            {
                string label = string.IsNullOrWhiteSpace(section.BarRange) ? section.Id.ToString() : section.BarRange;
                MLLogManager.Instance?.Log(
                    $"[Lifecycle] {oldState} → {newState} for '{label}' (Id={section.Id})",
                    LogLevel.Debug);
            }

            try
            {
                switch (newState)
                {
                    case LifecycleState.Active:
                        // Transitioning to Active from Maintenance or Inactive
                        // Need to restore normal scheduling
                        if (oldState == LifecycleState.Maintenance || oldState == LifecycleState.Inactive)
                        {
                            ReactivateSection(section);
                            MLLogManager.Instance?.Log(
                                $"Section '{section.BarRange}' reactivated from {oldState} - normal scheduling restored",
                                LogLevel.Info);
                        }
                        else
                        {
                            MLLogManager.Instance?.Log(
                                $"Section '{section.BarRange}' already Active (no state change)",
                                LogLevel.Info);
                        }
                        break;

                    case LifecycleState.Maintenance:
                        // Transitioning to Maintenance: enforce minimum interval
                        if (section.Interval < MaintenanceMinDays)
                        {
                            MLLogManager.Instance?.Log(
                                $"Section '{section.BarRange}' Maintenance mode: interval increased from {section.Interval} to {MaintenanceMinDays} days",
                                LogLevel.Info);
                            section.Interval = MaintenanceMinDays;
                        }

                        // Reset NextDueDate to today + interval
                        section.NextDueDate = DateHelper.GetCurrentSessionDate().AddDays(section.Interval);

                        // Clear overdue status - maintenance sections should not appear overdue
                        if (section.GetType().GetProperty("IsOverdue") != null)
                        {
                            var isOverdueProp = section.GetType().GetProperty("IsOverdue");
                            isOverdueProp?.SetValue(section, false);
                        }

                        // Remove old scheduled sessions and create a new one with the maintenance interval
                        RescheduleForMaintenance(section);

                        MLLogManager.Instance?.Log(
                            $"Section '{section.BarRange}' entered Maintenance mode: interval={section.Interval} days, next due={section.NextDueDate:yyyy-MM-dd}",
                            LogLevel.Info);
                        break;

                    case LifecycleState.Inactive:
                        // Transitioning to Inactive: clear scheduling data
                        section.NextDueDate = null;

                        // Clear overdue status
                        if (section.GetType().GetProperty("IsOverdue") != null)
                        {
                            var isOverdueProp = section.GetType().GetProperty("IsOverdue");
                            isOverdueProp?.SetValue(section, false);
                        }

                        // Remove any pending scheduled sessions for this inactive section
                        RemoveScheduledSessionsForSection(section.Id);

                        {
                            string label = string.IsNullOrWhiteSpace(section.BarRange) ? section.Id.ToString() : section.BarRange;
                            MLLogManager.Instance?.Log(
                                $"Section '{label}' marked as Inactive (will not be scheduled)",
                                LogLevel.Info);
                        }
                        break;
                }

                // AUTO-SAVE: Save the parent MusicPiece to persist the lifecycle state change
                SaveParentMusicPiece(section);

                // Optional: Trigger schedule refresh to update planning
                // Uncomment the following lines when ready to integrate with scheduler:
                // ScheduledPracticeSessionManager.Instance?.RefreshSchedule();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError(
                    $"Error applying lifecycle state change for section '{section?.BarRange}' from {oldState} to {newState}",
                    ex);
            }
        }

        /// <summary>
        /// Saves the parent MusicPiece to persist lifecycle state changes immediately.
        /// </summary>
        private static void SaveParentMusicPiece(BarSection section)
        {
            try
            {
                // Get MainWindow to access SaveMusicPiece method
                if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
                {
                    // Find the parent MusicPiece that contains this section
                    MusicPieceItem? parentPiece = null;

                    // First try: search by section ID in BarSections
                    parentPiece = MainWindow.MusicPieces?.FirstOrDefault(mp =>
                        mp.BarSections?.Any(bs => bs.Id == section.Id) == true);

                    // Second try: fallback to ParentMusicPieceId if available
                    if (parentPiece == null && section.ParentMusicPieceId != Guid.Empty)
                    {
                        parentPiece = MainWindow.MusicPieces?.FirstOrDefault(mp => mp.Id == section.ParentMusicPieceId);
                    }

                    // Third try (auto-repair): if ParentMusicPieceId is empty but we found a parent by containment
                    if (parentPiece != null && section.ParentMusicPieceId == Guid.Empty)
                    {
                        section.ParentMusicPieceId = parentPiece.Id; // persist linkage for future lookups
                        MLLogManager.Instance?.Log(
                            $"[AutoRepair] Assigned missing ParentMusicPieceId for section '{section.BarRange}' -> {parentPiece.Title} ({parentPiece.Id})",
                            LogLevel.Debug);
                    }

                    if (parentPiece != null)
                    {
                        mainWindow.SaveMusicPiece(parentPiece);
                        MLLogManager.Instance?.Log(
                            $"Auto-saved MusicPiece '{parentPiece.Title}' after lifecycle state change for section '{section.BarRange}'",
                            LogLevel.Debug);
                    }
                    else
                    {
                        // Differentiate between truly corrupt reference vs. legacy missing ParentMusicPieceId
                        if (section.ParentMusicPieceId == Guid.Empty)
                        {
                            // Legacy / orphan without parent pointer: downgrade to Debug to avoid log spam
                            MLLogManager.Instance?.Log(
                                $"[Lifecycle][Orphan] No parent MusicPiece found for section '{section.BarRange}' (ID: {section.Id}, ParentID: EMPTY) - skipped persistence (non-critical)",
                                LogLevel.Debug);
                        }
                        else
                        {
                            // ParentMusicPieceId was set but parent not found → potential data corruption
                            MLLogManager.Instance?.Log(
                                $"Could not find parent MusicPiece for section '{section.BarRange}' (ID: {section.Id}, ParentID: {section.ParentMusicPieceId}) - state change not persisted",
                                LogLevel.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError(
                    $"Error saving parent MusicPiece for section '{section?.BarRange}'",
                    ex);
            }
        }

        /// <summary>
        /// Removes all pending scheduled sessions for a section when it becomes Inactive.
        /// </summary>
        private static void RemoveScheduledSessionsForSection(Guid sectionId)
        {
            try
            {
                // Get all scheduled sessions for this section
                var manager = ScheduledPracticeSessionManager.Instance;
                var allSessions = manager.GetAllRegularScheduledSessions();

                // Find sessions for this section that are not yet completed
                var sessionsToRemove = allSessions
                    .Where(s => s.BarSectionId == sectionId &&
                               s.Status?.ToLower() != "completed")
                    .ToList();

                if (sessionsToRemove.Any())
                {
                    // Remove each session
                    foreach (var session in sessionsToRemove)
                    {
                        manager.RemoveScheduledSession(session.Id);
                    }

                    MLLogManager.Instance?.Log(
                        $"Removed {sessionsToRemove.Count} pending scheduled session(s) for inactive section (ID: {sectionId})",
                        LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError(
                    $"Error removing scheduled sessions for section {sectionId}",
                    ex);
            }
        }

        /// <summary>
        /// Reactivates a section that was in Maintenance or Inactive state.
        /// Recalculates interval based on current difficulty and repetitions, then creates new scheduled session.
        /// </summary>
        private static void ReactivateSection(BarSection section)
        {
            try
            {
                var manager = ScheduledPracticeSessionManager.Instance;

                // Step 1: Remove all old scheduled sessions for this section
                RemoveScheduledSessionsForSection(section.Id);

                // Step 2: Recalculate interval based on current state (not maintenance minimum)
                // Use Ebbinghaus formula with current difficulty and completed repetitions
                // NEW: Gebruik stage-aware overload voor graduele MASTERED groei
                double tau = EbbinghausConstants.CalculateAdjustedTau(
                    section.Difficulty ?? "Average",
                    section.CompletedRepetitions,
                    section.PracticeScheduleStage);

                // Calculate optimal interval for this section
                double targetRetention = EbbinghausConstants.GetRetentionTargetForDifficulty(section.Difficulty);
                double rawInterval = -tau * Math.Log((targetRetention - EbbinghausConstants.ASYMPTOTIC_RETENTION_BASELINE)
                    / EbbinghausConstants.INITIAL_LEARNING_STRENGTH);

                // Clamp interval to reasonable bounds (0-365 days)
                // NEW: Allow 0 for immediate/same-day scheduling (consistent with new sections)
                int newInterval = Math.Max(0, Math.Min(365, (int)Math.Round(rawInterval)));

                // Update section's interval
                section.Interval = newInterval;

                // Calculate new NextDueDate from today
                section.NextDueDate = DateHelper.GetCurrentSessionDate().AddDays(newInterval);

                MLLogManager.Instance?.Log(
                    $"Reactivated section '{section.BarRange}': interval recalculated to {newInterval} days (was 60+), next due: {section.NextDueDate:yyyy-MM-dd}",
                    LogLevel.Info);

                // Step 3: If section has a NextDueDate, create a new session for it
                if (section.NextDueDate.HasValue)
                {
                    // Find parent MusicPiece to get Title
                    var parentPiece = MainWindow.MusicPieces?.FirstOrDefault(mp =>
                        mp.BarSections?.Any(bs => bs.Id == section.Id) == true);

                    if (parentPiece != null)
                    {
                        var newSession = new ScheduledPracticeSession
                        {
                            Id = Guid.NewGuid(),
                            MusicPieceId = parentPiece.Id,
                            MusicPieceTitle = parentPiece.Title,
                            BarSectionId = section.Id,
                            BarSectionRange = section.BarRange,
                            ScheduledDate = section.NextDueDate.Value,
                            Difficulty = section.Difficulty ?? "Average",
                            Status = "Planned",
                            EstimatedDuration = TimeSpan.FromMinutes(5),
                            TauValue = section.AdaptiveTauMultiplier
                        };

                        manager.AddScheduledSession(newSession);

                        MLLogManager.Instance?.Log(
                            $"Created new session for reactivated section '{section.BarRange}' scheduled on {section.NextDueDate.Value:yyyy-MM-dd}",
                            LogLevel.Info);
                    }
                    else
                    {
                        MLLogManager.Instance?.Log(
                            $"Could not create session: parent MusicPiece not found for section '{section.BarRange}'",
                            LogLevel.Warning);
                    }
                }
                else
                {
                    // No NextDueDate - section needs to be practiced immediately or scheduled manually
                    MLLogManager.Instance?.Log(
                        $"Reactivated section '{section.BarRange}' has no NextDueDate - needs immediate attention or manual scheduling",
                        LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError(
                    $"Error reactivating section '{section?.BarRange}'",
                    ex);
            }
        }

        /// <summary>
        /// Reschedules a section for maintenance mode by removing old sessions and creating a new one.
        /// </summary>
        private static void RescheduleForMaintenance(BarSection section)
        {
            try
            {
                var manager = ScheduledPracticeSessionManager.Instance;

                // Step 1: Remove all pending (non-completed) sessions
                var allSessions = manager.GetAllRegularScheduledSessions();
                var sessionsToRemove = allSessions
                    .Where(s => s.BarSectionId == section.Id &&
                               s.Status?.ToLower() != "completed")
                    .ToList();

                foreach (var session in sessionsToRemove)
                {
                    manager.RemoveScheduledSession(session.Id);
                }

                if (sessionsToRemove.Any())
                {
                    MLLogManager.Instance?.Log(
                        $"Removed {sessionsToRemove.Count} old pending session(s) for maintenance section '{section.BarRange}'",
                        LogLevel.Debug);
                }

                // Step 2: Create a new scheduled session for the maintenance date
                if (section.NextDueDate.HasValue)
                {
                    // Find parent MusicPiece to get Title
                    var parentPiece = MainWindow.MusicPieces?.FirstOrDefault(mp =>
                        mp.BarSections?.Any(bs => bs.Id == section.Id) == true);

                    if (parentPiece != null)
                    {
                        var newSession = new ScheduledPracticeSession
                        {
                            Id = Guid.NewGuid(),
                            MusicPieceId = parentPiece.Id,
                            MusicPieceTitle = parentPiece.Title,
                            BarSectionId = section.Id,
                            BarSectionRange = section.BarRange,
                            ScheduledDate = section.NextDueDate.Value,
                            Difficulty = section.Difficulty ?? "Average",
                            Status = "Planned",
                            EstimatedDuration = TimeSpan.FromMinutes(5), // Default estimate
                            TauValue = 0.0 // Maintenance uses fixed interval
                        };

                        manager.AddScheduledSession(newSession);

                        MLLogManager.Instance?.Log(
                            $"Created new maintenance session for section '{section.BarRange}' scheduled on {section.NextDueDate.Value:yyyy-MM-dd}",
                            LogLevel.Info);
                    }
                    else
                    {
                        MLLogManager.Instance?.Log(
                            $"Could not create maintenance session: parent MusicPiece not found for section '{section.BarRange}'",
                            LogLevel.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError(
                    $"Error rescheduling section '{section?.BarRange}' for maintenance",
                    ex);
            }
        }
    }
}
