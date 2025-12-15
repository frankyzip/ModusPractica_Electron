using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace ModusPractica
{
    public class MusicPieceItem : INotifyPropertyChanged
    {
        private Guid _id;
        private string _title;
        private string _composer;
        private DateTime _creationDate;
        private double _progress;
        private SolidColorBrush? _colorBrush; // Marked as nullable
        private string? _colorResourceName; // Nieuwe property om de naam van de kleurresource op te slaan
        private string _notes;
        private ObservableCollection<NoteEntry> _noteEntries;
        private ObservableCollection<BarSection> _barSections;
        private ObservableCollection<PracticeSession> _practiceSessions;
        private bool _isPaused;
        private DateTime? _pauseUntilDate;
        private string _youTubeLink = string.Empty;

        public MusicPieceItem()
        {
            _title = string.Empty;
            _composer = string.Empty;
            _colorBrush = null;
            _colorResourceName = null;
            _notes = string.Empty;
            _noteEntries = new ObservableCollection<NoteEntry>();
            _barSections = new ObservableCollection<BarSection>();
            _practiceSessions = new ObservableCollection<PracticeSession>();
            _youTubeLink = string.Empty;
        }

        public Guid Id
        {
            get { return _id; }
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged("Id");
                }
            }
        }

        // NOTE: Archiving has been removed from the app. Pieces can be paused or permanently deleted.

        public string Title
        {
            get { return _title; }
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged("Title");
                }
            }
        }

        public string Composer
        {
            get { return _composer; }
            set
            {
                if (_composer != value)
                {
                    _composer = value;
                    OnPropertyChanged("Composer");
                }
            }
        }

        public DateTime CreationDate
        {
            get { return _creationDate; }
            set
            {
                if (_creationDate != value)
                {
                    _creationDate = value;
                    OnPropertyChanged("CreationDate");
                }
            }
        }

        public double Progress
        {
            get { return _progress; }
            set
            {
                if (_progress != value)
                {
                    _progress = value;
                    OnPropertyChanged("Progress");
                }
            }
        }

        [JsonIgnore] // Negeren bij JSON serialisatie
        public SolidColorBrush? ColorBrush
        {
            get { return _colorBrush; }
            set
            {
                if (_colorBrush != value)
                {
                    _colorBrush = value;

                    // Update ook de resource naam wanneer de kleur verandert
                    if (_colorBrush != null)
                    {
                        foreach (var entry in MainWindow.ColorResourceMap)
                        {
                            if (entry.Value.Color == _colorBrush.Color)
                            {
                                _colorResourceName = entry.Key;
                                break;
                            }
                        }
                    }
                    else
                    {
                        _colorResourceName = null;
                    }

                    OnPropertyChanged("ColorBrush");
                }
            }
        }

        // Property voor serialisatie
        public string ColorResourceName
        {
            get { return _colorResourceName ?? string.Empty; }
            set
            {
                if (_colorResourceName != value)
                {
                    _colorResourceName = value;
                    OnPropertyChanged("ColorResourceName");
                }
            }
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Optional per-piece YouTube quick link. Stored in the piece JSON file.
        /// </summary>
        public string YouTubeLink
        {
            get => _youTubeLink;
            set
            {
                if (_youTubeLink != value)
                {
                    _youTubeLink = value ?? string.Empty;
                    OnPropertyChanged(nameof(YouTubeLink));
                }
            }
        }

        /// <summary>
        /// Notificeert alle afgeleide retentie-properties zodat de UI correct wordt bijgewerkt.
        /// Roep deze aan na wijzigingen in BarSections die de retentie beïnvloeden.
        /// </summary>
        public void RefreshRetentionProperties()
        {
            OnPropertyChanged(nameof(AverageSuccessRatio));
            OnPropertyChanged(nameof(LowestSuccessRatio));
            OnPropertyChanged(nameof(IsLowestAboveOrEqualAverage));
            OnPropertyChanged(nameof(IsLowestCriticallyLow));
            OnPropertyChanged(nameof(WeakestSection));
            OnPropertyChanged(nameof(SectionsReadyCount));
        }

        // Helper methode om het bestandspad voor dit muziekstuk te genereren
        public string GetFilePath(string folderPath)
        {
            // Maak de titel veilig voor bestandsnamen door ongeldige karakters te vervangen
            string safeTitle = string.IsNullOrWhiteSpace(Title) ? "Untitled" : Title;

            // Vervang ongeldige bestandsnaam karakters door underscores
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char invalidChar in invalidChars)
            {
                safeTitle = safeTitle.Replace(invalidChar, '_');
            }

            // Beperk de lengte om te lange bestandsnamen te voorkomen
            if (safeTitle.Length > 50)
            {
                safeTitle = safeTitle.Substring(0, 50);
            }

            // Combineer ID en titel voor unieke en herkenbare bestandsnaam
            // Format: {ID}_{Title}.json
            return Path.Combine(folderPath, $"{Id}_{safeTitle}.json");
        }

        // Property voor maatsecties
        public ObservableCollection<BarSection> BarSections
        {
            get
            {
                if (_barSections == null)
                {
                    _barSections = new ObservableCollection<BarSection>();
                }
                return _barSections;
            }
            set
            {
                _barSections = value;
                OnPropertyChanged("BarSections");
            }
        }

        public ObservableCollection<NoteEntry> NoteEntries
        {
            get
            {
                if (_noteEntries == null)
                {
                    _noteEntries = new ObservableCollection<NoteEntry>();
                }
                return _noteEntries;
            }
            set
            {
                _noteEntries = value;
                OnPropertyChanged("NoteEntries");
            }
        }

        /// <summary>
        /// Berekent de gemiddelde success ratio over alle bar sections van dit muziekstuk.
        /// Gebruikt de 7-session rolling average per sectie.
        /// Returns null als geen secties met data beschikbaar zijn.
        /// </summary>
        [JsonIgnore]
        public double? AverageSuccessRatio
        {
            get
            {
                if (BarSections == null || BarSections.Count == 0) return null;

                var ratios = BarSections
                    .Select(s => s.AverageSuccessRatio)
                    .Where(r => r.HasValue)
                    .Select(r => r!.Value)
                    .ToList();

                if (ratios.Count == 0) return null;
                return ratios.Average();
            }
        }

        /// <summary>
        /// Vindt de laagste success ratio van alle bar sections (de "zwakste schakel").
        /// Dit is vaak de meest waardevolle metric, want bepaalt of je het complete stuk kunt spelen.
        /// Returns null als geen secties met data beschikbaar zijn.
        /// </summary>
        [JsonIgnore]
        public double? LowestSuccessRatio
        {
            get
            {
                if (BarSections == null || BarSections.Count == 0) return null;

                var ratios = BarSections
                    .Select(s => s.AverageSuccessRatio)
                    .Where(r => r.HasValue)
                    .Select(r => r!.Value)
                    .ToList();

                if (ratios.Count == 0) return null;
                return ratios.Min();
            }
        }

        /// <summary>
        /// Geeft aan of de laagste retentie >= gemiddelde retentie is.
        /// Gebruikt voor groene kleurcode in UI (geen waarschuwing nodig).
        /// </summary>
        [JsonIgnore]
        public bool IsLowestAboveOrEqualAverage
        {
            get
            {
                var lowest = LowestSuccessRatio;
                var average = AverageSuccessRatio;

                if (!lowest.HasValue || !average.HasValue) return false;

                return lowest.Value >= average.Value;
            }
        }

        /// <summary>
        /// Geeft aan of de laagste retentie kritiek laag is (< 60%).
        /// Gebruikt voor rode waarschuwing in UI.
        /// </summary>
        [JsonIgnore]
        public bool IsLowestCriticallyLow
        {
            get
            {
                var lowest = LowestSuccessRatio;
                if (!lowest.HasValue) return false;

                return lowest.Value < 0.60;
            }
        }

        /// <summary>
        /// Vindt de bar section met de laagste success ratio.
        /// Useful voor "Focus Here!" feedback.
        /// </summary>
        [JsonIgnore]
        public BarSection? WeakestSection
        {
            get
            {
                if (BarSections == null || BarSections.Count == 0) return null;

                return BarSections
                    .Where(s => s.AverageSuccessRatio.HasValue)
                    .OrderBy(s => s.AverageSuccessRatio!.Value)
                    .FirstOrDefault();
            }
        }

        /// <summary>
        /// Aantal secties dat "ready" is (success ratio >= 80% - Consolidation zone of hoger).
        /// </summary>
        [JsonIgnore]
        public int SectionsReadyCount
        {
            get
            {
                if (BarSections == null || BarSections.Count == 0) return 0;

                return BarSections.Count(s =>
                    s.AverageSuccessRatio.HasValue &&
                    s.AverageSuccessRatio.Value >= 0.80);
            }
        }

        /// <summary>
        /// Percentage van secties dat ready is (>= 80% success ratio).
        /// </summary>
        [JsonIgnore]
        public double SectionsReadyPercentage
        {
            get
            {
                if (BarSections == null || BarSections.Count == 0) return 0;

                var sectionsWithData = BarSections.Count(s => s.AverageSuccessRatio.HasValue);
                if (sectionsWithData == 0) return 0;

                return (double)SectionsReadyCount / sectionsWithData * 100;
            }
        }

        /// <summary>
        /// Bepaalt of het complete stuk "ready for performance" is.
        /// Criteria: Alle secties >= 85% success ratio (Consolidation of hoger).
        /// </summary>
        [JsonIgnore]
        public bool IsReadyForPerformance
        {
            get
            {
                if (BarSections == null || BarSections.Count == 0) return false;

                // Alle secties met data moeten >= 85% zijn
                var sectionsWithData = BarSections.Where(s => s.AverageSuccessRatio.HasValue).ToList();
                if (sectionsWithData.Count == 0) return false;

                return sectionsWithData.All(s => s.AverageSuccessRatio!.Value >= 0.85);
            }
        }

        // Methode om de voortgang te berekenen op basis van succesratio over laatste 7 sessies van het hele stuk
        public void UpdateProgress()
        {
            UpdateProgressFromSuccessRatio();
        }

        // Nieuwe methode om voortgang te berekenen op basis van succesratio over laatste 7 sessies van het hele stuk
        public void UpdateProgressFromSuccessRatio()
        {
            // Haal alle sessies van dit stuk op (inclusief verwijderde secties, want history blijft)
            var allHistory = PracticeHistoryManager.Instance.GetAllHistory()
                .Where(h => h.MusicPieceId == this.Id && !h.IsDeleted)
                .OrderByDescending(h => h.Date)
                .ToList();

            if (allHistory.Count == 0)
            {
                // Geen sessies: voortgang 0%
                Progress = 0;
                OnPropertyChanged("Progress");
                return;
            }

            // Neem laatste 7 sessies (of alle als minder)
            int sessionsToUse = Math.Min(7, allHistory.Count);
            var recentSessions = allHistory.Take(sessionsToUse);

            // Tel totaal correcte herhalingen en fouten
            int totalReps = recentSessions.Sum(s => s.Repetitions);
            int totalFailures = recentSessions.Sum(s => s.TotalFailures);
            int totalAttempts = totalReps + totalFailures;

            if (totalAttempts == 0)
            {
                // Geen geldige data: voortgang 0%
                Progress = 0;
                OnPropertyChanged("Progress");
                return;
            }

            // Bereken succesratio en zet om naar percentage
            double successRatio = (double)totalReps / totalAttempts;
            Progress = successRatio * 100;
            OnPropertyChanged("Progress");
        }

        public string Notes
        {
            get { return _notes; }
            set
            {
                if (_notes != value)
                {
                    _notes = value;
                    OnPropertyChanged("Notes");
                }
            }
        }

        public ObservableCollection<PracticeSession> PracticeSessions
        {
            get
            {
                if (_practiceSessions == null)
                {
                    _practiceSessions = new ObservableCollection<PracticeSession>();
                }
                return _practiceSessions;
            }
            set
            {
                _practiceSessions = value;
                OnPropertyChanged("PracticeSessions");
            }
        }

        public bool IsPaused
        {
            get { return _isPaused; }
            set
            {
                if (_isPaused != value)
                {
                    _isPaused = value;
                    OnPropertyChanged("IsPaused");
                }
            }
        }

        public DateTime? PauseUntilDate
        {
            get { return _pauseUntilDate; }
            set
            {
                if (_pauseUntilDate != value)
                {
                    _pauseUntilDate = value;
                    OnPropertyChanged("PauseUntilDate");
                }
            }
        }
    }
}