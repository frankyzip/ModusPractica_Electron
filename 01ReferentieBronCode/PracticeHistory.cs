using System.ComponentModel;

namespace ModusPractica
{
    public class PracticeHistory : INotifyPropertyChanged
    {
        private Guid _id;
        private Guid _musicPieceId;
        private string _musicPieceTitle;
        private Guid _barSectionId;
        private string _barSectionRange;
        private DateTime _date;
        private TimeSpan _duration;
        private int _repetitions;
        private string _difficulty;
        private string _notes;
        private int _attemptsTillSuccess;
        private int _repetitionStreakAttempts;
        private int _totalFailures; // v4.0: Alle failures tijdens de sessie (voor succesratio-berekening)
        private string _sessionOutcome;
        private bool _isDeleted;
        private TimeSpan _preparatoryPhaseDuration;


        // --- NIEUWE PROPERTIES ---
        private int _targetTempo;
        private int _achievedTempo;
        private int _targetRepetitions;

        // --- USER OVERRIDE PROPERTIES ---
        private double? _userOverrideInterval;
        private double? _originalAlgorithmInterval;
        private string _overrideReason;

        public PracticeHistory()
        {
            _id = Guid.NewGuid();
            _musicPieceId = Guid.Empty;
            _musicPieceTitle = string.Empty;
            _barSectionId = Guid.Empty;
            _barSectionRange = string.Empty;
            _date = DateTime.Now;
            _duration = TimeSpan.Zero;
            _repetitions = 0;
            _difficulty = string.Empty;
            _notes = string.Empty;
            _attemptsTillSuccess = 0;
            _repetitionStreakAttempts = 0;
            _totalFailures = 0;
            _sessionOutcome = "TargetReached";
            _isDeleted = false;
            _preparatoryPhaseDuration = TimeSpan.Zero;

            _targetTempo = 0; // Default to 0, meaning not set for this session
            _achievedTempo = 0; // Default to 0, meaning not set for this session
            _targetRepetitions = 0;

            // Initialize new override properties
            _userOverrideInterval = null;
            _originalAlgorithmInterval = null;
            _overrideReason = string.Empty;
        }

        public TimeSpan PreparatoryPhaseDuration
        {
            get { return _preparatoryPhaseDuration; }
            set
            {
                if (_preparatoryPhaseDuration != value)
                {
                    _preparatoryPhaseDuration = value;
                    OnPropertyChanged(nameof(PreparatoryPhaseDuration));
                }
            }
        }

        public Guid Id
        {
            get { return _id; }
            set { if (_id != value) { _id = value; OnPropertyChanged(nameof(Id)); } }
        }

        public Guid MusicPieceId
        {
            get { return _musicPieceId; }
            set { if (_musicPieceId != value) { _musicPieceId = value; OnPropertyChanged(nameof(MusicPieceId)); } }
        }

        public string MusicPieceTitle
        {
            get { return _musicPieceTitle; }
            set { if (_musicPieceTitle != value) { _musicPieceTitle = value; OnPropertyChanged(nameof(MusicPieceTitle)); } }
        }

        public int AttemptsTillSuccess
        {
            get { return _attemptsTillSuccess; }
            set { if (_attemptsTillSuccess != value) { _attemptsTillSuccess = value; OnPropertyChanged(nameof(AttemptsTillSuccess)); } }
        }

        public Guid BarSectionId
        {
            get { return _barSectionId; }
            set { if (_barSectionId != value) { _barSectionId = value; OnPropertyChanged(nameof(BarSectionId)); } }
        }

        public string BarSectionRange
        {
            get { return _barSectionRange; }
            set { if (_barSectionRange != value) { _barSectionRange = value; OnPropertyChanged(nameof(BarSectionRange)); } }
        }

        public DateTime Date
        {
            get { return _date; }
            set { if (_date != value) { _date = value; OnPropertyChanged(nameof(Date)); } }
        }

        public TimeSpan Duration
        {
            get { return _duration; }
            set { if (_duration != value) { _duration = value; OnPropertyChanged(nameof(Duration)); } }
        }

        public int Repetitions
        {
            get { return _repetitions; }
            set { if (_repetitions != value) { _repetitions = value; OnPropertyChanged(nameof(Repetitions)); } }
        }

        public string Difficulty
        {
            get { return _difficulty; }
            set { if (_difficulty != value) { _difficulty = value; OnPropertyChanged(nameof(Difficulty)); } }
        }

