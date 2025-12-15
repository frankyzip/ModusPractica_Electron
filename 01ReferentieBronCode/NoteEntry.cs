using System.ComponentModel;

namespace ModusPractica
{
    public class NoteEntry : INotifyPropertyChanged
    {
        private Guid _id;
        private DateTime _creationDate;
        private string _title;
        private string _content;

        public NoteEntry()
        {
            _id = Guid.NewGuid();
            _creationDate = DateTime.Now;
            _title = string.Empty;
            _content = string.Empty;
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

        public string Content
        {
            get { return _content; }
            set
            {
                if (_content != value)
                {
                    _content = value;
                    OnPropertyChanged("Content");
                }
            }
        }

        // INotifyPropertyChanged implementatie
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}