// --- START OF FILE DaySessionsWindow.xaml.cs ---

using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using System.Windows.Interop;

namespace ModusPractica
{
    public partial class DaySessionsWindow : Window
    {
        private readonly DateTime _selectedDate;
        private readonly List<ScheduledPracticeSession> _sessions;
        private readonly List<MusicPieceItem> _allMusicPieces; // NIEUW VELD
        private static readonly SolidColorBrush NextDueTextBrush = CreatePastelOrangeBrush();

        private bool _isLaunchingExtraPractice = false; // race guard
        private System.Timers.Timer _midnightTimer; // daggrens monitor


        // AANGEPASTE CONSTRUCTOR
        public DaySessionsWindow(DateTime selectedDate, List<ScheduledPracticeSession> sessions, List<MusicPieceItem> allMusicPieces)
        {
            _midnightTimer = new System.Timers.Timer(); // Initialize timer

            InitializeComponent();
            _selectedDate = selectedDate;
            _sessions = sessions ?? new List<ScheduledPracticeSession>();
            _allMusicPieces = allMusicPieces ?? new List<MusicPieceItem>(); // DATA OPSLAAN

            // OPLOSSING: Eerst muziekstukken herladen voordat de UI wordt geÃ¯nitialiseerd
            // om ervoor te zorgen dat we de meest actuele data hebben
            ReloadMusicPiecesFromDisk();

            // NIEUW: Herlaad ook altijd de sessies van de geselecteerde dag uit de manager
            // i.p.v. blind te vertrouwen op de doorgegeven (mogelijk verouderde) lijst
            RefreshSessionsForSelectedDay();
        }

        private void InitializeUI()
        {
            TxtSelectedDate.Text = _selectedDate.ToString("D", CultureHelper.Current);

            int totalSessions = _sessions.Count;
            int completedSessions = _sessions.Count(s => s.Status?.ToLower() == "completed");

            TxtSessionCount.Text = $"{totalSessions} {(totalSessions == 1 ? "Practice Session" : "Practice Sessions")}";
            if (completedSessions > 0)
            {
                TxtSessionCount.Text += $" ({completedSessions} completed)";
            }

            TxtTotalTime.Visibility = Visibility.Collapsed;
        }

