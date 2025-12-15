using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Linq;
using System;

namespace ModusPractica
{
    public partial class PracticeSessionWindow : Window
    {
        // --- GECORRIGEERD: Declaraties met volledige naam en zonder readonly waar nodig ---
        private System.Timers.Timer _sessionTimer;

        private Stopwatch _sessionStopwatch;
        private TimeSpan _totalElapsedTime;

        private System.Timers.Timer _pomodoroTimer;
        private Stopwatch _pomodoroStopwatch;
        private bool _isPomodoroRunning;
        private int _pomodoroDuration = 5;

        private readonly MusicPieceItem _musicPiece;
        private readonly BarSection _barSection;
        private readonly List<DateTime> _activityTimestamps;
        private readonly Guid _sessionInstanceId; // NIEUW: Unieke ID voor deze venster-instantie.
        private PracticeHistory _selectedHistoryItem;
        private bool _isEditingExistingItem = false;
        private readonly Guid? _existingScheduledSessionId;
        private TimeSpan _preparatoryPhaseDuration; // NIEUW
        private bool _preparatoryPhaseEnded = false; // NIEUW: om te zorgen dat we dit maar één keer doen per sessie
        private bool _isTimerRunning;
        private int _completedRepetitions;
        private int _attemptsThisSession;
        private int _repetitionStreakAttempts;
        private int _totalFailures; // v4.0: Alle failures tijdens de sessie
        private string _selectedDifficulty;
        private int _targetRepetitions;
        private bool _isInitializing = true;

        private MediaPlayer _mediaPlayer;
        private const int CRITICAL_ATTEMTS_THRESHOLD = 8;
        private System.Timers.Timer _saveCooldownTimer;
        private bool _isSaveAllowed = true;
        private const int SAVE_COOLDOWN_SECONDS = 2;

        // Reference to the SessionReportWindow
        private SessionReportWindow _sessionReportWindow;

        // nieuw
        private bool _isExtraPractice; // nieuw
        private bool _preserveDueDate; // nieuw
        private bool _isSubsequentSession; // nieuw
        private Guid _extraPracticeLastScheduledSessionId; // nieuw
        private bool _calibrationApplied = false; // ensure single calibration
        private bool _sessionAlreadySaved = false; // Prevent duplicate saving

        // NIEUW: wall-clock referentie voor fallback voorbereidingstijd
        private readonly DateTime _openedAtUtc;

        // Overlearning recommendations
        private int _recommended50Overlearning = 0;
        private int _recommended100Overlearning = 0;
        private readonly OverlearningTracker _overlearningTracker;

        // Cancel/Rollback support
        private PracticeHistory? _savedSession; // Backup van de opgeslagen sessie voor rollback
        private DateTime? _previousNextDueDate; // Backup van vorige NextDueDate
        private int _previousInterval; // Backup van vorig interval
        private int _previousCompletedRepetitions; // Backup van vorige completed reps
        private string? _previousDifficulty; // Backup van vorige difficulty
        private Guid? _createdScheduledSessionId; // ID van nieuw aangemaakte scheduled session

        public PracticeSessionWindow(MusicPieceItem musicPiece, BarSection barSection, PracticeHistory? historyItem = null)
        {
            InitializeComponent();
            this.Language = XmlLanguage.GetLanguage(CultureHelper.Current.IetfLanguageTag);

            _overlearningTracker = new OverlearningTracker();
            _openedAtUtc = DateTime.UtcNow; // ← registratie openingsmoment (UTC voor stabiliteit)
            _sessionInstanceId = Guid.NewGuid(); // NIEUW: Wijs een unieke ID toe bij creatie.

            _musicPiece = musicPiece;
            _barSection = barSection;
            _activityTimestamps = new List<DateTime> { DateTime.Now };

            _sessionStopwatch = new Stopwatch();
            _sessionTimer = new System.Timers.Timer(100);
            _sessionTimer.Elapsed += SessionTimer_Elapsed;

            _pomodoroStopwatch = new Stopwatch();
            _pomodoroTimer = new System.Timers.Timer(100);
            _pomodoroTimer.Elapsed += PomodoroTimer_Elapsed;

            // --- INITIALISATIE VAN SAVE COOLDOWN TIMER ---
            _saveCooldownTimer = new System.Timers.Timer(TimeSpan.FromSeconds(SAVE_COOLDOWN_SECONDS).TotalMilliseconds);
            _saveCooldownTimer.AutoReset = false; // Timer runs only once per Start()
            _saveCooldownTimer.Elapsed += (s, e) => { _isSaveAllowed = true; };

            if (historyItem != null)
            {
                LoadHistoryItemForEditing(historyItem);
            }
            else
            {
                InitializeNewSession();
            }

            this.Title = $"Practice Session: {_musicPiece.Title}";
            _existingScheduledSessionId = ScheduledPracticeSessionManager.Instance.GetScheduledSessionForBarSection(_barSection.Id)?.Id;

            InitializeUI();
            LoadPracticeHistory();
            InitializeAlarmSound();

            // Verwijderd: uitlezen Tag hier (te vroeg). Gebruik ApplyExtraPracticeContext na constructie.

            _isInitializing = false;
        }

        public void ApplyExtraPracticeContext(ExtraPracticeContext ctx)
        {
            if (ctx == null) return;
            if (ctx.Mode == "ExtraPractice")
            {
                _isExtraPractice = true;
                _preserveDueDate = ctx.PreserveDueDate;
                _isSubsequentSession = ctx.IsSubsequentSession;
                _extraPracticeLastScheduledSessionId = ctx.LastScheduledSessionId;
                _calibrationApplied = false; // reset calibration guard at session start
                MLLogManager.Instance.Log($"ApplyExtraPracticeContext: Subsequent={_isSubsequentSession}, PreserveDueDate={_preserveDueDate} SectionId={_barSection.Id} SessionId={_sessionInstanceId}", LogLevel.Info);
            }
        }

        private bool IsSimpleMode()
        {
            return SettingsManager.Instance.CurrentSettings.PracticeSessionMode == "Simple";
        }

        private void InitializeUI()
        {
            TxtMusicPieceTitle.SetCurrentValue(TextBlock.TextProperty, _musicPiece.Title);
            var desc = _barSection.Description?.Trim();
            var headerText = string.IsNullOrEmpty(desc)
                ? $"Bar Section: {_barSection.BarRange}"
                : $"Bar Section: {_barSection.BarRange} - {desc}";
            TxtBarSectionInfo.SetCurrentValue(TextBlock.TextProperty, headerText);

            // Configure UI based on mode
            ConfigureUIForMode();

            // --- START AANPASSING ---

            // Stap 1: Vul de ComboBox dynamisch met getallen van 1 tot 12
            CbTargetRepetitions.Items.Clear(); // Maak leeg voor de zekerheid
            for (int i = 1; i <= 12; i++)
            {
                CbTargetRepetitions.Items.Add(i.ToString());
            }

            // Stap 2: Bepaal en selecteer de juiste waarde
            if (_barSection.Difficulty?.ToLower() == "mastered")
            {
                _targetRepetitions = 2;
                CbTargetRepetitions.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, "2");
                CbTargetRepetitions.SetCurrentValue(IsEnabledProperty, false);
                TxtDynamicTargetInfo.SetCurrentValue(TextBlock.TextProperty, "Review session: target automatically set to 2 repetitions.");
                TxtDynamicTargetInfo.SetCurrentValue(VisibilityProperty, Visibility.Visible);
            }
            else
            {
                // Zoek de laatst opgeslagen waarde, of gebruik '6' als standaard
                string targetToSelect = _barSection.TargetRepetitions > 0 ? _barSection.TargetRepetitions.ToString() : "6";

                // Controleer of deze waarde wel in de lijst staat (bv. als het 1 was)
                if (!CbTargetRepetitions.Items.Contains(targetToSelect))
                {
                    targetToSelect = "6"; // Fallback naar de standaardwaarde
                }

                CbTargetRepetitions.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, targetToSelect);
                _targetRepetitions = int.Parse(targetToSelect);
                CbTargetRepetitions.SetCurrentValue(IsEnabledProperty, true);
                TxtDynamicTargetInfo.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
            }
            // --- EINDE AANPASSING ---

