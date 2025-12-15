using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ModusPractica
{
    public class PracticeSession : INotifyPropertyChanged
    {
        private Guid _id;
        private DateTime _date;
        private int _duration; // Duur in minuten
        private double _progress;
        private string _notes;


        public PracticeSession()
        {
            // Nieuwe ID genereren voor elke sessie
            _id = Guid.NewGuid();
            _date = DateTime.Now;
            _duration = 0;
            _progress = 0.0;
            _notes = string.Empty;
            _practiceSessions = new ObservableCollection<PracticeSession>();

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

        public DateTime Date
        {
            get { return _date; }
            set
            {
                if (_date != value)
                {
                    _date = value;
                    OnPropertyChanged("Date");
                }
            }
        }

        public int Duration
        {
            get { return _duration; }
            set
            {
                if (_duration != value)
                {
                    _duration = value;
                    OnPropertyChanged("Duration");
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



        // INotifyPropertyChanged implementatie
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Voeg deze code toe aan de MusicPieceItem klasse
        private ObservableCollection<PracticeSession> _practiceSessions;

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
    }
}