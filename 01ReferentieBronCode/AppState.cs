namespace ModusPractica
{
    /// <summary>
    /// Provides a simple, global state container for the application.
    /// </summary>
    public static class AppState
    {
        /// <summary>
        /// A flag that indicates if music piece data (e.g., after a practice session) has been modified.
        /// The MainWindow can check this flag upon activation to decide if a data refresh is needed.
        /// </summary>
        public static bool MusicDataChanged { get; set; } = false;

        /// <summary>
        /// When true, suppress lifecycle side-effects and persistence during bulk loads/deserialization.
        /// This prevents SectionLifecycleService from removing sessions or saving while simply loading data.
        /// </summary>
        public static bool SuppressLifecycleSideEffects { get; set; } = false;
    }
}