using System.Globalization;

namespace ModusPractica
{
    /// <summary>
    /// Provides a central, static point of access for the application's active culture.
    /// This bypasses potential WPF rendering issues where Thread.CurrentCulture might be ignored.
    /// The 'Current' property is set at application startup based on user settings.
    /// </summary>
    public static class CultureHelper
    {
        /// <summary>
        /// Gets or sets the active CultureInfo for all formatting operations in the UI.
        /// </summary>
        public static CultureInfo Current { get; set; }

        /// <summary>
        /// Initializes the CultureHelper class.
        /// The static constructor is called automatically only once before the first time
        /// any member of the class is accessed.
        /// </summary>
        static CultureHelper()
        {
            // Initialize with the system's default culture as a safe fallback.
            // This value will be overwritten by the user's saved setting in App.xaml.cs on startup.
            Current = CultureInfo.CurrentCulture;
        }
    }
}