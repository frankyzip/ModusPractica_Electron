using System.ComponentModel;

namespace ModusPractica
{
    /// <summary>
    /// Represents a single item in a practice playlist.
    /// Can reference a BarSection from any MusicPiece.
    /// </summary>
    public class PlaylistItem : INotifyPropertyChanged
    {
        private Guid _id;
        private Guid _musicPieceId;
        private string? _musicPieceTitle;
        private Guid _barSectionId;
        private string? _barSectionRange;
        private int _durationMinutes;
        private int _orderIndex;
        private bool _isCompleted;
        private DateTime? _completedAt;
        private string? _notes;

        public PlaylistItem()
        {
            _id = Guid.NewGuid();
            _durationMinutes = 2; // Default 2 minutes as Dr. Gebrian requested
            _isCompleted = false;
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

        public Guid MusicPieceId
        {
            get => _musicPieceId;
            set
            {
                if (_musicPieceId != value)
                {
                    _musicPieceId = value;
                    OnPropertyChanged(nameof(MusicPieceId));
                }
            }
        }

        public string MusicPieceTitle
        {
            get => _musicPieceTitle ?? string.Empty;
            set
            {
                if (_musicPieceTitle != value)
                {
                    _musicPieceTitle = value;
                    OnPropertyChanged(nameof(MusicPieceTitle));
                }
            }
        }

        public Guid BarSectionId
        {
            get => _barSectionId;
            set
            {
                if (_barSectionId != value)
                {
                    _barSectionId = value;
                    OnPropertyChanged(nameof(BarSectionId));
                }
            }
        }

        public string BarSectionRange
        {
            get => _barSectionRange ?? string.Empty;
            set
            {
                if (_barSectionRange != value)
                {
                    _barSectionRange = value;
                    OnPropertyChanged(nameof(BarSectionRange));
                }
            }
        }

        /// <summary>
        /// Duration for this specific playlist item (in minutes).
        /// Supports Dr. Gebrian's principle of focused, time-bounded practice.
        /// </summary>
        public int DurationMinutes
        {
            get => _durationMinutes;
            set
            {
                if (_durationMinutes != value)
                {
                    _durationMinutes = Math.Max(1, value); // Minimum 1 minute
                    OnPropertyChanged(nameof(DurationMinutes));
                }
            }
        }

        public int OrderIndex
        {
            get => _orderIndex;
            set
            {
                if (_orderIndex != value)
                {
                    _orderIndex = value;
                    OnPropertyChanged(nameof(OrderIndex));
                }
            }
        }

        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                if (_isCompleted != value)
                {
                    _isCompleted = value;
                    if (value && _completedAt == null)
                        _completedAt = DateTime.Now;
                    else if (!value)
                        _completedAt = null;

                    OnPropertyChanged(nameof(IsCompleted));
                    OnPropertyChanged(nameof(CompletedAt));
                    OnPropertyChanged(nameof(StatusDisplay));
                }
            }
        }

        public DateTime? CompletedAt
        {
            get => _completedAt;
            set
            {
                if (_completedAt != value)
                {
                    _completedAt = value;
                    OnPropertyChanged(nameof(CompletedAt));
                }
            }
        }

        public string Notes
        {
            get => _notes ?? string.Empty;
            set
            {
                if (_notes != value)
                {
                    _notes = value;
                    OnPropertyChanged(nameof(Notes));
                }
            }
        }

        /// <summary>
        /// Display-friendly status for UI binding
        /// </summary>
        public string StatusDisplay => _isCompleted ? "✅ Complete" : "⏱️ Pending";

        /// <summary>
        /// Full display name for the playlist item
        /// </summary>
        public string DisplayName => $"{MusicPieceTitle} - {BarSectionRange} ({DurationMinutes}min)";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Creates a PlaylistItem from an existing BarSection
        /// </summary>
        public static PlaylistItem FromBarSection(MusicPieceItem musicPiece, BarSection barSection, int durationMinutes = 2)
        {
            return new PlaylistItem
            {
                MusicPieceId = musicPiece.Id,
                MusicPieceTitle = musicPiece.Title,
                BarSectionId = barSection.Id,
                BarSectionRange = barSection.BarRange,
                DurationMinutes = durationMinutes
            };
        }
    }
}
