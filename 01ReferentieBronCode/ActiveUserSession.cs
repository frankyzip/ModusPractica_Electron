namespace ModusPractica
{
    /// <summary>
    /// Houdt de context bij voor de actieve gebruiker.
    /// </summary>
    public static class ActiveUserSession
    {
        /// <summary>
        /// Standaardprofielnaam wanneer er geen keuze mogelijk is.
        /// </summary>
        public const string DefaultProfileName = "Default";

        private static string _profileName = DefaultProfileName;

        /// <summary>
        /// Naam van het actieve profiel, gebruikt voor padbepaling.
        /// </summary>
        public static string ProfileName
        {
            get => _profileName;
            set => _profileName = string.IsNullOrWhiteSpace(value) ? DefaultProfileName : value;
        }
    }
}
