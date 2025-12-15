using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ModusPractica
{
    /// <summary>
    /// Represents a practice playlist that can contain sections from multiple pieces.
    /// Designed to support Dr. Gebrian's interleaved practice methodology.
    /// </summary>
    public class PracticePlaylist : INotifyPropertyChanged
    {
        private Guid _id;
        private string? _name;
        private string? _description;
        private DateTime _createdAt;
        private DateTime _lastUsedAt;
        private ObservableCollection<PlaylistItem> _items;
        private bool _isActive;
        private int _totalDurationMinutes;
        private string _createdBy;

        public PracticePlaylist()
        {
            _id = Guid.NewGuid();
            _createdAt = DateTime.Now;
            _lastUsedAt = DateTime.Now;
            _items = new ObservableCollection<PlaylistItem>();
            _items.CollectionChanged += Items_CollectionChanged;
            _isActive = false;
            _createdBy = ActiveUserSession.ProfileName ?? string.Empty;
        }

        public Guid Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged(nameof(Id));
                }
            }
        }

        public string Name
        {
            get => _name ?? string.Empty;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        public string Description
        {
            get => _description ?? string.Empty;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set
            {
                if (_createdAt != value)
                {
                    _createdAt = value;
                    OnPropertyChanged(nameof(CreatedAt));
                }
            }
        }

        public DateTime LastUsedAt
        {
            get => _lastUsedAt;
            set
            {
                if (_lastUsedAt != value)
                {
                    _lastUsedAt = value;
                    OnPropertyChanged(nameof(LastUsedAt));
                }
            }
        }

        public ObservableCollection<PlaylistItem> Items
        {
            get => _items;
            set
            {
                if (_items != value)
                {
                    if (_items != null)
                        _items.CollectionChanged -= Items_CollectionChanged;

                    _items = value ?? new ObservableCollection<PlaylistItem>();
                    _items.CollectionChanged += Items_CollectionChanged;

                    OnPropertyChanged(nameof(Items));
                    UpdateDerivedProperties();
                }
            }
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged(nameof(IsActive));
                }
            }
        }

        public int TotalDurationMinutes
        {
            get => _totalDurationMinutes;
            private set
            {
                if (_totalDurationMinutes != value)
                {
                    _totalDurationMinutes = value;
                    OnPropertyChanged(nameof(TotalDurationMinutes));
                    OnPropertyChanged(nameof(EstimatedDuration));
                }
            }
        }

        public string CreatedBy
        {
            get => _createdBy;
            set
            {
                if (_createdBy != value)
                {
                    _createdBy = value;
                    OnPropertyChanged(nameof(CreatedBy));
                }
            }
        }

        /// <summary>
        /// Display-friendly name for UI binding
        /// </summary>
        public string DisplayName => $"{Name} ({Items.Count} sections, ~{TotalDurationMinutes}min)";

        /// <summary>
        /// Human-readable duration estimate
        /// </summary>
        public string EstimatedDuration
        {
            get
            {
                if (TotalDurationMinutes < 60)
                    return $"{TotalDurationMinutes} minutes";

                int hours = TotalDurationMinutes / 60;
                int minutes = TotalDurationMinutes % 60;
                return minutes > 0 ? $"{hours}h {minutes}min" : $"{hours}h";
            }
        }

        /// <summary>
        /// Progress tracking for completed items
        /// </summary>
        public int CompletedItemsCount => Items.Count(item => item.IsCompleted);

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public double ProgressPercentage => Items.Count > 0 ? (CompletedItemsCount * 100.0 / Items.Count) : 0;

        /// <summary>
        /// Status display for UI
        /// </summary>
        public string StatusDisplay
        {
            get
            {
                if (Items.Count == 0) return "Empty";
                if (CompletedItemsCount == 0) return "Not Started";
                if (CompletedItemsCount == Items.Count) return "Complete";
                return $"In Progress ({CompletedItemsCount}/{Items.Count})";
            }
        }

        /// <summary>
        /// Gets the next incomplete item for practice
        /// </summary>
        public PlaylistItem? GetNextItem()
        {
            return Items.OrderBy(item => item.OrderIndex)
                       .FirstOrDefault(item => !item.IsCompleted);
        }

        /// <summary>
        /// Adds a BarSection to the playlist
        /// </summary>
        public void AddBarSection(MusicPieceItem musicPiece, BarSection barSection, int durationMinutes = 2)
        {
            var playlistItem = PlaylistItem.FromBarSection(musicPiece, barSection, durationMinutes);
            playlistItem.OrderIndex = Items.Count;
            Items.Add(playlistItem);
        }

        /// <summary>
        /// Removes an item from the playlist and reorders remaining items
        /// </summary>
        public void RemoveItem(PlaylistItem item)
        {
            if (Items.Remove(item))
            {
                // Reorder remaining items
                for (int i = 0; i < Items.Count; i++)
                {
                    Items[i].OrderIndex = i;
                }
            }
        }

        /// <summary>
        /// Moves an item to a new position in the playlist
        /// </summary>
        public void MoveItem(PlaylistItem item, int newIndex)
        {
            int currentIndex = Items.IndexOf(item);
            if (currentIndex == -1 || newIndex == currentIndex) return;

            Items.RemoveAt(currentIndex);
            Items.Insert(Math.Min(newIndex, Items.Count), item);

            // Reorder all items
            for (int i = 0; i < Items.Count; i++)
            {
                Items[i].OrderIndex = i;
            }
        }

        /// <summary>
        /// Resets all completion status for a fresh practice session
        /// </summary>
        public void ResetProgress()
        {
            foreach (var item in Items)
            {
                item.IsCompleted = false;
            }
            LastUsedAt = DateTime.Now;
        }

        /// <summary>
        /// Marks the playlist as used (updates LastUsedAt)
        /// </summary>
        public void MarkAsUsed()
        {
            LastUsedAt = DateTime.Now;
        }

        private void Items_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateDerivedProperties();
        }

        /// <summary>
        /// Automatically generates an interleaved practice playlist from selected music pieces
        /// </summary>
        public static PracticePlaylist AutoGenerateInterleavedPlaylist(IEnumerable<MusicPieceItem> selectedPieces, int defaultDurationMinutes = 2)
        {
            if (!selectedPieces.Any())
                throw new ArgumentException("At least one music piece must be selected");

            var allSections = new List<(MusicPieceItem Piece, BarSection Section)>();

            // Collect all sections from selected pieces
            foreach (var piece in selectedPieces)
            {
                if (piece.BarSections != null)
                {
                    foreach (var section in piece.BarSections)
                    {
                        allSections.Add((piece, section));
                    }
                }
            }

            if (!allSections.Any())
                throw new ArgumentException("Selected pieces have no sections to practice");

            // Create interleaved ordering
            var interleavedSections = CreateInterleavedOrdering(allSections);

            // Generate playlist name and description
            string playlistName = GeneratePlaylistName(selectedPieces);
            string description = GeneratePlaylistDescription(selectedPieces, interleavedSections.Count);

            // Create the playlist
            var playlist = new PracticePlaylist
            {
                Name = playlistName,
                Description = description,
                CreatedAt = DateTime.Now,
                LastUsedAt = DateTime.Now
            };

            // Add sections in interleaved order
            foreach (var (piece, section) in interleavedSections)
            {
                playlist.AddBarSection(piece, section, defaultDurationMinutes);
            }

            return playlist;
        }

        /// <summary>
        /// Creates an optimal interleaved ordering of sections for practice
        /// </summary>
        private static List<(MusicPieceItem Piece, BarSection Section)> CreateInterleavedOrdering(List<(MusicPieceItem Piece, BarSection Section)> sections)
        {
            if (sections.Count <= 1)
                return sections;

            // For true interleaved practice, randomize ALL sections completely
            // This prevents any predictable patterns that the brain could anticipate
            return sections.OrderBy(_ => Guid.NewGuid()).ToList();
        }

        /// <summary>
        /// Generates an appropriate name for the auto-generated playlist
        /// </summary>
        private static string GeneratePlaylistName(IEnumerable<MusicPieceItem> pieces)
        {
            var pieceList = pieces.ToList();
            if (pieceList.Count == 1)
            {
                return $"{pieceList[0].Title} - Interleaved Practice";
            }
            else if (pieceList.Count == 2)
            {
                return $"{pieceList[0].Title} + {pieceList[1].Title} - Interleaved Practice";
            }
            else
            {
                return $"{pieceList[0].Title} + {pieceList.Count - 1} others - Interleaved Practice";
            }
        }

        /// <summary>
        /// Generates a description for the auto-generated playlist
        /// </summary>
        private static string GeneratePlaylistDescription(IEnumerable<MusicPieceItem> pieces, int totalSections)
        {
            var pieceList = pieces.ToList();
            string pieceNames = string.Join(", ", pieceList.Select(p => p.Title));

            return $"Auto-generated interleaved practice session with {totalSections} sections from: {pieceNames}. " +
                   $"All sections are randomly shuffled for maximum context discrimination and enhanced retention.";
        }

        private void UpdateDerivedProperties()
        {
            TotalDurationMinutes = Items.Sum(item => item.DurationMinutes);
            OnPropertyChanged(nameof(CompletedItemsCount));
            OnPropertyChanged(nameof(ProgressPercentage));
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(DisplayName));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