        private void PopulateSessionsList()
        {
            SessionsPanel.Children.Clear();

            if (_sessions.Count == 0)
            {
                TextBlock noSessionsMessage = new TextBlock
                {
                    Text = "No practice sessions scheduled for this day.",
                    FontSize = 14,
                    Margin = new Thickness(8),
                    Foreground = Brushes.Gray
                };
                SessionsPanel.Children.Add(noSessionsMessage);
                return;
            }

            // ⇩ Gewijzigd: sorteer eerst op 'completed' (completed = onderaan), dan op titel, dan op maatsecties numeriek, dan op tekst suffix
            var sortedSessions = _sessions
                .OrderBy(s => s.Status?.ToLower() == "completed")
                .ThenBy(s => s.MusicPieceTitle)
                .ThenBy(s => ExtractFirstBarNumber(s.BarSectionRange))
                .ThenBy(s => s.BarSectionRange?.ToLowerInvariant() ?? string.Empty) // Case-insensitive text sort for suffixes like RH/LH
                .ToList();

            foreach (var session in sortedSessions)
            {
                // Resolve BarSection once per session (SSOT for Difficulty)
                var resolvedSection = ResolveBarSectionForSession(session);
                string diffForDisplay = resolvedSection?.Difficulty ?? session.Difficulty ?? "Average";

                Border sessionBorder = new Border();
                bool isCompleted = IsSessionCompleted(session);
                sessionBorder.Style = (Style)(isCompleted
                    ? FindResource("CompletedSessionStyle")
                    : FindResource("SessionItemStyle"));

                SolidColorBrush musicPieceBrush = GetMusicPieceColorBrush(session.MusicPieceId);
                Color backgroundColor = musicPieceBrush.Color;
                Color sessionColor = Color.FromArgb(180, backgroundColor.R, backgroundColor.G, backgroundColor.B);
                sessionBorder.Background = new SolidColorBrush(sessionColor);

                Grid sessionGrid = new Grid();
                sessionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Titel
                sessionGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: Details (2 kolommen)
                sessionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                sessionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                TextBlock titleBlock = new TextBlock
                {
                    Text = session.MusicPieceTitle,
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold
                };
                Grid.SetRow(titleBlock, 0);
                sessionGrid.Children.Add(titleBlock);
                var detailsGrid = new Grid();
                detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // links
                detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // rechts
                Grid.SetRow(detailsGrid, 1);
                sessionGrid.Children.Add(detailsGrid);

                // Linker kolom: Bars + Tau/Status + Difficulty
                var leftStack = new StackPanel { Orientation = Orientation.Vertical };
                Grid.SetColumn(leftStack, 0);
                detailsGrid.Children.Add(leftStack);

                TextBlock barSectionBlock = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(session.BarSectionRange)
                        ? "No bar section"
                        : $"Chunks: {session.BarSectionRange}",
                    FontSize = 12,
                    Margin = new Thickness(0, 2, 0, 0),
                    Foreground = Brushes.Gray
                };
                leftStack.Children.Add(barSectionBlock);

                // --- AANGEPASTE LOGICA VOOR TAU/STATUS + Difficulty (SSOT) ---
                TextBlock statusBlock = new TextBlock
                {
                    FontSize = 11,
                    Foreground = Brushes.DarkSlateGray,
                    Margin = new Thickness(0, 4, 0, 0)
                };
                string tauStatus = BuildTauStatusText(session);
                statusBlock.Text = string.IsNullOrWhiteSpace(tauStatus)
                    ? $"Diff: {diffForDisplay}"
                    : $"{tauStatus}\nDiff: {diffForDisplay}";
                // Tooltip-uitleg voor τ en R*
                try
                {
                    double rStarTooltipValue = EbbinghausConstants.GetRetentionTargetForDifficulty(resolvedSection?.Difficulty ?? session.Difficulty ?? "Average");
                    statusBlock.ToolTip = $"τ (tau) = rate of forgetting (higher = slower). R* = target retention level the planner aims to maintain in the next nession ({rStarTooltipValue.ToString("P0", CultureHelper.Current)}).";
                }
                catch { /* best-effort tooltip */ }
                leftStack.Children.Add(statusBlock);

                string nextDueText;
                if (isCompleted)
                {
                    if (resolvedSection?.NextDueDate != null)
                    {
                        DateTime dueDate = resolvedSection.NextDueDate.Value;
                        nextDueText = $"Next due: {dueDate.ToString("dddd dd/MM", CultureHelper.Current)}";
                    }
                    else
                    {
                        nextDueText = "Next due: not scheduled";
                    }
                }
                else
                {
                    nextDueText = $"Scheduled: {session.ScheduledDate.ToString("dddd dd/MM", CultureHelper.Current)}";
                }

                // Rechter kolom: Description + Next due / Scheduled
                var rightStack = new StackPanel { Orientation = Orientation.Vertical };
                Grid.SetColumn(rightStack, 1);
                detailsGrid.Children.Add(rightStack);

                // Beschrijving (indien aanwezig)
                string? descriptionText = resolvedSection?.Description;
                var descriptionBlock = new TextBlock
                {
                    Text = descriptionText,
                    FontSize = 12,
                    Foreground = Brushes.Black,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10, 2, 0, 0),
                };
                if (string.IsNullOrWhiteSpace(descriptionText))
                {
                    descriptionBlock.Visibility = Visibility.Collapsed; // verberg als leeg
                }
                rightStack.Children.Add(descriptionBlock);

                TextBlock nextDueBlock = new TextBlock
                {
                    Text = nextDueText,
                    FontSize = 11,
                    Margin = new Thickness(10, 4, 0, 0),
                    Foreground = NextDueTextBrush
                };
                rightStack.Children.Add(nextDueBlock);

                bool isTodaySelected = _selectedDate.Date == DateHelper.GetCurrentSessionDate();
                bool isCompletedToday = isCompleted && isTodaySelected;

