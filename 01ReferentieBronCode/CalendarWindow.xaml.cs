using System.IO; // NIEUW: Voor toegang tot Path, Directory, en File
using System.Text.Json; // NIEUW: Voor de JsonSerializer
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using ModusPractica.Infrastructure;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;

namespace ModusPractica
{
    /// <summary>
    /// Interaction logic for CalendarWindow.xaml
    /// </summary>
    public partial class CalendarWindow : Window
    {
        private DateTime _currentMonth;
        private List<MusicPieceItem> _musicPieces;
        private readonly object _musicPiecesLock = new object();
        private List<ScheduledPracticeSession> _localScheduledSessionsCache;
        private Dictionary<DateTime, List<ScheduledPracticeSession>> _localSessionsByDateCache;
        private Guid? _selectedMusicPieceId = null; // Voor filtering
        private bool _isInitializing = true; // Deze vlag is nu overbodig, maar we laten hem staan om geen nieuwe fouten te introduceren.
        public bool ScheduleWasRecalculated { get; private set; } = false;
        private readonly DispatcherTimer _debounceRefreshTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };

        // De constructor moet er weer zo uitzien:
        public CalendarWindow()
        {
            InitializeComponent();
            this.Language = XmlLanguage.GetLanguage(CultureHelper.Current.IetfLanguageTag);
            _currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            _musicPieces = new List<MusicPieceItem>();
            _localScheduledSessionsCache = new List<ScheduledPracticeSession>();
            _localSessionsByDateCache = new Dictionary<DateTime, List<ScheduledPracticeSession>>();

            this.Activated += CalendarWindow_Activated;

            this.Loaded += (s, e) =>
            {
                _musicPieces = GetAllMusicPieces(); // Belangrijk: deze regel moet blijven
                LoadMusicPieceFilter(); // Deze methode vult de filter en zorgt dat de SelectionChanged event correct wordt afgehandeld.
                // FIX: Voer de initiële lading alleen uit als de data nog niet is geladen.
                // Dit voorkomt een dubbele, redundante laadactie als een ander proces (bv. MainWindow)
                // de data al heeft ververst bij het opstarten.
                if (_localSessionsByDateCache == null || !_localSessionsByDateCache.Any())
                {
                    LoadScheduledSessionsAndUpdateCalendar();
                }
                _isInitializing = false; // Einde van initialisatie, nu mogen events de UI verversen.
            };

            _debounceRefreshTimer.Tick += (_, __) =>
            {
                _debounceRefreshTimer.Stop();
                LoadScheduledSessionsAndUpdateCalendar();
                // Optioneel: logging
                // _logger?.Info("CalendarWindow detected data change and refreshed its UI.");
            };

            AppEvents.ScheduledSessionsChanged += OnScheduledSessionsChanged;

            // belangrijk: netjes uitschrijven bij sluiten
            this.Unloaded += (_, __) =>
            {
                AppEvents.ScheduledSessionsChanged -= OnScheduledSessionsChanged;
                _debounceRefreshTimer.Stop();
            };
        }

        private void OnScheduledSessionsChanged()
        {
            if (!IsLoaded) return;
            _debounceRefreshTimer.Stop();
            _debounceRefreshTimer.Start();
        }

        // NIEUW: Deze methode controleert of de data is gewijzigd wanneer het venster focus krijgt.
        private void CalendarWindow_Activated(object? sender, EventArgs e)
        {
            // Als de centrale vlag aangeeft dat data is gewijzigd (bv. na een oefensessie),
            // ververs dan de kalender.
            if (AppState.MusicDataChanged)
            {
                RefreshCalendarData();

                // Reset de vlag zodat we niet onnodig blijven verversen.
                AppState.MusicDataChanged = false;
                MLLogManager.Instance.Log("CalendarWindow detected data change and refreshed its UI.", LogLevel.Debug);
            }
        }

        // In CalendarWindow.xaml.cs

