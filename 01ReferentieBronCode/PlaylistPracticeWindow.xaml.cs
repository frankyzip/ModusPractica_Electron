// VOLLEDIGE, CORRECTE EN ROBUUSTE VERSIE

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace ModusPractica
{
    public partial class PlaylistPracticeWindow : Window
    {
        private PracticePlaylist _playlist;
        private ObservableCollection<MusicPieceItem> _allMusicPieces;
        private PlaylistItem? _currentItem;
        private bool _autoAdvance = false;
        private PracticeTimerWindow _overallTimer;
        private PracticeTimerWindow? _activeItemTimerWindow;

        // AANGEPASTE CONSTRUCTOR
        public PlaylistPracticeWindow(PracticePlaylist playlist, ObservableCollection<MusicPieceItem> allMusicPieces)
        {
            InitializeComponent();
            this.Language = XmlLanguage.GetLanguage(CultureHelper.Current.IetfLanguageTag);
            _playlist = playlist;
            _allMusicPieces = allMusicPieces;
            InitializeUI();
            UpdateUI();

            // FIX: Koppel de ContentRendered event handler. Dit voorkomt de "Owner not shown" fout.
            this.ContentRendered += PlaylistPracticeWindow_ContentRendered;

            MLLogManager.Instance.Log($"Started playlist practice session: {_playlist.Name}", LogLevel.Info);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Get the working area of the screen (this excludes the taskbar)
            var workArea = System.Windows.SystemParameters.WorkArea;

            // Set the window's height to the available screen height
            this.Height = workArea.Height;

            // Position the window at the top of the screen
            this.Top = workArea.Top;

            // Optional: Keep the window centered horizontally
            this.Left = (workArea.Width - this.Width) / 2;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Close any open timer windows
                if (_overallTimer != null && _overallTimer.IsVisible)
                {
                    _overallTimer.Close();
                }

                if (_activeItemTimerWindow != null && _activeItemTimerWindow.IsVisible)
                {
                    _activeItemTimerWindow.Close();
                }

                MLLogManager.Instance.Log("PlaylistPracticeWindow closing: timer windows closed.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error during Window_Closing in PlaylistPracticeWindow", ex);
            }
        }

        // NIEUWE METHODE: Wordt aangeroepen nadat het venster volledig is weergegeven.
        private void PlaylistPracticeWindow_ContentRendered(object sender, EventArgs e)
        {
            // Zorg ervoor dat deze code maar één keer wordt uitgevoerd door de handler te ontkoppelen.
            this.ContentRendered -= PlaylistPracticeWindow_ContentRendered;

            // De logica van de oude InitializeOverallTimer() staat nu hier, wat veilig is.
            try
            {
                _overallTimer = new PracticeTimerWindow(60)
                {
                    Title = "Overall Practice Timer"
                };
                _overallTimer.Owner = this; // Dit zal nu correct werken.
                _overallTimer.Show();
                MLLogManager.Instance.Log("Overall practice timer started for playlist session", LogLevel.Info);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Failed to initialize overall practice timer", ex);
            }
        }

        // VERWIJDERDE METHODE: Deze methode is niet meer nodig, de logica is verplaatst.
        /*
        private void InitializeOverallTimer()
        {
            // ...
        }
        */

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _overallTimer?.Close();
                _activeItemTimerWindow?.Close();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error closing timers", ex);
            }
            base.OnClosed(e);
        }

        private void InitializeUI()
        {
            TxtPlaylistTitle.Text = $"🎵 {_playlist.Name}";
            LbPlaylistItems.ItemsSource = _playlist.Items;
            var nextItem = _playlist.GetNextItem();
            if (nextItem != null)
            {
                LbPlaylistItems.SelectedItem = nextItem;
                _currentItem = nextItem;
            }
        }

        private void UpdateUI()
        {
            int completed = _playlist.CompletedItemsCount;
            int total = _playlist.Items.Count;
            TxtProgress.Text = $"Progress: {completed}/{total} sections complete";
            TxtTotalTime.Text = $"• Total time: {_playlist.TotalDurationMinutes} minutes";
            if (_currentItem != null)
            {
                TxtCurrentPiece.Text = _currentItem.MusicPieceTitle;
                TxtCurrentSection.Text = $"Section: {_currentItem.BarSectionRange}";
                TxtCurrentDuration.Text = $"⏱️ Practice for {_currentItem.DurationMinutes} minutes";
                BtnPracticeSection.IsEnabled = _activeItemTimerWindow == null || !_activeItemTimerWindow.IsVisible;
                BtnCompleteSection.IsEnabled = !_currentItem.IsCompleted;
            }
            else
            {
                TxtCurrentPiece.Text = "All sections completed! 🎉";
                TxtCurrentSection.Text = "Great job following Dr. Gebrian's method!";
                TxtCurrentDuration.Text = "";
                BtnPracticeSection.IsEnabled = false;
                BtnCompleteSection.IsEnabled = false;
            }
            BtnAutoNext.Content = _autoAdvance ? "⚡ Auto-advance: ON" : "⚡ Auto-advance: OFF";
            LbPlaylistItems.Items.Refresh();
        }

        private void LbPlaylistItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LbPlaylistItems.SelectedItem is PlaylistItem selectedItem)
            {
                _currentItem = selectedItem;
                UpdateUI();
            }
        }

        private void BtnPracticeSection_Click(object sender, RoutedEventArgs e)
        {
            if (_currentItem == null) return;
            if (_activeItemTimerWindow != null && _activeItemTimerWindow.IsVisible) return;

            try
            {
                var musicPiece = _allMusicPieces.FirstOrDefault(mp => mp.Id == _currentItem.MusicPieceId);
                var barSection = musicPiece?.BarSections.FirstOrDefault(bs => bs.Id == _currentItem.BarSectionId);
                if (musicPiece == null || barSection == null)
                {
                    MessageBox.Show("Could not find the music piece or section.", "Section Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // --- FIX 5: Input-safety clamp ---
                int originalDuration = _currentItem.DurationMinutes;
                int clampedDuration = Math.Max(0, Math.Min(originalDuration, 240)); // Clamp [0, 240] minuten
                if (originalDuration != clampedDuration)
                {
                    MLLogManager.Instance.Log($"Playlist item practice duration was outside safe range ({originalDuration} min). Clamped to {clampedDuration} min for timer.", LogLevel.Warning);
                }
                // --- EINDE FIX 5 ---

                var timerWindow = new PracticeTimerWindow(clampedDuration, this) // GEBRUIK CLAMPED WAARDE
                {
                    Title = $"Practice Timer - {_currentItem.MusicPieceTitle} ({_currentItem.BarSectionRange})"
                };

                _activeItemTimerWindow = timerWindow;

                timerWindow.Closed += (s, e) =>
                {
                    // AANGEPAST: Controleer de nieuwe 'WasCompletedSuccessfully' vlag.
                    if (timerWindow.WasCompletedSuccessfully)
                    {
                        ProcessCompletedSession(musicPiece, barSection);
                    }
                    _activeItemTimerWindow = null;
                    UpdateUI();
                };

                timerWindow.Show();
                UpdateUI();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error starting practice session for playlist item", ex);
                MessageBox.Show($"Error starting practice session: {ex.Message}", "Practice Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ProcessCompletedSession(MusicPieceItem musicPiece, BarSection barSection)
        {
            // Stap 1: Vraag feedback van de gebruiker
            var feedbackDialog = new PlaylistFeedbackDialog(
                _currentItem.MusicPieceTitle,
                _currentItem.BarSectionRange,
                _currentItem.DurationMinutes)
            {
                Owner = this
            };
            feedbackDialog.ShowDialog();

            // --- FIX 5: Input-safety clamps ---
            int originalDuration = _currentItem.DurationMinutes;
            int clampedDuration = Math.Max(0, Math.Min(originalDuration, 240)); // Clamp [0, 240] minuten
            if (originalDuration != clampedDuration)
            {
                MLLogManager.Instance.Log($"Playlist session duration was outside safe range ({originalDuration} min). Clamped to {clampedDuration} min.", LogLevel.Warning);
            }

            int originalRepetitions = feedbackDialog.GetEstimatedRepetitions(clampedDuration);
            int clampedRepetitions = Math.Max(0, Math.Min(originalRepetitions, 1000)); // Clamp [0, 1000] herhalingen
            if (originalRepetitions != clampedRepetitions)
            {
                MLLogManager.Instance.Log($"Playlist session repetitions were outside safe range ({originalRepetitions}). Clamped to {clampedRepetitions}.", LogLevel.Warning);
            }
            // --- EINDE FIX 5 ---

            // Stap 2: Maak het practice history object aan
            var practiceHistory = new PracticeHistory
            {
                Id = Guid.NewGuid(),
                Date = DateTime.Now,
                MusicPieceId = musicPiece.Id,
                BarSectionId = barSection.Id,
                MusicPieceTitle = _currentItem.MusicPieceTitle,
                BarSectionRange = _currentItem.BarSectionRange,
                Duration = TimeSpan.FromMinutes(clampedDuration), // GEBRUIK CLAMPED WAARDE
                Difficulty = feedbackDialog.ExperiencedDifficulty,
                Notes = feedbackDialog.FeedbackProvided && !string.IsNullOrEmpty(feedbackDialog.UserNotes)
                       ? $"Playlist: {_playlist.Name} - {feedbackDialog.UserNotes}"
                       : $"Playlist practice session - {_playlist.Name}",
                Repetitions = clampedRepetitions, // GEBRUIK CLAMPED WAARDE
                RepetitionStreakAttempts = feedbackDialog.ExperiencedDifficulty == "VeryHard" ? 3 : 1,
                SessionOutcome = feedbackDialog.GetSessionOutcome(),
                TargetTempo = barSection.TargetTempo,
                AchievedTempo = barSection.TargetTempo,
                PreparatoryPhaseDuration = TimeSpan.FromMinutes(0)
            };

            // Stap 3: Bereken prestatiescore en sla de sessie op
            float performanceScore = PracticeUtils.CalculatePerformanceRating(practiceHistory);
            float plannerPerformanceScore = PracticeUtils.CalculatePerformanceRating(practiceHistory, ignoreTempoForPlanner: true);
            practiceHistory.PerformanceScore = performanceScore;
            PracticeHistoryManager.Instance.AddPracticeHistory(practiceHistory);

            // Refresh calculated properties like AverageSuccessRatio to update MainWindow grid
            barSection.RefreshCalculatedProperties();

            // *** PLAYLIST PRACTICE: GEEN SPACED REPETITION UPDATE ***
            // Playlist sessies worden NIET gebruikt voor spaced repetition planning
            // Dit houdt de algoritme data zuiver en voorkomt vervuiling

            DateTime nextDueDate;
            double tauValue = 0.0;

            if (!SettingsManager.Instance.CurrentSettings.PlaylistAffectsSpacedRepetition)
            {
                // Playlist sessies beïnvloeden GEEN planning - behoud bestaande due date
                nextDueDate = barSection.NextDueDate ?? DateTime.Today.AddDays(1);
                tauValue = ScheduledPracticeSessionManager.Instance.GetScheduledSessionForBarSection(barSection.Id)?.TauValue ?? 0.0;

                MLLogManager.Instance.Log($"Playlist session completed for '{barSection.BarRange}'. No impact on spaced repetition schedule. Next due date remains: {nextDueDate:yyyy-MM-dd}", LogLevel.Info);
            }
            else
            {
                // DEZE BRANCH WORDT NOOIT UITGEVOERD omdat PlaylistAffectsSpacedRepetition altijd false is
                // Maar voor veiligheid behouden we de oude logica

                int practicesAlreadyDoneToday = PracticeHistoryManager.Instance.GetHistoryForBarSection(barSection.Id)
                                                   .Count(h => h.Date.Date == DateTime.Today) - 1;

                if (practiceHistory.Repetitions == 0 && plannerPerformanceScore < 0.2f)
                {
                    nextDueDate = DateTime.Today.AddDays(1);
                    MLLogManager.Instance.Log($"Critical failure detected for '{barSection.BarRange}' in playlist (0 reps, planner score {plannerPerformanceScore:F2}). Forcing next due date to tomorrow: {nextDueDate:yyyy-MM-dd}.", LogLevel.Warning);
                }
                else
                {
                    if (practicesAlreadyDoneToday == 0)
                    {
                        MLLogManager.Instance.Log($"First session of the day for '{barSection.BarRange}' in playlist. Calculating next due date.", LogLevel.Info);
                        var scheduledSession = ScheduledPracticeSessionManager.Instance.GetScheduledSessionForBarSection(barSection.Id);
                        DateTime practiceDateForCalculation = scheduledSession?.ScheduledDate.Date ?? DateTime.Today;

                        var scheduleResult = new SpacedRepetitionAlgorithm().CalculateNextPracticeDate(
                            musicPiece,
                            barSection,
                            PracticeHistoryManager.Instance.GetHistoryForBarSection(barSection.Id).ToList(),
                            plannerPerformanceScore,
                            practiceDateForCalculation,
                            practiceHistory.Repetitions);

                        nextDueDate = scheduleResult.NextDate;
                        tauValue = scheduleResult.Tau;

                        PersonalizedMemoryCalibration.Instance.UpdateCalibrationFromSession(practiceHistory, barSection);
                    }
                    else
                    {
                        MLLogManager.Instance.Log($"Subsequent session ({practicesAlreadyDoneToday + 1}) for '{barSection.BarRange}' in playlist. Refining difficulty.", LogLevel.Info);
                        RefineDifficulty(barSection, plannerPerformanceScore);
                        nextDueDate = barSection.NextDueDate ?? DateTime.Today.AddDays(1);
                        tauValue = ScheduledPracticeSessionManager.Instance.GetScheduledSessionForBarSection(barSection.Id)?.TauValue ?? 0.0;
                    }
                }
            }

            // Stap 6: Minimale BarSection updates voor playlist sessies
            if (!SettingsManager.Instance.CurrentSettings.PlaylistAffectsSpacedRepetition)
            {
                // PLAYLIST MODUS: Alleen tijd tracking, geen planning updates
                barSection.LastPracticeDate = DateTime.Now;
                barSection.CompletedRepetitions += practiceHistory.Repetitions;
                // NextDueDate blijft ongewijzigd!

                MLLogManager.Instance.Log($"Playlist session recorded for '{barSection.BarRange}'. Time tracked, no scheduling changes made.", LogLevel.Info);
            }
            else
            {
                // NORMALE MODUS: Volledige updates (wordt nooit uitgevoerd)
                barSection.LastPracticeDate = DateTime.Now;
                barSection.Difficulty = feedbackDialog.ExperiencedDifficulty;
                barSection.CompletedRepetitions += practiceHistory.Repetitions;
                barSection.NextDueDate = nextDueDate.Date;

                ScheduledPracticeSessionManager.Instance.CompleteTodaysSessionFor(barSection.Id);

                var newScheduledSession = new ScheduledPracticeSession
                {
                    Id = Guid.NewGuid(),
                    MusicPieceId = musicPiece.Id,
                    MusicPieceTitle = musicPiece.Title,
                    BarSectionId = barSection.Id,
                    BarSectionRange = barSection.BarRange,
                    ScheduledDate = nextDueDate.Date,
                    Difficulty = barSection.Difficulty,
                    Status = "Planned",
                    EstimatedDuration = PracticeUtils.GetEstimatedDurationAsTimeSpan(barSection.Id),
                    TauValue = tauValue
                };
                ScheduledPracticeSessionManager.Instance.AddScheduledSession(newScheduledSession);
            }

            // Stap 6.5: Toon een rapport (optioneel) vergelijkbaar met de normale sessie
            bool showReport = SettingsManager.Instance.CurrentSettings.ShowSessionReport;
            if (showReport)
            {
                int numberOfBars = PracticeUtils.ParseBarCount(practiceHistory.BarSectionRange);
                float barsPerMinute = (practiceHistory.Duration.TotalMinutes > 0) ? (float)((practiceHistory.Repetitions * numberOfBars) / practiceHistory.Duration.TotalMinutes) : 0;

                // Calculate algorithm interval for override functionality
                double algorithmInterval = (nextDueDate.Date - DateTime.Today).TotalDays;

                // Calculate learning zone based on session success ratio
                string learningZone = "Challenge"; // Default
                if (practiceHistory.Repetitions > 0 && practiceHistory.AttemptsTillSuccess > 0)
                {
                    double successRatio = (double)practiceHistory.Repetitions / practiceHistory.AttemptsTillSuccess;
                    if (successRatio >= 0.90)
                        learningZone = "Mastery";
                    else if (successRatio >= 0.80)
                        learningZone = "Consolidation";
                    else if (successRatio >= 0.60)
                        learningZone = "Exploration";
                    // else stays "Challenge"
                }

                var reportWindow = new SessionReportWindow(practiceHistory, performanceScore, nextDueDate, barsPerMinute, plannerPerformanceScore, algorithmInterval, barSection, musicPiece, 0, 0, learningZone) { Owner = this };
                bool? result = reportWindow.ShowDialog();

                // NEW: Check if user provided an override and apply it (for playlist sessions that affect scheduling)
                if (result == true && reportWindow.OverrideData != null && reportWindow.OverrideInterval.HasValue &&
                    SettingsManager.Instance.CurrentSettings.PlaylistAffectsSpacedRepetition)
                {
                    double userOverrideInterval = reportWindow.OverrideInterval.Value;
                    string overrideReason = reportWindow.OverrideReason;

                    MLLogManager.Instance?.Log($"🔧 [Playlist] Applying user override: {algorithmInterval:F1}d → {userOverrideInterval:F1}d (Reason: '{overrideReason}')", LogLevel.Info);

                    // Calculate new due date based on user override
                    DateTime newDueDate = DateTime.Today.AddDays(userOverrideInterval);
                    barSection.NextDueDate = newDueDate.Date;

                    MLLogManager.Instance?.Log($"✅ [Playlist] Override applied! New due date: {newDueDate:yyyy-MM-dd}", LogLevel.Info);

                    // Update the override data in the practice history for ML learning
                    if (practiceHistory != null)
                    {
                        practiceHistory.UserOverrideInterval = userOverrideInterval;
                        practiceHistory.OriginalAlgorithmInterval = algorithmInterval;
                        practiceHistory.OverrideReason = overrideReason;

                        MLLogManager.Instance?.Log($"📊 [Playlist] Override data stored in practice history for ML learning", LogLevel.Info);
                    }
                }
            }

            // Stap 7: Sla de wijzigingen op
            if (Application.Current.MainWindow is MainWindow main)
            {
                main.SaveMusicPiece(musicPiece);
            }

            // Aangepaste log melding voor playlist sessies
            if (!SettingsManager.Instance.CurrentSettings.PlaylistAffectsSpacedRepetition)
            {
                MLLogManager.Instance.Log($"Playlist session completed: {barSection.BarRange}. Practice time recorded, no scheduling impact.", LogLevel.Info);
            }
            else
            {
                MLLogManager.Instance.Log($"Completed playlist item: {barSection.BarRange}. Next due: {nextDueDate:yyyy-MM-dd}", LogLevel.Info);
            }

            // Stap 8: Werk de UI bij
            _currentItem.IsCompleted = true;
            if (_autoAdvance)
            {
                var nextItem = _playlist.GetNextItem();
                if (nextItem != null)
                {
                    LbPlaylistItems.SelectedItem = nextItem;
                    _currentItem = nextItem;
                }
                else
                {
                    MessageBox.Show($"Playlist '{_playlist.Name}' completed!", "Playlist Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            UpdateUI();
        }

        /// <summary>
        /// Verfijnt de moeilijkheidsgraad van een BarSection op basis van de prestatiescore van een sessie.
        /// </summary>
        private void RefineDifficulty(BarSection section, float performanceScore)
        {
            string currentDifficulty = section.Difficulty;
            string newDifficulty = currentDifficulty;

            if (performanceScore < 0.4f) // Gebruiker worstelt
            {
                switch (currentDifficulty)
                {
                    case "Easy": newDifficulty = "Average"; break;
                    case "Average": newDifficulty = "Difficult"; break;
                }
            }
            else if (performanceScore > 0.9f) // Gebruiker vindt het makkelijk
            {
                switch (currentDifficulty)
                {
                    case "Difficult": newDifficulty = "Average"; break;
                    case "Average": newDifficulty = "Easy"; break;
                }
            }

            if (newDifficulty != currentDifficulty)
            {
                section.Difficulty = newDifficulty;
                MLLogManager.Instance.Log($"Difficulty for '{section.BarRange}' refined from {currentDifficulty} to {newDifficulty} based on performance score of {performanceScore:F2}.", LogLevel.Info);

                // STAP 4: Persist en refresh na difficulty refine
                try
                {
                    // Persist: gebruik bestaande save-util voor stukken/secties
                    var musicPiece = _allMusicPieces.FirstOrDefault(mp => mp.BarSections.Any(bs => bs.Id == section.Id));
                    if (musicPiece != null && Application.Current.MainWindow is MainWindow mainPersist)
                    {
                        mainPersist.SaveMusicPiece(musicPiece);
                    }

                    // Sync geplande (toekomstige) sessie-difficulty
                    ScheduledPracticeSessionManager.Instance.UpdateFutureSessionDifficulty(section.Id, newDifficulty);

                    // Refresh: herladen in-memory cache/collectie voor UI sync
                    AppState.MusicDataChanged = true;
                    if (Application.Current.MainWindow is MainWindow mainRefresh)
                    {
                        mainRefresh.SyncAllPiecesFromHistoryAndRefresh();
                    }

                    // Extra logregel: acceptatiecriteria
                    MLLogManager.Instance.Log(
                        $"Difficulty persisted and UI refreshed ({currentDifficulty} → {newDifficulty}). Future session difficulty synchronized.",
                        LogLevel.Info);
                }
                catch (Exception persistEx)
                {
                    MLLogManager.Instance.LogError($"Failed to persist difficulty change for '{section.BarRange}'", persistEx);
                }
            }
        }

        private void BtnCompleteSection_Click(object sender, RoutedEventArgs e)
        {
            if (_currentItem == null || _currentItem.IsCompleted) return;
            var result = MessageBox.Show(
                $"Mark '{_currentItem.BarSectionRange}' from '{_currentItem.MusicPieceTitle}' as complete without using the timer?",
                "Manual Complete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _currentItem.IsCompleted = true;
                MLLogManager.Instance.Log($"Manually completed playlist item: {_currentItem.MusicPieceTitle} - {_currentItem.BarSectionRange}", LogLevel.Info);
                if (_autoAdvance)
                {
                    var nextItem = _playlist.GetNextItem();
                    if (nextItem != null)
                    {
                        LbPlaylistItems.SelectedItem = nextItem;
                        _currentItem = nextItem;
                    }
                }
                UpdateUI();
            }
        }

        private void BtnAutoNext_Click(object sender, RoutedEventArgs e)
        {
            _autoAdvance = !_autoAdvance;
            UpdateUI();
            string status = _autoAdvance ? "enabled" : "disabled";
            MLLogManager.Instance.Log($"Auto-advance {status} for playlist practice", LogLevel.Info);
        }

        private void BtnResetPlaylist_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will reset the progress of all sections in this playlist. Are you sure?",
                "Reset Progress",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _playlist.ResetProgress();
                var firstItem = _playlist.Items.FirstOrDefault();
                if (firstItem != null)
                {
                    LbPlaylistItems.SelectedItem = firstItem;
                    _currentItem = firstItem;
                }
                UpdateUI();
                MLLogManager.Instance.Log($"Reset progress for playlist: {_playlist.Name}", LogLevel.Info);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            // AANGEPAST: Verwijder de ongeldige logica en sluit gewoon het venster.
            if (_activeItemTimerWindow != null && _activeItemTimerWindow.IsVisible)
            {
                _activeItemTimerWindow.Close();
            }

            var incompleteCount = _playlist.Items.Count(item => !item.IsCompleted);
            if (incompleteCount > 0)
            {
                var result = MessageBox.Show(
                    $"You have {incompleteCount} sections remaining in this playlist.\n\nAre you sure you want to finish the session?",
                    "Incomplete Session",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                    return;
            }
            else
            {
                MessageBox.Show(
                    $"🎉 Congratulations!\n\nYou completed all {_playlist.Items.Count} sections in your practice playlist.\n\n" +
                    "Following Dr. Gebrian's interleaved practice method helps strengthen neural pathways and improves long-term retention.",
                    "Session Complete!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            _playlist.MarkAsUsed();
            MLLogManager.Instance.Log($"Finished playlist practice session: {_playlist.Name}", LogLevel.Info);
            DialogResult = true;
            Close();
        }
    }
}