                Button practiceButton = new Button
                {
                    Content = isCompleted ? "Review" : "Practice",
                    Tag = session,
                    Margin = new Thickness(10, 0, 0, 0),
                    Padding = new Thickness(10, 4, 10, 4),
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                if (isCompleted)
                {
                    try
                    {
                        var sectionId = session.BarSectionId;
                        var today = DateHelper.LocalToday();
                        int todayCount = PracticeHistoryManager.Instance.CountSessionsForSectionOnLocalDate(sectionId, DateOnly.FromDateTime(today));
                        practiceButton.Content = todayCount >= 2 ? $"Review (x{todayCount} today)" : "Review";
                        practiceButton.ToolTip = todayCount >= 2
                            ? $"This will be the x{todayCount + 1} session today. Due stays unchanged. Current diff: {diffForDisplay}"
                            : $"Start an extra practice session for today. Due stays unchanged. Current diff: {diffForDisplay}";
                    }
                    catch (Exception ex)
                    {
                        MLLogManager.Instance.LogError("DaySessionsWindow: failed to compute todayCount for Review label", ex);
                    }
                }

                if (isCompletedToday)
                {
                    int currentCount = PracticeHistoryManager.Instance.CountSessionsForSectionOnLocalDate(session.BarSectionId, DateOnly.FromDateTime(DateHelper.LocalToday()));
                    if (practiceButton.ToolTip == null)
                    {
                        practiceButton.ToolTip = currentCount <= 1
                            ? $"Start an extra practice session for today. Due stays unchanged. Current diff: {diffForDisplay}"
                            : $"This will be the x{currentCount + 1} session today. Due stays unchanged. Current diff: {diffForDisplay}";
                    }
                    practiceButton.IsEnabled = true;
                    practiceButton.Background = Brushes.Orange;
                    practiceButton.Foreground = Brushes.White;
                    var secondaryStyle = TryFindResource("SecondaryButton") as Style;
                    if (secondaryStyle != null)
                    {
                        practiceButton.Style = secondaryStyle;
                    }
                    practiceButton.Click += PracticeButton_Click;
                }
                else
                {
                    if (isCompleted)
                    {
                        practiceButton.Background = Brushes.Orange;
                        practiceButton.Foreground = Brushes.White;
                        practiceButton.Click += PracticeButton_Click;
                    }
                    else
                    {
                        practiceButton.Background = Brushes.LightSeaGreen;
                        practiceButton.Foreground = Brushes.White;
                        practiceButton.Click += PracticeButton_Click;
                    }
                }

                Grid.SetRow(practiceButton, 0);
                Grid.SetColumn(practiceButton, 1);
                sessionGrid.Children.Add(practiceButton);

                sessionBorder.Child = sessionGrid;
                sessionBorder.Margin = new Thickness(8, 6, 8, 6);

                SessionsPanel.Children.Add(sessionBorder);
            }

            int totalSessions = _sessions.Count;
            int completedSessions = _sessions.Count(s => s.Status?.ToLower() == "completed");
            TxtSessionCount.Text = $"{totalSessions} {(totalSessions == 1 ? "Practice Session" : "Practice Sessions")}";
            if (completedSessions > 0)
            {
                TxtSessionCount.Text += $" ({completedSessions} completed)";
            }

            TxtTotalTime.Visibility = Visibility.Collapsed;
        }

        private bool IsSessionCompleted(ScheduledPracticeSession session)
        {
            return session.Status?.ToLower() == "completed";
        }

        // In DaySessionsWindow.xaml.cs