        private async void BtnGenerateSchedule_Click(object sender, RoutedEventArgs e)
        {
            string title = "Recalculate Schedule";
            string message = "This action will recalculate your entire practice schedule based on your current progress and the latest settings.\n\n" +
                             "What happens?\n" +
                             "- All sections will be rescheduled according to their practice history using spaced repetition.\n" +
                             "- Sections you've practiced more will be scheduled further in the future.\n" +
                             "- New or recently practiced sections will be scheduled sooner.\n" +
                             "- Your existing, uncompleted, scheduled practice sessions will be replaced by this new schedule.\n" +
                             "- Completed practice sessions (your practice history) will be preserved and used as a basis for the new schedule.\n\n" +
                             "When to use?\n" +
                             "- To update your schedule (e.g., after adding new music pieces or adjusting sections).\n" +
                             "- To correct missed practice days, so they are rescheduled.\n" +
                             "- After a longer break, to start with a fresh schedule.\n" +
                             "- If you simply want a completely new, optimized schedule.\n\n" +
                             "Do you want to proceed and regenerate the schedule?";

            MessageBoxResult result = MessageBox.Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Warning);

            if (result != MessageBoxResult.OK)
            {
                return;
            }

            BtnGenerateSchedule.SetCurrentValue(IsEnabledProperty, false);
            TxtGeneratedSessions.SetCurrentValue(TextBlock.TextProperty, "Schedule generation disabled");

