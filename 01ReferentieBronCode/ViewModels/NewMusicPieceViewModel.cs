using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace ModusPractica
{
    public sealed class NewMusicPieceViewModel : INotifyPropertyChanged
    {
        private readonly List<MusicPieceItem> _allMusicPieces;
        private readonly RelayCommand _createCommand;
        private readonly RelayCommand _cancelCommand;
        private readonly RelayCommand _removeTitleSuggestionCommand;
        private readonly RelayCommand _removeComposerSuggestionCommand;

        private string _title = string.Empty;
        private string _composer = string.Empty;
        private string _selectedColorKey = "PastelBlue";
        private SolidColorBrush? _selectedColorBrush;
        private MusicPieceItem? _createdMusicPiece;

        // Full lists of all suggestions
        private readonly List<string> _allTitles;
        private readonly List<string> _allComposers;

        // Flag to prevent filtering during programmatic updates
        private bool _isUpdating = false;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<RequestCloseEventArgs>? RequestClose;

        // Filtered collections bound to UI
        public ObservableCollection<string> TitleSuggestions { get; }
        public ObservableCollection<string> ComposerSuggestions { get; }

        public NewMusicPieceViewModel(IEnumerable<MusicPieceItem> allMusicPieces)
        {
            _allMusicPieces = allMusicPieces?.ToList() ?? new List<MusicPieceItem>();

            _createCommand = new RelayCommand(_ => Create(), _ => CanCreate());
            _cancelCommand = new RelayCommand(_ => Cancel());
            _removeTitleSuggestionCommand = new RelayCommand(p => RemoveTitleSuggestion(p as string));
            _removeComposerSuggestionCommand = new RelayCommand(p => RemoveComposerSuggestion(p as string));

            // Load autocomplete suggestions
            var autocompleteData = AutocompleteDataManager.Load();
            _allTitles = autocompleteData.Titles
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _allComposers = autocompleteData.Composers
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();

            TitleSuggestions = new ObservableCollection<string>(_allTitles);
            ComposerSuggestions = new ObservableCollection<string>(_allComposers);

            UpdateSelectedColorBrush();
        }

        public RelayCommand CreateCommand => _createCommand;

        public RelayCommand CancelCommand => _cancelCommand;

        public RelayCommand RemoveTitleSuggestionCommand => _removeTitleSuggestionCommand;

        public RelayCommand RemoveComposerSuggestionCommand => _removeComposerSuggestionCommand;

        public MusicPieceItem? CreatedMusicPiece
        {
            get => _createdMusicPiece;
            private set => SetProperty(ref _createdMusicPiece, value);
        }

        // Archiving/restoring removed: no RestoredMusicPiece flow

        public string Title
        {
            get => _title;
            set
            {
                if (SetProperty(ref _title, value))
                {
                    OnPropertyChanged(nameof(PreviewTitle));
                    _createCommand.RaiseCanExecuteChanged();
                    FilterTitleSuggestions(value);
                }
            }
        }

        public string Composer
        {
            get => _composer;
            set
            {
                if (SetProperty(ref _composer, value))
                {
                    OnPropertyChanged(nameof(PreviewComposer));
                    _createCommand.RaiseCanExecuteChanged();
                    FilterComposerSuggestions(value);
                }
            }
        }

        public string PreviewTitle => string.IsNullOrWhiteSpace(Title) ? "Music Piece Title" : Title;

        public string PreviewComposer => string.IsNullOrWhiteSpace(Composer) ? "Composer" : Composer;

        public string SelectedColorKey
        {
            get => _selectedColorKey;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                if (SetProperty(ref _selectedColorKey, value))
                {
                    UpdateSelectedColorBrush();
                }
            }
        }

        public SolidColorBrush? SelectedColorBrush
        {
            get => _selectedColorBrush;
            private set => SetProperty(ref _selectedColorBrush, value);
        }

        private bool CanCreate()
        {
            return !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(Composer);
        }

        private void Create()
        {
            string trimmedTitle = (Title ?? string.Empty).Trim();
            string trimmedComposer = (Composer ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(trimmedTitle))
            {
                MessageBox.Show("Please enter a title for the music piece.", "Missing Title", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(trimmedComposer))
            {
                MessageBox.Show("Please enter a composer for the music piece.", "Missing Composer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool duplicateExists = _allMusicPieces.Any(p =>
                string.Equals(p.Title, trimmedTitle, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.Composer, trimmedComposer, StringComparison.OrdinalIgnoreCase));

            if (duplicateExists)
            {
                MessageBox.Show($"A piece named '{trimmedTitle}' by '{trimmedComposer}' already exists. Please choose a unique name.",
                    "Duplicate Piece", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string finalTitle = DeletedPieceRegistry.ApplyPrefixIfNeeded(trimmedTitle, trimmedComposer);
            if (!string.Equals(finalTitle, trimmedTitle, StringComparison.OrdinalIgnoreCase))
            {
                string candidate = finalTitle;
                int counter = 2;
                while (_allMusicPieces.Any(p =>
                    string.Equals(p.Title, candidate, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.Composer, trimmedComposer, StringComparison.OrdinalIgnoreCase)))
                {
                    candidate = $"{finalTitle} ({counter})";
                    counter++;
                }

                finalTitle = candidate;
            }

            var createdPiece = new MusicPieceItem
            {
                Id = Guid.NewGuid(),
                Title = finalTitle,
                Composer = trimmedComposer,
                CreationDate = DateTime.Now,
                Progress = 0,
                // Archiveren bestaat niet meer
                ColorBrush = SelectedColorBrush,
                ColorResourceName = SelectedColorKey
            };

            // Save to autocomplete data
            AutocompleteDataManager.AddEntry(trimmedTitle, trimmedComposer);

            // Update local collections if needed
            if (!_allTitles.Any(t => string.Equals(t, trimmedTitle, StringComparison.OrdinalIgnoreCase)))
            {
                _allTitles.Add(trimmedTitle);
                _allTitles.Sort();
            }
            if (!_allComposers.Any(c => string.Equals(c, trimmedComposer, StringComparison.OrdinalIgnoreCase)))
            {
                _allComposers.Add(trimmedComposer);
                _allComposers.Sort();
            }

            CreatedMusicPiece = createdPiece;

            RequestClose?.Invoke(this, new RequestCloseEventArgs(true, createdPiece));
        }

        private void Cancel()
        {
            RequestClose?.Invoke(this, new RequestCloseEventArgs(false));
        }

        private void UpdateSelectedColorBrush()
        {
            SelectedColorBrush = TryFindBrush(SelectedColorKey) ?? CreateFallbackBrush();
        }

        private static SolidColorBrush? TryFindBrush(string resourceKey)
        {
            if (string.IsNullOrWhiteSpace(resourceKey) || Application.Current == null)
            {
                return null;
            }

            if (Application.Current.TryFindResource(resourceKey) is SolidColorBrush brush)
            {
                return brush;
            }

            return null;
        }

        private static SolidColorBrush CreateFallbackBrush()
        {
            return new SolidColorBrush(Color.FromRgb(0xB3, 0xD9, 0xFF));
        }

        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void RemoveTitleSuggestion(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return;

            var result = MessageBox.Show($"Verwijder suggestie '{title}'?", "Bevestigen", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            _isUpdating = true;

            // Remove from UI list and full list (case-insensitive)
            for (int i = TitleSuggestions.Count - 1; i >= 0; i--)
            {
                if (string.Equals(TitleSuggestions[i], title, StringComparison.OrdinalIgnoreCase))
                {
                    TitleSuggestions.RemoveAt(i);
                }
            }

            _allTitles.RemoveAll(t => string.Equals(t, title, StringComparison.OrdinalIgnoreCase));

            AutocompleteDataManager.RemoveTitle(title);

            _isUpdating = false;

            // Re-filter to refresh list based on current text
            FilterTitleSuggestions(Title);
        }

        private void RemoveComposerSuggestion(string? composer)
        {
            if (string.IsNullOrWhiteSpace(composer)) return;

            var result = MessageBox.Show($"Verwijder suggestie '{composer}'?", "Bevestigen", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            _isUpdating = true;

            for (int i = ComposerSuggestions.Count - 1; i >= 0; i--)
            {
                if (string.Equals(ComposerSuggestions[i], composer, StringComparison.OrdinalIgnoreCase))
                {
                    ComposerSuggestions.RemoveAt(i);
                }
            }

            _allComposers.RemoveAll(c => string.Equals(c, composer, StringComparison.OrdinalIgnoreCase));

            AutocompleteDataManager.RemoveComposer(composer);

            _isUpdating = false;

            FilterComposerSuggestions(Composer);
        }

        // Called by code-behind on selection to avoid recursive filtering glitches
        public void SetTitleFromSelection(string title)
        {
            _isUpdating = true;
            Title = title;
            _isUpdating = false;
        }

        public void SetComposerFromSelection(string composer)
        {
            _isUpdating = true;
            Composer = composer;
            _isUpdating = false;
        }

        private void FilterTitleSuggestions(string? searchText)
        {
            if (_isUpdating) return;

            // Filter suggestions based on current text without clearing user input
            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Show all suggestions if empty
                UpdateCollection(TitleSuggestions, _allTitles);
            }
            else
            {
                // Filter suggestions: match on start of string OR start of any word
                var filtered = _allTitles
                    .Where(t => MatchesSearchText(t, searchText))
                    .ToList();

                UpdateCollection(TitleSuggestions, filtered);
            }
        }

        private void FilterComposerSuggestions(string? searchText)
        {
            if (_isUpdating) return;

            // Filter suggestions based on current text without clearing user input
            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Show all suggestions if empty
                UpdateCollection(ComposerSuggestions, _allComposers);
            }
            else
            {
                // Filter suggestions: match on start of string OR start of any word
                var filtered = _allComposers
                    .Where(c => MatchesSearchText(c, searchText))
                    .ToList();

                UpdateCollection(ComposerSuggestions, filtered);
            }
        }

        /// <summary>
        /// Checks if the text matches the search term. Matches at the start of the string
        /// or at the start of any word (after a space).
        /// </summary>
        private static bool MatchesSearchText(string text, string searchText)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(searchText))
                return false;

            // Match at start of string
            if (text.StartsWith(searchText, StringComparison.OrdinalIgnoreCase))
                return true;

            // Match at start of any word (after a space)
            int index = 0;
            while ((index = text.IndexOf(' ', index)) >= 0)
            {
                index++; // Move past the space
                if (index < text.Length &&
                    text.Substring(index).StartsWith(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void UpdateCollection(ObservableCollection<string> target, IEnumerable<string> source)
        {
            // Efficient update: remove items not in source, add items not in target
            var sourceList = source.ToList();

            // Remove items that are no longer in the filtered list
            for (int i = target.Count - 1; i >= 0; i--)
            {
                if (!sourceList.Contains(target[i], StringComparer.OrdinalIgnoreCase))
                {
                    target.RemoveAt(i);
                }
            }

            // Add new items that aren't already in the target
            foreach (var item in sourceList)
            {
                if (!target.Any(t => string.Equals(t, item, StringComparison.OrdinalIgnoreCase)))
                {
                    target.Add(item);
                }
            }
        }
    }
}
