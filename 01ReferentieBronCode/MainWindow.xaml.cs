using ModusPractica.Infrastructure;
using ModusPractica.ViewModels;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Line = System.Windows.Shapes.Line;
using Rectangle = System.Windows.Shapes.Rectangle;
using ModusPractica.Views;

namespace ModusPractica
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, System.ComponentModel.INotifyPropertyChanged
    {
        // NEW: Feature flag for manual start date selection
        private bool EnableManualStartDateForNewSections = false;

        // Variable to keep track of the currently selected note
        private NoteEntry? _currentNote;

        private bool _isNotesInitializing = false;

        // Notes ViewModel
        private NotesViewModel _notesViewModel;
        public NotesViewModel NotesViewModel
        {
            get { return _notesViewModel; }
            set
            {
                _notesViewModel = value;
                OnPropertyChanged(nameof(NotesViewModel));
            }
        }

        // Path to the folder for saving music pieces
        private string? _musicPiecesFolder;

        // Variables for sorting functionality
        private bool _isSortAscending = true;
        private string _currentSortProperty = "Title"; // Default sort by title
        private CalendarWindow? _calendarWindowInstance;

        // NEW: Flags to ensure data for tabs is loaded only once ("lazy loading").

        private PracticeTimerWindow? _practiceTimerWindow;

        // ViewModel-like property to hold the new BarSection being created
        private BarSection _newBarSection = new BarSection();
        public BarSection NewBarSection
        {
            get => _newBarSection;
            private set
            {
                if (!Equals(_newBarSection, value))
                {
                    _newBarSection = value;
                    OnPropertyChanged(nameof(NewBarSection));
                }
            }
        }

        // Scheduled sessions: watcher + in-memory collection for grid/list binding
        private FileSystemWatcher? _scheduledWatcher;
        private readonly object _scheduledWatcherLock = new object();
        private DateTime _lastScheduledChange = DateTime.MinValue;
        private string? _scheduledSessionsFile; // path to scheduled_sessions.json
        private ObservableCollection<ScheduledPracticeSession> _plannedSessions = new ObservableCollection<ScheduledPracticeSession>();


        public MainWindow()
        {
            InitializeComponent();

            // Ensure nullable fields are initialized or marked as nullable
            _calendarWindowInstance = null;
            // ML Debug Window removed
            _practiceTimerWindow = null;
            _notesViewModel = new NotesViewModel();

            // Attach event handlers
            this.ContentRendered += MainWindow_ContentRendered;
            this.Closing += MainWindow_Closing;

            // Set culture for UI
            this.Language = XmlLanguage.GetLanguage(CultureHelper.Current.IetfLanguageTag);

            // ML Manager removed - no longer using ML training/scheduling

            this.Loaded += MainWindow_Loaded;
            this.Activated += MainWindow_Activated;

            // Prepare a fresh NewBarSection for bindings (StartDate defaults to Today in model)
            PrepareNewBarSection();

            // Initialize the color map
            InitializeColorResourceMap();

            TxtTotalMusicPieces.Text = "Loading data...";

            // Initialize the static music pieces collection
            MusicPieces = new ObservableCollection<MusicPieceItem>();

            // Initialize the Notes ViewModel
            NotesViewModel = new NotesViewModel();

            // Set the DataContext for the window (for databinding)
            this.DataContext = this;

            // Set the ItemsSource for the ListView
            LvMusicPieces.ItemsSource = MusicPieces;

            TxtSearch.TextChanged += TxtSearch_TextChanged;

            // ML training and logging removed

            // Subscribe to history changes globally so the Today's Practice card refreshes immediately
            PracticeHistoryManager.Instance.HistoryChanged += (_, __) =>
            {
                try { Dispatcher.Invoke(UpdateTodaysPracticeTimeDisplay); } catch { }
                // Also refresh charts summary if practice history changes
                try { Dispatcher.Invoke(UpdatePracticeChartsSummary); } catch { }
            };

            // YouTube button remains enabled; click handler shows guidance if no link is set
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

        // Initialize or reset the NewBarSection with defaults
        public void PrepareNewBarSection()
        {
            NewBarSection = new BarSection();
        }

        private void SetupScheduledSessionsWatcher()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_scheduledSessionsFile))
                    return;

                string folder = Path.GetDirectoryName(_scheduledSessionsFile);
                string file = Path.GetFileName(_scheduledSessionsFile);
                if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(file))
                    return;

                _scheduledWatcher = new FileSystemWatcher(folder, file)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
                };

                _scheduledWatcher.Changed += OnScheduledSessionsFileChanged;
                _scheduledWatcher.Created += OnScheduledSessionsFileChanged;
                _scheduledWatcher.Renamed += OnScheduledSessionsFileChanged;
                _scheduledWatcher.EnableRaisingEvents = true;

                MLLogManager.Instance.Log("MainWindow: File watcher attached to scheduled_sessions.json", LogLevel.Info);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("MainWindow: Failed to setup scheduled sessions watcher.", ex);
            }
        }

        private void OnScheduledSessionsFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                lock (_scheduledWatcherLock)
                {
                    var now = DateTime.UtcNow;
                    // Increased debounce time from 350ms to 500ms to prevent rapid successive file change handling
                    if ((now - _lastScheduledChange).TotalMilliseconds < 500)
                        return;
                    _lastScheduledChange = now;
                }

                // Increased delay from 200ms to 300ms to ensure file is fully written before we read it
                Task.Delay(300).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            // Kritisch voor performance: Voorkom ping-pong effecten door eerst ScheduledPracticeSessionManager
                            // zijn interne data te laten herladen voordat we de data synchroniseren naar BarSection objecten
                            ScheduledPracticeSessionManager.Instance.ReloadScheduledSessions();

                            // AANGEPAST: Dit is nu één-richting synchronisatie (scheduled sessions ? BarSection.NextDueDate)
                            // We synchroniseren alleen de NextDueDate van BarSection objecten vanuit de scheduled sessions
                            SynchronizeAllNextDueDates();

                            // Then reload the grid data for UI display
                            ReloadScheduledGridData();
                        }
                        catch (Exception exReload)
                        {
                            MLLogManager.Instance.LogError("MainWindow: error refreshing grid after scheduled file change.", exReload);
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("MainWindow: error in OnScheduledSessionsFileChanged.", ex);
            }
        }

        private void ReloadScheduledGridData()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_scheduledSessionsFile) || !File.Exists(_scheduledSessionsFile))
                {
                    _plannedSessions.Clear();
                    return;
                }

                string json = File.ReadAllText(_scheduledSessionsFile);
                var sessions = JsonSerializer.Deserialize<List<ScheduledPracticeSession>>(json) ?? new List<ScheduledPracticeSession>();

                sessions = sessions
                    .OrderBy(s => s.ScheduledDate)
                    .ThenBy(s => s.MusicPieceTitle)
                    .ToList();

                _plannedSessions.Clear();
                foreach (var s in sessions)
                    _plannedSessions.Add(s);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("MainWindow: error reloading scheduled sessions for grid.", ex);
            }
        }

        // --- MISSING METHODS START ---

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySortingAndFiltering();
        }

        /// <summary>
        /// Formats a bar range to ensure two-digit numbers (e.g., "1-2" becomes "01-02").
        /// </summary>
        private string FormatBarRange(string barRange)
        {
            if (string.IsNullOrWhiteSpace(barRange))
                return barRange;

            // Split by dash
            var parts = barRange.Split('-');
            if (parts.Length != 2)
                return barRange; // Invalid format, return as-is

            // Parse the first part (should be a number)
            if (!int.TryParse(parts[0].Trim(), out int start))
                return barRange; // If parsing fails, return original

            // Second part may contain number followed by optional text (e.g., "04" or "04 RH")
            string secondPart = parts[1].Trim();
            string[] secondPartTokens = secondPart.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);

            if (secondPartTokens.Length == 0 || !int.TryParse(secondPartTokens[0].Trim(), out int end))
                return barRange; // If parsing fails, return original

            // Format as two-digit numbers, preserve optional text suffix
            string formattedRange = $"{start:D2}-{end:D2}";

            // If there was text after the second number, append it (with a space)
            if (secondPartTokens.Length > 1)
            {
                formattedRange += " " + secondPartTokens[1].Trim();
            }

            return formattedRange;
        }

        /// <summary>
        /// Validates basic bar range format (e.g., has two numbers separated by dash).
        /// </summary>
        private bool ValidateBarRangeFormat(string barRange, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(barRange))
            {
                errorMessage = "Geef een maatbereik in (bijv. 01-02 of 01-02 RH).";
                return false;
            }

            // Split on dash first to get the main parts
            var dashParts = barRange.Split('-');
            if (dashParts.Length != 2)
            {
                errorMessage = "Gebruik het formaat: XX-YY (bijv. 01-02 of 10-11 RH).";
                return false;
            }

            // First part should be a number
            if (!int.TryParse(dashParts[0].Trim(), out int start))
            {
                errorMessage = "Het eerste getal in het maatbereik is ongeldig.";
                return false;
            }

            // Second part may contain a number followed by optional text (e.g., "04" or "04 RH")
            string secondPart = dashParts[1].Trim();
            string[] secondPartTokens = secondPart.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);

            if (secondPartTokens.Length == 0 || !int.TryParse(secondPartTokens[0].Trim(), out int end))
            {
                errorMessage = "Het tweede getal in het maatbereik is ongeldig.";
                return false;
            }

            // Check if numbers are positive
            if (start <= 0 || end <= 0)
            {
                errorMessage = "Maatnummers moeten positief zijn.";
                return false;
            }

            // Note: We allow start > end for cases like "page 9 - exercise 5" (09-05)
            // Note: Optional text after the second number is allowed (e.g., "01-04 RH")

            return true;
        }

        private void AddBarSectionFromInputs()
        {
            try
            {
                // Zorg dat er een muziekstuk geselecteerd is
                if (!(LvMusicPieces.SelectedItem is MusicPieceItem selectedMusicPiece))
                {
                    MessageBox.Show("Selecteer eerst een muziekstuk.", "Geen selectie", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Lees invoer
                string barRange = txtBarNumber?.Text?.Trim() ?? string.Empty; // dit veld accepteert ook een bereik zoals "1-5"
                string description = txtBarDescription?.Text?.Trim() ?? string.Empty;

                // Validate and auto-format bar range
                if (!ValidateBarRangeFormat(barRange, out string errorMessage))
                {
                    MessageBox.Show(errorMessage, "Validatie", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtBarNumber?.Focus();
                    return;
                }

                // Auto-format to two-digit format
                barRange = FormatBarRange(barRange);

                // Controleer op duplicaat bereik in dit muziekstuk
                bool exists = selectedMusicPiece.BarSections?.Any(bs => bs.BarRange.Equals(barRange, StringComparison.OrdinalIgnoreCase)) == true;
                if (exists)
                {
                    var result = MessageBox.Show(
                        $"Het maatbereik '{barRange}' bestaat al. Toch toevoegen?",
                        "Dubbele sectie",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                        return;
                }

                // Werk via NewBarSection zodat StartDate/NextDueDate en defaults goed staan
                var newSection = NewBarSection ?? new BarSection();
                newSection.BarRange = barRange;
                newSection.Description = description;

                // Lees doel herhalingen uit de combobox indien aanwezig
                if (CbNewBarSectionRepetitions?.SelectedItem is ComboBoxItem sel && int.TryParse(sel.Content?.ToString(), out var reps))
                {
                    newSection.TargetRepetitions = reps;
                }

                // NEW: In automatische modus direct voor vandaag inplannen (i.p.v. morgen)
                DateTime scheduledDate;
                if (!EnableManualStartDateForNewSections)
                {
                    // Automatic mode: schedule for today
                    var today = DateHelper.LocalToday();
                    scheduledDate = today;
                    MLLogManager.Instance.Log($"NewSection scheduling: mode=auto, due={scheduledDate:yyyy-MM-dd} (LocalToday)", LogLevel.Info);
                }
                else
                {
                    // Manual mode: use DatePicker value
                    scheduledDate = DpNewBarSectionStartDate?.SelectedDate ?? DateTime.Today;
                    MLLogManager.Instance.Log($"NewSection scheduling: mode=manual, due={scheduledDate:yyyy-MM-dd}", LogLevel.Info);
                }

                // Set the dates for the new section
                newSection.StartDate = scheduledDate;
                newSection.NextDueDate = scheduledDate;

                // SAFETY RAIL: Ensure we never schedule in the past (today is allowed)
                if (scheduledDate.Date < DateHelper.LocalToday().Date)
                {
                    scheduledDate = DateHelper.LocalToday().AddDays(1);
                    newSection.NextDueDate = scheduledDate;
                    MLLogManager.Instance.Log($"SAFETY RAIL: Corrected scheduling from {newSection.StartDate:yyyy-MM-dd} to {scheduledDate:yyyy-MM-dd} (never schedule in the past)", LogLevel.Warning);
                }

                if (selectedMusicPiece.BarSections == null)
                    selectedMusicPiece.BarSections = new ObservableCollection<BarSection>();

                // Set ParentMusicPieceId for reliable parent lookup
                newSection.ParentMusicPieceId = selectedMusicPiece.Id;

                selectedMusicPiece.BarSections.Add(newSection);

                // Sort the BarSections after adding the new one
                selectedMusicPiece.BarSections = new ObservableCollection<BarSection>(
                    SortBarSections(selectedMusicPiece.BarSections)
                );

                // Pak de datum rechtstreeks uit de DatePicker om focus/binding-issues te vermijden
                var startDate = DpNewBarSectionStartDate?.SelectedDate ?? DateTime.Today;
                newSection.StartDate = startDate;

                // Forceer dat de DataGrid meteen de nieuwe rij toont
                DgBarSections.SetCurrentValue(ItemsControl.ItemsSourceProperty, selectedMusicPiece.BarSections); // (her)bind expliciet
                DgBarSections.Items.Refresh();
                DgBarSections.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedItemProperty, newSection);          // highlight de nieuwe sectie
                DgBarSections.ScrollIntoView(newSection);         // scroll in beeld
                DgBarSections.UpdateLayout();

                // Opslaan van het stuk
                SaveMusicPiece(selectedMusicPiece);

                // FIXED: Use bypass to avoid preserve context blocking for new section creation
                try
                {
                    ScheduledPracticeSession scheduledSession = new ScheduledPracticeSession
                    {
                        Id = Guid.NewGuid(),
                        MusicPieceId = selectedMusicPiece.Id,
                        MusicPieceTitle = selectedMusicPiece.Title,
                        BarSectionId = newSection.Id,
                        BarSectionRange = newSection.BarRange,
                        ScheduledDate = scheduledDate,
                        EstimatedDuration = PracticeUtils.GetEstimatedDurationAsTimeSpan(newSection.Id),
                        Difficulty = newSection.Difficulty,
                        Status = "Planned"
                    };

                    // Use bypass to ensure scheduling works during any preserve context
                    ScheduledPracticeSessionManager.Instance.AddScheduledSessionWithBypass(scheduledSession, "New Section Creation");

                    // Signaleer aan andere vensters (zoals CalendarWindow) dat data gewijzigd is.
                    AppState.MusicDataChanged = true;

                    // Vernieuw de DataGrid om de nieuwe maatsectie direct weer te geven
                    this.Dispatcher.Invoke(() => RefreshBarSectionsGrid());

                    MLLogManager.Instance.Log($"[BYPASS PRESERVE] New Section Creation - Adding session for {newSection.Id}", LogLevel.Info);
                    MLLogManager.Instance.Log($"Created new section '{barRange}' scheduled for {scheduledDate:yyyy-MM-dd}", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance.LogError("Error scheduling new bar section during AddBarSectionFromInputs.", ex);
                }

                // UI opschonen en focus terug op eerste veld
                txtBarNumber.SetCurrentValue(TextBox.TextProperty, string.Empty);
                txtBarDescription.SetCurrentValue(TextBox.TextProperty, string.Empty);

                // Zet focus en selecteer de inhoud zodat direct nieuw bereik ingevoerd kan worden
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    txtBarNumber.Focus();
                    txtBarNumber.SelectAll();
                    Keyboard.Focus(txtBarNumber);
                }), DispatcherPriority.ContextIdle);

                // Reset het formulier naar een nieuwe sectie (StartDate weer vandaag)
                PrepareNewBarSection();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Fout bij toevoegen maatsectie via inputs.", ex);
                MessageBox.Show($"Er ging iets mis bij het toevoegen van de maatsectie: {ex.Message}",
                    "Fout", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ========================= Practice Charts Tab =========================
        private void CbChartsMusicPiece_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                LbChartsBarSections.SetCurrentValue(ItemsControl.ItemsSourceProperty, null);
                if (CbChartsMusicPiece?.SelectedItem is MusicPieceItem piece && piece.BarSections != null)
                {
                    // Order sections by natural bar range (string order kept as-is)
                    LbChartsBarSections.SetCurrentValue(ItemsControl.ItemsSourceProperty, piece.BarSections);
                    if (piece.BarSections.Count > 0)
                        LbChartsBarSections.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedIndexProperty, 0);
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error updating bar sections for Practice Charts", ex);
            }
        }

        private void LbChartsBarSections_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (LbChartsBarSections?.SelectedItem is BarSection section)
                {
                    PracticeChartsControl.SetCurrentValue(SuccessRatioTrendChartControl.BarSectionProperty, section);
                    UpdatePracticeChartsSummary();
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error updating chart control for selected section", ex);
            }
        }

        private void CbChartsMaxSessions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (PracticeChartsControl == null) return;
                if (CbChartsMaxSessions?.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out int value))
                {
                    PracticeChartsControl.SetCurrentValue(SuccessRatioTrendChartControl.MaxSessionsProperty, Math.Clamp(value, 3, 10));
                    UpdatePracticeChartsSummary();
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error changing max sessions for charts", ex);
            }
        }

        private void ChkChartsIncludeDeleted_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PracticeChartsControl == null) return;
                var includeDeleted = (sender as CheckBox)?.IsChecked == true;
                PracticeChartsControl.SetCurrentValue(SuccessRatioTrendChartControl.IncludeDeletedProperty, includeDeleted);
                UpdatePracticeChartsSummary();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error toggling include deleted in charts", ex);
            }
        }

        private void ChkChartsTempoOverlay_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PracticeChartsControl == null) return;
                bool enabled = (sender as CheckBox)?.IsChecked == true;
                PracticeChartsControl.SetCurrentValue(SuccessRatioTrendChartControl.ShowTempoOverlayProperty, enabled);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error toggling tempo overlay in charts", ex);
            }
        }

        private void UpdatePracticeChartsSummary()
        {
            try
            {
                if (LbChartsBarSections?.SelectedItem is not BarSection section)
                {
                    TxtChartsCurrentRatio.SetCurrentValue(TextBlock.TextProperty, "");
                    TxtChartsAvg7.SetCurrentValue(TextBlock.TextProperty, "");
                    TxtChartsZone.SetCurrentValue(TextBlock.TextProperty, "");
                    return;
                }

                var all = PracticeHistoryManager.Instance.GetHistoryForBarSection(section.Id).Where(h => PracticeChartsControl.IncludeDeleted || !h.IsDeleted).OrderByDescending(h => h.Date).ToList();

                // Current ratio: laatste sessie
                double currentRatio = 0.0;
                if (all.Count > 0)
                {
                    var last = all.First();
                    int total = last.Repetitions + last.TotalFailures;
                    currentRatio = total > 0 ? (double)last.Repetitions / total : 0.0;
                }

                // Rolling avg (7 of gekozen MaxSessions)
                int window = PracticeChartsControl?.MaxSessions ?? 7;
                int take = Math.Min(window, all.Count);
                double avg7 = 0.0;
                if (take > 0)
                {
                    int reps = all.Take(take).Sum(s => s.Repetitions);
                    int fails = all.Take(take).Sum(s => s.TotalFailures);
                    int tot = reps + fails;
                    avg7 = tot > 0 ? (double)reps / tot : 0.0;
                }

                string zone = PracticeHistory.GetLearningZoneFromRatio(avg7);

                TxtChartsCurrentRatio.SetCurrentValue(TextBlock.TextProperty, currentRatio.ToString("P0"));
                TxtChartsAvg7.SetCurrentValue(TextBlock.TextProperty, avg7.ToString("P0"));
                TxtChartsZone.SetCurrentValue(TextBlock.TextProperty, PracticeHistory.GetLearningZoneDisplayLabel(zone));
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error updating charts summary", ex);
            }
        }

        private void BarSectionInput_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true; // voorkomt systeem-‘ding’ geluid en dubbele events
                    AddBarSectionFromInputs();
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Fout in BarSectionInput_KeyDown.", ex);
            }
        }

        /// <summary>
        /// Auto-formats the bar range to two-digit format when the TextBox loses focus.
        /// </summary>
        private void TxtBarNumber_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is TextBox textBox)
                {
                    string input = textBox.Text?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(input))
                    {
                        // Try to format the input
                        string formatted = FormatBarRange(input);
                        if (formatted != input)
                        {
                            textBox.Text = formatted;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Fout in TxtBarNumber_LostFocus.", ex);
            }
        }

        private void AddBarSection_Click(object sender, RoutedEventArgs e)
        {
            AddBarSectionFromInputs();
        }


        private void ApplySortingAndFiltering()
        {
            try
            {
                if (MusicPieces == null) return;

                var filteredAndSorted = MusicPieces.AsEnumerable();

                // Apply search filter
                string searchText = TxtSearch?.Text?.ToLower() ?? "";
                if (!string.IsNullOrEmpty(searchText))
                {
                    filteredAndSorted = filteredAndSorted.Where(mp =>
                        (mp.Title?.ToLower().Contains(searchText) == true ||
                         mp.Composer?.ToLower().Contains(searchText) == true));
                }

                // Apply sorting
                filteredAndSorted = _currentSortProperty switch
                {
                    "Title" => _isSortAscending ? filteredAndSorted.OrderBy(mp => mp.Title) : filteredAndSorted.OrderByDescending(mp => mp.Title),
                    "Composer" => _isSortAscending ? filteredAndSorted.OrderBy(mp => mp.Composer) : filteredAndSorted.OrderByDescending(mp => mp.Composer),
                    "Progress" => _isSortAscending ? filteredAndSorted.OrderBy(mp => mp.Progress) : filteredAndSorted.OrderByDescending(mp => mp.Progress),
                    "CreationDate" => _isSortAscending ? filteredAndSorted.OrderBy(mp => mp.CreationDate) : filteredAndSorted.OrderByDescending(mp => mp.CreationDate),
                    "Color" => _isSortAscending
                        ? filteredAndSorted.OrderBy(mp => mp.ColorResourceName)
                        : filteredAndSorted.OrderByDescending(mp => mp.ColorResourceName),
                    _ => filteredAndSorted.OrderBy(mp => mp.Title)
                };

                // Update the ListView
                var result = filteredAndSorted.ToList();
                LvMusicPieces.SetCurrentValue(ItemsControl.ItemsSourceProperty, result);

                // Update status
                TxtTotalMusicPieces.SetCurrentValue(TextBlock.TextProperty, $"Ready | {result.Count} pieces");
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error in ApplySortingAndFiltering", ex);
            }
        }

        private void MakeBarSectionColumnsReadOnly()
        {
            try
            {
                if (DgBarSections?.Columns != null)
                {
                    foreach (var column in DgBarSections.Columns)
                    {
                        if (column.Header?.ToString() == "Bar Range" ||
                            column.Header?.ToString() == "Description" ||
                            column.Header?.ToString() == "Target Repetitions")
                        {
                            column.IsReadOnly = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error making bar section columns read-only", ex);
            }
        }

        public void SaveMusicPiece(MusicPieceItem musicPiece)
        {
            try
            {
                if (musicPiece == null || string.IsNullOrEmpty(_musicPiecesFolder))
                    return;

                string newFilePath = musicPiece.GetFilePath(_musicPiecesFolder);

                // Check for and remove old files with different titles but same ID
                CleanupOldMusicPieceFiles(musicPiece.Id, newFilePath);

                string jsonContent = JsonSerializer.Serialize(musicPiece, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(newFilePath, jsonContent);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving music piece '{musicPiece?.Title}': {ex.Message}",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                MLLogManager.Instance.LogError($"Error saving music piece {musicPiece?.Title}", ex);
            }
        }

        /// <summary>
        /// Removes old music piece files with the same ID but different titles to prevent duplicates
        /// </summary>
        private void CleanupOldMusicPieceFiles(Guid musicPieceId, string currentFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(_musicPiecesFolder) || !Directory.Exists(_musicPiecesFolder))
                    return;

                string idPrefix = musicPieceId.ToString() + "_";
                string[] existingFiles = Directory.GetFiles(_musicPiecesFolder, idPrefix + "*.json");

                foreach (string file in existingFiles)
                {
                    // Don't delete the current file we're about to save
                    if (!string.Equals(file, currentFilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            File.Delete(file);
                            MLLogManager.Instance.Log(
                                $"Deleted old music piece file: '{Path.GetFileName(file)}'",
                                LogLevel.Info);
                        }
                        catch (Exception ex)
                        {
                            MLLogManager.Instance.LogError(
                                $"Could not delete old music piece file: '{file}'", ex);
                        }
                    }
                }

                // Also check for legacy files with just the ID (no title)
                string legacyFile = Path.Combine(_musicPiecesFolder, $"{musicPieceId}.json");
                if (File.Exists(legacyFile) &&
                    !string.Equals(legacyFile, currentFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Delete(legacyFile);
                        MLLogManager.Instance.Log(
                            $"Deleted legacy music piece file: '{Path.GetFileName(legacyFile)}'",
                            LogLevel.Info);
                    }
                    catch (Exception ex)
                    {
                        MLLogManager.Instance.LogError(
                            $"Could not delete legacy music piece file: '{legacyFile}'", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error during music piece file cleanup", ex);
            }
        }

        public void SaveAllMusicPieces()
        {
            try
            {
                if (MusicPieces == null) return;

                foreach (var musicPiece in MusicPieces)
                {
                    SaveMusicPiece(musicPiece);
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error saving all music pieces", ex);
            }
        }

        private void RemoveDuplicateMusicPieces()
        {
            try
            {
                if (MusicPieces == null) return;

                var duplicates = MusicPieces
                    .GroupBy(mp => mp.Id)
                    .Where(g => g.Count() > 1)
                    .SelectMany(g => g.Skip(1))
                    .ToList();

                foreach (var duplicate in duplicates)
                {
                    MusicPieces.Remove(duplicate);
                }

                if (duplicates.Count > 0)
                {
                    MLLogManager.Instance.Log($"Removed {duplicates.Count} duplicate music pieces", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error removing duplicate music pieces", ex);
            }
        }

        /// <summary>
        /// Vernieuwt de DataGrid met BarSections om wijzigingen direct weer te geven
        /// </summary>
        private void RefreshBarSectionsGrid()
        {
            if (DgBarSections != null && DgBarSections.ItemsSource != null)
            {
                // Re-sort the sections when refreshing
                if (LvMusicPieces.SelectedItem is MusicPieceItem selectedMusicPiece && selectedMusicPiece.BarSections != null)
                {
                    DgBarSections.ItemsSource = new ObservableCollection<BarSection>(
                        SortBarSections(selectedMusicPiece.BarSections)
                    );
                }
                DgBarSections.Items.Refresh();
            }
        }

        private (int start, int end)? ParseBarRange(string barRange)
        {
            try
            {
                if (string.IsNullOrEmpty(barRange)) return null;

                barRange = barRange.Trim();

                // Handle single bar (e.g., "5")
                if (int.TryParse(barRange, out int singleBar))
                {
                    return (singleBar, singleBar);
                }

                // Handle range (e.g., "1-8" or "01-04 RH")
                var parts = barRange.Split('-');
                if (parts.Length == 2)
                {
                    // First part should be a number
                    if (!int.TryParse(parts[0].Trim(), out int start))
                        return null;

                    // Second part may contain number followed by optional text (e.g., "04" or "04 RH")
                    // Extract just the numeric part for sorting purposes
                    string secondPart = parts[1].Trim();
                    string[] secondPartTokens = secondPart.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);

                    if (secondPartTokens.Length > 0 && int.TryParse(secondPartTokens[0].Trim(), out int end))
                    {
                        return (start, end);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"Error parsing bar range '{barRange}'.", ex);
                return null;
            }
        }

        /// <summary>
        /// Sorts bar sections by numeric range first, then by text suffix (case-insensitive).
        /// Example order: 01-04, 01-04 LH, 01-04 RH, 01-08, 02-04
        /// </summary>
        private IEnumerable<BarSection> SortBarSections(IEnumerable<BarSection> sections)
        {
            return sections
                .OrderBy(s => ParseBarRange(s.BarRange)?.start ?? 0)
                .ThenBy(s => ParseBarRange(s.BarRange)?.end ?? 0)
                .ThenBy(s => s.BarRange?.ToLowerInvariant() ?? string.Empty); // Case-insensitive text sort
        }

        private string GetHardestDifficulty(IEnumerable<string> difficulties)
        {
            var difficultyOrder = new Dictionary<string, int>
            {
                { "Easy", 1 },
                { "Average", 2 },
                { "Difficult", 3 },
                { "Mastered", 4 }
            };

            return difficulties
                .Where(d => !string.IsNullOrEmpty(d))
                .OrderByDescending(d => difficultyOrder.GetValueOrDefault(d, 0))
                .FirstOrDefault() ?? "Average";
        }

        // Event handlers for XAML elements
        private void MenuSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsWindow = new SettingsWindow { Owner = this };
                settingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error opening settings window", ex);
                MessageBox.Show("Settings window is not yet implemented.", "Not Implemented",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CbSortBy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (CbSortBy?.SelectedItem is ComboBoxItem selectedItem)
                {
                    _currentSortProperty = selectedItem.Content.ToString();
                    ApplySortingAndFiltering();
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error in sort selection changed", ex);
            }
        }

        private void BtnSortDirection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _isSortAscending = !_isSortAscending;
                ApplySortingAndFiltering();

                // Update button text
                if (sender is Button btn)
                {
                    btn.Content = _isSortAscending ? "?" : "?";
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error changing sort direction", ex);
            }
        }

        private void SetColor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // AANPASSING: De sender is een MenuItem, geen Button.
                if (sender is MenuItem menuItem &&
                    menuItem.Tag is string colorName &&
                    LvMusicPieces.SelectedItem is MusicPieceItem selectedMusicPiece)
                {
                    if (ColorResourceMap.TryGetValue(colorName, out SolidColorBrush colorBrush))
                    {
                        selectedMusicPiece.ColorBrush = MakeColorTransparent(colorBrush, 0.5) as SolidColorBrush;
                        selectedMusicPiece.ColorResourceName = colorName;
                        SaveMusicPiece(selectedMusicPiece);
                        LvMusicPieces.Items.Refresh(); // Vernieuw de lijstweergave direct
                    }
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error setting color", ex);
            }
        }

        private void PauseMusicPiece_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (LvMusicPieces.SelectedItem is MusicPieceItem selectedMusicPiece)
                {
                    // Open het dialoogvenster om de pauzedatum te selecteren
                    PauseMusicPieceWindow pauseWindow = new PauseMusicPieceWindow(selectedMusicPiece) { Owner = this };
                    bool? result = pauseWindow.ShowDialog();

                    if (result == true)
                    {
                        // Sla de wijzigingen op (pauzedatum is ingesteld in het dialoogvenster)
                        SaveMusicPiece(selectedMusicPiece);

                        // Vernieuw de UI om de status "PAUSED" direct weer te geven
                        LvMusicPieces.Items.Refresh();

                        // Informeer andere vensters (zoals de kalender) over de wijziging
                        AppState.MusicDataChanged = true;

                        MLLogManager.Instance.Log($"Music piece '{selectedMusicPiece.Title}' paused until {selectedMusicPiece.PauseUntilDate:d}", LogLevel.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error during custom pause operation", ex);
            }
        }

        private void ResumeMusicPiece_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (LvMusicPieces.SelectedItem is MusicPieceItem selectedMusicPiece)
                {
                    selectedMusicPiece.IsPaused = false;
                    selectedMusicPiece.PauseUntilDate = null;
                    SaveMusicPiece(selectedMusicPiece);

                    // Vernieuw de UI om de status "PAUSED" direct te verbergen
                    LvMusicPieces.Items.Refresh();

                    // Informeer andere vensters (zoals de kalender) dat dit stuk weer actief is
                    AppState.MusicDataChanged = true;

                    MessageBox.Show($"'{selectedMusicPiece.Title}' has been resumed.",
                        "Music Piece Resumed", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error resuming music piece", ex);
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (e.Source is TabControl tabControl && tabControl.SelectedItem is TabItem selectedTab)
                {
                    // TabAIPlanner removed - AI Planner tab no longer exists
                    // TabMLDebug removed - ML debug tab no longer exists
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error in tab selection changed", ex);
            }
        }



        private void DgBarSections_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            try
            {
                // Extra beveiliging: wijzigingen aan datum-kolommen annuleren
                if (e.Column.Header.ToString() == "Last Practice" || e.Column.Header.ToString() == "Next Due")
                {
                    e.Cancel = true;
                    return;
                }

                // Validate and format BarRange column
                if (e.Column.Header.ToString() == "Bar Section" && e.EditAction == DataGridEditAction.Commit)
                {
                    if (e.EditingElement is TextBox textBox)
                    {
                        string input = textBox.Text?.Trim() ?? string.Empty;

                        // Validate basic format (only check for valid numbers and format)
                        if (!ValidateBarRangeFormat(input, out string errorMessage))
                        {
                            MessageBox.Show(errorMessage, "Validatie", MessageBoxButton.OK, MessageBoxImage.Warning);
                            e.Cancel = true;
                            return;
                        }

                        // Auto-format to two-digit format (no warning, just format)
                        textBox.Text = FormatBarRange(input);
                    }
                }

                if (sender is DataGrid dg && e.EditAction == DataGridEditAction.Commit)
                {
                    // Voer nabehandeling uit nádat WPF de commit heeft afgerond
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (LvMusicPieces?.SelectedItem is MusicPieceItem selectedPiece)
                        {
                            selectedPiece.UpdateProgress();
                        }

                        dg.Items.Refresh();
                        LvMusicPieces?.Items.Refresh();
                    }), DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error in cell edit ending", ex);
            }
        }

        private void DgBarSections_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Removed merge button logic as the button has been removed
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error in bar sections selection changed", ex);
            }
        }

        // Deze methoden zijn niet meer nodig omdat we nu databinding gebruiken
        // private void TxtNoteTitle_TextChanged(object sender, TextChangedEventArgs e)
        // {
        //     try
        //     {
        //         if (!_isNotesInitializing && _currentNote != null && sender is TextBox textBox)
        //         {
        //             _currentNote.Title = textBox.Text;
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         MLLogManager.Instance.LogError("Error in note title text changed", ex);
        //     }
        // }

        // private void TxtNoteContent_TextChanged(object sender, TextChangedEventArgs e)
        // {
        //     try
        //     {
        //         if (!_isNotesInitializing && _currentNote != null && sender is TextBox textBox)
        //         {
        //             _currentNote.Content = textBox.Text;
        //         }
        //     }
        //     catch (Exception ex)
        //     {
        //         MLLogManager.Instance.LogError("Error in note content text changed", ex);
        //     }
        // }

        // ML logging and debug UI removed

        // --- MISSING METHODS END ---

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // NEW, MORE ROBUST APPROACH:
            // Force the entire application to shut down. This ensures that all
            // open windows, including the timer, are guaranteed to close.
            Application.Current.Shutdown();
        }



        /// <summary>
        /// AANPASSING: Initializes the main window's data sources for a specific user profile.
        /// This method sets the correct folder path and loads the music pieces.
        /// </summary>
        /// <param name="profileName">The name of the user profile to load data for.</param>
        public void InitializeDataForUser(string profileName)
        {
            // NEW: Clear the static list to prevent data from other profiles from persisting.
            if (MusicPieces != null)
            {
                MusicPieces.Clear();
            }

            // Set the correct, profile-specific path for music pieces.
            string profileFolder = DataPathProvider.GetProfileFolder(profileName);

            _musicPiecesFolder = Path.Combine(profileFolder, "MusicPieces");

            if (!Directory.Exists(_musicPiecesFolder))
            {
                Directory.CreateDirectory(_musicPiecesFolder);
                DeletedPieceRegistry.Initialize(profileFolder);
            }

            // Now that the path is correct, load the music pieces.
            LoadExistingMusicPieces();
            // Archiving is no longer used; no purge needed.

            // Apply the initial sorting and filtering for the current profile (archiving removed; filtering now relies on pause state).
            ApplySortingAndFiltering();

            // Update the status bar after loading.
            var pieceCount = MusicPieces != null ? MusicPieces.Count : 0;
            TxtTotalMusicPieces?.SetCurrentValue(TextBlock.TextProperty, $"Ready | {pieceCount} pieces");

            // NEW RULE: Set the profile name in the UI (card already shows 'Profile' label).
            TxtProfileName?.SetCurrentValue(TextBlock.TextProperty, profileName);
        }

        // In MainWindow.xaml.cs

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MakeBarSectionColumnsReadOnly();

            // OPLOSSING: Voeg deze regel toe om alle muziekstukken te synchroniseren met hun historie bij het opstarten
            // Hierdoor worden de LastPracticeDate waarden correct gezet vóór enige UI-interactie
            SyncAllPiecesFromHistoryAndRefresh();

            // After loading all music pieces, perform a cleanup of scheduled sessions
            // to remove any "orphaned" sessions that point to non-existent sections.
            // This enhances data integrity and prevents crashes.
            ScheduledPracticeSessionManager.Instance.CleanupOrphanedSessions(MusicPieces.ToList());

            // Log that the training check is starting.
            MLLogManager.Instance.Log("MainWindow is loaded. Running startup scheduler sync.", LogLevel.Info);

            // Automatically reschedule any sessions that were planned for a past date but not completed.
            ScheduledPracticeSessionManager.Instance.RescheduleOverdueSessions(MusicPieces.ToList());

            // AUTO-REPAIR: Fix sections that have been practiced today but have no scheduled session
            ScheduledPracticeSessionManager.Instance.AutoRepairMissingSessions(MusicPieces.ToList());

            // Synchronize all NextDueDates with scheduled sessions
            SynchronizeAllNextDueDates();

            // No AI planner retraining required anymore.

            // Setup scheduled sessions file binding and watcher
            try
            {
                string scheduledFolder = DataPathProvider.GetScheduledFolder(ActiveUserSession.ProfileName);
                _scheduledSessionsFile = Path.Combine(scheduledFolder, "scheduled_sessions.json");

                // Bind the planned sessions collection to a grid/list if present (named 'myGrid')
                if (this.FindName("myGrid") is System.Windows.Controls.ItemsControl myGrid)
                {
                    myGrid.ItemsSource = _plannedSessions;
                }

                ReloadScheduledGridData();
                SetupScheduledSessionsWatcher();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("MainWindow: Failed to initialize scheduled sessions watcher/binding.", ex);
            }
        }

        /// <summary>
        /// This event handler is called once, after the window's content has been fully rendered.
        /// We use a short, one-time timer to delay setting the focus. This ensures that our focus call
        /// is the absolute last thing to happen during startup, overcoming any race conditions
        /// that occur in Release/Run mode but not in Debug mode.
        /// </summary>
        private void MainWindow_ContentRendered(object? sender, EventArgs e)
        {
            // Unsubscribe from the event so this code only runs once.
            this.ContentRendered -= MainWindow_ContentRendered;

            // Create a timer that will fire only once, after a short delay.
            var focusTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // 100ms delay
            };

            focusTimer.Tick += (s, args) =>
            {
                // Stop the timer, we only need it to run once.
                focusTimer.Stop();

                // Set the keyboard focus to the ListView.
                LvMusicPieces.Focus();

                // Optional but recommended: Select the first item.
                // This makes it clear to the user where the focus is.
                if (LvMusicPieces.Items.Count > 0)
                {
                    LvMusicPieces.SelectedIndex = 0;
                }
            };

            // Start the one-time timer.
            focusTimer.Start();
        }

        private void MenuCreateBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get the path to the ModusPractica application data folder
                string sourceDirectory = DataPathProvider.GetAppRoot();

                // Check if the source directory exists
                if (!Directory.Exists(sourceDirectory))
                {
                    MessageBox.Show("The application folder 'ModusPractica' does not exist. There is no data to back up.", "Backup Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    MLLogManager.Instance.Log("Backup failed: ModusPractica data folder not found.", LogLevel.Warning);
                    return;
                }

                try
                {
                    MLLogManager.Instance.Log("Starting pre-backup flush of in-memory data.", LogLevel.Debug);
                    SaveAllMusicPieces();
                    ScheduledPracticeSessionManager.Instance.SaveScheduledSessions();
                    PracticeHistoryManager.Instance.SaveHistoryData();
                    SettingsManager.Instance.SaveSettings();
                    MLLogManager.Instance.Log("Pre-backup flush completed successfully.", LogLevel.Debug);
                }
                catch (Exception flushEx)
                {
                    MLLogManager.Instance.LogError("Pre-backup flush encountered an error. Proceeding with best-effort backup.", flushEx);
                }

                // Use SaveFileDialog to let the user choose where to save the backup
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Zip files (*.zip)|*.zip",
                    FileName = $"ModusPractica_Backup_{DataPathProvider.Sanitize(ActiveUserSession.ProfileName)}_{DateTime.Now:yyyyMMdd_HHmmss}.zip",
                    Title = "Save Modus Practica Data Backup"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    string destinationZipFile = saveFileDialog.FileName;

                    // Ensure the destination file does not exist to prevent errors, or allow overwrite
                    if (File.Exists(destinationZipFile))
                    {
                        try
                        {
                            File.Delete(destinationZipFile);
                        }
                        catch (Exception deleteEx)
                        {
                            MessageBox.Show($"Could not overwrite the existing backup file: {deleteEx.Message}\nChoose a different file name or close programs that might be using the file.", "Backup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            MLLogManager.Instance.LogError($"Backup failed: Could not overwrite existing file {destinationZipFile}", deleteEx);
                            return;
                        }
                    }

                    // Create the ZIP archive with basic retry handling in case files are locked
                    const int maxAttempts = 3;
                    bool backupCompleted = false;

                    for (int attempt = 1; attempt <= maxAttempts; attempt++)
                    {
                        try
                        {
                            ZipFile.CreateFromDirectory(sourceDirectory, destinationZipFile, CompressionLevel.Fastest, false);
                            backupCompleted = true;

                            if (attempt > 1)
                            {
                                MLLogManager.Instance.Log($"Backup succeeded on attempt {attempt}.", LogLevel.Info);
                            }

                            break;
                        }
                        catch (IOException ioEx)
                        {
                            MLLogManager.Instance.LogError($"Backup attempt {attempt} failed due to IO error.", ioEx);

                            if (attempt < maxAttempts)
                            {
                                Thread.Sleep(250);
                                continue;
                            }

                            MessageBox.Show($"Could not create the backup because some files were in use or locked.\nPlease close other Modus Practica windows or wait a moment and try again.\n\nDetails: {ioEx.Message}", "Backup Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                            MLLogManager.Instance.Log("Backup aborted after IO retries.", LogLevel.Warning);
                            return;
                        }
                    }

                    if (!backupCompleted)
                    {
                        return;
                    }

                    MessageBox.Show($"Backup successfully created at:\n{destinationZipFile}", "Backup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    MLLogManager.Instance.Log($"Backup successfully created at: {destinationZipFile}", LogLevel.Info);
                }
                else
                {
                    MLLogManager.Instance.Log("Backup operation cancelled by user.", LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred during the backup: {ex.Message}\n\nPlease ensure you have permission to write to the selected location.", "Backup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                MLLogManager.Instance.LogError("An unexpected error occurred during backup creation", ex);
            }
        }

        // NIEUW: Event handler voor het herstellen van een backup.
        private void MenuRestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            var go = MessageBox.Show(
                "WARNING: Restoring a backup will overwrite all your current data.\n\n" +
                "Continue?",
                "Confirm Restore Operation",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (go != MessageBoxResult.Yes) return;

            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Zip files (*.zip)|*.zip",
                Title = $"Select Modus Practica Backup File to Restore (Current Profile: {ActiveUserSession.ProfileName})"
            };
            if (ofd.ShowDialog() != true) return;

            string backupZipFile = ofd.FileName;
            string targetRoot = DataPathProvider.GetAppRoot();

            // --- NEW: release all file handles before touching the data folder ---
            try
            {
                // Stop/Dispose watcher safely
                var w = _scheduledWatcher;
                if (w != null)
                {
                    w.EnableRaisingEvents = false;
                    w.Dispose();
                    _scheduledWatcher = null;
                }

                if (_calendarWindowInstance != null) _calendarWindowInstance.Close();
                if (_practiceTimerWindow != null) _practiceTimerWindow.Close();

                // Flush pending writes (best effort)
                SaveAllMusicPieces();
                ScheduledPracticeSessionManager.Instance.SaveScheduledSessions();
                PracticeHistoryManager.Instance.SaveHistoryData();
            }
            catch { /* best effort; never crash */ }

            try
            {
                // 1) Remove current data folder (no pre-backup, no move to _pre-restore_)
                if (Directory.Exists(targetRoot))
                {
                    Directory.Delete(targetRoot, true);
                }

                // 2) Extract to temporary folder
                string tempExtract = Path.Combine(Path.GetTempPath(), "ModusPractica_restore_tmp");
                if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);
                System.IO.Compression.ZipFile.ExtractToDirectory(backupZipFile, tempExtract);

                // 3) Some zips contain a top-level 'ModusPractica' folder
                string extractedRoot = Directory.Exists(Path.Combine(tempExtract, "ModusPractica"))
                    ? Path.Combine(tempExtract, "ModusPractica")
                    : tempExtract;

                // 4) Move to final location
                Directory.Move(extractedRoot, targetRoot);

                // 5) Cleanup
                if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true);

                MessageBox.Show(
                    "Backup successfully restored. The application will now restart.",
                    "Restore Complete", MessageBoxButton.OK, MessageBoxImage.Information);

                System.Diagnostics.Process.Start(Environment.ProcessPath);
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"An error occurred during the restore process: {ex.Message}\n\n" +
                    "You may need to manually restore your data.",
                    "Restore Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // AANPASSING
        private void MainWindow_Activated(object sender, EventArgs e)
        {
            // Bestaande logica blijft
            // --- NIEUWE LOGICA: Ververs de UI alleen als data gewijzigd is ---
            if (AppState.MusicDataChanged)
            {
                // Synchroniseer alle stukken met historie en herbereken voortgang
                SyncAllPiecesFromHistoryAndRefresh();

                // Reset de vlag zodat we niet onnodig verversen
                AppState.MusicDataChanged = false;
            }
        }

        /// <summary>
        /// Synchronizes CompletedRepetitions and Difficulty for all BarSections of all pieces
        /// from PracticeHistory, recomputes Progress, and refreshes the UI lists.
        /// </summary>
        public void SyncAllPiecesFromHistoryAndRefresh()
        {
            try
            {
                if (MusicPieces != null)
                {
                    foreach (var mp in MusicPieces)
                    {
                        SyncPieceFromHistory(mp);
                    }
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error updating progress for all pieces from history", ex);
            }

            if (LvMusicPieces.ItemsSource != null)
            {
                LvMusicPieces.Items.Refresh();
            }
            if (DgBarSections.ItemsSource != null)
            {
                DgBarSections.Items.Refresh();
            }
        }

        /// <summary>
        /// Synchronizes one MusicPieceItem's sections from history and updates its Progress.
        /// </summary>
        /// <param name="mp">The piece to synchronize.</param>
        /// <summary>
        /// Synchronizes NextDueDates for all music pieces in the application
        /// 
        /// AANGEPAST: Dit is nu één-richting synchronisatie (scheduled sessions ? BarSection.NextDueDate)
        /// Deze methode implementeert het "single source of truth" patroon waarbij
        /// scheduled_sessions.json de bron van waarheid is voor NextDueDate waarden.
        /// </summary>
        private void SynchronizeAllNextDueDates()
        {
            try
            {
                MLLogManager.Instance.Log("Starting one-way synchronization of NextDueDates FROM scheduled sessions TO BarSection objects", LogLevel.Info);
                int totalUpdated = 0;

                foreach (var mp in MusicPieces)
                {
                    int pieceUpdated = SyncNextDueDatesWithScheduledSessions(mp);
                    if (pieceUpdated > 0)
                    {
                        SaveMusicPiece(mp);
                        MLLogManager.Instance.Log($"Updated {pieceUpdated} section NextDueDates for '{mp.Title}' (ID: {mp.Id})", LogLevel.Info);
                    }
                    totalUpdated += pieceUpdated;
                }

                MLLogManager.Instance.Log($"Successfully synchronized and saved NextDueDates (one-way, from schedule ? BarSections). Updated {totalUpdated} sections total.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error during one-way synchronization of NextDueDates", ex);
            }
        }

        /// <summary>
        /// Synchronizes the NextDueDate values from scheduled practice sessions TO 
        /// the BarSection NextDueDate properties. This is a ONE-WAY synchronization
        /// that implements the single source of truth pattern where scheduled_sessions.json
        /// is the authoritative source for NextDueDate values.
        /// </summary>
        /// <param name="mp">The music piece to synchronize NextDueDates for</param>
        /// <returns>The number of sections that were updated</returns>
        private int SyncNextDueDatesWithScheduledSessions(MusicPieceItem mp)
        {
            if (mp == null || mp.BarSections == null) return 0;

            int updatedCount = 0;

            foreach (var bs in mp.BarSections)
            {
                try
                {
                    var nextPlanned = ScheduledPracticeSessionManager.Instance.GetScheduledSessionForBarSection(bs.Id);
                    var plannedDate = nextPlanned?.ScheduledDate.Date;

                    if (plannedDate.HasValue)
                    {
                        if (!bs.NextDueDate.HasValue || bs.NextDueDate.Value.Date != plannedDate.Value)
                        {
                            MLLogManager.Instance.Log(
                                $"[ONE-WAY SYNC] Updated NextDueDate for '{mp.Title} - {bs.BarRange}' from {bs.NextDueDate:yyyy-MM-dd} to {plannedDate.Value:yyyy-MM-dd} (schedule ? section)",
                                LogLevel.Debug);
                            bs.NextDueDate = plannedDate.Value;
                            updatedCount++;
                        }
                    }
                    else
                    {
                        // Als er geen geplande sessie is, maar de BarSection heeft wel een NextDueDate,
                        // dan maken we de NextDueDate leeg om het single source of truth patroon te volgen
                        if (bs.NextDueDate.HasValue)
                        {
                            MLLogManager.Instance.Log(
                                $"[ONE-WAY SYNC] Cleared NextDueDate for '{mp.Title} - {bs.BarRange}' (was {bs.NextDueDate:yyyy-MM-dd}) omdat er geen geplande sessie bestaat",
                                LogLevel.Debug);
                            bs.NextDueDate = null;
                            updatedCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance.LogError($"Error synchronizing NextDueDate for bar section {bs.BarRange} of '{mp.Title}'", ex);
                }
            }

            // Let the outer method log the piece-level summary.
            return updatedCount;
        }

        private void SyncPieceFromHistory(MusicPieceItem mp)
        {
            if (mp == null) return;

            if (mp.BarSections != null)
            {
                foreach (var bs in mp.BarSections)
                {
                    try
                    {
                        var history = PracticeHistoryManager.Instance
                            .GetHistoryForBarSection(bs.Id)
                            .Where(h => !h.IsDeleted)
                            .OrderBy(h => h.Date)
                            .ToList();

                        // Fallback/migratie: als er geen Id-gebaseerde geschiedenis bestaat (ouderwetse data),
                        // probeer te vinden op BarRange binnen dit muziekstuk en migreer die records naar de huidige BarSection.Id.
                        if (history.Count == 0 && !string.IsNullOrWhiteSpace(bs.BarRange))
                        {
                            var altHistory = PracticeHistoryManager.Instance
                                .GetHistoryForMusicPiece(mp.Id)
                                .Where(h => !h.IsDeleted && string.Equals(h.BarSectionRange, bs.BarRange, StringComparison.OrdinalIgnoreCase))
                                .OrderBy(h => h.Date)
                                .ToList();

                            if (altHistory.Count > 0)
                            {
                                MLLogManager.Instance.Log(
                                    $"Migrating {altHistory.Count} history records for piece '{mp.Title}' (ID: {mp.Id}) and range '{bs.BarRange}' to section {bs.Id}",
                                    LogLevel.Info);

                                foreach (var h in altHistory)
                                {
                                    try
                                    {
                                        h.BarSectionId = bs.Id;
                                        h.BarSectionRange = bs.BarRange; // keep consistent
                                        PracticeHistoryManager.Instance.UpdatePracticeHistory(h);
                                    }
                                    catch (Exception migrateEx)
                                    {
                                        MLLogManager.Instance.LogError($"Failed to migrate history record {h.Id} to section {bs.Id}", migrateEx);
                                    }
                                }

                                // Reload Id-based history after migration
                                history = PracticeHistoryManager.Instance
                                    .GetHistoryForBarSection(bs.Id)
                                    .Where(h => !h.IsDeleted)
                                    .OrderBy(h => h.Date)
                                    .ToList();
                            }
                        }

                        // --- VERBETERDE LASTPRACTICEDATE SYNCHRONISATIE ---
                        var lastHistoryEntry = history.OrderByDescending(h => h.Date).FirstOrDefault();
                        if (lastHistoryEntry != null)
                        {
                            // Gebruik alleen de datum zonder tijd en sla dit expliciet op als de datum waarde
                            DateTime lastPracticeDate = lastHistoryEntry.Date.Date;

                            // Log alleen als de datum daadwerkelijk wijzigt
                            if (bs.LastPracticeDate != lastPracticeDate)
                            {
                                MLLogManager.Instance.Log(
                                    $"Updating LastPracticeDate for '{mp.Title}' [{bs.BarRange}] from {bs.LastPracticeDate:yyyy-MM-dd} to {lastPracticeDate:yyyy-MM-dd}",
                                    LogLevel.Debug);

                                bs.LastPracticeDate = lastPracticeDate;
                            }
                        }

                        // Totaal uitgevoerde herhalingen (stuurt ProgressPercentage)
                        int totalReps = history.Sum(h => h.Repetitions);
                        bs.CompletedRepetitions = totalReps;

                        // Meest recente difficulty uit historie
                        var lastDiff = history.LastOrDefault()?.Difficulty;
                        if (!string.IsNullOrWhiteSpace(lastDiff) &&
                            !string.Equals(bs.Difficulty, lastDiff, StringComparison.Ordinal))
                        {
                            bs.Difficulty = lastDiff;
                        }

                        // TargetRepetitions up-to-date houden (anders blijft ProgressPercentage 0)
                        var lastTarget = history.LastOrDefault()?.TargetRepetitions ?? 0;
                        if (lastTarget > 0 && bs.TargetRepetitions != lastTarget)
                        {
                            bs.TargetRepetitions = lastTarget;
                        }

                        // Fallback: voorkom doel = 0 (oude/migratie-data)
                        if (bs.TargetRepetitions <= 0)
                        {
                            int fallbackTarget = lastTarget > 0 ? lastTarget : 6;
                            if (bs.TargetRepetitions != fallbackTarget)
                            {
                                MLLogManager.Instance.Log(
                                    $"Applying fallback TargetRepetitions={fallbackTarget} for '{mp.Title}' [{bs.BarRange}] (was 0)",
                                    LogLevel.Warning);
                                bs.TargetRepetitions = fallbackTarget;
                            }
                        }

                        // Eén-richting sync: geplande NextDueDate ? BarSection.NextDueDate
                        try
                        {
                            var nextPlanned = ScheduledPracticeSessionManager.Instance.GetScheduledSessionForBarSection(bs.Id);
                            var plannedDate = nextPlanned?.ScheduledDate.Date;
                            if (plannedDate.HasValue)
                            {
                                if (!bs.NextDueDate.HasValue || bs.NextDueDate.Value.Date != plannedDate.Value)
                                {
                                    bs.NextDueDate = plannedDate.Value;
                                }
                            }
                        }
                        catch
                        {
                            // nooit UI laten crashen tijdens sync
                        }

                        // Debug/trace: log progress inputs to verify UI sync state
                        try
                        {
                            double progressPct = bs.ProgressPercentage;
                            MLLogManager.Instance.Log(
                                $"Sync progress for '{mp.Title}' [{bs.BarRange}]: reps={bs.CompletedRepetitions}, target={bs.TargetRepetitions}, diff='{bs.Difficulty}', progress={progressPct:P0}",
                                LogLevel.Debug);
                        }
                        catch
                        {
                            // ignore logging errors
                        }
                    }
                    catch (Exception innerEx)
                    {
                        MLLogManager.Instance.LogError($"Error syncing section {bs.Id} from history", innerEx);
                    }
                }
            }

            // Herbereken piece-progress
            try
            {
                mp.UpdateProgress();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Failed to update progress for piece after sync", ex);
            }

            // Refresh retentie-gerelateerde properties voor UI update (kleurcodes, iconen)
            try
            {
                mp.RefreshRetentionProperties();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Failed to refresh retention properties for piece after sync", ex);
            }
        }
        // Map to keep track of color resources
        public static Dictionary<string, SolidColorBrush> ColorResourceMap { get; private set; }


        // Observable collection of music pieces for the ListView
        public static ObservableCollection<MusicPieceItem> MusicPieces { get; private set; }

        private void OnAddBarSectionClick(object sender, RoutedEventArgs e)
        {
            AddBarSectionFromInputs();
        }

        private void BtnOpenCalendarFromBarSections_Click(object sender, RoutedEventArgs e)
        {
            OpenCalendarWindow();
        }

        // NEW: Handles the click event for the new "Playlists" button.
        private void BtnOpenPlaylistManagerFromBarSections_Click(object sender, RoutedEventArgs e)
        {
            // Reuse the existing logic from the main menu to open the playlist manager.
            MenuPlaylistManager_Click(sender, e);
        }

        private void OpenCalendarWindow()
        {
            // Controleer of er al een kalendervenster open is
            if (_calendarWindowInstance != null && _calendarWindowInstance.IsVisible)
            {
                _calendarWindowInstance.Activate(); // Breng naar voren
                return;
            }

            // Maak een nieuw kalendervenster
            _calendarWindowInstance = new CalendarWindow { Owner = this };

            // Wanneer het venster gesloten wordt: ververs UI en zet referentie op null
            _calendarWindowInstance.Closed += (s, e) =>
            {
                if (LvMusicPieces.SelectedItem is MusicPieceItem selectedMusicPiece)
                {
                    ReloadSelectedMusicPiece();
                    DgBarSections.Items.Refresh();
                }

                LvMusicPieces.Items.Refresh();
                _calendarWindowInstance = null;
            };

            // Open het venster niet-modaal
            _calendarWindowInstance.Show();
        }

        // NIEUW
        private void MenuShowOverallTimer_Click(object sender, RoutedEventArgs e)
        {
            ShowPracticeTimerWindow();
        }

        // NIEUW: Deze methode is nu 'public' zodat App.xaml.cs deze kan aanroepen.
        public void ShowPracticeTimerWindow()
        {
            // Controleer of het venster niet al open is.
            if (_practiceTimerWindow == null || !_practiceTimerWindow.IsVisible)
            {
                _practiceTimerWindow = new PracticeTimerWindow();

                // Zorg ervoor dat de referentie wordt opgeschoond als de gebruiker het venster sluit met 'X'.
                // Dit is cruciaal om het venster opnieuw te kunnen openen.
                _practiceTimerWindow.Closed += (s, args) => _practiceTimerWindow = null;

                _practiceTimerWindow.Owner = this;
                _practiceTimerWindow.Show();
            }
            else
            {
                // Als het venster al bestaat, breng het dan naar voren.
                _practiceTimerWindow.Activate();
            }
        }

        // --- YouTube Quick Link ---
        private void BtnOpenYouTubeLink_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Prefer per-piece link; fall back to global user link
                string? url = null;
                if (LvMusicPieces?.SelectedItem is MusicPieceItem piece)
                {
                    url = piece.YouTubeLink;
                }
                if (string.IsNullOrWhiteSpace(url))
                {
                    url = SettingsManager.Instance.CurrentSettings.YouTubeQuickLink;
                }
                if (string.IsNullOrWhiteSpace(url))
                {
                    MessageBox.Show("No YouTube link is set yet. Right-click the button and choose 'Set YouTube Link...'.", "No Link", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Basic validation
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    MessageBox.Show("The stored link is not a valid http/https URL.", "Invalid Link", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = uri.ToString(),
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Failed to open YouTube link", ex);
                MessageBox.Show($"Failed to open link: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnYouTubeSetLink_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Seed dialog with per-piece link when available, else global
                string seed = (LvMusicPieces?.SelectedItem as MusicPieceItem)?.YouTubeLink;
                if (string.IsNullOrWhiteSpace(seed)) seed = SettingsManager.Instance.CurrentSettings.YouTubeQuickLink;
                var dlg = new UrlInputDialog("Set YouTube Link", "Paste or type the YouTube URL:", seed)
                {
                    Owner = this
                };
                if (dlg.ShowDialog() == true)
                {
                    var url = dlg.ResponseText.Trim();
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        MessageBox.Show("Please enter a valid http/https URL.", "Invalid URL", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    // Save to current piece if available; otherwise save globally
                    if (LvMusicPieces?.SelectedItem is MusicPieceItem piece)
                    {
                        piece.YouTubeLink = uri.ToString();
                        try { SaveMusicPiece(piece); } catch { }
                    }
                    else
                    {
                        SettingsManager.Instance.CurrentSettings.YouTubeQuickLink = uri.ToString();
                        SettingsManager.Instance.SaveSettings();
                    }
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Failed to set YouTube link", ex);
                MessageBox.Show($"Failed to set link: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnYouTubeClearLink_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.CurrentSettings.YouTubeQuickLink = string.Empty;
            SettingsManager.Instance.SaveSettings();
        }

        // No runtime enable/disable; handler will inform user if link isn't set.

        private void ReloadSelectedMusicPiece()
        {
            try
            {
                if (LvMusicPieces.SelectedItem is MusicPieceItem selectedMusicPiece)
                {
                    // Find the music piece in the collection and reload its data
                    var musicPieceInCollection = MusicPieces.FirstOrDefault(mp => mp.Id == selectedMusicPiece.Id);
                    if (musicPieceInCollection != null && !string.IsNullOrEmpty(_musicPiecesFolder))
                    {
                        // Reload the music piece from disk
                        string filePath = musicPieceInCollection.GetFilePath(_musicPiecesFolder);
                        if (File.Exists(filePath))
                        {
                            string jsonContent = File.ReadAllText(filePath);
                            MusicPieceItem reloadedMusicPiece = JsonSerializer.Deserialize<MusicPieceItem>(jsonContent);

                            // Restore the ColorBrush
                            if (!string.IsNullOrEmpty(reloadedMusicPiece.ColorResourceName) &&
                                ColorResourceMap.TryGetValue(reloadedMusicPiece.ColorResourceName, out SolidColorBrush colorBrush))
                            {
                                reloadedMusicPiece.ColorBrush = MakeColorTransparent(colorBrush, 0.5) as SolidColorBrush;
                            }

                            // Update the properties of the existing object
                            int index = MusicPieces.IndexOf(musicPieceInCollection);
                            if (index >= 0)
                            {
                                MusicPieces[index] = reloadedMusicPiece;
                                LvMusicPieces.SelectedItem = reloadedMusicPiece;

                                // Sync from history (ensures TargetRepetitions/CompletedRepetitions/Difficulty) and recalc progress
                                try
                                {
                                    SyncPieceFromHistory(reloadedMusicPiece);
                                }
                                catch (Exception ex)
                                {
                                    MLLogManager.Instance.LogError("Failed to sync piece from history after reload", ex);
                                }

                                // Update the UI with the new data (sorted)
                                if (reloadedMusicPiece.BarSections != null)
                                {
                                    DgBarSections.ItemsSource = new ObservableCollection<BarSection>(
                                        SortBarSections(reloadedMusicPiece.BarSections)
                                    );
                                }
                                DgBarSections.Items.Refresh();

                                // Also refresh the pieces list to reflect updated Progress
                                LvMusicPieces.Items.Refresh();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error reloading selected music piece", ex);
            }
        }



        // Event handlers for the Notes tab
        private void BtnAddNote_Click(object sender, RoutedEventArgs e)
        {
            NotesViewModel.AddNote();
            if (NotesViewModel.CurrentNote != null)
            {
                // Focus the title textbox so the user can type immediately
                TxtNoteTitle.Focus();
            }
        }

        // AANPASSING: Logica voor de timestamp knop toegevoegd.
        private void BtnAddTimestamp_Click(object sender, RoutedEventArgs e)
        {
            if (NotesViewModel.CurrentNote == null) return;

            // Genereer de timestamp string
            string timestamp = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ";

            // Sla de huidige cursorpositie op
            int caretIndex = TxtNoteContent.CaretIndex;

            // Voeg de timestamp in de tekst in op de cursorpositie
            NotesViewModel.CurrentNote.Content = NotesViewModel.CurrentNote.Content.Insert(caretIndex, timestamp);

            // Plaats de cursor direct na de ingevoegde timestamp
            TxtNoteContent.CaretIndex = caretIndex + timestamp.Length;
            TxtNoteContent.Focus();
        }

        // ADJUSTMENT: This method passes the full list of music pieces to the NewMusicPieceWindow.
        private void BtnNewMusicPiece_Click(object sender, RoutedEventArgs e)
        {
            // Pass the entire collection to the window for checking.
            NewMusicPieceWindow newMusicPieceWindow = new NewMusicPieceWindow(MusicPieces.ToList()) { Owner = this };

            bool? result = newMusicPieceWindow.ShowDialog();

            // A new piece was successfully created.
            if (result == true && newMusicPieceWindow.CreatedMusicPiece != null)
            {
                MusicPieces.Add(newMusicPieceWindow.CreatedMusicPiece);
                SaveMusicPiece(newMusicPieceWindow.CreatedMusicPiece);
                ApplySortingAndFiltering(); // Refresh the list
                TxtTotalMusicPieces.SetCurrentValue(TextBlock.TextProperty, MusicPieces.Count.ToString());
            }
        }

        // ML debug window removed - no longer needed

        // AANPASSING: Opslaglogica toegevoegd.
        // AANPASSING: Correcte logica voor de "Save All Notes" knop.
        private void BtnSaveNotes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var musicPiece in MusicPieces)
                {
                    SaveMusicPiece(musicPiece);
                }

                MessageBox.Show("All notes for all music pieces have been successfully saved.", "All Notes Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while saving all notes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                MLLogManager.Instance.LogError("Failed to save all notes", ex);
            }
        }

        // NIEUWE: De ontbrekende methode voor de 'Save Note' knop.
        private void BtnSaveNote_Click(object sender, RoutedEventArgs e)
        {
            if (NotesViewModel.CurrentNote == null || LvMusicPieces.SelectedItem is not MusicPieceItem selectedMusicPiece)
            {
                MessageBox.Show("Select a music piece and a note to save.", "No Note Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Roep de centrale opslagmethode aan om de wijzigingen naar het JSON-bestand te schrijven
            SaveMusicPiece(selectedMusicPiece);

            // Vernieuw de lijst om de (mogelijk gewijzigde) titel te tonen
            LvNotes.Items.Refresh();

            // Optionele feedback aan de gebruiker
            // MessageBox.Show($"Note '{NotesViewModel.CurrentNote.Title}' has been saved.", "Note Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeleteBarSection_Click(object sender, RoutedEventArgs e)
        {
            if (DgBarSections.SelectedItem is BarSection selectedBarSection &&
                LvMusicPieces.SelectedItem is MusicPieceItem selectedMusicPiece)
            {
                MessageBoxResult result = MessageBox.Show(
                    $"Are you sure you want to delete the bar section '{selectedBarSection.BarRange}'?\n\nNote: Historical practice data for this section will be preserved, but scheduled sessions will be removed.",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // Make sure any pending edits are committed to avoid stale UI references
                    DgBarSections.CommitEdit(DataGridEditingUnit.Row, true);
                    DgBarSections.CommitEdit();

                    // Capture ID because SelectedItem reference can be different from the one inside the ObservableCollection
                    Guid sectionId = selectedBarSection.Id;

                    // 1) Remove scheduled sessions and persist
                    ScheduledPracticeSessionManager.Instance.RemoveSessionsForBarSectionWithoutSaving(sectionId);
                    ScheduledPracticeSessionManager.Instance.SaveScheduledSessions();

                    // 2) Remove the actual section from the live collection by Id (not by object reference)
                    var sectionInCollection = selectedMusicPiece.BarSections?.FirstOrDefault(bs => bs.Id == sectionId);
                    if (sectionInCollection != null)
                    {
                        selectedMusicPiece.BarSections.Remove(sectionInCollection);
                    }
                    else
                    {
                        // As a fallback, try removing the SelectedItem reference (in case it maps correctly)
                        selectedMusicPiece.BarSections.Remove(selectedBarSection);
                    }

                    // 3) Recalculate progress and save to disk
                    selectedMusicPiece.UpdateProgress();
                    SaveMusicPiece(selectedMusicPiece);

                    // 4) Force the DataGrid to refresh by resetting the ItemsSource and clearing selection (sorted)
                    DgBarSections.ItemsSource = null;
                    DgBarSections.ItemsSource = new ObservableCollection<BarSection>(
                        SortBarSections(selectedMusicPiece.BarSections)
                    );
                    DgBarSections.SelectedItem = null;
                    DgBarSections.Items.Refresh();
                    DgBarSections.UpdateLayout();

                    // 5) Refresh the music pieces list as progress might have changed
                    LvMusicPieces.Items.Refresh();

                    // 6) Notify other windows and refresh calendar if open
                    AppState.MusicDataChanged = true;
                    if (_calendarWindowInstance != null && _calendarWindowInstance.IsVisible)
                    {
                        _calendarWindowInstance.RefreshCalendarData();
                    }
                }
            }
            else
            {
                MessageBox.Show("Selecteer een bar sectie om te verwijderen.", "Geen Selectie",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // NIEUWE: Event handler voor het openen van het archiefvenster.

        private void EditBarSection_Click(object sender, RoutedEventArgs e)
        {
            // Ensure a bar section and its parent music piece are selected
            if (DgBarSections.SelectedItem is BarSection selectedBarSection &&
                LvMusicPieces.SelectedItem is MusicPieceItem selectedMusicPiece)
            {
                // Open the new edit window, passing the selected objects
                var editWindow = new EditBarSectionWindow(selectedBarSection, selectedMusicPiece) { Owner = this };

                bool? result = editWindow.ShowDialog();

                // If the user clicked "Save" in the dialog



                if (result == true)
                {
                    // The object 'selectedBarSection' in memory is already updated.
                    // All we need todo is save the parent music piece to persist the changes.
                    SaveMusicPiece(selectedMusicPiece);

                    // Refresh the DataGrid to show the updated values
                    DgBarSections.Items.Refresh();

                    MessageBox.Show("The section has been successfully updated.", "Update Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("Select a bar section to edit.", "No Section Selected", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Helper method to delete all scheduled sessions for a bar section
        private void DeleteScheduledSessionsForBarSection(Guid barSectionId)
        {
            try
            {
                // Get all scheduled sessions
                var allSessions = ScheduledPracticeSessionManager.Instance.GetAllRegularScheduledSessions();
                // Filter the sessions belonging to this bar section

                var sessionsToDelete = allSessions.Where(s => s.BarSectionId == barSectionId).ToList();
                // Remove each session
                foreach (var session in sessionsToDelete)
                {
                    ScheduledPracticeSessionManager.Instance.RemoveScheduledSession(session.Id);
                }
                MLLogManager.Instance.Log($"Deleted {sessionsToDelete.Count} scheduled sessions for bar section {barSectionId}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                // Log the error, but let the process continue
                MLLogManager.Instance.LogError($"Error deleting scheduled sessions for bar section {barSectionId}", ex);
            }
        }

        // ADAPTATION: This method permanently deletes the piece from disk. Practice history remains in logs.
        private void DeleteMusicPiece_Click(object sender, RoutedEventArgs e)
        {
            if (LvMusicPieces.SelectedItem is MusicPieceItem selectedMusicPiece)
            {
                string warningMessage = $"Are you sure you want to permanently delete '{selectedMusicPiece.Title}'?\\n\\n" +
                    "This will remove the piece from your library but keeps all practice history for analytics.";
                MessageBoxResult result = MessageBox.Show(
                    warningMessage,
                    "Confirm Permanent Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    DeletedPieceRegistry.RecordDeletion(selectedMusicPiece.Title, selectedMusicPiece.Composer);

                    DeleteScheduledSessionsForMusicPiece(selectedMusicPiece.Id);
                    DeleteMusicPieceFile(selectedMusicPiece);

                    MusicPieces.Remove(selectedMusicPiece);
                    ApplySortingAndFiltering();
                    TxtTotalMusicPieces.Text = $"Ready | {MusicPieces.Count} pieces";
                }
            }
        }

        // Helper method to delete all scheduled practice sessions for a music piece
        private void DeleteScheduledSessionsForMusicPiece(Guid musicPieceId)
        {
            try
            {
                // Get all scheduled sessions
                var allSessions = ScheduledPracticeSessionManager.Instance.GetAllRegularScheduledSessions();
                // Filter the sessions belonging to this music piece
                var sessionsToDelete = allSessions.Where(s => s.MusicPieceId == musicPieceId).ToList();
                // Remove each session
                foreach (var session in sessionsToDelete)
                {
                    ScheduledPracticeSessionManager.Instance.RemoveScheduledSession(session.Id);
                }
                MLLogManager.Instance.Log($"Deleted {sessionsToDelete.Count} scheduled sessions for music piece {musicPieceId}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                // Log the error, but let the process continue
                MLLogManager.Instance.LogError($"Error deleting scheduled sessions for music piece {musicPieceId}", ex);
            }
        }

        // New method to delete a music piece from disk
        private void DeleteMusicPieceFile(MusicPieceItem musicPiece)
        {
            try
            {
                if (!string.IsNullOrEmpty(_musicPiecesFolder))
                {
                    // Probeer eerst het nieuwe bestandsformaat
                    string newFilePath = musicPiece.GetFilePath(_musicPiecesFolder);
                    if (File.Exists(newFilePath))
                    {
                        File.Delete(newFilePath);
                    }
                    else
                    {
                        // Probeer het oude formaat als fallback (alleen ID)
                        string oldFilePath = Path.Combine(_musicPiecesFolder, $"{musicPiece.Id}.json");
                        if (File.Exists(oldFilePath))
                        {
                            File.Delete(oldFilePath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting file for '{musicPiece.Title}': {ex.Message}",
                    "Error Deleting File", MessageBoxButton.OK, MessageBoxImage.Error);
                MLLogManager.Instance.LogError($"Error deleting music piece file for {musicPiece.Title}", ex);
            }
        }

        private void DeleteNote_Click(object sender, RoutedEventArgs e)
        {
            if (LvNotes.SelectedItem is NoteEntry selectedNote &&
                LvMusicPieces.SelectedItem is MusicPieceItem selectedMusicPiece)
            {
                MessageBoxResult result = MessageBox.Show(
                    $"Weet u zeker dat u de noot '{selectedNote.Title}' wilt verwijderen?",
                    "Bevestig Verwijderen", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    selectedMusicPiece.NoteEntries.Remove(selectedNote);

                    if (_currentNote == selectedNote)
                    {
                        _currentNote = null;
                        TxtNoteDate.Text = "Date: ";
                        TxtNoteTitle.Text = "";
                        TxtNoteContent.Text = "";
                    }

                    SaveMusicPiece(selectedMusicPiece);
                }
            }
        }

        // ML status display removed - no longer tracking ML metrics

        private void BtnOpenSightReading_Click(object sender, RoutedEventArgs e)
        {
            var w = new SightReadingWindow
            {
                Owner = this
            };
            w.Show();
        }

        private void DiagnoseColorIssue()
        {
            if (LvMusicPieces.SelectedItem is MusicPieceItem selectedItem)
            {
                string message = $"Selected item: {selectedItem.Title}\n" +
                                 $"Current color: {(selectedItem.ColorBrush != null ? selectedItem.ColorBrush.Color.ToString() : "null")}\n";
                message += "\nAvailable colors:\n";
                foreach (string colorName in new[] { "PastelBlue", "PastelGreen", "PastelPink", "PastelPurple",
                                            "PastelYellow", "PastelOrange", "PastelTeal", "PastelRed" })
                {
                    if (FindResource(colorName) is SolidColorBrush brush)
                    {
                        message += $"{colorName}: {brush.Color}\n";
                    }
                    else
                    {
                        message += $"{colorName}: not found\n";
                    }
                }
                MessageBox.Show(message, "Color Diagnosis");
            }
            else
            {
                MessageBox.Show("No item selected.", "Color Diagnosis");
            }
        }



        // Existing method replaced with this new implementation
        private void EditMusicPiece_Click(object sender, RoutedEventArgs e)
        {
            if (LvMusicPieces.SelectedItem is MusicPieceItem selectedMusicPiece)
            {
                // Create a new edit window and pass the selected music piece
                EditMusicPieceWindow editWindow = new EditMusicPieceWindow(selectedMusicPiece) { Owner = this };
                // Show the window
                bool? result = editWindow.ShowDialog();
                // Check if the changes were saved
                if (result == true && editWindow.IsSaved)
                {
                    // Refresh the ListView to show the changes immediately
                    LvMusicPieces.Items.Refresh();
                    // Save the updated music piece (this will also clean up old files)
                    SaveMusicPiece(selectedMusicPiece);

                    // Notify other windows about the change
                    AppState.MusicDataChanged = true;

                    // Refresh calendar window if it's open
                    if (_calendarWindowInstance != null && _calendarWindowInstance.IsVisible)
                    {
                        _calendarWindowInstance.RefreshCalendarData();
                    }

                    // ML debug window removed - no longer available

                    MLLogManager.Instance.Log($"Music piece '{selectedMusicPiece.Title}' was edited and old files cleaned up", LogLevel.Info);
                }
            }
            else
            {
                MessageBox.Show("Selecteer een muziekstuk om te bewerken.", "Geen Selectie",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void EditNote_Click(object sender, RoutedEventArgs e)
        {
            // Same functionality as when a note is selected
            if (LvNotes.SelectedItem is NoteEntry selectedNote)
            {
                NotesViewModel.OnNoteSelectionChanged(selectedNote);
                TxtNoteTitle.Focus();
            }
        }

        // Helper method to format time
        private string FormatTime(double minutes)
        {
            // Calculate total seconds
            int totalSeconds = (int)Math.Round(minutes * 60);
            // Calculate hours, minutes, and seconds
            int hours = totalSeconds / 3600;
            int mins = (totalSeconds % 3600) / 60;
            int secs = totalSeconds % 60;
            // Return in hh:mm:ss format or mm:ss if no hours
            return hours > 0 ? $"{hours:00}:{mins:00}:{secs:00}" : $"{mins:00}:{secs:00}";
        }



        private void InitializeColorResourceMap()
        {
            ColorResourceMap = new Dictionary<string, SolidColorBrush>
            {
                { "PastelBlue", FindResource("PastelBlue") as SolidColorBrush },
                { "PastelGreen", FindResource("PastelGreen") as SolidColorBrush },
                { "PastelPink", FindResource("PastelPink") as SolidColorBrush },
                { "PastelPurple", FindResource("PastelPurple") as SolidColorBrush },
                { "PastelYellow", FindResource("PastelYellow") as SolidColorBrush },
                { "PastelOrange", FindResource("PastelOrange") as SolidColorBrush },
                { "PastelTeal", FindResource("PastelTeal") as SolidColorBrush },
                { "PastelRed", FindResource("PastelRed") as SolidColorBrush }
            };
        }

        private void LoadExistingMusicPieces()
        {
            try
            {
                if (!Directory.Exists(_musicPiecesFolder))
                    return;

                int pieceTitleFixes = 0;
                int fileNameMigrations = 0;

                var jsonFiles = Directory.GetFiles(_musicPiecesFolder, "*.json");
                foreach (var jsonFile in jsonFiles)
                {
                    try
                    {
                        string jsonContent = File.ReadAllText(jsonFile);
                        MusicPieceItem musicPiece = JsonSerializer.Deserialize<MusicPieceItem>(jsonContent);

                        if (musicPiece == null)
                            return;

                        // Herstel kleur (zoals je al had)
                        if (!string.IsNullOrEmpty(musicPiece.ColorResourceName) &&
                            ColorResourceMap.TryGetValue(musicPiece.ColorResourceName, out SolidColorBrush colorBrush))
                        {
                            musicPiece.ColorBrush = MakeColorTransparent(colorBrush, 0.5) as SolidColorBrush;
                        }

                        // --- NORMALISATIE: titel-typo's op piece-niveau ---
                        if (string.Equals(musicPiece.Title, "Pelude", StringComparison.Ordinal))
                        {
                            musicPiece.Title = "Prelude";
                            pieceTitleFixes++;
                        }
                        // --- EINDE NORMALISATIE ---

                        // --- DATUM NORMALISATIE: zorg dat LastPracticeDate alleen de datum bevat, geen tijd ---
                        if (musicPiece.BarSections != null)
                        {
                            bool dateNormalizationNeeded = false;
                            foreach (var barSection in musicPiece.BarSections)
                            {
                                if (barSection.LastPracticeDate.HasValue)
                                {
                                    DateTime originalDate = barSection.LastPracticeDate.Value;
                                    DateTime normalizedDate = originalDate.Date;

                                    if (originalDate != normalizedDate)
                                    {
                                        barSection.LastPracticeDate = normalizedDate;
                                        dateNormalizationNeeded = true;
                                        MLLogManager.Instance.Log(
                                            $"Normalized LastPracticeDate for '{musicPiece.Title}' [{barSection.BarRange}] from {originalDate} to {normalizedDate:yyyy-MM-dd}",
                                            LogLevel.Debug);
                                    }
                                }
                            }

                            // Sla het stuk op als er datum normalisaties nodig waren
                            if (dateNormalizationNeeded)
                            {
                                SaveMusicPiece(musicPiece);
                            }
                        }
                        // --- EINDE DATUM NORMALISATIE ---

                        // --- MIGRATIE: lege Id vervangen door nieuwe GUID ---
                        if (musicPiece.Id == Guid.Empty)
                        {
                            var oldFileName = Path.GetFileName(jsonFile);
                            musicPiece.Id = Guid.NewGuid();

                            // herbewaar onder de nieuwe bestandsnaam
                            SaveMusicPiece(musicPiece);

                            // verwijder het oude bestand
                            try
                            {
                                File.Delete(jsonFile);
                            }
                            catch (Exception exDel)
                            {
                                MLLogManager.Instance.LogError(
                                    $"Could not delete legacy piece file {oldFileName}",
                                    exDel
                                );
                            }
                        }

                        // --- NIEUWE MIGRATIE: Check of bestandsnaam het nieuwe formaat heeft ---
                        string currentFileName = Path.GetFileName(jsonFile);
                        string expectedFileName = Path.GetFileName(musicPiece.GetFilePath(_musicPiecesFolder));

                        if (!string.Equals(currentFileName, expectedFileName, StringComparison.OrdinalIgnoreCase))
                        {
                            // Bestand heeft oude naamgeving, migreer naar nieuwe naamgeving
                            string newFilePath = musicPiece.GetFilePath(_musicPiecesFolder);

                            // Sla op onder de nieuwe naam
                            SaveMusicPiece(musicPiece);

                            // Verwijder oude bestand als nieuwe bestand succesvol is aangemaakt
                            if (File.Exists(newFilePath) && !string.Equals(jsonFile, newFilePath, StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    File.Delete(jsonFile);
                                    fileNameMigrations++;
                                    MLLogManager.Instance.Log(
                                        $"Migrated file name: '{currentFileName}' ? '{Path.GetFileName(newFilePath)}'",
                                        LogLevel.Info
                                );
                                }
                                catch (Exception exDel)
                                {
                                    MLLogManager.Instance.LogError(
                                        $"Could not delete old file {currentFileName} after migration",
                                        exDel
                                    );
                                }
                            }
                        }
                        // --- EINDE BESTANDSNAAM MIGRATIE ---

                        // --- MIGRATIE: BarSections zonder ParentMusicPieceId ---
                        if (musicPiece.BarSections != null)
                        {
                            bool migrationNeeded = false;
                            foreach (var barSection in musicPiece.BarSections)
                            {
                                if (barSection.ParentMusicPieceId == Guid.Empty)
                                {
                                    barSection.ParentMusicPieceId = musicPiece.Id;
                                    migrationNeeded = true;
                                    MLLogManager.Instance?.Log(
                                        $"Migrated BarSection '{barSection.BarRange}' in '{musicPiece.Title}' to include ParentMusicPieceId",
                                        LogLevel.Debug);
                                }
                            }

                            if (migrationNeeded)
                            {
                                SaveMusicPiece(musicPiece);
                            }
                        }
                        // --- EINDE PARENT ID MIGRATIE ---

                        MusicPieces.Add(musicPiece);
                    }
                    catch (Exception exPiece)
                    {
                        MLLogManager.Instance.LogError(
                            $"Error loading music piece from file {jsonFile}",
                            exPiece
                        );
                    }
                }

                if (pieceTitleFixes > 0)
                {
                    MLLogManager.Instance.Log(
                        $"Sanitized {pieceTitleFixes} music piece title(s): 'Pelude' -> 'Prelude'.",
                        LogLevel.Info
                    );
                }

                if (fileNameMigrations > 0)
                {
                    MLLogManager.Instance.Log(
                        $"Migrated {fileNameMigrations} file(s) to new naming format with title included.",
                        LogLevel.Info
                    );
                }

                MLLogManager.Instance.Log(
                    $"Successfully loaded {MusicPieces.Count} music pieces from disk.",
                    LogLevel.Info
                );
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error loading music pieces", ex);
            }
        }





        // Event handler for selection of a music piece
        // In MainWindow.xaml.cs

        private void LvMusicPieces_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // --- DE DEFINITIEVE OPLOSSING ---
            // Forceer het annuleren van een actieve bewerking op de DataGrid
            DgBarSections.CancelEdit();

            if (LvMusicPieces.SelectedItem is MusicPieceItem selectedMusicPiece)
            {
                // Update header info
                TxtSelectedMusicPieceTitle.Text = selectedMusicPiece.Title;
                TxtSelectedMusicPieceComposer.Text = selectedMusicPiece.Composer;
                TxtSelectedMusicPieceComposer.Visibility = Visibility.Visible;

                // Set the background color of the header to match the selected piece's color
                if (selectedMusicPiece.ColorBrush != null)
                {
                    BorderSelectedMusicPieceInfo.Background = MakeColorTransparent(selectedMusicPiece.ColorBrush, 0.5);
                }
                else
                {
                    BorderSelectedMusicPieceInfo.Background = Brushes.Transparent;
                }

                // Sync NextDueDates from scheduled sessions before setting the ItemsSource
                SyncNextDueDatesWithScheduledSessions(selectedMusicPiece);

                // Load bar sections - sorted by bar number, then by text suffix
                if (selectedMusicPiece.BarSections != null)
                {
                    DgBarSections.ItemsSource = new ObservableCollection<BarSection>(
                        SortBarSections(selectedMusicPiece.BarSections)
                    );
                }
                else
                {
                    DgBarSections.ItemsSource = null;
                }

                // Load practice sessions


                // Update total practice time UI for this piece
                UpdateTotalPracticeTimeDisplay();

                // Migrate old notes to the new structure if necessary
                if (!string.IsNullOrEmpty(selectedMusicPiece.Notes) &&
                    (selectedMusicPiece.NoteEntries == null || selectedMusicPiece.NoteEntries.Count == 0))
                {
                    // Create a new note with the old content
                    NoteEntry migratedNote = new NoteEntry { Title = "Migrated Notes", Content = selectedMusicPiece.Notes };
                    selectedMusicPiece.NoteEntries.Add(migratedNote);
                }

                // Update the Notes ViewModel with the selected music piece
                NotesViewModel.SelectedMusicPiece = selectedMusicPiece;

                // Update the status of the Resume menu item
                if (LvMusicPieces.ContextMenu != null)
                {
                    // Find the Resume menu item
                    var resumeMenuItem = LvMusicPieces.ContextMenu.Items.OfType<MenuItem>().FirstOrDefault(item => item.Header.ToString() == "Resume Now");
                    if (resumeMenuItem != null) resumeMenuItem.IsEnabled = selectedMusicPiece.IsPaused; // Enable/disable based on pause status
                }
            }
            else
            {
                // No selection, reset the UI
                TxtSelectedMusicPieceTitle.Text = "No music piece selected";
                TxtSelectedMusicPieceComposer.Text = "";
                TxtSelectedMusicPieceComposer.Visibility = Visibility.Collapsed;

                // Reset the background color
                BorderSelectedMusicPieceInfo.Background = Brushes.Transparent;

                // Reset bar sections
                DgBarSections.ItemsSource = null;



                // Update the Notes ViewModel with null
                NotesViewModel.SelectedMusicPiece = null;

                // Reset total time display
                if (TxtTotalPracticeTime != null)
                    TxtTotalPracticeTime.Text = "00:00:00"; // HH:MM:SS format

                // Disable the Resume menu item
                if (LvMusicPieces.ContextMenu != null)
                {
                    var resumeMenuItem = LvMusicPieces.ContextMenu.Items.OfType<MenuItem>().FirstOrDefault(item => item.Header.ToString() == "Resume Now");
                    if (resumeMenuItem != null) resumeMenuItem.IsEnabled = false;
                }
            }

        }

        private void LvNotes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LvNotes.SelectedItem is NoteEntry selectedNote)
            {
                NotesViewModel.OnNoteSelectionChanged(selectedNote);
            }
            else
            {
                NotesViewModel.OnNoteSelectionChanged(null);
            }
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            // Create a new About window
            AboutWindow aboutWindow = new AboutWindow { Owner = this };
            // Show the window
            aboutWindow.ShowDialog();
        }

        private void MenuPracticeTips_Click(object sender, RoutedEventArgs e)
        {
            // Create a new Practice Tips window
            PracticeTipsWindow tipsWindow = new PracticeTipsWindow { Owner = this };
            // Show the window
            tipsWindow.ShowDialog();
        }

        private void MenuUserManual_Click(object sender, RoutedEventArgs e) { OpenDocumentFile("Modus Practica 1.5 manual.pdf"); }
        private void MenuFutureOfPractice_Click(object sender, RoutedEventArgs e) { OpenDocumentFile("Modus Practica The Future of Musical Practice.pdf"); }
        private void MenuReadMe_Click(object sender, RoutedEventArgs e) { OpenDocumentFile("ReadMe.txt"); }
        private void MenuUpdatesList_Click(object sender, RoutedEventArgs e) { OpenDocumentFile("ChangeLog.txt"); }

        private void OpenDocumentFile(string filename)
        {
            try
            {
                // Determine the path to the documentation folder
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string docPath = Path.Combine(appDir, "Documentation", filename);
                // Check if the file exists
                if (File.Exists(docPath))
                {
                    // Open the file with the default application
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = docPath, UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show($"The file '{filename}' could not be found in the Documentation folder.", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening the document: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                MLLogManager.Instance.LogError($"Error opening document {filename}", ex);
            }
        }

        private void MenuAppSettings_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Configure application settings", "Not yet implemented"); }
        private void MenuExit_Click(object sender, RoutedEventArgs e) { Application.Current.Shutdown(); }
        private void MenuMLSettings_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Configure ML settings", "Not yet implemented"); }
        // Event handlers for menu items
        private void MenuOpen_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Music pieces are loaded automatically when the application starts.", "Information", MessageBoxButton.OK, MessageBoxImage.Information); }

        // AANPASSING
        private void MenuShowCalendar_Click(object sender, RoutedEventArgs e)
        {
            OpenCalendarWindow();
        }

        private void MenuPlaylistManager_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var playlistManager = new PlaylistManagerWindow(MusicPieces)
                {
                    Owner = this
                };
                playlistManager.ShowDialog();

                MLLogManager.Instance.Log("Opened Practice Playlist Manager", LogLevel.Info);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Failed to open Playlist Manager", ex);
                MessageBox.Show($"Error opening Playlist Manager: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuShowStatistics_Click(object sender, RoutedEventArgs e)
        {
            // Create the statistics window
            PracticeStatisticsWindow statisticsWindow = new PracticeStatisticsWindow { Owner = this };
            // Show the window as a dialog
            statisticsWindow.ShowDialog();
        }

        // NIEUWE: Open het venster voor vrije oefensessies via de knop
        private void BtnOpenFreePracticeWindow_Click(object sender, RoutedEventArgs e)
        {
            FreePracticeWindow freePracticeWindow = new FreePracticeWindow { Owner = this };
            freePracticeWindow.ShowDialog();
            // After closing (saved or canceled), refresh today's total in case a free session was recorded
            UpdateTodaysPracticeTimeDisplay();
        }

        private void MenuSpacedPracticeHelp_Click(object sender, RoutedEventArgs e) { MessageBox.Show("Explanation of Spaced Practice", "Not yet implemented"); }

        // Add this method to the MainWindow class
        private void MigrateOldFiles()
        {
            try
            {
                if (Directory.Exists(_musicPiecesFolder))
                {
                    // Zoek naar oude bestandsformaten: zowel de originele formaten als ID-only formaten
                    string[] oldFiles = Directory.GetFiles(_musicPiecesFolder, "*_*_*.json") // Origineel formaat
                        .Concat(Directory.GetFiles(_musicPiecesFolder, "*.json")
                            .Where(f => Guid.TryParse(Path.GetFileNameWithoutExtension(f), out _))) // ID-only formaat
                        .ToArray();

                    int migratedCount = 0;
                    foreach (string oldFile in oldFiles)
                    {
                        try
                        {
                            string jsonContent = File.ReadAllText(oldFile);
                            MusicPieceItem musicPiece = JsonSerializer.Deserialize<MusicPieceItem>(jsonContent);

                            // Check if the music piece already has a valid ID
                            if (musicPiece.Id == Guid.Empty)
                                musicPiece.Id = Guid.NewGuid(); // Generate a new ID

                            // Get the new filename with ID and title
                            string newPath = musicPiece.GetFilePath(_musicPiecesFolder);

                            // Save the file with the new name format
                            string updatedJsonContent = JsonSerializer.Serialize(musicPiece, new JsonSerializerOptions { WriteIndented = true });
                            File.WriteAllText(newPath, updatedJsonContent);

                            // Delete the old file if the new file was created successfully and paths are different
                            if (File.Exists(newPath) && !string.Equals(oldFile, newPath, StringComparison.OrdinalIgnoreCase))
                            {
                                File.Delete(oldFile);
                                migratedCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            MLLogManager.Instance.LogError($"Error migrating old file {oldFile}", ex);
                        } // Log the error but continue with other files
                    }
                    if (migratedCount > 0)
                        MessageBox.Show($"Migrated {migratedCount} music pieces to the new file format with readable names.", "Migration Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during migration: {ex.Message}", "Migration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                MLLogManager.Instance.LogError("Error during file migration", ex);
            }
        }

        private void OpenDrGebrianWindow_Click(object sender, RoutedEventArgs e)
        {
            DrMollyGebrianWindow drMollyGebrianWindow = new DrMollyGebrianWindow { Owner = this };
            drMollyGebrianWindow.ShowDialog();
        }

        // AANPASSING: Update scheduled session creation to use the calculated scheduledDate instead of NextDueDate
        private void PracticeBarSection_Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is BarSection barSection && LvMusicPieces.SelectedItem is MusicPieceItem selectedMusicPiece)
            {
                // Roep de nieuwe, centrale methode aan
                StartPracticeSessionForSection(selectedMusicPiece, barSection);
            }
        }

        // AANPASSING
        // Voorbeeld:
        private void PracticeBarSection_Click(object sender, RoutedEventArgs e)
        {
            if (DgBarSections.SelectedItem is BarSection selectedBarSection &&
                LvMusicPieces.SelectedItem is MusicPieceItem selectedMusicPiece)
            {
                // Check of de sectie Inactive is (extra check voor context menu)
                if (selectedBarSection.LifecycleState == LifecycleState.Inactive)
                {
                    MessageBox.Show($"Deze sectie is inactief en kan niet geoefend worden.\\n\\nZet de status eerst op 'Active' of 'Maintenance' om te kunnen oefenen.",
                        "Sectie Inactief", MessageBoxButton.OK, MessageBoxImage.Information);
                    MLLogManager.Instance?.Log($"Practice blocked (context menu): Section '{selectedBarSection.BarRange}' is Inactive", LogLevel.Info);
                    return;
                }

                // Roep de nieuwe, centrale methode aan
                StartPracticeSessionForSection(selectedMusicPiece, selectedBarSection);
            }
            else
            {
                MessageBox.Show("Selecteer een barsectie om te oefenen.", "Geen Selectie",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void StartPracticeSessionForSection(MusicPieceItem musicPiece, BarSection barSection)
        {
            // STAP 1: VOORBEREIDING EN CONTROLES (ALLEEN selectiecontrole)
            if (musicPiece == null || barSection == null)
            {
                MessageBox.Show("Selecteer een muziekstuk en een maatsectie om te oefenen.", "Geen Selectie",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Check of de sectie Inactive is
            if (barSection.LifecycleState == LifecycleState.Inactive)
            {
                MessageBox.Show($"Deze sectie is inactief en kan niet geoefend worden.\n\nZet de status eerst op 'Active' of 'Maintenance' om te kunnen oefenen.",
                    "Sectie Inactief", MessageBoxButton.OK, MessageBoxImage.Information);
                MLLogManager.Instance?.Log($"Practice blocked: Section '{barSection.BarRange}' is Inactive", LogLevel.Info);
                return;
            }

            // GEEN controle meer op 'vandaag' en GEEN waarschuwing meer.

            // STAP 1.5: Commit lopende DataGrid-edits (bijv. Lifecycle ComboBox wijzigingen)
            if (DgBarSections != null)
            {
                DgBarSections.CommitEdit(DataGridEditingUnit.Cell, true);
                DgBarSections.CommitEdit(DataGridEditingUnit.Row, true);
            }

            // STAP 1.6: Gebruik de canonieke sectie uit musicPiece.BarSections (by Id)
            var canonicalSection = musicPiece.BarSections?.FirstOrDefault(bs => bs.Id == barSection.Id) ?? barSection;

            // STAP 2: DE KERN ACTIE (CORE ACTION)
            var practiceWindow = new PracticeSessionWindow(musicPiece, canonicalSection) { Owner = this };

            // STAP 3: Alleen opslaan/verversen als DialogResult = true
            if (practiceWindow.ShowDialog() == true)
            {
                SaveMusicPiece(musicPiece);                          // opslaan
                ReloadSelectedMusicPiece();                          // herladen van schijf
                // historie verversen - no longer needed since Practice Sessions tab was removed
                DgBarSections.Items.Refresh();                       // grid verversen

                if (LvMusicPieces.SelectedItem is MusicPieceItem reloadedPiece)
                    reloadedPiece.UpdateProgress();                  // voortgang herberekenen

                LvMusicPieces.Items.Refresh();
                DgBarSections.Items.Refresh();
                SyncAllPiecesFromHistoryAndRefresh();                // alles syncen

                UpdateTotalPracticeTimeDisplay();                    // totale tijd bijwerken
            }
            else
            {
                MLLogManager.Instance.Log($"Practice session cancelled for '{barSection.BarRange}'.", LogLevel.Info);
            }
        }


        // TabMLDebug removed - ML debug tab no longer exists

        // --- Total practice time calculation and UI update ---
        private void UpdateTotalPracticeTimeDisplay()
        {
            try
            {
                if (TxtTotalPracticeTime == null) return;

                if (LvMusicPieces.SelectedItem is not MusicPieceItem piece)
                {
                    TxtTotalPracticeTime.Text = "00:00:00";
                    UpdateTodaysPracticeTimeDisplay();
                    return;
                }

                // Sum durations of non-deleted history entries for this piece
                var sessions = PracticeHistoryManager.Instance.GetHistoryForMusicPiece(piece.Id);
                var filtered = sessions.Where(h => !h.IsDeleted);
                var practice = filtered.Aggregate(TimeSpan.Zero, (acc, h) => acc + h.Duration);

                // Display HH:MM:SS format - only show Duration (session timer)
                TxtTotalPracticeTime.Text = $"{(int)practice.TotalHours:00}:{practice.Minutes:00}:{practice.Seconds:00}";

                // Update today's practice time display
                UpdateTodaysPracticeTimeDisplay();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Failed to update total practice time display", ex);
            }
        }

        /// <summary>
        /// Updates the large display showing today's total practice time.
        /// </summary>
        private void UpdateTodaysPracticeTimeDisplay()
        {
            try
            {
                if (TxtTodayPracticeTimeDisplay == null) return;

                // Get all sessions from today
                var allSessions = PracticeHistoryManager.Instance.GetAllHistory();
                if (allSessions == null)
                {
                    TxtTodayPracticeTimeDisplay.SetCurrentValue(TextBlock.TextProperty, "00:00:00");
                    return;
                }

                // Calculate total seconds practiced today (local Brussels day boundary)
                // PreparatoryPhaseDuration wordt nu gebruikt in performance score berekening
                var localToday = DateHelper.LocalToday();
                long todaySeconds = allSessions
                    .Where(s => DateOnly.FromDateTime(DateHelper.ToLocalBrussels(s.Date)) == DateOnly.FromDateTime(localToday))
                    .Sum(s => (long)Math.Round(s.Duration.TotalSeconds));

                // Format strictly as HH:MM:SS
                var ts = TimeSpan.FromSeconds(Math.Max(0, todaySeconds));
                int totalHours = (int)ts.TotalHours; // ensures >= 24h displays correctly
                string hhmmss = $"{totalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
                TxtTodayPracticeTimeDisplay.SetCurrentValue(TextBlock.TextProperty, hhmmss);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Failed to update today's practice time display", ex);
            }
        }



        private void CbNewBarSectionRepetitions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item)
                {
                    if (int.TryParse(item.Content?.ToString(), out var reps))
                    {
                        if (NewBarSection != null)
                        {
                            NewBarSection.TargetRepetitions = reps;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error updating TargetRepetitions from combo.", ex);
            }
        }



        #region Helper Methods

        /// <summary>
        /// Creates a transparent version of a given brush with specified opacity
        /// </summary>
        /// <param name="brush">The original brush</param>
        /// <param name="opacity">Opacity value between 0.0 and 1.0</param>
        /// <returns>A new brush with the specified opacity</returns>
        private Brush MakeColorTransparent(Brush brush, double opacity)
        {
            if (brush == null) return Brushes.Transparent;

            if (brush is SolidColorBrush solidBrush)
            {
                var color = solidBrush.Color;
                var transparentColor = Color.FromArgb(
                    (byte)(color.A * opacity),
                    color.R,
                    color.G,
                    color.B
                );
                return new SolidColorBrush(transparentColor);
            }

            // For other brush types, clone and set opacity
            var clonedBrush = brush.Clone();
            clonedBrush.Opacity = opacity;
            return clonedBrush;
        }

        #endregion
    }
}

namespace ModusPractica
{
    /// <summary>
    /// Simple dialog to input a URL with optional initial value
    /// </summary>
    public class UrlInputDialog : Window
    {
        private readonly TextBox _textBox;
        public string ResponseText => _textBox.Text;

        public UrlInputDialog(string title, string message, string? initialValue = null)
        {
            Title = title;
            Width = 500;
            Height = 190;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid { Margin = new Thickness(15) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var textBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(textBlock, 0);
            grid.Children.Add(textBlock);

            _textBox = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                Height = 30,
                VerticalContentAlignment = VerticalAlignment.Center,
                Text = initialValue ?? string.Empty
            };
            Grid.SetRow(_textBox, 1);
            grid.Children.Add(_textBox);

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnOk = new Button
            {
                Content = "OK",
                Width = 80,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            btnOk.Click += (s, e) => { DialogResult = true; Close(); };

            var btnCancel = new Button
            {
                Content = "Cancel",
                Width = 80,
                IsCancel = true
            };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };

            panel.Children.Add(btnOk);
            panel.Children.Add(btnCancel);
            Grid.SetRow(panel, 2);
            grid.Children.Add(panel);

            Content = grid;

            Loaded += (s, e) => _textBox.Focus();
        }
    }
}