            try
            {
                // NEW: Recalculate all section intervals and update scheduled sessions
                var allMusicPieces = GetAllMusicPieces();
                int totalUpdated = 0;
                var today = DateHelper.GetCurrentSessionDate();

                var scheduleManager = ScheduledPracticeSessionManager.Instance;

                foreach (var mp in allMusicPieces)
                {
                    if (mp.BarSections != null)
                    {
                        bool pieceChanged = false;

                        foreach (var section in mp.BarSections)
                        {
                            try
                            {
                                // Skip inactive sections
                                if (section.LifecycleState == LifecycleState.Inactive)
                                    continue;

                                // FOR RECALCULATION: Recalculate interval using normal spaced repetition logic
                                // This ensures sections are scheduled according to their practice history
                                double tau = EbbinghausConstants.CalculateAdjustedTau(
                                    section.Difficulty ?? "Average",
                                    section.CompletedRepetitions,
                                    section.PracticeScheduleStage);

                                double targetRetention = EbbinghausConstants.GetRetentionTargetForDifficulty(section.Difficulty);
                                double rawInterval = -tau * Math.Log((targetRetention - EbbinghausConstants.ASYMPTOTIC_RETENTION_BASELINE)
                                    / EbbinghausConstants.INITIAL_LEARNING_STRENGTH);

                                // Allow 0-day intervals (consistent with new sections)
                                int newInterval = Math.Max(0, Math.Min(365, (int)Math.Round(rawInterval)));
                                var originalNextDue = section.NextDueDate;
                                var originalInterval = section.Interval;

                                DateTime computedNextDue = today.AddDays(newInterval);

                                // Preserve existing future dates from backups; clamp past dates to today.
                                DateTime finalNextDue = computedNextDue;
                                DateTime? preservedCandidate = null;

                                var existingSession = scheduleManager.GetScheduledSessionForBarSection(section.Id);
                                if (existingSession != null)
                                {
                                    preservedCandidate = DateHelper.NormalizeToDateOnly(existingSession.ScheduledDate);
                                }
                                else if (section.NextDueDate.HasValue)
                                {
                                    preservedCandidate = DateHelper.NormalizeToDateOnly(section.NextDueDate.Value);
                                }

                                if (preservedCandidate.HasValue)
                                {
                                    if (preservedCandidate.Value > today)
                                    {
                                        finalNextDue = preservedCandidate.Value;
                                    }
                                    else
                                    {
                                        finalNextDue = today;
                                    }
                                }

                                if (finalNextDue < today)
                                {
                                    finalNextDue = today;
                                }

                                finalNextDue = DateHelper.NormalizeToDateOnly(finalNextDue);

                                section.NextDueDate = finalNextDue;
                                section.Interval = Math.Max(0, Math.Min(365, (finalNextDue - today).Days));

                                if (existingSession != null)
                                {
                                    existingSession.ScheduledDate = finalNextDue;
                                }
                                else
                                {
                                    var newSession = new ScheduledPracticeSession
                                    {
                                        Id = Guid.NewGuid(),
                                        MusicPieceId = mp.Id,
                                        MusicPieceTitle = mp.Title,
                                        BarSectionId = section.Id,
                                        BarSectionRange = section.BarRange,
                                        ScheduledDate = finalNextDue,
                                        Difficulty = section.Difficulty ?? "Average",
                                        Status = "Planned",
                                        EstimatedDuration = TimeSpan.FromMinutes(5),
                                        TauValue = section.AdaptiveTauMultiplier
                                    };

                                    scheduleManager.AddScheduledSession(newSession);
                                }

                                if (section.NextDueDate != originalNextDue || section.Interval != originalInterval)
                                {
                                    pieceChanged = true;
                                }

                                totalUpdated++;
                                MLLogManager.Instance.Log($"Recalculated section '{mp.Title} - {section.BarRange}': interval→{section.Interval} days, next due: {section.NextDueDate:yyyy-MM-dd}", LogLevel.Info);
                            }
                            catch (Exception ex)
                            {
                                MLLogManager.Instance.LogError($"Error recalculating section '{mp.Title} - {section.BarRange}'", ex);
                            }
                        }

                        if (pieceChanged)
                        {
                            if (System.Windows.Application.Current.MainWindow is MainWindow mw)
                            {
                                mw.SaveMusicPiece(mp);
                            }
                        }
                    }
                }

                // Save all changes at once
                scheduleManager.SaveScheduledSessions();

                MessageBox.Show(
                    $"Schedule recalculation complete.\n\nUpdated {totalUpdated} active sections:\n- Future dates imported from the backup stay in place\n- Overdue sections are rescheduled to today ({today:yyyy-MM-dd}).",
                    "Recalculation Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                MLLogManager.Instance.Log($"Schedule recalculation completed. {totalUpdated} sections updated with preserved future dates and today-clamped overdue items.", LogLevel.Info);

                // Refresh the calendar display
                this.ScheduleWasRecalculated = true;
                LoadScheduledSessionsAndUpdateCalendar();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error accessing schedule: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                MLLogManager.Instance.LogError("Error in BtnGenerateSchedule_Click", ex);
            }
            finally
            {
                BtnGenerateSchedule.SetCurrentValue(IsEnabledProperty, true);
            }
        }

