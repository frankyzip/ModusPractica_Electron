using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ModusPractica
{
    /// <summary>
    /// Manages autocomplete data for titles and composers, stored in a JSON file.
    /// </summary>
    public static class AutocompleteDataManager
    {
        private static string GetDataFilePath()
        {
            // Scope autocomplete per profiel om data-lekkage te voorkomen
            return DataPathProvider.GetAutocompleteDataFile(ActiveUserSession.ProfileName);
        }

        private static readonly object _lockObject = new object();

        /// <summary>
        /// Loads the autocomplete data from the JSON file.
        /// </summary>
        public static AutocompleteData Load()
        {
            lock (_lockObject)
            {
                try
                {
                    string dataFile = GetDataFilePath();
                    if (!File.Exists(dataFile))
                    {
                        return new AutocompleteData();
                    }

                    string json = File.ReadAllText(dataFile);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        return new AutocompleteData();
                    }

                    var data = JsonSerializer.Deserialize<AutocompleteData>(json);
                    return data ?? new AutocompleteData();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading autocomplete data: {ex.Message}");
                    return new AutocompleteData();
                }
            }
        }

        /// <summary>
        /// Removes a title from the autocomplete data (case-insensitive).
        /// Returns true when data changed.
        /// </summary>
        public static bool RemoveTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return false;
            }

            var data = Load();
            int before = data.Titles.Count;
            data.Titles = data.Titles
                .Where(t => !string.Equals(t, title.Trim(), StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (data.Titles.Count != before)
            {
                Save(data);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes a composer from the autocomplete data (case-insensitive).
        /// Returns true when data changed.
        /// </summary>
        public static bool RemoveComposer(string composer)
        {
            if (string.IsNullOrWhiteSpace(composer))
            {
                return false;
            }

            var data = Load();
            int before = data.Composers.Count;
            data.Composers = data.Composers
                .Where(c => !string.Equals(c, composer.Trim(), StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (data.Composers.Count != before)
            {
                Save(data);
                return true;
            }

            return false;
        }
        /// <summary>
        /// Saves the autocomplete data to the JSON file.
        /// </summary>
        public static void Save(AutocompleteData data)
        {
            lock (_lockObject)
            {
                try
                {
                    string dataFile = GetDataFilePath();
                    string? directory = Path.GetDirectoryName(dataFile);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };

                    string json = JsonSerializer.Serialize(data, options);
                    File.WriteAllText(dataFile, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving autocomplete data: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Adds a new title to the autocomplete data if it doesn't already exist.
        /// </summary>
        public static void AddTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            var data = Load();
            string trimmedTitle = title.Trim();

            if (!data.Titles.Any(t => string.Equals(t, trimmedTitle, StringComparison.OrdinalIgnoreCase)))
            {
                data.Titles.Add(trimmedTitle);
                data.Titles.Sort();
                Save(data);
            }
        }

        /// <summary>
        /// Adds a new composer to the autocomplete data if it doesn't already exist.
        /// </summary>
        public static void AddComposer(string composer)
        {
            if (string.IsNullOrWhiteSpace(composer))
            {
                return;
            }

            var data = Load();
            string trimmedComposer = composer.Trim();

            if (!data.Composers.Any(c => string.Equals(c, trimmedComposer, StringComparison.OrdinalIgnoreCase)))
            {
                data.Composers.Add(trimmedComposer);
                data.Composers.Sort();
                Save(data);
            }
        }

        /// <summary>
        /// Adds both a title and composer to the autocomplete data.
        /// </summary>
        public static void AddEntry(string title, string composer)
        {
            var data = Load();
            bool changed = false;

            if (!string.IsNullOrWhiteSpace(title))
            {
                string trimmedTitle = title.Trim();
                if (!data.Titles.Any(t => string.Equals(t, trimmedTitle, StringComparison.OrdinalIgnoreCase)))
                {
                    data.Titles.Add(trimmedTitle);
                    changed = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(composer))
            {
                string trimmedComposer = composer.Trim();
                if (!data.Composers.Any(c => string.Equals(c, trimmedComposer, StringComparison.OrdinalIgnoreCase)))
                {
                    data.Composers.Add(trimmedComposer);
                    changed = true;
                }
            }

            if (changed)
            {
                data.Titles.Sort();
                data.Composers.Sort();
                Save(data);
            }
        }
    }

    /// <summary>
    /// Data structure for storing autocomplete suggestions.
    /// </summary>
    public class AutocompleteData
    {
        public List<string> Titles { get; set; } = new List<string>();
        public List<string> Composers { get; set; } = new List<string>();
    }
}