            // Set the appropriate radio button based on _selectedDifficulty
            switch (_selectedDifficulty?.ToLower())
            {
                case "difficult": RbDifficult.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true); break;
                case "easy": RbEasy.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true); break;
                case "mastered": RbMastered.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true); break;
                case "average": RbAverage.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true); break;
                default:
                    // Default to "Difficult" for new sections
                    RbDifficult.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, true);
                    _selectedDifficulty = "Difficult";
                    break;
            }

            TxtTargetTempo.SetCurrentValue(TextBox.TextProperty, _barSection.TargetTempo > 0 ? _barSection.TargetTempo.ToString() : string.Empty);
            var lastSessionWithTempo = PracticeHistoryManager.Instance.GetHistoryForBarSection(_barSection.Id).Where(h => h.AchievedTempo > 0).OrderByDescending(h => h.Date).FirstOrDefault();
            if (lastSessionWithTempo != null)
            {
                TxtLastAchievedTempo.SetCurrentValue(TextBlock.TextProperty, $"Last achieved: {lastSessionWithTempo.AchievedTempo} BPM");
                TxtLastAchievedTempo.SetCurrentValue(VisibilityProperty, Visibility.Visible);
            }

            UpdateAllTextFields();
            UpdateTimerButtonStates();

            // Initialiseer Pomodoro timer display
            TxtMicroBreakTimer.SetCurrentValue(TextBlock.TextProperty, $"{_pomodoroDuration:00}:00");

            // Initialiseer overlearning aanbevelingen
            UpdateOverlearningRecommendations();
        }

        private void ConfigureUIForMode()
        {
            if (IsSimpleMode())
            {
                // Hide detailed tracking elements in Simple Mode
                GbAttempts.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
                GbRepetitions.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
                GbTempo.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);

                // Keep only essential elements visible
                // - Timer stays visible (core practice tracking)
                // - Difficulty stays visible (needed for algorithm)
                // - Notes stays visible (valuable user input)

                // Add simple mode indicator
                this.SetCurrentValue(TitleProperty, $"Practice Session (Simple Mode): {_musicPiece.Title}");

                MLLogManager.Instance.Log("Practice session started in Simple Mode", LogLevel.Info);
            }
            else
            {
                // Show all elements in Advanced Mode
                GbAttempts.SetCurrentValue(VisibilityProperty, Visibility.Visible);
                GbRepetitions.SetCurrentValue(VisibilityProperty, Visibility.Visible);
                GbTempo.SetCurrentValue(VisibilityProperty, Visibility.Visible);

                this.SetCurrentValue(TitleProperty, $"Practice Session (Advanced Mode): {_musicPiece.Title}");

                MLLogManager.Instance.Log("Practice session started in Advanced Mode", LogLevel.Info);
            }
        }

        private void SaveSessionSimpleMode(string resultReason)
        {
            try
            {
                // Pause timer if running
                if (_isTimerRunning)
                {
                    BtnPauseTimer_Click(null, null);
                }
                UpdateElapsedTimeFromTextBoxes();

                // Validate minimum session time
                if (_totalElapsedTime.TotalSeconds < 1)
                {
                    MessageBox.Show("Please record at least one second of practice time.", "No Time Recorded", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Show evaluation dialog
                var sectionInfo = $"Section: {_musicPiece.Title} - {_barSection.BarRange}";
                bool isFrustrationSession = resultReason.Contains("Frustration");
                var evaluationDialog = new SimpleSessionEvaluationDialog(sectionInfo, isFrustrationSession) { Owner = this };
                var result = evaluationDialog.ShowDialog();

                if (result != true || !evaluationDialog.Result.WasSaved)
                {
                    // User cancelled, don't close session window
                    return;
                }

                var eval = evaluationDialog.Result;

                // Create session with evaluation data
                var sessionToSave = new PracticeHistory
                {
                    MusicPieceId = _musicPiece.Id,
                    MusicPieceTitle = _musicPiece.Title,
                    BarSectionId = _barSection.Id,
                    BarSectionRange = _barSection.BarRange,
                    Date = DateTime.Now,
                    Duration = _totalElapsedTime,
                    Repetitions = eval.EstimatedRepetitions,
                    Difficulty = eval.DifficultyLevel,
                    Notes = eval.Notes,
                    AttemptsTillSuccess = eval.EstimatedAttempts,
                    RepetitionStreakAttempts = Math.Max(0, eval.EstimatedAttempts - eval.EstimatedRepetitions),
                    TotalFailures = Math.Max(0, eval.EstimatedAttempts - eval.EstimatedRepetitions),
                    SessionOutcome = resultReason == "TargetNotReached" ? "TargetReached" : resultReason, // Simple mode assumes success
                    TargetTempo = 0, // Not tracked in simple mode
                    AchievedTempo = 0, // Not tracked in simple mode
                    PreparatoryPhaseDuration = _preparatoryPhaseDuration,
                    TargetRepetitions = eval.EstimatedRepetitions
                };

                // Set performance score from evaluation
                sessionToSave.PerformanceScore = eval.PerformanceScore;

                // Update internal state to match evaluation
                _completedRepetitions = eval.EstimatedRepetitions;
                _selectedDifficulty = eval.DifficultyLevel;
                _attemptsThisSession = eval.EstimatedAttempts;
                _repetitionStreakAttempts = Math.Max(0, eval.EstimatedAttempts - eval.EstimatedRepetitions);
                _totalFailures = Math.Max(0, eval.EstimatedAttempts - eval.EstimatedRepetitions);

                // Apply safety clamps and validate
                ApplyUiInputSafetyClamps(sessionToSave);
                if (!IsValidPracticeHistory(sessionToSave))
                {
                    _saveCooldownTimer.Stop();
                    _isSaveAllowed = true;
                    return;
                }

                // Continue with standard save logic (abbreviated for Simple Mode)
                PracticeHistoryManager.Instance.AddPracticeHistory(sessionToSave);
                _sessionAlreadySaved = true;

                // Update bar section properties
                _barSection.LastPracticeDate = DateTime.Today;
                _barSection.Difficulty = sessionToSave.Difficulty;
                _barSection.TargetRepetitions = sessionToSave.TargetRepetitions;
                _barSection.AttemptsTillSuccess = sessionToSave.AttemptsTillSuccess;

                // Calculate next practice date using Ebbinghaus
                var historyAfterAdd = PracticeHistoryManager.Instance.GetHistoryForBarSection(_barSection.Id).ToList();
                float plannerPerformanceScore = PracticeUtils.CalculatePerformanceRating(sessionToSave, ignoreTempoForPlanner: true);

                var scheduleResult = new SpacedRepetitionAlgorithm().CalculateNextPracticeDate(
                    _musicPiece,
                    _barSection,
                    historyAfterAdd,
                    plannerPerformanceScore,
                    DateTime.Today,
                    sessionToSave.Repetitions);

                var nextDueDate = scheduleResult.NextDate;
                var tauValue = EbbinghausConstants.ClampTauToSafeBounds(scheduleResult.Tau);

                _barSection.NextDueDate = nextDueDate.Date;
                _barSection.Interval = Math.Max(1, (int)Math.Round((nextDueDate.Date - DateTime.Today).TotalDays));

                // Update completed repetitions
                _barSection.CompletedRepetitions = historyAfterAdd.Where(h => !h.IsDeleted).Sum(h => h.Repetitions);
                _barSection.RefreshCalculatedProperties();

                // Schedule next session
                ScheduledPracticeSessionManager.Instance.CompleteTodaysSessionFor(_barSection.Id);
                var newScheduledSession = new ScheduledPracticeSession
                {
                    Id = Guid.NewGuid(),
                    MusicPieceId = _musicPiece.Id,
                    MusicPieceTitle = _musicPiece.Title,
                    BarSectionId = _barSection.Id,
                    BarSectionRange = _barSection.BarRange,
                    ScheduledDate = nextDueDate.Date,
                    Difficulty = _barSection.Difficulty,
                    Status = "Planned",
                    EstimatedDuration = TimeSpan.FromMinutes(PracticeUtils.GetEstimatedDurationForSection(_barSection.Id)),
                    TauValue = tauValue
                };
                ScheduledPracticeSessionManager.Instance.AddScheduledSession(newScheduledSession);

                // Update music piece and save
                _musicPiece.UpdateProgress();
                if (Application.Current.MainWindow is MainWindow main)
                {
                    var canonical = _musicPiece.BarSections?.FirstOrDefault(bs => bs.Id == _barSection.Id);
                    if (canonical != null && canonical.LifecycleState != _barSection.LifecycleState)
                    {
                        canonical.LifecycleState = _barSection.LifecycleState;
                        MLLogManager.Instance?.Log($"[LifecycleSync] Updated lifecycle state for '{canonical.BarRange}' (Id={canonical.Id})", LogLevel.Info);
                    }

                    main.SaveMusicPiece(_musicPiece);
                    main.SyncAllPiecesFromHistoryAndRefresh();
                }

                // Show simple completion message
                var nextDateText = nextDueDate.Date == DateTime.Today.AddDays(1) ? "tomorrow" : nextDueDate.ToString("MMM d");
                MessageBox.Show(
                    $"Great session! 🎵\n\nNext practice: {nextDateText}\n\nDifficulty adjusted to: {eval.DifficultyLevel}",
                    "Session Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                MLLogManager.Instance.Log($"Simple Mode session completed: {eval.EstimatedRepetitions} reps, difficulty={eval.DifficultyLevel}, next={nextDueDate:yyyy-MM-dd}", LogLevel.Info);

                // Close session window
                try { this.DialogResult = true; } catch { /* non-modal safety */ }
                DataContext = null;
                Close();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error in SaveSessionSimpleMode", ex);
                MessageBox.Show("An error occurred while saving the session. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeNewSession()
        {
            _sessionAlreadySaved = false; // Reset save flag for new sessions
            _totalElapsedTime = TimeSpan.Zero;
            _isTimerRunning = false;
            _completedRepetitions = 0;
            _selectedDifficulty = _barSection.Difficulty ?? "Difficult"; // ← CHANGED FALLBACK FROM "Average" TO "Difficult"
            _attemptsThisSession = 0;
            _repetitionStreakAttempts = 0;
            _totalFailures = 0; // v4.0
            _preparatoryPhaseDuration = TimeSpan.Zero; // NIEUW
            _preparatoryPhaseEnded = false; // NIEUW

            // Sight reading feature removed

            // v4.0: Initialize success ratio display
            UpdateSuccessRatioDisplay();
        }

        private void LoadHistoryItemForEditing(PracticeHistory historyItem)
        {
            _isEditingExistingItem = true;
            _selectedHistoryItem = historyItem; // <-- VOEG DEZE REGEL TOE

            _totalElapsedTime = historyItem.Duration;
            _completedRepetitions = historyItem.Repetitions;
            _attemptsThisSession = historyItem.AttemptsTillSuccess;
            _repetitionStreakAttempts = historyItem.RepetitionStreakAttempts;
            _totalFailures = historyItem.TotalFailures; // v4.0
            _selectedDifficulty = historyItem.Difficulty;
            TxtTargetTempo.Text = historyItem.TargetTempo > 0 ? historyItem.TargetTempo.ToString() : "";
            TxtAchievedTempo.Text = historyItem.AchievedTempo > 0 ? historyItem.AchievedTempo.ToString() : "";
            TxtNotes.Text = historyItem.Notes;

            // Sight reading feature removed

            // v4.0: Update success ratio display with loaded data
            UpdateSuccessRatioDisplay();

            foreach (RadioButton rb in new[] { RbDifficult, RbAverage, RbEasy, RbMastered })
            {
                if (rb.Content.ToString() == _selectedDifficulty)
                {
                    rb.IsChecked = true;
                    break;
                }
            }
        }

        private void InitializeAlarmSound()
        {
            try
            {
                string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sound", "warning.mp3");
                if (File.Exists(soundPath))
                {
                    _mediaPlayer = new MediaPlayer();
                    _mediaPlayer.Open(new Uri(soundPath, UriKind.Absolute));
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"Error initializing alarm sound: {ex.Message}", ex);
            }
        }

        private void SessionTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.Dispatcher.InvokeAsync(UpdateTimerDisplay);
        }

        private void PomodoroTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                TimeSpan remaining = TimeSpan.FromMinutes(_pomodoroDuration) - _pomodoroStopwatch.Elapsed;
                if (remaining.TotalSeconds <= 0)
                {
                    remaining = TimeSpan.Zero;
                    BtnStopMicroBreak_Click(null, null);
                    PlayAlarmSound();
                    MessageBox.Show($"Your {_pomodoroDuration}-minute practice block is over. Time for a short break!", "Practice Block Complete!", MessageBoxButton.OK, MessageBoxImage.Information);
                    // FIXED: Stop alarm sound after user clicks OK
                    StopAlarmSound();
                }
                TxtMicroBreakTimer.Text = remaining.ToString(@"mm\:ss");
            });
        }

        private void BtnStartTimer_Click(object sender, RoutedEventArgs e)
        {
            AddActivityTimestamp();
            if (!_isTimerRunning)
            {
                _sessionStopwatch.Start();
                _sessionTimer.Start();
                _isTimerRunning = true;
                UpdateTimerButtonStates();
            }
        }

        private void BtnPauseTimer_Click(object sender, RoutedEventArgs e)
        {
            AddActivityTimestamp();
            if (_isTimerRunning)
            {
                _sessionStopwatch.Stop();
                _sessionTimer.Stop();
                _isTimerRunning = false;
                _totalElapsedTime += _sessionStopwatch.Elapsed;
                _sessionStopwatch.Reset();
                UpdateTimerButtonStates();
            }
        }

        private void BtnStopTimer_Click(object sender, RoutedEventArgs e)
        {
            AddActivityTimestamp();
            _sessionTimer.Stop();
            _sessionStopwatch.Stop();
            _totalElapsedTime = _sessionStopwatch.Elapsed;
            _sessionStopwatch.Reset();
            _isTimerRunning = false;
            UpdateTimerButtonStates();
            UpdateTimerDisplay();
        }

        private void UpdateTimerDisplay()
        {
            TimeSpan displayedTime = _totalElapsedTime + _sessionStopwatch.Elapsed;
            TxtTimer.Text = displayedTime.ToString(@"hh\:mm\:ss");
            if (!_isInitializing)
            {
                TxtHours.Text = displayedTime.Hours.ToString();
                TxtMinutes.Text = displayedTime.Minutes.ToString();
                TxtSeconds.Text = displayedTime.Seconds.ToString();
            }
        }

        private void UpdateTimerButtonStates()
        {
            BtnStartTimer.IsEnabled = !_isTimerRunning;
            BtnPauseTimer.IsEnabled = _isTimerRunning;
            BtnStopTimer.IsEnabled = _isTimerRunning || _totalElapsedTime > TimeSpan.Zero;
        }

        // --- TERUGGEVOEGDE METHODE ---
        private void UpdateElapsedTimeFromTextBoxes()
        {
            if (_isTimerRunning || _isInitializing) return;

            int.TryParse(TxtHours.Text, out int hours);
            int.TryParse(TxtMinutes.Text, out int minutes);
            int.TryParse(TxtSeconds.Text, out int seconds);
            _totalElapsedTime = new TimeSpan(Math.Max(0, hours), Math.Max(0, minutes), Math.Max(0, seconds));
            TxtTimer.Text = _totalElapsedTime.ToString(@"hh\:mm\:ss");
        }

        private void UpdateAllTextFields()
        {
            TxtRepetitions.Text = _completedRepetitions.ToString();
            TxtAttemptsTillSuccess.Text = _attemptsThisSession.ToString();
            TxtRepetitionStreakAttempts.Text = _repetitionStreakAttempts.ToString();
            UpdateTimerDisplay();
        }

        private void TxtPomodoroDuration_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing || !(sender is TextBox textBox)) return;

            if (int.TryParse(textBox.Text, out int duration) && duration > 0 && duration <= 120)
            {
                _pomodoroDuration = duration;

                if (!_isPomodoroRunning)
                {
                    TxtMicroBreakTimer.Text = $"{_pomodoroDuration:00}:00";
                }
            }
            else
            {
                // Ongeldig getal - herstel naar vorige waarde
                if (!string.IsNullOrEmpty(textBox.Text))
                {
                    textBox.Text = _pomodoroDuration.ToString();
                    textBox.SelectionStart = textBox.Text.Length; // Cursor naar einde
                }
            }
        }

        private void BtnStartMicroBreak_Click(object sender, RoutedEventArgs e)
        {
            if (!_isPomodoroRunning)
            {
                _pomodoroStopwatch.Restart();
                _pomodoroTimer.Start();
                _isPomodoroRunning = true;
                BtnStartMicroBreak.IsEnabled = false;
                BtnStopMicroBreak.IsEnabled = true;
            }
        }

        private void BtnStopMicroBreak_Click(object sender, RoutedEventArgs e)
        {
            _pomodoroTimer.Stop();
            _pomodoroStopwatch.Stop();
            _isPomodoroRunning = false;
            TxtMicroBreakTimer.Text = $"{_pomodoroDuration:00}:00";
            BtnStartMicroBreak.IsEnabled = true;
            BtnStopMicroBreak.IsEnabled = false;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Simple Mode: Direct save with evaluation dialog
            if (IsSimpleMode())
            {
                SaveSessionAndClose("TargetReached"); // Simple mode assumes success
                return;
            }

            // --- STAP 1: CONTROLEER OF OPSLAAN IS TOEGESTAAN (COOLDOWN CHECK) ---
            if (!_isSaveAllowed)
            {
                return;
            }

            // --- STAP 2: PAUZEER DE TIMER EN VOER INACTIVITEITSCHECK UIT OP DE GEMETEN TIJD ---
            if (_isTimerRunning)
            {
                BtnPauseTimer_Click(null, null);
            }

            // Check for inactivity BEFORE processing manual textbox overrides.
            const int inactivityThresholdMinutes = 3;
            TimeSpan timeSinceLastActivity = _activityTimestamps.Any() ? DateTime.Now - _activityTimestamps.Last() : TimeSpan.Zero;

            if (!_isEditingExistingItem && _totalElapsedTime.TotalMinutes > (inactivityThresholdMinutes + 1) && timeSinceLastActivity.TotalMinutes > inactivityThresholdMinutes)
            {
                var confirmDialog = new ConfirmDurationDialog(timeSinceLastActivity) { Owner = this };
                confirmDialog.ShowDialog();

                if (!confirmDialog.KeepFullDuration)
                {
                    _totalElapsedTime -= timeSinceLastActivity;
                    // The timer display will be updated later by UpdateElapsedTimeFromTextBoxes or UpdateTimerDisplay.
                }
            }

            // --- STAP 3: VERWERK HANDMATIGE AANPASSINGEN (DEZE OVERSCHRIJVEN NU DE INACTIVITEITSCHECK) ---
            UpdateElapsedTimeFromTextBoxes();

            // --- STAP 4: VOER DEFINITIEVE VALIDATIE UIT ---
            if (_totalElapsedTime.TotalSeconds == 0 && !_isEditingExistingItem)
            {
                MessageBox.Show("Please record some practice time or enter a duration manually before saving.", "No Time Recorded", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // User can try again immediately, no cooldown started.
            }

            // --- STAP 5: ACTIVEER DE COOLDOWN EN START DE OPSLAGPROCEDURE ---
            _isSaveAllowed = false;
            _saveCooldownTimer.Start();

            bool targetReached = _completedRepetitions >= _targetRepetitions;

            if (_isEditingExistingItem)
            {
                SaveSessionAndClose("TargetReached");
                return;
            }

            // [V2.2.1 CHANGE] Active Coaching is verwijderd voor een soepelere ervaring.
            // De sessie wordt nu altijd direct opgeslagen zonder onderbreking.
            SaveSessionAndClose(targetReached ? "TargetReached" : "TargetNotReached");
        }

        private void HandleActiveCoachingFlow(bool targetReached)
        {
            if (targetReached)
            {
                MessageBox.Show($"Excellent work! You reached your goal of {_targetRepetitions} correct repetitions. This is a very effective way to consolidate your learning.", "Goal Reached!", MessageBoxButton.OK, MessageBoxImage.Information);
                SaveSessionAndClose("TargetReached");
            }
            else
            {
                // --- NIEUWE, DYNAMISCHE LOGICA ---
                // Drempel 1: Initiele moeilijkheid. 5 pogingen is een duidelijke worsteling.
                const int attemptsThreshold = 5;

                // Drempel 2: Consistentie. Bereken de drempel voor streak resets als de helft van het doel.
                // Math.Ceiling rondt naar boven af (bv. 5 / 2.0 = 2.5 -> 3).
                int streakResetThreshold = (int)Math.Ceiling(_targetRepetitions / 2.0);
                // Zorg voor een minimum van 2 om te voorkomen dat de check te gevoelig is bij lage doelen.
                streakResetThreshold = Math.Max(2, streakResetThreshold);

                // De uiteindelijke voorwaarde vervangt de oude 'wasStruggling' regel.
                bool wasStruggling = _attemptsThisSession >= attemptsThreshold || _repetitionStreakAttempts >= streakResetThreshold;

                // Log de berekende drempels voor debugging.
                MLLogManager.Instance.Log($"Struggle Check: Attempts={_attemptsThisSession} (Threshold={attemptsThreshold}), StreakResets={_repetitionStreakAttempts} (Threshold={streakResetThreshold}). Result: {wasStruggling}", LogLevel.Debug);
                // --- EINDE NIEUWE LOGICA ---

                if (wasStruggling)
                {
                    string message = $"Good work! You've already completed {_completedRepetitions} repetition(s). To truly learn this passage, aiming for {_targetRepetitions} is ideal. You're almost there!";
                    var dialog = new PracticeOutcomeDialog(message) { Owner = this };
                    dialog.ShowDialog();

                    if (dialog.SelectedOutcome != "Continue")
                    {
                        SaveSessionAndClose(dialog.SelectedOutcome);
                    }
                }
                else
                {
                    SaveSessionAndClose("TargetNotReached");
                }
            }
        }

        private void BtnEndWithFrustration_Click(object sender, RoutedEventArgs e)
        {
            // Simple Mode: Direct save with frustration outcome
            if (IsSimpleMode())
            {
                SaveSessionAndClose("ManualFrustration");
                return;
            }

            // Stap 1: Controleer of opslaan is toegestaan (cooldown)
            if (!_isSaveAllowed)
            {
                return;
            }

            // Stap 2: Vraag de gebruiker om een expliciete bevestiging
            MessageBoxResult result = MessageBox.Show(
                "Are you sure you want to end this session and mark it as 'Frustration'?\n\nThis will provide feedback to the AI planner, which may suggest a short break for this section.",
                "Confirm Frustration",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return; // Gebruiker annuleert de actie
            }

            // Stap 3: Voer de nodige voorbereidende acties uit
            if (_isTimerRunning)
            {
                BtnPauseTimer_Click(null, null);
            }
            UpdateElapsedTimeFromTextBoxes();

            // Een minimale tijd is nog steeds vereist om corrupte of nutteloze data te voorkomen
            if (_totalElapsedTime.TotalSeconds < 1 && !_isEditingExistingItem)
            {
                MessageBox.Show("Please record at least one second of practice time before ending the session.", "No Time Recorded", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Stap 4: Activeer de cooldown en roep de centrale opslagmethode aan met de "ManualFrustration" outcome
            _isSaveAllowed = false;
            _saveCooldownTimer.Start();

            SaveSessionAndClose("ManualFrustration"); // AANGEPAST
        }

        // AANPASSING: Geconsolideerde opslaglogica om redundantie te elimineren en dataconsistentie te garanderen.
        /// <summary>
        /// Registration path: may record multiple same-day sessions (0.0 days delta).
        /// Planner due date remains unchanged for same-day extras; only metrics/difficulty may be refined.
        /// </summary>
        private void SaveSessionAndClose(String resultReason)
        {
            try
            {
                // Simple Mode: Show evaluation dialog and populate data based on results
                if (IsSimpleMode() && !_isEditingExistingItem)
                {
                    SaveSessionSimpleMode(resultReason);
                    return;
                }
                // Prevent duplicate saving
                if (_sessionAlreadySaved)
                {
                    MLLogManager.Instance.Log("SaveSessionAndClose: Session already saved, updating existing session with current UI values", LogLevel.Warning);

                    // Get the most recent session for this bar section from today
                    var lastSession = PracticeHistoryManager.Instance.GetHistoryForBarSection(_barSection.Id)
                        .Where(h => h.Date.Date == DateTime.Today)
                        .OrderByDescending(h => h.Date)
                        .FirstOrDefault();

                    if (lastSession != null)
                    {
                        // Update the existing session with current UI values instead of creating new one
                        lastSession.Duration = _totalElapsedTime;
                        lastSession.Difficulty = _selectedDifficulty;
                        lastSession.AttemptsTillSuccess = _attemptsThisSession;
                        lastSession.Repetitions = _completedRepetitions;
                        lastSession.RepetitionStreakAttempts = _repetitionStreakAttempts;
                        lastSession.TotalFailures = _totalFailures; // v4.0
                        lastSession.TargetTempo = int.TryParse(TxtTargetTempo.Text, out var parsedTargetTempo) ? parsedTargetTempo : 0;
                        lastSession.AchievedTempo = int.TryParse(TxtAchievedTempo.Text, out var parsedAchievedTempo) ? parsedAchievedTempo : 0;
                        lastSession.Notes = TxtNotes.Text ?? string.Empty;
                        lastSession.PreparatoryPhaseDuration = _preparatoryPhaseDuration;

                        // Update the existing session in the database
                        PracticeHistoryManager.Instance.UpdatePracticeHistory(lastSession);
                        MLLogManager.Instance.Log($"Updated existing session with new values: Difficulty={_selectedDifficulty}, Duration={_totalElapsedTime}", LogLevel.Info);

                        // Show report with updated values if enabled
                        if (SettingsManager.Instance.CurrentSettings.ShowSessionReport)
                        {
                            float performanceScore = PracticeUtils.CalculatePerformanceRating(lastSession);
                            float plannerPerformanceScore = PracticeUtils.CalculatePerformanceRating(lastSession, ignoreTempoForPlanner: true);
                            DateTime nextDueDate = _barSection.NextDueDate ?? DateTime.Today.AddDays(1);
                            int numberOfBars = PracticeUtils.ParseBarCount(lastSession.BarSectionRange);
                            float barsPerMinute = (lastSession.Duration.TotalMinutes > 0) ? (float)((lastSession.Repetitions * numberOfBars) / lastSession.Duration.TotalMinutes) : 0;

                            // Calculate algorithm interval for override functionality
                            double algorithmInterval = (nextDueDate.Date - DateTime.Today).TotalDays;

                            // Bereken success ratio voor rapport
                            var (currentSessionRatio, rollingRatio, ratioLabel, learningZone) = CalculateCurrentSuccessRatio();

                            _sessionReportWindow = new SessionReportWindow(this, lastSession, performanceScore, nextDueDate, barsPerMinute, plannerPerformanceScore, algorithmInterval, _barSection, _musicPiece, currentSessionRatio, rollingRatio, learningZone) { Owner = this };
                            bool? result = _sessionReportWindow.ShowDialog();

                            if (result == false)
                            {
                                return; // User clicked Cancel in report, don't close practice window
                            }

                            // NEW: Check if user provided an override and apply it
                            if (result == true && _sessionReportWindow.OverrideData != null && _sessionReportWindow.OverrideInterval.HasValue)
                            {
                                double userOverrideInterval = _sessionReportWindow.OverrideInterval.Value;
                                string overrideReason = _sessionReportWindow.OverrideReason;

                                MLLogManager.Instance?.Log($"🔧 Applying user override: {algorithmInterval:F1}d → {userOverrideInterval:F1}d (Reason: '{overrideReason}')", LogLevel.Info);

                                // Calculate new due date based on user override
                                DateTime newDueDate = DateTime.Today.AddDays(userOverrideInterval);
                                _barSection.NextDueDate = newDueDate.Date;

                                MLLogManager.Instance?.Log($"✅ Override applied! New due date: {newDueDate:yyyy-MM-dd}", LogLevel.Info);

                                // Update the override data in the practice history for ML learning
                                if (lastSession != null)
                                {
                                    lastSession.UserOverrideInterval = userOverrideInterval;
                                    lastSession.OriginalAlgorithmInterval = algorithmInterval;
                                    lastSession.OverrideReason = overrideReason;

                                    MLLogManager.Instance?.Log($"📊 Override data stored in practice history for ML learning", LogLevel.Info);
                                }
                            }
                        }
                    }

                    this.DialogResult = true;
                    this.Close();
                    return;
                }
                if (_isExtraPractice && _preserveDueDate && _isSubsequentSession)
                {
                    // DELETED: This was replaced by the try/finally block with SetExtraPracticePreserveContext
                }

                // --- Stap 1: Dataverzameling en validatie ---
                int.TryParse(TxtTargetTempo.Text, out int targetTempo);
                int.TryParse(TxtAchievedTempo.Text, out int achievedTempo);

                // --- FIX 5: Input-safety clamps ---
                int originalTargetTempo = targetTempo;
                targetTempo = Math.Max(0, Math.Min(targetTempo, 500)); // Clamp [0, 500] BPM
                if (originalTargetTempo != targetTempo)
                {
                    MLLogManager.Instance.Log($"Target tempo was outside safe range ({originalTargetTempo} BPM). Clamped to {targetTempo} BPM.", LogLevel.Warning);
                }

                int originalAchievedTempo = achievedTempo;
                achievedTempo = Math.Max(0, Math.Min(achievedTempo, 500)); // Clamp [0, 500] BPM
                if (originalAchievedTempo != achievedTempo)
                {
                    MLLogManager.Instance.Log($"Achieved tempo was outside safe range ({originalAchievedTempo} BPM). Clamped to {achievedTempo} BPM.", LogLevel.Warning);
                }

                int originalRepetitions = _completedRepetitions;
                _completedRepetitions = Math.Max(0, Math.Min(_completedRepetitions, 10000)); // Clamp [0, 10000]
                if (originalRepetitions != _completedRepetitions)
                {
                    MLLogManager.Instance.Log($"Completed repetitions were outside safe range ({originalRepetitions}). Clamped to {_completedRepetitions}.", LogLevel.Warning);
                }

                TimeSpan originalDuration = _totalElapsedTime;
                if (_totalElapsedTime.TotalHours > 10)
                {
                    _totalElapsedTime = TimeSpan.FromHours(10); // Clamp to 10 hours max
                    MLLogManager.Instance.Log($"Practice duration was excessive ({originalDuration}). Clamped to {_totalElapsedTime}.", LogLevel.Warning);
                }
                if (_totalElapsedTime.TotalSeconds < 0)
                {
                    _totalElapsedTime = TimeSpan.Zero; // Clamp to 0 if negative
                    MLLogManager.Instance.Log($"Practice duration was negative ({originalDuration}). Clamped to zero.", LogLevel.Warning);
                }
                // --- EINDE FIX 5 ---

                // Note: We keep all time in Duration for simplicity, regardless of repetition count
                // PreparatoryPhaseDuration wordt nu gebruikt in performance score berekening voor leerproces-evaluatie

                // --- Stap 2: Pad selecteren (Bewerken vs. Nieuw) ---
                if (_isEditingExistingItem && _selectedHistoryItem != null)
                {
                    // --- PAD A: BIJWERKEN BESTAANDE SESSIE ---
                    _selectedHistoryItem.Duration = _totalElapsedTime;
                    _selectedHistoryItem.Repetitions = _completedRepetitions;
                    _selectedHistoryItem.Difficulty = _selectedDifficulty;
                    _selectedHistoryItem.Notes = TxtNotes.Text;
                    _selectedHistoryItem.AttemptsTillSuccess = _attemptsThisSession;
                    _selectedHistoryItem.RepetitionStreakAttempts = _repetitionStreakAttempts;
                    _selectedHistoryItem.TotalFailures = _totalFailures; // v4.0
                    _selectedHistoryItem.TargetTempo = targetTempo;
                    _selectedHistoryItem.AchievedTempo = achievedTempo;
                    _selectedHistoryItem.TargetRepetitions = _targetRepetitions;

                    ApplyUiInputSafetyClamps(_selectedHistoryItem);
                    if (!IsValidPracticeHistory(_selectedHistoryItem))
                    {
                        _saveCooldownTimer.Stop();
                        _isSaveAllowed = true;
                        return;
                    }
                    PracticeHistoryManager.Instance.UpdatePracticeHistory(_selectedHistoryItem);
                    _sessionAlreadySaved = true; // Mark as saved for editing flow
                    MessageBox.Show("The practice history entry has been updated successfully.", "Update Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (!_isEditingExistingItem)
                {
                    // --- PAD B: OPSLAAN NIEUWE SESSIE ---
                    var sessionToSave = new PracticeHistory
                    {
                        MusicPieceId = _musicPiece.Id,
                        MusicPieceTitle = _musicPiece.Title,
                        BarSectionId = _barSection.Id,
                        BarSectionRange = _barSection.BarRange,
                        Date = DateTime.Now,
                        Duration = _totalElapsedTime,
                        Repetitions = _completedRepetitions,
                        Difficulty = _selectedDifficulty,
                        Notes = TxtNotes.Text,
                        AttemptsTillSuccess = _attemptsThisSession,
                        RepetitionStreakAttempts = _repetitionStreakAttempts,
                        TotalFailures = _totalFailures, // v4.0
                        SessionOutcome = resultReason,
                        TargetTempo = targetTempo,
                        AchievedTempo = achievedTempo,
                        PreparatoryPhaseDuration = _preparatoryPhaseDuration,
                        TargetRepetitions = _targetRepetitions
                    };

                    // Backup current state BEFORE saving (for potential rollback)
                    _previousNextDueDate = _barSection.NextDueDate;
                    _previousInterval = _barSection.Interval;
                    _previousCompletedRepetitions = _barSection.CompletedRepetitions;
                    _previousDifficulty = _barSection.Difficulty ?? "Average";
                    _savedSession = sessionToSave;

                    ApplyUiInputSafetyClamps(sessionToSave);
                    if (!IsValidPracticeHistory(sessionToSave))
                    {
                        _saveCooldownTimer.Stop();
                        _isSaveAllowed = true;
                        return;
                    }

                    // Prestatieberekening
                    float performanceScore = PracticeUtils.CalculatePerformanceRating(sessionToSave);
                    float plannerPerformanceScore = PracticeUtils.CalculatePerformanceRating(sessionToSave, ignoreTempoForPlanner: true);
                    sessionToSave.PerformanceScore = performanceScore;

                    // Clamp performance to [0..10] and log if adjusted (align with core rails)
                    float psOrig = sessionToSave.PerformanceScore;
                    float psSafe = Math.Clamp(psOrig, 0f, 10f);
                    if (psSafe != psOrig)
                    {
                        sessionToSave.PerformanceScore = psSafe;
                        MLLogManager.Instance.Log($"InputClamp perf={psOrig:F2}→{psSafe:F2}", LogLevel.Info);
                    }

                    PracticeHistoryManager.Instance.AddPracticeHistory(sessionToSave);
                    _sessionAlreadySaved = true; // Mark as saved immediately after save to prevent duplicates

                    // Refresh calculated properties like AverageSuccessRatio to update MainWindow grid
                    _barSection.RefreshCalculatedProperties();

                    DateTime nextDueDate = _barSection.NextDueDate ?? DateTime.Today.AddDays(1);

                    // Since sight reading feature was removed, all sessions are treated as normal practice sessions
                    {
                        var historyBeforeAdd = PracticeHistoryManager.Instance.GetHistoryForBarSection(_barSection.Id).ToList();
                        var historyAfterAdd = new List<PracticeHistory>(historyBeforeAdd) { sessionToSave };
                        int practicesAlreadyDoneToday = historyBeforeAdd.Count(h => h.Date.Date == DateTime.Today);
                        bool needsRecalculation = practicesAlreadyDoneToday == 0 || (_barSection.NextDueDate.HasValue && _barSection.NextDueDate.Value.Date <= DateTime.Today);

                        double tauValue;

                        if (_isExtraPractice && _preserveDueDate && _isSubsequentSession)
                        {
                            // SCOPED: Set preserve context strictly for ExtraPractice same-day scenarios
                            try
                            {
                                ScheduledPracticeSessionManager.SetExtraPracticePreserveContext(true, _barSection.Id, "ExtraPractice same-day session");

                                MLLogManager.Instance.Log($"Subsequent session for '{_musicPiece.Title} - {_barSection.BarRange}'. Refining difficulty and preserving future due date.", LogLevel.Info);
                                RefineDifficulty(_barSection, plannerPerformanceScore);
                                var futureSession = ScheduledPracticeSessionManager.Instance.GetScheduledSessionForBarSection(_barSection.Id);
                                nextDueDate = futureSession?.ScheduledDate ?? _barSection.NextDueDate ?? DateTime.Today.AddDays(1);
                                tauValue = futureSession?.TauValue ?? 0.0;

                                // NEW: Per-item τ update integratie voor Subsequent ExtraPractice sessions
                                MLLogManager.Instance.Log($"[DEBUG] Starting per-item τ update for EXTRA PRACTICE section={_barSection.Id}, perfScore={plannerPerformanceScore:F2}", LogLevel.Info);
                                try
                                {
                                    // Definieer een itemId (kan later verfijnd worden; nu section.Id voldoende)
                                    string itemId = _barSection.Id.ToString();

                                    // Bepaal intervalDays: tijd tot volgende scheduled datum
                                    double intervalDays = Math.Max(1.0, (nextDueDate - DateTime.Today).TotalDays);

                                    // Evalueer of de huidige performance een 'correct' geheugen-event impliceert.
                                    // Gebruik SessionOutcome als echte recall metric (TargetReached = success)
                                    bool correct = (resultReason?.ToLower() == "targetreached");

                                    // Doelretentie vanuit difficulty
                                    double targetRetention = EbbinghausConstants.GetRetentionTargetForDifficulty(_barSection.Difficulty);

                                    // Initialisatie τ-prior indien item nieuw: gebruik tauValue als startpunt
                                    Func<double> initTauFactory = () => Math.Max(1.0, tauValue);

                                    MLLogManager.Instance.Log($"[DEBUG] ExtraPractice Pre-update: itemId={itemId}, interval={intervalDays:F2}d, correct={correct}, targetR={targetRetention:F3}, initTau={tauValue:F3}", LogLevel.Info);

                                    var updated = ItemMemoryModel.Update(
                                        itemId: itemId,
                                        intervalDays: intervalDays,
                                        correct: correct,
                                        targetRetention: targetRetention,
                                        initTauFactory: initTauFactory);

                                    MLLogManager.Instance.Log($"[DEBUG] ExtraPractice Post-update: τ_old={tauValue:F3} τ_new={updated.TauDays:F3} diff={Math.Abs(updated.TauDays - tauValue):F6}", LogLevel.Info);

                                    // ALTIJD loggen
                                    MLLogManager.Instance.Log(
                                        $"[PerItemTau] EXTRA_PRACTICE section={_barSection.Id} τ_session={tauValue:F3} → τ_item={updated.TauDays:F3} nextPlanned≈{updated.LastPlannedIntervalDays:F2}d (correct={correct})",
                                        LogLevel.Info);
                                }
                                catch (Exception ex)
                                {
                                    MLLogManager.Instance.LogError($"Per-item tau update failed for EXTRA_PRACTICE section {_barSection.Id}", ex);
                                }

                                if (!_calibrationApplied)
                                {
                                    PersonalizedMemoryCalibration.Instance.UpdateCalibrationFromSession(sessionToSave, _barSection);
                                    _calibrationApplied = true;
                                }
                            }
                            finally
                            {
                                // ALWAYS reset the preserve context in finally block
                                ScheduledPracticeSessionManager.SetExtraPracticePreserveContext(false, _barSection.Id, "ExtraPractice session complete");
                            }
                        }
                        else if (needsRecalculation)
                        {
                            MLLogManager.Instance.Log($"Recalculation needed for '{_musicPiece.Title} - {_barSection.BarRange}'. Reason: First practice of day or due date is not in the future.", LogLevel.Info);
                            var scheduleResult = new SpacedRepetitionAlgorithm().CalculateNextPracticeDate(
                                _musicPiece,
                                _barSection,
                                historyAfterAdd,
                                plannerPerformanceScore,
                                DateTime.Today,
                                sessionToSave.Repetitions);
                            nextDueDate = scheduleResult.NextDate;
                            tauValue = EbbinghausConstants.ClampTauToSafeBounds(scheduleResult.Tau);
                            if (!_calibrationApplied)
                            {
                                PersonalizedMemoryCalibration.Instance.UpdateCalibrationFromSession(sessionToSave, _barSection);
                                _calibrationApplied = true;
                            }
                        }
                        else
                        {
                            MLLogManager.Instance.Log($"Subsequent session for '{_musicPiece.Title} - {_barSection.BarRange}'. Refining difficulty and preserving future due date.", LogLevel.Info);
                            RefineDifficulty(_barSection, plannerPerformanceScore);
                            var futureSession = ScheduledPracticeSessionManager.Instance.GetScheduledSessionForBarSection(_barSection.Id);
                            nextDueDate = futureSession?.ScheduledDate ?? _barSection.NextDueDate ?? DateTime.Today.AddDays(1);
                            tauValue = futureSession?.TauValue ?? 0.0;

                            // NEW: Per-item τ update integratie voor Subsequent sessions
                            MLLogManager.Instance.Log($"[DEBUG] Starting per-item τ update for SUBSEQUENT section={_barSection.Id}, perfScore={plannerPerformanceScore:F2}", LogLevel.Info);
                            try
                            {
                                // Definieer een itemId (kan later verfijnd worden; nu section.Id voldoende)
                                string itemId = _barSection.Id.ToString();

                                // Bepaal intervalDays: tijd tot volgende scheduled datum
                                double intervalDays = Math.Max(1.0, (nextDueDate - DateTime.Today).TotalDays);

                                // Evalueer of de huidige performance een 'correct' geheugen-event impliceert.
                                // Gebruik SessionOutcome als echte recall metric (TargetReached = success)
                                bool correct = (resultReason?.ToLower() == "targetreached");

                                // Doelretentie vanuit difficulty
                                double targetRetention = EbbinghausConstants.GetRetentionTargetForDifficulty(_barSection.Difficulty);

                                // Initialisatie τ-prior indien item nieuw: gebruik tauValue als startpunt
                                Func<double> initTauFactory = () => Math.Max(1.0, tauValue);

                                MLLogManager.Instance.Log($"[DEBUG] Subsequent Pre-update: itemId={itemId}, interval={intervalDays:F2}d, correct={correct}, targetR={targetRetention:F3}, initTau={tauValue:F3}", LogLevel.Info);

                                var updated = ItemMemoryModel.Update(
                                    itemId: itemId,
                                    intervalDays: intervalDays,
                                    correct: correct,
                                    targetRetention: targetRetention,
                                    initTauFactory: initTauFactory);

                                MLLogManager.Instance.Log($"[DEBUG] Subsequent Post-update: τ_old={tauValue:F3} τ_new={updated.TauDays:F3} diff={Math.Abs(updated.TauDays - tauValue):F6}", LogLevel.Info);

                                // ALTIJD loggen
                                MLLogManager.Instance.Log(
                                    $"[PerItemTau] SUBSEQUENT section={_barSection.Id} τ_session={tauValue:F3} → τ_item={updated.TauDays:F3} nextPlanned≈{updated.LastPlannedIntervalDays:F2}d (correct={correct})",
                                    LogLevel.Info);
                            }
                            catch (Exception ex)
                            {
                                MLLogManager.Instance.LogError($"Per-item tau update failed for SUBSEQUENT section {_barSection.Id}", ex);
                            }

                            if (!_calibrationApplied)
                            {
                                PersonalizedMemoryCalibration.Instance.UpdateCalibrationFromSession(sessionToSave, _barSection);
                                _calibrationApplied = true;
                            }
                        }

                        if (!_isExtraPractice && _selectedDifficulty.ToLower() == "mastered")
                        {
                            var (recalculatedNextDate, recalculatedTau) = new SpacedRepetitionAlgorithm().CalculateNextPracticeDate(
                                _musicPiece, _barSection, historyAfterAdd, plannerPerformanceScore, DateTime.Today, sessionToSave.Repetitions);
                            if (nextDueDate.Date != recalculatedNextDate.Date)
                            {
                                MLLogManager.Instance.Log($"Difficulty set to Mastered. Adjusting NextDueDate for '{_musicPiece.Title} - {_barSection.BarRange}' from {nextDueDate:yyyy-MM-dd} to {recalculatedNextDate:yyyy-MM-dd}", LogLevel.Info);
                                nextDueDate = recalculatedNextDate;
                                tauValue = EbbinghausConstants.ClampTauToSafeBounds(recalculatedTau);
                            }
                        }

                        if (!(_isExtraPractice && _preserveDueDate && _isSubsequentSession))
                        {
                            _barSection.NextDueDate = nextDueDate.Date;
                            int actualDays = (int)Math.Round((nextDueDate.Date - DateTime.Today).TotalDays);
                            _barSection.Interval = Math.Max(1, actualDays);
                        }

                        if (!(_isExtraPractice && _preserveDueDate && _isSubsequentSession))
                        {
                            ScheduledPracticeSessionManager.Instance.CompleteTodaysSessionFor(_barSection.Id);
                            var newScheduledSession = new ScheduledPracticeSession
                            {
                                Id = Guid.NewGuid(),
                                MusicPieceId = _musicPiece.Id,
                                MusicPieceTitle = _musicPiece.Title,
                                BarSectionId = _barSection.Id,
                                BarSectionRange = _barSection.BarRange,
                                ScheduledDate = nextDueDate.Date,
                                Difficulty = _barSection.Difficulty,
                                Status = "Planned",
                                EstimatedDuration = TimeSpan.FromMinutes(PracticeUtils.GetEstimatedDurationForSection(_barSection.Id)),
                                TauValue = tauValue
                            };
                            _createdScheduledSessionId = newScheduledSession.Id; // Backup voor rollback
                            ScheduledPracticeSessionManager.Instance.AddScheduledSession(newScheduledSession);
                            MLLogManager.Instance.Log($"Created new scheduled session for '{_barSection.BarRange}' on {nextDueDate.Date:yyyy-MM-dd} with Tau {tauValue:F1}.", LogLevel.Info);
                        }
                        else
                        {
                            // Preserve flow: geen completion markering / geen nieuwe schedule entry
                        }
                    }

                    // Toon rapport (optioneel)
                    if (SettingsManager.Instance.CurrentSettings.ShowSessionReport)
                    {
                        int numberOfBars = PracticeUtils.ParseBarCount(sessionToSave.BarSectionRange);
                        float barsPerMinute = (sessionToSave.Duration.TotalMinutes > 0) ? (float)((sessionToSave.Repetitions * numberOfBars) / sessionToSave.Duration.TotalMinutes) : 0;

                        // Calculate algorithm interval for override functionality
                        double algorithmInterval = (nextDueDate.Date - DateTime.Today).TotalDays;

                        // Bereken success ratio voor rapport
                        var (currentSessionRatio, rollingRatio, ratioLabel, learningZone) = CalculateCurrentSuccessRatio();

                        _sessionReportWindow = new SessionReportWindow(this, sessionToSave, performanceScore, nextDueDate, barsPerMinute, plannerPerformanceScore, algorithmInterval, _barSection, _musicPiece, currentSessionRatio, rollingRatio, learningZone) { Owner = this };
                        bool? result = _sessionReportWindow.ShowDialog();

                        // If the user clicked Cancel in the report window, ROLLBACK all changes
                        if (result == false)
                        {
                            RollbackSessionSave();
                            return; // Return early, don't close the practice session window
                        }

                        // NEW: Check if user provided an override and apply it
                        if (result == true && _sessionReportWindow.OverrideData != null && _sessionReportWindow.OverrideInterval.HasValue)
                        {
                            double userOverrideInterval = _sessionReportWindow.OverrideInterval.Value;
                            string overrideReason = _sessionReportWindow.OverrideReason;

                            MLLogManager.Instance?.Log($"🔧 Applying user override: {algorithmInterval:F1}d → {userOverrideInterval:F1}d (Reason: '{overrideReason}')", LogLevel.Info);

                            // Calculate new due date based on user override
                            DateTime newDueDate = DateTime.Today.AddDays(userOverrideInterval);
                            _barSection.NextDueDate = newDueDate.Date;
                            nextDueDate = newDueDate; // Update local variable too

                            MLLogManager.Instance?.Log($"✅ Override applied! New due date: {newDueDate:yyyy-MM-dd}", LogLevel.Info);

                            // Update the override data in the practice history for ML learning
                            if (sessionToSave != null)
                            {
                                sessionToSave.UserOverrideInterval = userOverrideInterval;
                                sessionToSave.OriginalAlgorithmInterval = algorithmInterval;
                                sessionToSave.OverrideReason = overrideReason;

                                MLLogManager.Instance?.Log($"📊 Override data stored in practice history for ML learning", LogLevel.Info);
                            }
                        }
                    }
                }

                // --- Stap 3: Geconsolideerde update van BarSection en opslag ---
                _barSection.LastPracticeDate = DateTime.Today;
                _barSection.Difficulty = _selectedDifficulty;
                _barSection.TargetRepetitions = _targetRepetitions;
                _barSection.AttemptsTillSuccess = _attemptsThisSession;
                _barSection.TargetTempo = targetTempo > 0 ? targetTempo : _barSection.TargetTempo;

                _barSection.CompletedRepetitions = PracticeHistoryManager.Instance
                    .GetHistoryForBarSection(_barSection.Id)
                    .Where(h => !h.IsDeleted)
                    .Sum(h => h.Repetitions);

                _musicPiece.UpdateProgress();
                if (Application.Current.MainWindow is MainWindow main)
                {
                    // Sync lifecycle state terug naar canonieke sectie vóór opslaan
                    var desiredState = _barSection.LifecycleState;
                    var canonical = _musicPiece.BarSections?.FirstOrDefault(bs => bs.Id == _barSection.Id);
                    if (canonical != null && canonical.LifecycleState != desiredState)
                    {
                        var old = canonical.LifecycleState;
                        canonical.LifecycleState = desiredState; // triggert SectionLifecycleService.Apply
                        MLLogManager.Instance?.Log($"[LifecycleSync] {old} → {desiredState} for '{canonical.BarRange}' (Id={canonical.Id})", LogLevel.Info);
                    }

                    main.SaveMusicPiece(_musicPiece);
                    main.SyncAllPiecesFromHistoryAndRefresh(); // Forceer UI refres
                }

                // --- Opschonen en afsluiten ---
                try { this.DialogResult = true; } catch { /* non-modal safety */ }
                DataContext = null;
                Close();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error in SaveSessionAndClose", ex);
                MessageBox.Show("An error occurred while saving the session. Please try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Rolls back all changes made by SaveSessionAndClose when user clicks Cancel in report
        /// </summary>
        private void RollbackSessionSave()
        {
            try
            {
                MLLogManager.Instance?.Log("🔄 Rolling back session save (user clicked Cancel in report)", LogLevel.Info);

                // 1. Verwijder de PracticeHistory entry die we net hebben toegevoegd
                if (_savedSession != null)
                {
                    PracticeHistoryManager.Instance.DeletePracticeHistory(_savedSession.Id);
                    MLLogManager.Instance?.Log($"Removed practice history entry {_savedSession.Id}", LogLevel.Info);
                }

                // 2. Herstel BarSection properties
                _barSection.NextDueDate = _previousNextDueDate;
                _barSection.Interval = _previousInterval;
                _barSection.CompletedRepetitions = _previousCompletedRepetitions;
                _barSection.Difficulty = _previousDifficulty ?? "Average"; // Fallback to Average if null
                MLLogManager.Instance?.Log($"Restored BarSection: NextDueDate={_previousNextDueDate}, Interval={_previousInterval}", LogLevel.Info);

                // 3. Verwijder de nieuwe scheduled session die we hebben aangemaakt
                if (_createdScheduledSessionId.HasValue)
                {
                    ScheduledPracticeSessionManager.Instance.RemoveScheduledSession(_createdScheduledSessionId.Value);
                    MLLogManager.Instance?.Log($"Removed scheduled session {_createdScheduledSessionId.Value}", LogLevel.Info);
                }

                // 4. Sla de herstelde MusicPiece op
                if (Application.Current.MainWindow is MainWindow main)
                {
                    main.SaveMusicPiece(_musicPiece);
                    MLLogManager.Instance?.Log("Saved restored MusicPiece to disk", LogLevel.Info);
                }

                // 5. Reset de saved session flag
                _sessionAlreadySaved = false;

                // 6. Clear backup variables
                _savedSession = null;
                _previousNextDueDate = null;
                _previousInterval = 0;
                _previousCompletedRepetitions = 0;
                _previousDifficulty = null;
                _createdScheduledSessionId = null;

                MLLogManager.Instance?.Log("✅ Rollback complete - practice window remains open for corrections", LogLevel.Info);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError("Error during rollback", ex);
                MessageBox.Show("An error occurred while canceling. Some changes may not have been reverted.", "Rollback Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Helper: activity timestamp toevoegen
        private void AddActivityTimestamp()
        {
            _activityTimestamps.Add(DateTime.Now);
        }

        // Helper: alarm geluid
        private void PlayAlarmSound()
        {
            try
            {
                if (_mediaPlayer?.Source != null)
                {
                    _mediaPlayer.Position = TimeSpan.Zero;
                    _mediaPlayer.Play();
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("PlayAlarmSound failed", ex);
            }
        }

        private void StopAlarmSound()
        {
            try
            {
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Stop();
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Failed to stop alarm sound", ex);
            }
        }

        // Helper: laad history list
        private void LoadPracticeHistory()
        {
            try
            {
                var items = PracticeHistoryManager.Instance
                    .GetHistoryForBarSection(_barSection.Id)
                    .Where(h => !h.IsDeleted)
                    .ToList();
                LvPracticeHistory.ItemsSource = new ObservableCollection<PracticeHistory>(items);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("LoadPracticeHistory failed", ex);
            }
        }

        // UI safety rails
        private void ApplyUiInputSafetyClamps(PracticeHistory s)
        {
            var clamps = new List<string>();
            int origReps = s.Repetitions;
            s.Repetitions = Math.Clamp(s.Repetitions, 0, 200);
            if (s.Repetitions != origReps) clamps.Add($"reps={origReps}->{s.Repetitions}");

            double hours = s.Duration.TotalHours;
            if (hours < 0)
            {
                s.Duration = TimeSpan.Zero; clamps.Add("duration<0->0");
            }
            else if (hours > 8)
            {
                s.Duration = TimeSpan.FromHours(8); clamps.Add($"duration>{hours:F2}->8h");
            }
            if (clamps.Count > 0)
                MLLogManager.Instance.Log($"InputClamp {string.Join(" ", clamps)}", LogLevel.Info);
        }

        private bool IsValidPracticeHistory(PracticeHistory session)
        {
            if (session.Duration.TotalSeconds < 0)
            {
                MessageBox.Show("Invalid negative duration.", "Validation", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (session.Repetitions < 0)
            {
                MessageBox.Show("Invalid negative repetitions.", "Validation", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (session.Repetitions > 10000)
            {
                MessageBox.Show("Repetitions too large.", "Validation", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (session.AchievedTempo > 500 || session.TargetTempo > 500)
            {
                MessageBox.Show("Tempo exceeds safe maximum (500 BPM).", "Validation", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        // === BEGIN: RefineDifficulty (enige geldige implementatie) ===
        private void RefineDifficulty(BarSection section, float performanceScore)
        {
            string currentDifficulty = section.Difficulty;
            string newDifficulty = currentDifficulty;

            if (performanceScore < 4.0f)
            {
                switch (currentDifficulty)
                {
                    case "Easy": newDifficulty = "Average"; break;
                    case "Average": newDifficulty = "Difficult"; break;
                }
            }
            else if (performanceScore > 9.0f)
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
                _selectedDifficulty = newDifficulty;
                MLLogManager.Instance.Log($"Difficulty for '{section.BarRange}' refined from {currentDifficulty} to {newDifficulty} based on performance score of {performanceScore.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}.", LogLevel.Info);

                try { ScheduledPracticeSessionManager.Instance.UpdateDifficultyForUpcomingSessions(section.Id, newDifficulty); }
                catch (Exception ex) { MLLogManager.Instance.LogError("RefineDifficulty: propagation failed", ex); }

                try
                {
                    // Sync lifecycle state terug naar canonieke sectie vóór opslaan
                    var desiredState = section.LifecycleState;
                    var canonical = _musicPiece.BarSections?.FirstOrDefault(bs => bs.Id == section.Id);
                    if (canonical != null && canonical.LifecycleState != desiredState)
                    {
                        var old = canonical.LifecycleState;
                        canonical.LifecycleState = desiredState; // triggert SectionLifecycleService.Apply
                        MLLogManager.Instance?.Log($"[LifecycleSync] {old} → {desiredState} for '{canonical.BarRange}' (Id={canonical.Id})", LogLevel.Info);
                    }

                    if (Application.Current.MainWindow is MainWindow main) main.SaveMusicPiece(_musicPiece);
                    AppState.MusicDataChanged = true;
                    if (Application.Current.MainWindow is MainWindow mainWindow) mainWindow.SyncAllPiecesFromHistoryAndRefresh();
                    MLLogManager.Instance.Log($"Difficulty persisted and UI refreshed ({currentDifficulty} → {newDifficulty}).", LogLevel.Info);
                }
                catch (Exception ex)
                { MLLogManager.Instance.LogError($"Failed to persist difficulty change for '{section.BarRange}'", ex); }
            }
        }
        // === END: RefineDifficulty ===

        // === EVENT HANDLERS (hersteld) ===
        private void PracticeSessionWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Window loaded - no special window state handling needed
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("PracticeSessionWindow_Loaded failed", ex);
            }
        }

        private void PracticeSessionWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Stop all timers when the window is closing
                if (_sessionTimer != null)
                {
                    _sessionTimer.Stop();
                    _sessionTimer.Dispose();
                }

                if (_pomodoroTimer != null)
                {
                    _pomodoroTimer.Stop();
                    _pomodoroTimer.Dispose();
                }

                if (_saveCooldownTimer != null)
                {
                    _saveCooldownTimer.Stop();
                    _saveCooldownTimer.Dispose();
                }

                // Stop the stopwatches
                if (_sessionStopwatch?.IsRunning == true)
                    _sessionStopwatch.Stop();

                if (_pomodoroStopwatch?.IsRunning == true)
                    _pomodoroStopwatch.Stop();

                // FIXED: Stop alarm sound when window closes
                StopAlarmSound();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error during PracticeSessionWindow_Closing", ex);
            }
        }

        private void BtnDecreaseAttempts_Click(object sender, RoutedEventArgs e)
        {
            AddActivityTimestamp();
            if (_attemptsThisSession > 0)
            {
                _attemptsThisSession--;
                if (_totalFailures > 0) _totalFailures--; // Sync failures counter with attempts
            }
            if (TxtAttemptsTillSuccess != null) TxtAttemptsTillSuccess.Text = _attemptsThisSession.ToString();
            UpdateOverlearningRecommendations();
            UpdateSuccessRatioDisplay(); // Update failures display
        }
        private void BtnIncreaseAttempts_Click(object sender, RoutedEventArgs e)
        {
            AddActivityTimestamp();
            _attemptsThisSession++;
            _totalFailures++; // Sync failures counter with attempts
            if (TxtAttemptsTillSuccess != null) TxtAttemptsTillSuccess.Text = _attemptsThisSession.ToString();
            UpdateOverlearningRecommendations();
            UpdateSuccessRatioDisplay(); // Update failures display
        }
        private void BtnResetAttempts_Click(object sender, RoutedEventArgs e)
        {
            AddActivityTimestamp();
            _attemptsThisSession = 0;
            _totalFailures = 0; // Also reset failures counter
            if (TxtAttemptsTillSuccess != null) TxtAttemptsTillSuccess.Text = "0";
            UpdateOverlearningRecommendations();
            UpdateSuccessRatioDisplay(); // Update failures display
        }

        private void BtnDecreaseReps_Click(object sender, RoutedEventArgs e)
        {
            AddActivityTimestamp();
            if (_completedRepetitions > 0)
            {
                _completedRepetitions--;
                _totalFailures++; // Add failure when correcting a repetition count
            }
            if (TxtRepetitions != null) TxtRepetitions.Text = _completedRepetitions.ToString();
            UpdateSuccessRatioDisplay(); // v4.0
        }
        private void BtnIncreaseReps_Click(object sender, RoutedEventArgs e)
        {
            AddActivityTimestamp();
            // Preparatory phase capture (mirrors eerder logic) + WALL-CLOCK FALLBACK
            if (_completedRepetitions == 0 && !_preparatoryPhaseEnded && !_isEditingExistingItem)
            {
                var currentDuration = _isTimerRunning ? _totalElapsedTime + _sessionStopwatch.Elapsed : _totalElapsedTime;

                if (currentDuration.TotalSeconds <= 1)
                {
                    var wallClock = DateTime.UtcNow - _openedAtUtc;
                    if (wallClock.TotalSeconds > 5)
                    {
                        currentDuration = wallClock;
                        if (_totalElapsedTime.TotalSeconds < 1 && !_isTimerRunning)
                        {
                            _totalElapsedTime = wallClock;
                            UpdateTimerDisplay();
                        }
                        MLLogManager.Instance.Log($"PreparatoryPhase fallback gebruikt (stopwatch≈0). WallClock={wallClock}.", LogLevel.Info);
                    }
                }

                if (currentDuration.TotalSeconds > 1)
                {
                    _preparatoryPhaseDuration = currentDuration;
                    _preparatoryPhaseEnded = true;
                    MLLogManager.Instance.Log($"PreparatoryPhase geregistreerd: {_preparatoryPhaseDuration} (Ended={_preparatoryPhaseEnded}).", LogLevel.Debug);

                    // NIEUW: label updaten
                    try
                    {
                        if (TxtPreparatoryPhaseInfo != null)
                        {
                            TxtPreparatoryPhaseInfo.Text = $"Preparatory phase: {_preparatoryPhaseDuration:hh\\:mm\\:ss}";
                            TxtPreparatoryPhaseInfo.Visibility = Visibility.Visible;
                        }
                    }
                    catch (Exception ex)
                    {
                        MLLogManager.Instance.LogError("PreparatoryPhase label update failed", ex);
                    }
                }
            }
            _completedRepetitions++;
            if (TxtRepetitions != null) TxtRepetitions.Text = _completedRepetitions.ToString();
            UpdateSuccessRatioDisplay(); // v4.0
        }
        private void BtnResetRepetitions_Click(object sender, RoutedEventArgs e)
        {
            AddActivityTimestamp();
            _completedRepetitions = 0;
            if (TxtRepetitions != null) TxtRepetitions.Text = "0";
            _repetitionStreakAttempts++;
            _totalFailures++; // Also increment total failures when streak is reset
            if (TxtRepetitionStreakAttempts != null) TxtRepetitionStreakAttempts.Text = _repetitionStreakAttempts.ToString();
            UpdateSuccessRatioDisplay(); // v4.0 - This will also update the total failures display
        }

        private void BtnDecreaseStreak_Click(object sender, RoutedEventArgs e)
        {
            AddActivityTimestamp();
            if (_repetitionStreakAttempts > 0)
            {
                _repetitionStreakAttempts--;
                if (_totalFailures > 0) _totalFailures--; // Sync failures counter with streak decrements
            }
            if (TxtRepetitionStreakAttempts != null) TxtRepetitionStreakAttempts.Text = _repetitionStreakAttempts.ToString();
            UpdateSuccessRatioDisplay(); // Update failures display
        }
        private void BtnIncreaseStreak_Click(object sender, RoutedEventArgs e)
        {
            AddActivityTimestamp();
            _repetitionStreakAttempts++;
            _totalFailures++; // Sync failures counter with streak increments
            if (TxtRepetitionStreakAttempts != null) TxtRepetitionStreakAttempts.Text = _repetitionStreakAttempts.ToString();
            UpdateSuccessRatioDisplay(); // Update failures display
        }
        private void BtnResetStreak_Click(object sender, RoutedEventArgs e)
        {
            AddActivityTimestamp();
            _repetitionStreakAttempts = 0;
            _totalFailures = 0; // Also reset failures counter
            if (TxtRepetitionStreakAttempts != null) TxtRepetitionStreakAttempts.Text = "0";
            UpdateSuccessRatioDisplay(); // Update failures display
        }

        // v4.0: TotalFailures button handlers
        private void BtnDecreaseTotalFailures_Click(object sender, RoutedEventArgs e)
        {
            AddActivityTimestamp();
            if (_totalFailures > 0) _totalFailures--;
            UpdateSuccessRatioDisplay();
        }
        private void BtnIncreaseTotalFailures_Click(object sender, RoutedEventArgs e)
        {
            AddActivityTimestamp();
            _totalFailures++;
            UpdateSuccessRatioDisplay();
        }
        private void BtnResetTotalFailures_Click(object sender, RoutedEventArgs e)
        {
            AddActivityTimestamp();
            _totalFailures = 0;
            UpdateSuccessRatioDisplay();
        }

        // v4.0: Update SuccessRatio display in real-time using rolling 7-session average
        private void UpdateSuccessRatioDisplay()
        {
            if (TxtTotalFailures != null)
                TxtTotalFailures.Text = _totalFailures.ToString();

            // Haal alle history op voor deze bar section
            var allHistory = PracticeHistoryManager.Instance.GetHistoryForBarSection(_barSection.Id).ToList();

            // Bereken rolling average over laatste 7 sessies
            double rollingRatio = PracticeHistory.CalculateRollingSuccessRatio(_barSection.Id, allHistory, windowSize: 7);

            // Als er nog geen history is, gebruik current session data als preview
            int currentTotal = _completedRepetitions + _totalFailures;
            double currentSessionRatio = currentTotal > 0 ? (double)_completedRepetitions / currentTotal : 0.0;

            // Bepaal welke ratio we tonen
            double displayRatio = rollingRatio;
            string ratioLabel = "";

            if (allHistory.Count == 0 && currentTotal > 0)
            {
                // Nieuwe bar section, nog geen history - toon current session als preview
                displayRatio = currentSessionRatio;
                ratioLabel = " (preview)";
            }
            else if (allHistory.Count > 0 && allHistory.Count < 7)
            {
                // Minder dan 7 sessies - toon dat we limited data hebben
                ratioLabel = $" (avg {allHistory.Count})";
            }
            else if (allHistory.Count >= 7)
            {
                // Genoeg data - toon rolling average
                ratioLabel = " (7-session avg)";
            }

            if (displayRatio == 0.0 && currentTotal == 0)
            {
                if (TxtSuccessRatio != null) TxtSuccessRatio.Text = "—%";
                if (TxtLearningZone != null)
                {
                    TxtLearningZone.Text = "—";
                    TxtLearningZone.Foreground = Brushes.Gray;
                }
                return;
            }

            double ratio = displayRatio;
            if (TxtSuccessRatio != null)
            {
                TxtSuccessRatio.Text = $"{ratio * 100:F0}%{ratioLabel}";

                // Color feedback based on proximity to 85% target
                if (ratio >= 0.80 && ratio < 0.90) // Consolidation zone (ideal)
                    TxtSuccessRatio.Foreground = Brushes.Green;
                else if (ratio >= 0.60 && ratio < 0.80) // Exploration zone
                    TxtSuccessRatio.Foreground = Brushes.Orange;
                else if (ratio >= 0.90 && ratio < 0.95) // Polish zone
                    TxtSuccessRatio.Foreground = Brushes.DarkBlue;
                else if (ratio >= 0.95) // Mastered zone
                    TxtSuccessRatio.Foreground = Brushes.DarkGreen;
                else // TooHard (<60%)
                    TxtSuccessRatio.Foreground = Brushes.Red;
            }

            // Update Learning Zone label
            if (TxtLearningZone != null)
            {
                string zoneName = PracticeHistory.GetLearningZoneFromRatio(ratio);
                string displayLabel = PracticeHistory.GetLearningZoneDisplayLabel(zoneName);

                Brush zoneBrush = zoneName switch
                {
                    "TooHard" => Brushes.Red,
                    "Exploration" => Brushes.Orange,
                    "Consolidation" => Brushes.Green,
                    "Polish" => Brushes.DarkBlue,
                    "Mastered" => Brushes.DarkGreen,
                    _ => Brushes.Gray
                };

                TxtLearningZone.Text = displayLabel;
                TxtLearningZone.Foreground = zoneBrush;
            }
        }

        private void CbTargetRepetitions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            if (CbTargetRepetitions?.SelectedItem is string s && int.TryParse(s, out int val))
            {
                _targetRepetitions = val;
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        private void Duration_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateElapsedTimeFromTextBoxes();
        }

        private void RbDifficulty_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
            if (sender is RadioButton rb && rb.IsChecked == true)
            {
                string newDiff = rb.Content?.ToString() ?? "Difficult";
                _selectedDifficulty = newDiff;
            }
        }

        private void LvPracticeHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedHistoryItem = LvPracticeHistory.SelectedItem as PracticeHistory;
        }

        private void EditHistoryItem_Click(object sender, RoutedEventArgs e)
        {
            if (LvPracticeHistory.SelectedItem is PracticeHistory ph)
            {
                _selectedHistoryItem = ph;
                _isEditingExistingItem = true;
                _isInitializing = true;
                LoadHistoryItemForEditing(ph);
                _isInitializing = false;
                UpdateAllTextFields();
            }
        }
        private void AddCustomMinutes_Click(object sender, RoutedEventArgs e) { /* not implemented */ }
        private void DeleteHistoryItem_Click(object sender, RoutedEventArgs e) { /* not implemented */ }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // === SUCCESS RATIO HELPER ===
        /// <summary>
        /// Berekent de actuele success ratio voor deze sessie (inclusief rolling average info)
        /// </summary>
        private (double currentSessionRatio, double rollingRatio, string ratioLabel, string learningZone) CalculateCurrentSuccessRatio()
        {
            // Haal alle history op voor deze bar section
            var allHistory = PracticeHistoryManager.Instance.GetHistoryForBarSection(_barSection.Id).ToList();

            // Bereken rolling average over laatste 7 sessies
            double rollingRatio = PracticeHistory.CalculateRollingSuccessRatio(_barSection.Id, allHistory, windowSize: 7);

            // Bereken current session ratio
            int currentTotal = _completedRepetitions + _totalFailures;
            double currentSessionRatio = currentTotal > 0 ? (double)_completedRepetitions / currentTotal : 0.0;

            // Bepaal label
            string ratioLabel = "";
            if (allHistory.Count == 0 && currentTotal > 0)
            {
                ratioLabel = " (preview)";
            }
            else if (allHistory.Count > 0 && allHistory.Count < 7)
            {
                ratioLabel = $" (avg {allHistory.Count})";
            }
            else if (allHistory.Count >= 7)
            {
                ratioLabel = " (7-session avg)";
            }

            // Calculate learning zone based on rolling ratio
            string learningZone;
            if (rollingRatio < 0.60)
            {
                learningZone = "TooHard";
            }
            else if (rollingRatio < 0.80)
            {
                learningZone = "Exploration";
            }
            else if (rollingRatio <= 0.90)
            {
                learningZone = "Consolidation";
            }
            else if (rollingRatio < 0.95)
            {
                learningZone = "Polish";
            }
            else
            {
                learningZone = "Mastered";
            }

            return (currentSessionRatio, rollingRatio, ratioLabel, learningZone);
        }

        // === OVERLEARNING RECOMMENDATIONS ===
        private void UpdateOverlearningRecommendations()
        {
            try
            {
                // Panel is always visible (per requirement)
                if (OverlearningRecommendationPanel != null)
                    OverlearningRecommendationPanel.Visibility = Visibility.Visible;

                // Default: greyed out and disabled when no mistakes
                if (_attemptsThisSession <= 0)
                {
                    if (TxtOverlearningRecommendation != null)
                    {
                        TxtOverlearningRecommendation.Text = "Overlearning advice activates after the first mistake.";
                        TxtOverlearningRecommendation.Foreground = Brushes.Gray;
                    }
                    if (BtnApply50Overlearning != null)
                    {
                        BtnApply50Overlearning.IsEnabled = false;
                        BtnApply50Overlearning.Background = new SolidColorBrush(Color.FromRgb(224, 224, 224)); // #E0E0E0
                        BtnApply50Overlearning.Foreground = Brushes.Gray;
                    }
                    if (BtnApply100Overlearning != null)
                    {
                        BtnApply100Overlearning.IsEnabled = false;
                        BtnApply100Overlearning.Background = new SolidColorBrush(Color.FromRgb(224, 224, 224));
                        BtnApply100Overlearning.Foreground = Brushes.Gray;
                    }
                    return;
                }

                // Calculate recommendations based on current attempts
                _recommended50Overlearning = _overlearningTracker.CalculateRequiredRepetitions(_attemptsThisSession, false);
                _recommended100Overlearning = _overlearningTracker.CalculateRequiredRepetitions(_attemptsThisSession, true);

                // Update text
                if (TxtOverlearningRecommendation != null)
                {
                    TxtOverlearningRecommendation.Text = $"Based on {_attemptsThisSession} attempts: 50%={_recommended50Overlearning}, 100%={_recommended100Overlearning} reps";
                }

                // Enable buttons and color-code by number of mistakes
                Brush btnBg;
                Brush txtFg;
                if (_attemptsThisSession == 1)
                {
                    btnBg = Brushes.Orange;     // one error → orange
                    txtFg = Brushes.Orange;
                }
                else // 2 or more
                {
                    btnBg = Brushes.Red;        // two+ errors → red
                    txtFg = Brushes.Red;
                }

                if (BtnApply50Overlearning != null)
                {
                    BtnApply50Overlearning.IsEnabled = true;
                    BtnApply50Overlearning.Background = btnBg;
                    BtnApply50Overlearning.Foreground = Brushes.White;
                }
                if (BtnApply100Overlearning != null)
                {
                    BtnApply100Overlearning.IsEnabled = true;
                    BtnApply100Overlearning.Background = btnBg;
                    BtnApply100Overlearning.Foreground = Brushes.White;
                }
                if (TxtOverlearningRecommendation != null)
                {
                    TxtOverlearningRecommendation.Foreground = txtFg;
                }

                MLLogManager.Instance.Log($"Overlearning recommendations updated: attempts={_attemptsThisSession}, 50%={_recommended50Overlearning}, 100%={_recommended100Overlearning}", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("UpdateOverlearningRecommendations failed", ex);
            }
        }

        private void BtnApply50Overlearning_Click(object sender, RoutedEventArgs e)
        {
            ApplyOverlearningRecommendation(_recommended50Overlearning, "50%");
        }

        private void BtnApply100Overlearning_Click(object sender, RoutedEventArgs e)
        {
            ApplyOverlearningRecommendation(_recommended100Overlearning, "100%");
        }

        private void ApplyOverlearningRecommendation(int recommendedReps, string overlearningType)
        {
            if (recommendedReps <= 0 || recommendedReps > 20)
            {
                MessageBox.Show($"Invalid recommendation: {recommendedReps} repetitions", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Vind het juiste ComboBoxItem
            string targetValue = recommendedReps.ToString();
            CbTargetRepetitions.SelectedItem = targetValue;
            _targetRepetitions = recommendedReps;

            MLLogManager.Instance.Log($"Applied {overlearningType} overlearning recommendation: {recommendedReps} repetitions (based on {_attemptsThisSession} attempts)", LogLevel.Info);

            MessageBox.Show($"Target repetitions set to {recommendedReps} ({overlearningType} overlearning)\nBased on {_attemptsThisSession} attempts before success.",
                           "Overlearning Applied", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // === EINDE EVENT HANDLERS ===
    }
}