        private void LoadScheduledSessionsAndUpdateCalendar()
        {
            try
            {
                MLLogManager.Instance.Log("CalendarWindow: Loading scheduled sessions.", LogLevel.Debug);

                // Step 1: Get all music pieces and identify the ones that are currently active
                // Active pieces are defined as not paused (archiving is no longer used).
                var allMusicPieces = GetAllMusicPieces();
                // Archiveren bestaat niet meer: filter alleen op niet-gepauzeerde stukken.
                var activeMusicPieceIds = new HashSet<Guid>(
                    allMusicPieces
                        .Where(mp => !MusicPieceUtils.IsMusicPiecePaused(mp.Id))
                        .Select(mp => mp.Id)
                );
                MLLogManager.Instance.Log($"CalendarWindow: Found {activeMusicPieceIds.Count} active (not paused) music pieces.", LogLevel.Debug);

                // Step 2: Fetch all scheduled sessions from the manager.
                var allSessionsFromManager = ScheduledPracticeSessionManager.Instance.GetAllRegularScheduledSessions();

                // Step 3: Filter these sessions to only include those belonging to active music pieces
                // AND exclude sessions for Inactive bar sections.
                var activeSessions = allSessionsFromManager
                    .Where(s =>
                    {
                        // Filter 1: Must belong to an active (not paused) music piece
                        if (!activeMusicPieceIds.Contains(s.MusicPieceId))
                            return false;

                        // Filter 2: Find the corresponding BarSection and check its lifecycle state
                        var musicPiece = _musicPieces.FirstOrDefault(mp => mp.Id == s.MusicPieceId);
                        var barSection = musicPiece?.BarSections?.FirstOrDefault(bs => bs.Id == s.BarSectionId);

                        // Exclude Inactive sections from calendar display
                        if (barSection != null && barSection.LifecycleState == LifecycleState.Inactive)
                            return false;

                        return true;
                    })
                    .ToList();
                MLLogManager.Instance.Log($"CalendarWindow: {activeSessions.Count} sessions remaining after filtering for active music pieces and non-Inactive sections.", LogLevel.Debug);

                // Step 4: Apply the dropdown filter if a specific music piece is selected.
                if (_selectedMusicPieceId.HasValue)
                {
                    _localScheduledSessionsCache = activeSessions
                        .Where(s => s.MusicPieceId == _selectedMusicPieceId.Value)
                        .ToList();
                    MLLogManager.Instance.Log($"CalendarWindow: Filtered to {_localScheduledSessionsCache.Count} sessions for selected MusicPieceId: {_selectedMusicPieceId.Value}.", LogLevel.Debug);
                }
                else
                {
                    _localScheduledSessionsCache = activeSessions;
                }

                // Step 5: Group the final list of sessions by date for the UI cache.
                _localSessionsByDateCache = _localScheduledSessionsCache
                    .GroupBy(s => s.ScheduledDate.Date)
                    .ToDictionary(g => g.Key, g => g.ToList());
                MLLogManager.Instance.Log($"CalendarWindow: Sessions grouped for UI. {_localSessionsByDateCache.Count} dates with sessions.", LogLevel.Debug);

                // Step 6: Update the calendar display.
                UpdateCalendarUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading scheduled sessions for calendar: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                MLLogManager.Instance.LogError("Error in CalendarWindow.LoadScheduledSessionsAndUpdateCalendar", ex);
            }
        }

        public void RefreshCalendarData()
        {
            // --- START AANPASSING ---
            // Forceer de session manager om zijn data van de schijf te herladen.
            // Dit zorgt ervoor dat we altijd de meest recente planning hebben,
            // vooral na acties zoals 'merge' of 'archive'.
            ScheduledPracticeSessionManager.Instance.ReloadScheduledSessions();
            // --- EINDE AANPASSING ---

            // Haal de actuele data opnieuw op
            _musicPieces = GetAllMusicPieces(); // Deze leest al van schijf (vorige fix)
            LoadMusicPieceFilter();
            LoadScheduledSessionsAndUpdateCalendar(); // Deze zal nu de zojuist herladen sessies gebruiken
        }