        private void PracticeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isLaunchingExtraPractice) return; // prevent race double-click
            if (sender is Button button && button.Tag is ScheduledPracticeSession session)
            {
                button.IsEnabled = false; // immediate UI guard
                _isLaunchingExtraPractice = true;
                try
                {
                    // Explicitly nullable - can be null if music piece was deleted
                    MusicPieceItem? musicPiece = null;
                    BarSection? barSection = null;

                    musicPiece = _allMusicPieces.FirstOrDefault(mp => mp.Id == session.MusicPieceId);

                    if (musicPiece == null)
                    {
                        MLLogManager.Instance.Log($"MusicPiece {session.MusicPieceId} not found in live memory. Attempting fallback load from disk.", LogLevel.Warning);
                        try
                        {
                            string profileFolder = DataPathProvider.GetProfileFolder(ActiveUserSession.ProfileName);
                            string musicPiecesFolder = Path.Combine(profileFolder, "MusicPieces");

                            string idPrefix = session.MusicPieceId.ToString() + "_";
                            string[] candidates = Directory.Exists(musicPiecesFolder)
                                ? Directory.GetFiles(musicPiecesFolder, session.MusicPieceId + "_*.json")
                                : Array.Empty<string>();

                            string? filePath = candidates.FirstOrDefault();
                            if (filePath == null)
                            {
                                string legacyPath = Path.Combine(musicPiecesFolder, $"{session.MusicPieceId}.json");
                                if (File.Exists(legacyPath)) filePath = legacyPath;
                            }

                            if (filePath != null && File.Exists(filePath))
                            {
                                string jsonContent = File.ReadAllText(filePath);
                                musicPiece = JsonSerializer.Deserialize<MusicPieceItem>(jsonContent);

                                if (musicPiece != null && !string.IsNullOrEmpty(musicPiece.ColorResourceName) &&
                                    MainWindow.ColorResourceMap.TryGetValue(musicPiece.ColorResourceName, out SolidColorBrush colorBrush))
                                {
                                    musicPiece.ColorBrush = colorBrush;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"An error occurred while loading the music piece data from disk: {ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            MLLogManager.Instance.LogError($"Failed to load music piece {session.MusicPieceId} from DaySessionsWindow as fallback.", ex);
                            return;
                        }
                    }

                    barSection = musicPiece?.BarSections.FirstOrDefault(bs => bs.Id == session.BarSectionId);

                    if (musicPiece != null && barSection == null)
                    {
                        barSection = musicPiece.BarSections.FirstOrDefault(bs => string.Equals(bs.BarRange, session.BarSectionRange, StringComparison.OrdinalIgnoreCase));

                        if (barSection == null)
                        {
                            try
                            {
                                var replacement = ScheduledPracticeSessionManager.Instance
                                    .GetAllRegularScheduledSessions()
                                    .Where(s => s.MusicPieceId == session.MusicPieceId && s.ScheduledDate.Date == _selectedDate.Date && s.Status != "Completed")
                                    .FirstOrDefault(s => musicPiece.BarSections.Any(bs => bs.Id == s.BarSectionId));

                                if (replacement != null)
                                {
                                    session = replacement;
                                    barSection = musicPiece.BarSections.FirstOrDefault(bs => bs.Id == replacement.BarSectionId);
                                    MLLogManager.Instance.Log($"DaySessionsWindow: Rebound to replacement session {replacement.Id} after merge.", LogLevel.Info);
                                }
                            }
                            catch (Exception ex)
                            {
                                MLLogManager.Instance.LogError("DaySessionsWindow: error while searching for replacement session after merge.", ex);
                            }
                        }

                        if (barSection == null)
                        {
                            MLLogManager.Instance.Log("DaySessionsWindow: Stale session detected (bar section missing). Refreshing day sessions.", LogLevel.Warning);
                            RefreshSessionsForSelectedDay();
                            return;
                        }
                    }

                    if (musicPiece == null || barSection == null)
                    {
                        MessageBox.Show("Could not open the practice session because the related music piece or section could not be found.", "Missing Data", MessageBoxButton.OK, MessageBoxImage.Warning);
                        MLLogManager.Instance.LogError($"CRITICAL: Could not find MusicPiece/BarSection for session {session.Id} even after fallback.", new NullReferenceException());
                        return;
                    }

                    bool isCompleted = IsSessionCompleted(session);
                    bool isInitialFoundationPhase = barSection.PracticeScheduleStage < 3;
                    string reason;
                    bool isTodaySelected = _selectedDate.Date == DateHelper.GetCurrentSessionDate();
                    bool isCompletedToday = isCompleted && isTodaySelected;

                    bool isFutureDate = DateHelper.CalculateIntervalDays(DateHelper.GetCurrentSessionDate(), _selectedDate) > 0;

                    if (!isInitialFoundationPhase && isFutureDate)
                    {
                        reason = $"Denied because session is in the future ({DateHelper.FormatDisplayDate(_selectedDate)}) and section is past foundation phase (Stage {barSection.PracticeScheduleStage}).";
                        MLLogManager.Instance.Log($"Practice denied for '{barSection.BarRange}'. Reason: {reason}", LogLevel.Info);
                        MessageBox.Show(
                            $"This session is scheduled for a future date ({DateHelper.FormatDisplayDate(session.ScheduledDate)}).\n\nYou can only practice a session on its scheduled day to follow the spaced repetition plan.",
                            "Practice Not Available Yet",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        return;
                    }
                    else if (isCompletedToday)
                    {
                        reason = "Subsequent same-day review initiating ExtraPractice (due preserved).";
                    }
                    else if (isCompleted)
                    {
                        reason = "Review session initiated for an already completed item (historical).";
                    }
                    else if (isInitialFoundationPhase)
                    {
                        reason = $"Allowed due to flexible foundation phase (Stage {barSection.PracticeScheduleStage}).";
                    }
                    else
                    {
                        reason = "Practice allowed for a due or overdue session.";
                    }

                    MLLogManager.Instance.Log($"Practice initiated for '{barSection.BarRange}'. {reason}", LogLevel.Info);

                    PracticeSessionWindow practiceWindow = new PracticeSessionWindow(musicPiece, barSection);
                    DateOnly todayDateOnly = DateOnly.FromDateTime(DateHelper.LocalToday());
                    int existingCountBefore = PracticeHistoryManager.Instance.CountSessionsForSectionOnLocalDate(barSection.Id, todayDateOnly);

                    if (isCompletedToday)
                    {
                        if (existingCountBefore == 0)
                        {
                            MLLogManager.Instance.Log($"WARNING: TodayCount=0 for ExtraPractice start '{musicPiece.Title} [{barSection.BarRange}]' SectionId={barSection.Id}", LogLevel.Warning);
                        }
                        MLLogManager.Instance.Log($"Starting ExtraPractice (N={existingCountBefore + 1}) for '{musicPiece.Title} [{barSection.BarRange}]' PreserveDueDate=true SectionId={barSection.Id} SessionId={Guid.NewGuid()}", LogLevel.Info);
                        practiceWindow.ApplyExtraPracticeContext(new ExtraPracticeContext
                        {
                            Mode = "ExtraPractice",
                            IsSubsequentSession = true,
                            PreserveDueDate = true,
                            SectionId = barSection.Id,
                            ParentPieceId = musicPiece.Id,
                            LastScheduledSessionId = session.Id
                        });
                    }

                    practiceWindow.Owner = this;
                    bool? result = practiceWindow.ShowDialog();
                    if (result == true)
                    {
                        if (!isCompletedToday)
                        {
                            session.Status = "Completed";
                            barSection.LastPracticeDate = DateHelper.GetCurrentSessionDate();
                        }
                        PracticeHistoryManager.Instance.ReloadHistoryData();
                        int count = PracticeHistoryManager.Instance.CountSessionsForSectionOnLocalDate(barSection.Id, todayDateOnly);
                        if (isCompletedToday)
                        {
                            MLLogManager.Instance.Log($"Subsequent session for '{musicPiece.Title} [{barSection.BarRange}]'. Refining difficulty and preserving future due date. (N={count}) SectionId={barSection.Id} SessionId={session.Id}", LogLevel.Info);
                        }
                        RefreshSessionsForSelectedDay();
                        AppState.MusicDataChanged = true;
                        if (Application.Current?.MainWindow is MainWindow main)
                        {
                            main.SyncAllPiecesFromHistoryAndRefresh();
                        }
                    }
                    else
                    {
                        // Nieuw: Cancelled ExtraPractice logging wanneer geen save plaatsvond
                        if (isCompletedToday)
                        {
                            MLLogManager.Instance.Log(
                                $"Cancelled ExtraPractice (no save) for '{musicPiece.Title} [{barSection.BarRange}]'. N remains {existingCountBefore}, PreserveDueDate=true.",
                                LogLevel.Info);
                        }
                        // Force refresh ook bij cancel / andere uitkomsten om randgevallen op te vangen
                        RefreshSessionsForSelectedDay();
                    }
                }
                finally
                {
                    _isLaunchingExtraPractice = false;
                    button.IsEnabled = true; // always re-enable
                }
            }
        }
        private static SolidColorBrush CreatePastelOrangeBrush()
        {
            var brush = new SolidColorBrush(Color.FromRgb(204, 102, 0)); // Darker orange for better contrast
            brush.Freeze();
            return brush;
        }

        ////////////////////////////////////////////////////////////////////////////

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _midnightTimer?.Stop();
            this.Close();
        }

        // NIEUWE HULPMETHODE
        /// <summary>
        /// Reloads all music pieces from disk into the local cache (_allMusicPieces).
        /// This ensures that changes made in other windows (like PracticeSessionWindow)
        /// are reflected here, specifically the updated NextDueDate on BarSection objects.
        /// Logic adapted from CalendarWindow.GetAllMusicPieces().
        /// </summary>
        private void ReloadMusicPiecesFromDisk()
        {
            bool prevSuppress = AppState.SuppressLifecycleSideEffects;
            AppState.SuppressLifecycleSideEffects = true; // prevent lifecycle side-effects during load
            var musicPieces = new List<MusicPieceItem>();
            try
            {
                // Stap 1: Bepaal de correcte map op basis van het actieve profiel.
                string profileFolder = DataPathProvider.GetProfileFolder(ActiveUserSession.ProfileName);
                string musicPiecesFolder = Path.Combine(profileFolder, "MusicPieces");

                if (Directory.Exists(musicPiecesFolder))
                {
                    // Stap 2: Lees alle .json-bestanden in de map (inclusief oude en nieuwe formaten).
                    string[] jsonFiles = Directory.GetFiles(musicPiecesFolder, "*.json");
                    foreach (string jsonFile in jsonFiles)
                    {
                        try
                        {
                            // Stap 3: Deserialiseer elk bestand naar een MusicPieceItem-object.
                            string jsonContent = File.ReadAllText(jsonFile);
                            var musicPiece = JsonSerializer.Deserialize<MusicPieceItem>(jsonContent);

                            if (musicPiece != null)
                            {
                                // Stap 4: Herstel de ColorBrush op basis van de opgeslagen resourcenaam.
                                if (!string.IsNullOrEmpty(musicPiece.ColorResourceName) &&
                                    MainWindow.ColorResourceMap.TryGetValue(musicPiece.ColorResourceName, out SolidColorBrush colorBrush))
                                {
                                    musicPiece.ColorBrush = colorBrush;
                                }


                                // NIEUW: Normaliseer LastPracticeDate waarden (alleen datum, geen tijdscomponent)
                                if (musicPiece.BarSections != null)
                                {
                                    foreach (var barSection in musicPiece.BarSections)
                                    {
                                        if (barSection.LastPracticeDate.HasValue)
                                        {
                                            DateTime originalDate = barSection.LastPracticeDate.Value;
                                            DateTime normalizedDate = originalDate.Date;

                                            if (originalDate != normalizedDate)
                                            {
                                                barSection.LastPracticeDate = normalizedDate;
                                                MLLogManager.Instance.Log(
                                                    $"DaySessionsWindow: Normalized LastPracticeDate for '{musicPiece.Title}' [{barSection.BarRange}] from {originalDate} to {normalizedDate:yyyy-MM-dd}",
                                                    LogLevel.Debug);
                                            }
                                        }
                                    }
                                }

                                // NIEUW: Check of bestandsnaam migratie nodig is
                                string currentFileName = Path.GetFileName(jsonFile);
                                string expectedFileName = Path.GetFileName(musicPiece.GetFilePath(musicPiecesFolder));

                                if (!string.Equals(currentFileName, expectedFileName, StringComparison.OrdinalIgnoreCase))
                                {
                                    MLLogManager.Instance.Log(
                                        $"DaySessionsWindow: File name migration detected for '{currentFileName}' should be '{expectedFileName}'",
                                        LogLevel.Debug);
                                    // Noteer alleen de discrepantie, laat MainWindow de daadwerkelijke migratie afhandelen
                                }

                                musicPieces.Add(musicPiece);
                            }
                        }
                        catch (Exception ex)
                        {
                            MLLogManager.Instance.LogError($"DaySessionsWindow: Error loading individual music piece {jsonFile}", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("DaySessionsWindow: A critical error occurred in ReloadMusicPiecesFromDisk", ex);
            }
            finally
            {
                AppState.SuppressLifecycleSideEffects = prevSuppress;
            }

            _allMusicPieces.Clear();
            _allMusicPieces.AddRange(musicPieces);
            MLLogManager.Instance.Log($"DaySessionsWindow: Reloaded {_allMusicPieces.Count} music pieces from disk to refresh UI data.", LogLevel.Debug);
        }


        private BarSection? ResolveBarSectionForSession(ScheduledPracticeSession session)
        {
            try
            {
                var musicPiece = _allMusicPieces.FirstOrDefault(mp => mp.Id == session.MusicPieceId);
                if (musicPiece == null)
                {
                    return null;
                }

                var barSection = musicPiece.BarSections.FirstOrDefault(bs => bs.Id == session.BarSectionId);
                if (barSection != null)
                {
                    return barSection;
                }

                if (!string.IsNullOrWhiteSpace(session.BarSectionRange))
                {
                    barSection = musicPiece.BarSections.FirstOrDefault(
                        bs => string.Equals(bs.BarRange, session.BarSectionRange, StringComparison.OrdinalIgnoreCase));
                }

                return barSection;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"DaySessionsWindow: Error resolving bar section for session {session.Id}", ex);
                return null;
            }
        }

        private SolidColorBrush GetMusicPieceColorBrush(Guid musicPieceId)
        {

            try
            {
                // --- AANPASSING HIER: Gebruik de lokale _allMusicPieces lijst ---
                var musicPiece = _allMusicPieces.FirstOrDefault(mp => mp.Id == musicPieceId);

                if (musicPiece != null && musicPiece.ColorBrush != null)
                {
                    return musicPiece.ColorBrush;
                }
                else
                {
                    return new SolidColorBrush(Colors.LightGray);
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"Error getting music piece color for ID {musicPieceId}", ex);
                return new SolidColorBrush(Colors.LightGray);
            }
        }

        // Build the Tau/Status line and include the target retention R* based on difficulty
        private string BuildTauStatusText(ScheduledPracticeSession session)
        {
            string tauText = string.Empty;
            string statusText = string.Empty;

            if (session.TauValue > 0)
            {
                tauText = $"Ï„ = {session.TauValue:F1}";
            }

            // Determine difficulty (prefer resolved section, fall back to session)
            string difficulty = ResolveBarSectionForSession(session)?.Difficulty ?? session.Difficulty ?? "Average";
            double rStar = EbbinghausConstants.GetRetentionTargetForDifficulty(difficulty);
            string targetText = $"Target retention R* = {rStar.ToString("P0", CultureHelper.Current)}";

            // Status if available
            if (!string.IsNullOrWhiteSpace(session.Status))
            {
                statusText = session.Status;
            }

            // Compose line(s)
            string head = string.IsNullOrWhiteSpace(tauText) ? targetText : $"{tauText}  |  {targetText}";

            if (string.IsNullOrWhiteSpace(statusText))
            {
                return head;
            }
            else
            {
                return $"{head}\n{statusText}";
            }
        }

        // Event handler for window loaded to adjust size based on screen dimensions
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Get screen dimensions
            double screenWidth = SystemParameters.WorkArea.Width;
            double screenHeight = SystemParameters.WorkArea.Height;

            // Set width to a reasonable value
            this.Width = Math.Max(650, Math.Min(850, screenWidth * 0.75));

            // For height, use a large percentage of screen height to show more content
            // but leave small margins at top and bottom
            this.Height = Math.Min(screenHeight * 0.92, 1000); // Use 92% of screen height, max 1000px

            // Ensure window isn't too small
            if (this.Height < 600)
                this.Height = 600;

            // Center the window on the screen
            this.Left = (screenWidth - this.Width) / 2 + SystemParameters.WorkArea.Left;
            this.Top = (screenHeight - this.Height) / 2 + SystemParameters.WorkArea.Top;
        }

        private void InitializeMidnightRolloverTimer()
        {
            _midnightTimer = new System.Timers.Timer(TimeSpan.FromMinutes(1).TotalMilliseconds);
            _midnightTimer.AutoReset = true;
            _midnightTimer.Elapsed += (_, __) =>
            {
                var nowLocal = DateHelper.LocalToday();
                if (nowLocal > _selectedDate.Date && _selectedDate.Date == DateHelper.LocalToday().AddDays(-1))
                {
                    Dispatcher.Invoke(() => RefreshSessionsForSelectedDay());
                }
            };
            _midnightTimer.Start();
        }
    }
}

namespace ModusPractica
{
    public class ExtraPracticeContext
    {
        // Mode is always set in constructor usage, but make it explicit with default
        public string Mode { get; set; } = "Normal";
        public bool IsSubsequentSession { get; set; }
        public bool PreserveDueDate { get; set; }
        public Guid SectionId { get; set; }
        public Guid ParentPieceId { get; set; }
        public Guid LastScheduledSessionId { get; set; }
    }
}

namespace ModusPractica
{
    public partial class DaySessionsWindow
    {
        // Reload sessions for the selected day from the manager and refresh UI
        private void RefreshSessionsForSelectedDay()
        {
            // --- STAP 1: VERNIEUW DE BRONGEGEVENS VAN DE MUZIEKSTUKKEN ---
            // Dit zorgt ervoor dat de bijgewerkte NextDueDate van de BarSection objecten
            // opnieuw wordt ingelezen voordat de UI opnieuw wordt opgebouwd.
            ReloadMusicPiecesFromDisk();
            // --- EINDE TOEVOEGING ---

            try
            {
                ScheduledPracticeSessionManager.Instance.ReloadScheduledSessions();
            }
            catch { /* Best effort reload */ }

            var all = ScheduledPracticeSessionManager.Instance.GetAllRegularScheduledSessions();

            var activeIds = GetActivePieceIds();
            var refreshed = all
                .Where(s => s.ScheduledDate.Date == _selectedDate.Date)
                .Where(s => activeIds.Contains(s.MusicPieceId))
                .Where(s =>
                {
                    // Filter: Exclude Inactive bar sections from display
                    // This ensures DaySessionsWindow is consistent with CalendarWindow behavior
                    var musicPiece = _allMusicPieces.FirstOrDefault(mp => mp.Id == s.MusicPieceId);
                    var barSection = musicPiece?.BarSections?.FirstOrDefault(bs => bs.Id == s.BarSectionId);

                    // Exclude sessions where the bar section is Inactive
                    if (barSection != null && barSection.LifecycleState == LifecycleState.Inactive)
                        return false;

                    return true;
                })
                .OrderBy(s => s.Status?.ToLower() == "completed")
                .ThenBy(s => s.MusicPieceTitle)
                .ToList();

            _sessions.Clear();
            _sessions.AddRange(refreshed);

            InitializeUI();
            PopulateSessionsList();
        }

        /// <summary>
        /// Extracts the first bar number from a bar range string (e.g., "1-10" returns 1, "50" returns 50).
        /// Returns int.MaxValue if the range is null/empty/invalid to sort invalid entries to the end.
        /// </summary>
        private int ExtractFirstBarNumber(string barRange)
        {
            if (string.IsNullOrWhiteSpace(barRange))
                return int.MaxValue;

            // Handle formats like "1-10", "50", "1 - 10", etc.
            string[] parts = barRange.Split('-');
            if (parts.Length > 0 && int.TryParse(parts[0].Trim(), out int barNumber))
                return barNumber;

            return int.MaxValue;
        }

        // Geeft de IDs van actieve (niet-gepauzeerde) stukken terug
        private HashSet<Guid> GetActivePieceIds()
        {
            // _allMusicPieces is al een veld in deze class
            var source = _allMusicPieces ?? new List<MusicPieceItem>();

            // Archiveren bestaat niet meer; filter alleen op IsPaused.
            return new HashSet<Guid>(
                source
                    .Where(mp => mp != null && !mp.IsPaused)
                    .Select(mp => mp.Id)
            );
        }
    }
}