        public string Notes
        {
            get { return _notes; }
            set { if (_notes != value) { _notes = value; OnPropertyChanged(nameof(Notes)); } }
        }

        public int RepetitionStreakAttempts
        {
            get { return _repetitionStreakAttempts; }
            set { if (_repetitionStreakAttempts != value) { _repetitionStreakAttempts = value; OnPropertyChanged(nameof(RepetitionStreakAttempts)); } }
        }

        /// <summary>
        /// v4.0: Total aantal failures tijdens de hele sessie.
        /// Verschil met AttemptsTillSuccess: AttemptsTillSuccess = failures vóór eerste succes.
        /// TotalFailures = alle failures gedurende de hele sessie.
        /// Gebruikt voor succesratio-berekening: Repetitions / (Repetitions + TotalFailures).
        /// </summary>
        public int TotalFailures
        {
            get { return _totalFailures; }
            set { if (_totalFailures != value) { _totalFailures = value; OnPropertyChanged(nameof(TotalFailures)); OnPropertyChanged(nameof(SuccessRatio)); } }
        }

        /// <summary>
        /// v4.0: Succesratio gebaseerd op het 85%-succesregel onderzoek.
        /// Berekent: correct / (correct + failures).
        /// Ideale waarde volgens onderzoek: ~0.85 (85% succes, 15% errors).
        /// </summary>
        public double SuccessRatio
        {
            get
            {
                int total = _repetitions + _totalFailures;
                if (total == 0) return 0.0;
                return (double)_repetitions / total;
            }
        }

        /// <summary>
        /// v4.0: Classificatie van de leerzone op basis van succesratio.
        /// Exploration: 60-80% (uitdaging, nieuwe materiaal)
        /// Consolidation: 80-90% (optimale leerzone, 85%-regel)
        /// Polish: 90-95% (verfijning, performance-ready)
        /// Mastered: >95% (beheerst, klaar voor langere intervallen)
        /// TooHard: <60% (te moeilijk, meer voorbereiding nodig)
        /// </summary>
        public string LearningZone
        {
            get
            {
                // Voor oude sessies zonder TotalFailures data: toon "Legacy" in plaats van foutieve zone
                if (_repetitions > 0 && _totalFailures == 0)
                {
                    return "Legacy";
                }

                double ratio = SuccessRatio;
                if (ratio < 0.60) return "TooHard";
                if (ratio < 0.80) return "Exploration";
                if (ratio < 0.90) return "Consolidation";
                if (ratio < 0.95) return "Polish";
                return "Mastered";
            }
        }

        public string SessionOutcome
        {
            get { return _sessionOutcome; }
            set { if (_sessionOutcome != value) { _sessionOutcome = value; OnPropertyChanged(nameof(SessionOutcome)); } }
        }



        /// <summary>
        /// Gets or sets the target tempo (in BPM) for this specific practice session.
        /// </summary>
        public int TargetTempo
        {
            get => _targetTempo;
            set => _targetTempo = Math.Clamp(value, 30, 300);
        }

        /// <summary>
        /// Gets or sets the highest successful tempo (in BPM) achieved during this practice session.
        /// </summary>
        public int AchievedTempo
        {
            get { return _achievedTempo; }
            set { if (_achievedTempo != value) { _achievedTempo = value; OnPropertyChanged(nameof(AchievedTempo)); } }
        }

        /// <summary>
        /// Gets or sets the target number of repetitions for this specific practice session.
        /// </summary>
        public int TargetRepetitions
        {
            get { return _targetRepetitions; }
            set { if (_targetRepetitions != value) { _targetRepetitions = value; OnPropertyChanged(nameof(TargetRepetitions)); } }
        }

        public bool IsDeleted
        {
            get { return _isDeleted; }
            set { if (_isDeleted != value) { _isDeleted = value; OnPropertyChanged(nameof(IsDeleted)); } }
        }

        private float _performanceScore;

        /// <summary>
        /// The calculated performance score (0-10) for this session, based on multiple factors.
        /// </summary>
        public float PerformanceScore
        {
            get { return _performanceScore; }
            set { if (_performanceScore != value) { _performanceScore = value; OnPropertyChanged(nameof(PerformanceScore)); } }
        }