        private void AddSessionsToDayCell(Border dayCell, List<ScheduledPracticeSession> sessions)
        {
            if (dayCell.Child is StackPanel cellContent && cellContent.Tag is StackPanel sessionsList)
            {
                sessionsList.Children.Clear(); // Maak de lijst leeg voordat je opnieuw vult

                // Explicitly nullable - dateText may not be found
                TextBlock? dateText = cellContent.Children.Count > 0 && cellContent.Children[0] is TextBlock tb ? tb : null;

                if (sessions.Count > 0 && dateText != null)
                {
                    // Verwijder oude icoon-grid als die bestaat
                    if (cellContent.Children[0] is Grid oldHeaderGrid)
                    {
                        cellContent.Children.RemoveAt(0); // Verwijder oude grid
                        // Zoek de originele dateText binnen de oude grid
                        var originalDateText = oldHeaderGrid.Children.OfType<TextBlock>().FirstOrDefault(tb => int.TryParse(tb.Text, out _));
                        if (originalDateText != null)
                        {
                            cellContent.Children.Insert(0, originalDateText); // Voeg originele dateText terug
                        }
                        else // Fallback als de originele dateText niet gevonden kan worden
                        {
                            TextBlock newDateText = new TextBlock { Text = (dayCell.Tag as DateTime?)?.Day.ToString() ?? "?", FontWeight = FontWeights.Normal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 5) };
                            cellContent.Children.Insert(0, newDateText);
                        }
                        // Update reference - use pattern matching for safer cast
                        dateText = cellContent.Children[0] is TextBlock textBlock ? textBlock : null;
                    }


                    Grid headerGrid = new Grid();
                    headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    cellContent.Children.RemoveAt(0);
                    if (dateText != null)
                    {
                        dateText.HorizontalAlignment = HorizontalAlignment.Right;
                        Grid.SetColumn(dateText, 0);
                        headerGrid.Children.Add(dateText);
                    }

                    TextBlock iconText = new TextBlock
                    {
                        Text = "🗓️", // Kalender icoon
                        FontSize = 12,
                        Margin = new Thickness(5, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        ToolTip = "Click to view sessions"
                    };
                    Grid.SetColumn(iconText, 1);
                    headerGrid.Children.Add(iconText);

                    cellContent.Children.Insert(0, headerGrid);
                }

                var sortedSessions = sessions
                    .OrderBy(s => s.Status?.ToLower() == "completed")
                    .ThenBy(s => s.MusicPieceTitle)
                    .ToList();

                foreach (var session in sortedSessions)
                {
                    bool isCompleted = session.Status?.ToLower() == "completed";
                    Border sessionBorder = new Border
                    {
                        Style = (Style)FindResource("PracticeSessionItemStyle"),
                        Opacity = isCompleted ? 0.6 : 1.0
                    };

                    SolidColorBrush musicPieceBrush = GetMusicPieceColorBrush(session.MusicPieceId);
                    Color backgroundColor = musicPieceBrush.Color;
                    Color sessionColor = Color.FromArgb(180, backgroundColor.R, backgroundColor.G, backgroundColor.B);
                    sessionBorder.Background = new SolidColorBrush(sessionColor);

                    StackPanel sessionContentPanel = new StackPanel(); // Hernoemd om verwarring te voorkomen

                    TextBlock titleText = new TextBlock
                    {
                        Text = session.MusicPieceTitle,
                        FontWeight = FontWeights.SemiBold,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        TextDecorations = isCompleted ? TextDecorations.Strikethrough : null
                    };
                    sessionContentPanel.Children.Add(titleText);

                    TextBlock sectionText = new TextBlock
                    {
                        Text = $"Bars: {session.BarSectionRange}",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85))
                    };
                    sessionContentPanel.Children.Add(sectionText);
                    sessionBorder.Child = sessionContentPanel;
                    sessionsList.Children.Add(sessionBorder);
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private SolidColorBrush GetMusicPieceColorBrush(Guid musicPieceId)
        {
            try
            {
                var musicPiece = _musicPieces.FirstOrDefault(mp => mp.Id == musicPieceId); // Gebruik lokale _musicPieces
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
                MLLogManager.Instance.LogError("Error in GetMusicPieceColorBrush", ex);
                return new SolidColorBrush(Colors.LightGray);
            }
        }

        private void BtnNextMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(1);
            UpdateCalendarUI();
        }

