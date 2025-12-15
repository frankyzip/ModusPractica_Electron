using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace ModusPractica.ViewModels
{
    public class NotesViewModel : INotifyPropertyChanged
    {
        private NoteEntry? _currentNote;
        private bool _isNotesInitializing = false;
        private MusicPieceItem? _selectedMusicPiece;
        private ICommand? _addNoteCommand;
        private ICommand? _saveNoteCommand;
        private ICommand? _addTimestampCommand;

        public NotesViewModel()
        {
        }

        public NoteEntry? CurrentNote
        {
            get { return _currentNote; }
            set
            {
                if (_currentNote != value)
                {
                    _currentNote = value;
                    OnPropertyChanged(nameof(CurrentNote));
                }
            }
        }

        public bool IsNotesInitializing
        {
            get { return _isNotesInitializing; }
            set
            {
                if (_isNotesInitializing != value)
                {
                    _isNotesInitializing = value;
                    OnPropertyChanged(nameof(IsNotesInitializing));
                }
            }
        }

        public MusicPieceItem? SelectedMusicPiece
        {
            get { return _selectedMusicPiece; }
            set
            {
                if (_selectedMusicPiece != value)
                {
                    _selectedMusicPiece = value;
                    OnPropertyChanged(nameof(SelectedMusicPiece));
                    OnPropertyChanged(nameof(NoteEntries));
                }
            }
        }

        public ObservableCollection<NoteEntry>? NoteEntries
        {
            get { return SelectedMusicPiece?.NoteEntries; }
        }

        public ICommand AddNoteCommand
        {
            get
            {
                return _addNoteCommand ??= new RelayCommand(
                    param => AddNote(),
                    param => SelectedMusicPiece != null);
            }
        }

        public ICommand SaveNoteCommand
        {
            get
            {
                return _saveNoteCommand ??= new RelayCommand(
                    param => SaveNote(),
                    param => CurrentNote != null && SelectedMusicPiece != null);
            }
        }

        public ICommand AddTimestampCommand
        {
            get
            {
                return _addTimestampCommand ??= new RelayCommand(
                    param => AddTimestamp(param),
                    param => CurrentNote != null);
            }
        }

        public void AddNote()
        {
            if (SelectedMusicPiece != null)
            {
                // Create a new note
                NoteEntry newNote = new NoteEntry
                {
                    // CreationDate is set in NoteEntry constructor
                    Title = $"Note {SelectedMusicPiece.NoteEntries.Count + 1}",
                    Content = ""
                };
                // Add to the collection
                SelectedMusicPiece.NoteEntries.Add(newNote);
                // Select the new note
                CurrentNote = newNote;
            }
            else
            {
                MessageBox.Show("Selecteer een muziekstuk.", "Geen Selectie",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public void SaveNote()
        {
            if (CurrentNote == null || SelectedMusicPiece == null)
            {
                MessageBox.Show("Select a music piece and a note to save.", "No Note Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // De daadwerkelijke opslag gebeurt in de UI-laag
            // We notificeren alleen dat er wijzigingen zijn
            OnPropertyChanged(nameof(NoteEntries));
        }

        public void AddTimestamp(object? parameter)
        {
            // Deze methode wordt aangeroepen vanuit de view met de juiste parameter
            // De daadwerkelijke implementatie gebeurt in de code-behind van de view
        }

        public void OnNoteSelectionChanged(NoteEntry? selectedNote)
        {
            IsNotesInitializing = true;
            if (selectedNote != null)
            {
                CurrentNote = selectedNote;
            }
            else
            {
                CurrentNote = null;
            }
            IsNotesInitializing = false;
        }

        // INotifyPropertyChanged implementatie
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Eenvoudige implementatie van ICommand
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke(parameter) ?? true;
        }

        public void Execute(object? parameter)
        {
            _execute(parameter);
        }
    }
}