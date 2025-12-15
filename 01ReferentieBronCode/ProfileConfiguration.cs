using System;
using System.IO;
using System.Text.Json;

namespace ModusPractica
{
    /// <summary>
    /// Persistent configuration for profile selection and custom data root.
    /// Stored outside the data root itself to survive data folder changes.
    /// </summary>
    public class ProfileConfiguration
    {
        public string LastUsedProfile { get; set; } = "Default";
        public string CustomRootPath { get; set; } = string.Empty;

        private static string GetConfigFilePath()
        {
            // Store in LocalApplicationData (not roaming, machine-specific)
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string configFolder = Path.Combine(localAppData, "ModusPractica");
            Directory.CreateDirectory(configFolder);
            return Path.Combine(configFolder, "profile_config.json");
        }

        public static ProfileConfiguration Load()
        {
            try
            {
                string configPath = GetConfigFilePath();
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<ProfileConfiguration>(json);
                    return config ?? new ProfileConfiguration();
                }
            }
            catch (Exception ex)
            {
                // Log but don't crash - return defaults
                System.Diagnostics.Debug.WriteLine($"Failed to load profile config: {ex.Message}");
            }

            return new ProfileConfiguration();
        }

        public static void Save(ProfileConfiguration config)
        {
            try
            {
                string configPath = GetConfigFilePath();
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save profile config: {ex.Message}");
                throw;
            }
        }
    }
}
