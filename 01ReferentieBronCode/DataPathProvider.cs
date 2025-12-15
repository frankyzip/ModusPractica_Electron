using System;
using System.IO;

namespace ModusPractica
{
    /// <summary>
    /// Centralizes all filesystem paths for user data to prevent cross-profile leakage.
    /// Default root is %APPDATA%\ModusPractica. A custom root can be set at runtime.
    /// </summary>
    public static class DataPathProvider
    {
        // In-memory override; can be persisted later if a UI is added
        private static string? _customRoot;

        /// <summary>
        /// Sets a custom data root (e.g., a user-chosen folder). No validation here.
        /// </summary>
        public static void SetCustomRoot(string? absolutePath)
        {
            _customRoot = string.IsNullOrWhiteSpace(absolutePath) ? null : absolutePath;
        }

        /// <summary>
        /// Returns the application data root folder.
        /// Default: %APPDATA%\ModusPractica
        /// </summary>
        public static string GetAppRoot()
        {
            if (!string.IsNullOrWhiteSpace(_customRoot))
                return _customRoot!;

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ModusPractica");
        }

        public static string GetProfilesRoot()
        {
            return Path.Combine(GetAppRoot(), "Profiles");
        }

        public static string GetProfileFolder(string profileName)
        {
            var folder = Path.Combine(GetProfilesRoot(), Sanitize(profileName));
            Directory.CreateDirectory(folder);
            return folder;
        }

        public static string GetLogsFolder(string profileName)
        {
            var folder = Path.Combine(GetProfileFolder(profileName), "Logs");
            Directory.CreateDirectory(folder);
            return folder;
        }

        public static string GetHistoryFolder(string profileName)
        {
            var folder = Path.Combine(GetProfileFolder(profileName), "History");
            Directory.CreateDirectory(folder);
            return folder;
        }

        public static string GetScheduledFolder(string profileName)
        {
            var folder = Path.Combine(GetProfileFolder(profileName), "Scheduled");
            Directory.CreateDirectory(folder);
            return folder;
        }

        public static string GetSightReadingFolder(string profileName)
        {
            var folder = Path.Combine(GetProfileFolder(profileName), "SightReading");
            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>
        /// Returns the path to the autocomplete data file. Scoped per profile to avoid leakage.
        /// </summary>
        public static string GetAutocompleteDataFile(string profileName)
        {
            var profileFolder = GetProfileFolder(profileName);
            return Path.Combine(profileFolder, "autocomplete_data.json");
        }

        /// <summary>
        /// Sanitizes a profile name to be safe for use in file paths and names.
        /// </summary>
        public static string Sanitize(string name)
        {
            // Very basic sanitization to avoid illegal path chars
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return string.IsNullOrWhiteSpace(name) ? ActiveUserSession.DefaultProfileName : name;
        }
    }
}