        private void BtnPreviousMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = _currentMonth.AddMonths(-1);
            UpdateCalendarUI();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadScheduledSessionsAndUpdateCalendar();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error in BtnRefresh_Click", ex);
            }
        }

        private void BtnToday_Click(object sender, RoutedEventArgs e)
        {
            _currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            UpdateCalendarUI();
        }

        private void CmbMusicPieceFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // NIEUWE CONTROLE: Voer niets uit als het venster nog aan het initialiseren is.
                if (_isInitializing) return;

                if (CmbMusicPieceFilter.SelectedItem is ComboBoxItem selectedItem)
                {
                    _selectedMusicPieceId = selectedItem.Tag as Guid?;
                    LoadScheduledSessionsAndUpdateCalendar(); // Herlaad en update UI
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error in CmbMusicPieceFilter_SelectionChanged", ex);
            }
        }

        private Border CreateDayCell(DateTime date, bool isToday)
        {
            Border cellBorder = new Border
            {
                Style = (Style)FindResource("CalendarDayCellStyle"),
                Background = isToday ? new SolidColorBrush(Color.FromRgb(232, 240, 254)) :
                            (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) ?
                            new SolidColorBrush(Color.FromRgb(250, 250, 252)) : Brushes.White,
                Tag = date
            };

            StackPanel cellContent = new StackPanel();
            TextBlock dateText = new TextBlock
            {
                Text = date.Day.ToString(),
                FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 0, 5)
            };
            cellContent.Children.Add(dateText);

            ScrollViewer sessionsViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 400
            };

            StackPanel sessionsList = new StackPanel();
            sessionsViewer.Content = sessionsList;
            cellContent.Tag = sessionsList;
            cellContent.Children.Add(sessionsViewer);
            cellBorder.Child = cellContent;
            cellBorder.MouseLeftButtonDown += DayCell_Click;
            cellBorder.Cursor = System.Windows.Input.Cursors.Hand;
            return cellBorder;
        }

        private void DayCell_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border dayCell && dayCell.Tag is DateTime date)
            {
                _musicPieces = GetAllMusicPieces();

                List<ScheduledPracticeSession> daySessions = new List<ScheduledPracticeSession>();
                if (_localSessionsByDateCache.ContainsKey(date))
                {
                    daySessions = _localSessionsByDateCache[date].ToList();
                }

                DaySessionsWindow daySessionsWindow = new DaySessionsWindow(date, daySessions, _musicPieces);
                daySessionsWindow.Owner = this;

                // De complexe 'Closed' event handler is niet meer nodig.
                // De 'Activated' event handler van dit venster zal de UI verversen.

                // Show the window as non-modal, which does not block other windows.
                daySessionsWindow.Show();
            }
        }

        private List<MusicPieceItem> GetAllMusicPieces()
        {
            // --- START VAN DEFINITIEVE FIX ---
            // Deze methode leest nu de muziekstukken rechtstreeks van de schijf,
            // in plaats van te vertrouwen op de in-memory collectie van MainWindow.
            // Dit voorkomt synchronisatie- en timingproblemen (race conditions).

            var musicPieces = new List<MusicPieceItem>();
            bool prevSuppress = AppState.SuppressLifecycleSideEffects;
            AppState.SuppressLifecycleSideEffects = true; // prevent lifecycle side-effects during load
            try
            {
                // Stap 1: Bepaal de correcte map op basis van het actieve profiel.
                string profileFolder = DataPathProvider.GetProfileFolder(ActiveUserSession.ProfileName);
                string musicPiecesFolder = Path.Combine(profileFolder, "MusicPieces");

                if (Directory.Exists(musicPiecesFolder))
                {
                    // Stap 2: Lees alle .json-bestanden in de map.
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
                                // Dit is cruciaal om de kleuren correct weer te geven.
                                if (!string.IsNullOrEmpty(musicPiece.ColorResourceName))
                                {
                                    if (MainWindow.ColorResourceMap.TryGetValue(musicPiece.ColorResourceName, out SolidColorBrush? colorBrush) && colorBrush != null)
                                    {
                                        musicPiece.ColorBrush = colorBrush;
                                    }
                                }
                                musicPieces.Add(musicPiece);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log een fout voor een individueel bestand, maar ga door met de rest.
                            MLLogManager.Instance.LogError($"CalendarWindow: Error loading music piece from {jsonFile}", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("CalendarWindow: A critical error occurred in GetAllMusicPieces while reading from disk", ex);
                MessageBox.Show($"An error occurred while reloading music piece data for the calendar: {ex.Message}", "Data Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                AppState.SuppressLifecycleSideEffects = prevSuppress;
            }

            return musicPieces;
            // --- EINDE VAN DEFINITIEVE FIX ---
        }

        private int GetWeeksInMonth(DateTime date)
        {
            DateTime firstDayOfMonth = new DateTime(date.Year, date.Month, 1);
            int firstDayOffset = ((int)firstDayOfMonth.DayOfWeek + 6) % 7;
            int daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);
            int totalCells = firstDayOffset + daysInMonth;
            return (int)Math.Ceiling(totalCells / 7.0);
        }

        /// <summary>
        /// Refactored method to populate the music piece filter dropdown.
        /// It temporarily detaches the SelectionChanged event to prevent multiple reloads
        /// and robustly preserves the user's selection using the MusicPiece ID.
        /// </summary>
        private void LoadMusicPieceFilter()
        {
            try
            {
                if (CmbMusicPieceFilter == null)
                {
                    MLLogManager.Instance.Log("CmbMusicPieceFilter is null! Cannot load filter.", LogLevel.Warning);
                    return;
                }

                // --- CORRECTIE START: Koppel de event handler tijdelijk los ---
                // This prevents the calendar from reloading multiple times while we update the list.
                CmbMusicPieceFilter.SelectionChanged -= CmbMusicPieceFilter_SelectionChanged;

                // --- REFACTOR: Store the currently selected ID for robust re-selection ---
                Guid? selectedId = (CmbMusicPieceFilter.SelectedItem as ComboBoxItem)?.Tag as Guid?;

                // Get the "All Music Pieces" item to preserve it.
                var allPiecesItem = CmbMusicPieceFilter.Items.Count > 0 ? CmbMusicPieceFilter.Items[0] : null;
                CmbMusicPieceFilter.Items.Clear();
                if (allPiecesItem != null)
                {
                    CmbMusicPieceFilter.Items.Add(allPiecesItem);
                }

                // --- REFACTOR: Populate the filter with a single, clear loop ---
                // Archiveren bestaat niet meer: toon alle stukken (optioneel: je kunt gepauzeerde verbergen als gewenst).
                var activePieces = _musicPieces
                    .OrderBy(p => p.Title);

                foreach (var piece in activePieces)
                {
                    CmbMusicPieceFilter.Items.Add(new ComboBoxItem
                    {
                        Content = piece.Title,
                        Tag = piece.Id
                    });
                }

                // --- REFACTOR: Re-select the previous item using LINQ ---
                var itemToReselect = CmbMusicPieceFilter.Items.OfType<ComboBoxItem>()
                                        .FirstOrDefault(item => item.Tag as Guid? == selectedId);

                if (itemToReselect != null)
                {
                    CmbMusicPieceFilter.SetCurrentValue(Selector.SelectedItemProperty, itemToReselect);
                }
                else if (CmbMusicPieceFilter.Items.Count > 0)
                {
                    // If the previously selected item is gone (e.g., deleted or paused), select the first item.
                    CmbMusicPieceFilter.SetCurrentValue(Selector.SelectedIndexProperty, 0);
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error in LoadMusicPieceFilter", ex);
            }
            finally
            {
                // --- CORRECTIE EIND: Koppel de event handler altijd weer aan ---
                // This guarantees that the user can use the filter again after the setup.
                CmbMusicPieceFilter.SelectionChanged += CmbMusicPieceFilter_SelectionChanged;
            }
        }
        ////////////////////////////////////////////////////////////////////////////////

        private void UpdateCalendarUI()
        {
            try
            {
                if (CalendarGrid == null)
                {
                    MLLogManager.Instance.Log("CalendarGrid is null! The UI will be updated later.", LogLevel.Warning);
                    return;
                }

                TextBlock monthText = this.FindName("TxtCurrentMonth") as TextBlock ?? TxtCurrentMonth;
                if (monthText != null)
                {
                    // --- PROPOSED CHANGE ---
                    // Use the CultureHelper to format the month and year display.
                    // The "Y" specifier automatically selects the correct Year/Month pattern for the active culture.
                    monthText.Text = _currentMonth.ToString("Y", CultureHelper.Current);
                }
                else
                {
                    MLLogManager.Instance.Log("TxtCurrentMonth could not be found - month title not updated", LogLevel.Warning);
                }

                CalendarGrid.Children.Clear();
                CalendarGrid.RowDefinitions.Clear();
                CalendarGrid.ColumnDefinitions.Clear();

                int weeksInMonth = GetWeeksInMonth(_currentMonth);
                for (int i = 0; i < weeksInMonth; i++)
                {
                    CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                }

                DateTime firstDay = new DateTime(_currentMonth.Year, _currentMonth.Month, 1);
                int firstDayOffset = ((int)firstDay.DayOfWeek + 6) % 7; // Monday = 0
                int daysInMonth = DateTime.DaysInMonth(_currentMonth.Year, _currentMonth.Month);
                DateTime today = DateTime.Today;
                int dayCounter = 1;

                for (int week = 0; week < weeksInMonth; week++)
                {
                    // Determine week date range (Mon-Sun) within the visible grid
                    DateTime weekStart = firstDay.AddDays(week * 7 - firstDayOffset);
                    DateTime weekEnd = weekStart.AddDays(6);

                    // Build header text (e.g., "Mon 1 – Sun 7")
                    string headerText = $"{weekStart.ToString("ddd d", CultureHelper.Current)} – {weekEnd.ToString("ddd d", CultureHelper.Current)}";

                    // Create weekly Expander
                    var weekExpander = new Expander
                    {
                        Header = headerText,
                        IsExpanded = (today >= weekStart && today <= weekEnd &&
                                      today.Month == _currentMonth.Month && today.Year == _currentMonth.Year),
                        Margin = new Thickness(0, 4, 0, 4),
                        Background = Brushes.Transparent
                    };

                    // Inner grid for 7 day cells in this week
                    var weekGrid = new Grid();
                    weekGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    for (int c = 0; c < 7; c++)
                    {
                        weekGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    }

                    for (int dow = 0; dow < 7; dow++)
                    {
                        if (week == 0 && dow < firstDayOffset)
                        {
                            // Empty placeholder for days from previous month to keep alignment
                            var placeholder = new Border { Background = Brushes.Transparent, Margin = new Thickness(3) };
                            Grid.SetRow(placeholder, 0);
                            Grid.SetColumn(placeholder, dow);
                            weekGrid.Children.Add(placeholder);
                            continue;
                        }

                        if (dayCounter > daysInMonth)
                        {
                            // Empty placeholder after month ends
                            var placeholder = new Border { Background = Brushes.Transparent, Margin = new Thickness(3) };
                            Grid.SetRow(placeholder, 0);
                            Grid.SetColumn(placeholder, dow);
                            weekGrid.Children.Add(placeholder);
                            continue;
                        }

                        DateTime currentDate = new DateTime(_currentMonth.Year, _currentMonth.Month, dayCounter);
                        Border dayCell = CreateDayCell(currentDate, currentDate == today);

                        if (currentDate == today)
                        {
                            dayCell.ToolTip = "Extra oefening op dezelfde dag start je via MainWindow ➜ Extra practice.";
                        }

                        if (_localSessionsByDateCache != null && _localSessionsByDateCache.ContainsKey(currentDate))
                        {
                            AddSessionsToDayCell(dayCell, _localSessionsByDateCache[currentDate]);
                        }

                        Grid.SetRow(dayCell, 0);
                        Grid.SetColumn(dayCell, dow);
                        weekGrid.Children.Add(dayCell);
                        dayCounter++;
                    }

                    weekExpander.Content = weekGrid;
                    Grid.SetRow(weekExpander, week);
                    CalendarGrid.Children.Add(weekExpander);
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error in UpdateCalendarUI", ex);
            }
        }
    }
}
