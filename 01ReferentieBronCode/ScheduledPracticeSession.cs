namespace ModusPractica
{
    public class ScheduledPracticeSession
    {
        private DateTime _scheduledDate;

        public Guid Id { get; set; }
        public Guid MusicPieceId { get; set; }
        public string MusicPieceTitle { get; set; }
        public Guid BarSectionId { get; set; }
        public string BarSectionRange { get; set; }

        /// <summary>
        /// STANDARDIZED: Scheduled date with automatic normalization to date-only
        /// </summary>
        public DateTime ScheduledDate
        {
            get => _scheduledDate;
            set => _scheduledDate = DateHelper.NormalizeToDateOnly(value);
        }

        public TimeSpan EstimatedDuration { get; set; }
        public string Difficulty { get; set; }
        public string Status { get; set; }

        /// <summary>
        /// Completion date when the session was marked as completed
        /// </summary>
        public DateTime? CompletionDate { get; set; }

        /// <summary>
        /// Reason why the session was marked as completed (e.g., "TargetReached", "Repetitions=5", etc.)
        /// </summary>
        public string CompletionReason { get; set; }


        /// <summary>
        /// The calculated Tau (τ) value, representing the memory decay rate in days.
        /// This indicates how long the memory for this section is expected to last.
        /// </summary>
        public double TauValue { get; set; }

        /// <summary>
        /// STANDARDIZED: Check if this session is due for practice today
        /// </summary>
        public bool IsDueToday => DateHelper.IsSessionDueToday(ScheduledDate);

        /// <summary>
        /// STANDARDIZED: Check if this session is scheduled for today
        /// </summary>
        public bool IsToday => DateHelper.IsToday(ScheduledDate);
    }
}