        /// <summary>
        /// The interval (in days) that the user manually overrode, if any.
        /// </summary>
        public double? UserOverrideInterval
        {
            get { return _userOverrideInterval; }
            set { if (_userOverrideInterval != value) { _userOverrideInterval = value; OnPropertyChanged(nameof(UserOverrideInterval)); } }
        }

        /// <summary>
        /// The original interval (in days) suggested by the algorithm before user override.
        /// </summary>
        public double? OriginalAlgorithmInterval
        {
            get { return _originalAlgorithmInterval; }
            set { if (_originalAlgorithmInterval != value) { _originalAlgorithmInterval = value; OnPropertyChanged(nameof(OriginalAlgorithmInterval)); } }
        }

        /// <summary>
        /// The reason the user provided for overriding the interval.
        /// </summary>
        public string OverrideReason
        {
            get { return _overrideReason; }
            set { if (_overrideReason != value) { _overrideReason = value ?? string.Empty; OnPropertyChanged(nameof(OverrideReason)); } }
        }

        /// <summary>
        /// Indicates whether this practice history entry contains a user override.
        /// </summary>
        public bool IsUserOverride => UserOverrideInterval.HasValue;

        /// <summary>
        /// v4.0: Berekent rolling average succesratio over laatste N sessies voor deze bar section.
        /// Gebruikt minimum 3 sessies voor betrouwbaarheid, maximum 10 voor responsiviteit.
        /// Default: 7 sessies (optimale balans tussen stabiliteit en responsiviteit).
        /// </summary>
        /// <param name="barSectionId">De bar section waarvoor de ratio berekend wordt</param>
        /// <param name="allHistory">Alle practice history records (gesorteerd op datum)</param>
        /// <param name="windowSize">Aantal sessies voor rolling average (default=7)</param>
        /// <returns>Succesratio tussen 0.0 en 1.0, of 0.0 als geen data beschikbaar</returns>
        public static double CalculateRollingSuccessRatio(Guid barSectionId, List<PracticeHistory> allHistory, int windowSize = 7)
        {
            if (allHistory == null || allHistory.Count == 0) return 0.0;

            // Filter op deze bar section en sorteer op datum (nieuwste eerst)
            var relevantSessions = allHistory
                .Where(h => h.BarSectionId == barSectionId && !h.IsDeleted)
                .OrderByDescending(h => h.Date)
                .ToList();

            if (relevantSessions.Count == 0) return 0.0;

            // Minimum 3 sessies voor betrouwbaarheid, gebruik beschikbare data als er minder zijn
            int effectiveWindow = Math.Min(windowSize, relevantSessions.Count);
            effectiveWindow = Math.Max(1, effectiveWindow); // Minimum 1 sessie

            var sessionsForAverage = relevantSessions.Take(effectiveWindow).ToList();

            // Tel totaal aantal successes en failures over deze sessies
            int totalReps = sessionsForAverage.Sum(s => s.Repetitions);
            int totalFailures = sessionsForAverage.Sum(s => s.TotalFailures);
            int total = totalReps + totalFailures;

            if (total == 0) return 0.0;
            return (double)totalReps / total;
        }

        /// <summary>
        /// v4.0: Berekent learning zone op basis van rolling average succesratio.
        /// </summary>
        /// <param name="successRatio">Succesratio tussen 0.0 en 1.0</param>
        /// <returns>Learning zone classificatie</returns>
        public static string GetLearningZoneFromRatio(double successRatio)
        {
            if (successRatio < 0.60) return "TooHard";
            if (successRatio < 0.80) return "Exploration";
            if (successRatio < 0.90) return "Consolidation";
            if (successRatio < 0.95) return "Polish";
            return "Mastered";
        }

        /// <summary>
        /// v4.0: Converteert learning zone naar display label met emoji en beschrijving.
        /// </summary>
        /// <param name="learningZone">Learning zone naam (TooHard, Exploration, etc.)</param>
        /// <returns>Geformatteerde display string</returns>
        public static string GetLearningZoneDisplayLabel(string learningZone)
        {
            return learningZone switch
            {
                "TooHard" => "⚠ Too Hard",
                "Exploration" => "🔍 Exploration",
                "Consolidation" => "✅ Consolidation (Ideal)",
                "Polish" => "💎 Polish",
                "Mastered" => "⭐ Mastered",
                _ => learningZone // fallback voor onbekende zones
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}