using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace ModusPractica
{
    public class PracticeHistoryManager
    {
        private static PracticeHistoryManager? _instance;
        public static PracticeHistoryManager Instance => _instance ??= new PracticeHistoryManager();

        private string? _historyFilePath; // Enkele declaratie
        private List<PracticeHistory> _practiceHistoryList;

        // Raised whenever the in-memory history changes (add/update/merge/reload)
        public event EventHandler? HistoryChanged;

        // Maximum aantal records dat we bewaren in practice_history.json
        // REDUCED: ML is verwijderd, alleen laatste 10 sessies per sectie nodig voor grafieken
        private const int MaxHistoryRecords = 5_000;

        private PracticeHistoryManager()
        {
            _practiceHistoryList = new List<PracticeHistory>();
        }

        public void InitializeForUser(string profileName)
        {
            string historyFolder = DataPathProvider.GetHistoryFolder(profileName);
            _historyFilePath = Path.Combine(historyFolder, "practice_history.json");
            LoadHistoryData();
        }

        // Add this public method to your PracticeHistoryManager class
        public void ReloadHistoryData()
        {
            // This method simply calls the existing private load method
            // to refresh the in-memory list from the JSON file.
            LoadHistoryData();
            // Notify listeners that the history content was refreshed from disk
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        // Zorg ervoor dat we nooit meer dan MaxHistoryRecords records bewaren (meest recente blijven bewaard)
        private int EnforceHistoryLimit()
        {
            if (_practiceHistoryList == null) return 0;
            int count = _practiceHistoryList.Count;
            if (count <= MaxHistoryRecords) return 0;

            // Bewaar enkel de meest recente MaxHistoryRecords op basis van Date
            var pruned = _practiceHistoryList
                .OrderByDescending(h => h.Date)
                .Take(MaxHistoryRecords)
                .ToList();

            int removed = count - pruned.Count;
            DateTime? earliestKept = pruned.Count > 0 ? pruned.Min(h => h.Date) : null;

            _practiceHistoryList = pruned;

            MLLogManager.Instance.Log($"PracticeHistoryManager: Pruned {removed} oude records. Oudste bewaarde datum: {earliestKept:yyyy-MM-dd HH:mm:ss}.", LogLevel.Info);
            return removed;
        }



        // Replace SaveHistoryData with this:
        public void SaveHistoryData()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_historyFilePath))
                {
                    MLLogManager.Instance.Log("SaveHistoryData: history file path is not initialized.", LogLevel.Warning);
                    return;
                }
                // Enforce limiet voor het wegschrijven
                EnforceHistoryLimit();

                var displayPath = (_historyFilePath ?? string.Empty).Replace("\\", "/");
                MLLogManager.Instance.Log($"Saving {_practiceHistoryList.Count} records to path: '{displayPath}'", LogLevel.Info);

                string jsonContent = JsonSerializer.Serialize(_practiceHistoryList, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Use atomic write operation
                var path = _historyFilePath!;
                FileLockManager.WriteAllTextWithLock(path, jsonContent);
            }
            catch (Exception ex)
            {
                string errorMessage = $"CRITICAL ERROR: Could not save the practice history to the file.\n\n" +
                                    $"Path: {_historyFilePath}\n" +
                                    $"Error: {ex.Message}\n\n" +
                                    "Any new practice sessions will be lost when the app closes.";

                MessageBox.Show(errorMessage, "File Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                MLLogManager.Instance.LogError("Failed to save practice history", ex);
            }
        }

        // Replace LoadHistoryData with this:
        private void LoadHistoryData()
        {
            _practiceHistoryList = new List<PracticeHistory>();

            try
            {
                if (string.IsNullOrWhiteSpace(_historyFilePath))
                {
                    MLLogManager.Instance.Log("LoadHistoryData: history file path is not initialized.", LogLevel.Warning);
                    return;
                }

                string jsonContent = FileLockManager.ReadAllTextWithLock(_historyFilePath);

                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    MLLogManager.Instance.Log("PracticeHistoryManager: The practice_history.json file is empty. No data to load.", LogLevel.Info);
                    return;
                }

                using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                {
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        int recordIndex = 0;
                        int successfulLoads = 0;

                        foreach (JsonElement element in doc.RootElement.EnumerateArray())
                        {
                            try
                            {
                                var historyItem = JsonSerializer.Deserialize<PracticeHistory>(element.GetRawText());
                                if (historyItem != null)
                                {
                                    _practiceHistoryList.Add(historyItem);
                                    successfulLoads++;
                                }
                            }
                            catch (JsonException ex)
                            {
                                MLLogManager.Instance.Log($"PracticeHistoryManager: Skipped loading corrupt record at index {recordIndex}. Error: {ex.Message}", LogLevel.Error);
                                MLLogManager.Instance.Log($"Corrupt record data: {element.GetRawText()}", LogLevel.Debug);
                            }
                            recordIndex++;
                        }

                        // --- NORMALISATIE VAN VEELVOORKOMENDE TITEL-TYPO'S ---
                        int fixes = 0;
                        foreach (var h in _practiceHistoryList)
                        {
                            if (string.Equals(h.MusicPieceTitle, "Pelude", StringComparison.Ordinal))
                            {
                                h.MusicPieceTitle = "Prelude";
                                fixes++;
                            }
                        }
                        if (fixes > 0)
                        {
                            MLLogManager.Instance.Log($"Sanitized {fixes} history record title(s): 'Pelude' -> 'Prelude'.", LogLevel.Info);
                            SaveHistoryData(); // persist de verbeterde data
                        }
                        // --- EINDE NORMALISATIE ---

                        // Enforce limiet direct na laden om geheugen- en diskgebruik te beperken
                        int removed = EnforceHistoryLimit();
                        if (removed > 0)
                        {
                            // Sla meteen op zodat het bestand ook gepruned wordt
                            SaveHistoryData();
                        }

                        MLLogManager.Instance.Log($"PracticeHistoryManager: Successfully loaded {successfulLoads} of {recordIndex} records.", LogLevel.Info);
                    }
                }
            }
            catch (JsonException ex)
            {
                MLLogManager.Instance.LogError("PracticeHistoryManager: CRITICAL JSON DESERIALIZATION ERROR. The file content is not a valid JSON structure.", ex);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("PracticeHistoryManager: An unexpected error occurred while loading practice history.", ex);
            }
        }


        /// <summary>
        /// Multiple sessions on the same calendar day are allowed.
        /// Extra same-day practice never pulls the due date earlier; due date preservation is expected.
        /// </summary>
        public void AddPracticeHistory(PracticeHistory practiceHistory)
        {
            if (practiceHistory == null)
            {
                MLLogManager.Instance.Log("AddPracticeHistory: practiceHistory is null; nothing to add.", LogLevel.Warning);
                return;
            }

            try
            {
                // 1) Toevoegen + opslaan (zoals voorheen)
                _practiceHistoryList.Add(practiceHistory);
                SaveHistoryData();

                // Notify observers (UI, stats, etc.) that history changed
                HistoryChanged?.Invoke(this, EventArgs.Empty);

                // 2) Zoek vorige NIET-sight-reading sessie voor dezelfde BarSection (voor kalibratie)
                var historyForSection = GetHistoryForBarSection(practiceHistory.BarSectionId)
                    .Where(h => h != null)
                    .OrderByDescending(h => h.Date)
                    .ToList();

                var previousRealSession = historyForSection
                    .Where(h => h.Date < practiceHistory.Date)
                    .OrderByDescending(h => h.Date)
                    .FirstOrDefault();

                DateTime? prevDate = previousRealSession?.Date;

                // 3) Bouw een minimale "shadow" BarSection voor kalibratie
                //    - Difficulty: conservatief "Average" (we normaliseren elders toch)
                //    - LastPracticeDate: vorige echte sessiedatum (indien aanwezig)
                //    - CompletedRepetitions: ruwe schatting op basis van aantal eerdere "echte" sessies
                int priorEffectiveSessions = historyForSection
                    .Count(h => h.Date < practiceHistory.Date && (h.Repetitions > 0 || (h.SessionOutcome ?? string.Empty).ToLowerInvariant().Contains("targetreached")));

                var shadowSection = new BarSection
                {
                    Id = practiceHistory.BarSectionId,
                    Difficulty = "Average",
                    CompletedRepetitions = Math.Max(0, priorEffectiveSessions),
                    LastPracticeDate = prevDate
                };

                // 4) Trigger personalisatie-kalibratie
                try
                {
                    PersonalizedMemoryCalibration.Instance?.UpdateCalibrationFromSession(practiceHistory, shadowSection);

                    MLLogManager.Instance.Log(
                        $"Calibration updated after history add. Section={shadowSection.Id}, PrevDate={(prevDate.HasValue ? DateHelper.FormatDisplayDate(prevDate.Value) : "none")}, " +
                        $"PriorEffectiveSessions={priorEffectiveSessions}, Today={DateHelper.FormatDisplayDate(practiceHistory.Date)}",
                        LogLevel.Info);
                }
                catch (Exception calibEx)
                {
                    MLLogManager.Instance.LogError("AddPracticeHistory: Calibration update failed.", calibEx);
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("AddPracticeHistory: Unexpected error while adding practice history.", ex);
                throw;
            }
        }


        // NEW: Method to update an existing practice history item.
        public void UpdatePracticeHistory(PracticeHistory historyItemToUpdate)
        {
            if (historyItemToUpdate == null) return;

            // Find the existing item in the list by its unique ID.
            var existingItem = _practiceHistoryList.FirstOrDefault(h => h.Id == historyItemToUpdate.Id);
            if (existingItem != null)
            {
                // Replace the old item with the updated one in the list.
                int index = _practiceHistoryList.IndexOf(existingItem);
                _practiceHistoryList[index] = historyItemToUpdate;
                MLLogManager.Instance.Log($"Updated practice history record with ID: {historyItemToUpdate.Id}", LogLevel.Info);

                // Save the entire updated list back to the JSON file.
                SaveHistoryData();

                // Notify observers that a record was updated
                HistoryChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Removes a practice history record by ID (used for rollback operations)
        /// </summary>
        public void DeletePracticeHistory(Guid historyId)
        {
            var existingItem = _practiceHistoryList.FirstOrDefault(h => h.Id == historyId);
            if (existingItem != null)
            {
                _practiceHistoryList.Remove(existingItem);
                MLLogManager.Instance.Log($"Deleted practice history record with ID: {historyId}", LogLevel.Info);

                SaveHistoryData();
                HistoryChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // Krijg alle historische gegevens voor een specifiek muziekstuk
        public ObservableCollection<PracticeHistory> GetHistoryForMusicPiece(Guid musicPieceId)
        {
            var filteredHistory = _practiceHistoryList
                .Where(h => h.MusicPieceId == musicPieceId)
                .OrderByDescending(h => h.Date)
                .ToList();

            return new ObservableCollection<PracticeHistory>(filteredHistory);
        }

        // Krijg alle historische gegevens voor een specifieke maatsectie
        public ObservableCollection<PracticeHistory> GetHistoryForBarSection(Guid barSectionId)
        {
            var filteredHistory = _practiceHistoryList
                .Where(h => h.BarSectionId == barSectionId)
                .OrderByDescending(h => h.Date)
                .ToList();

            return new ObservableCollection<PracticeHistory>(filteredHistory);
        }

        // Krijg alle historische gegevens (voor toekomstige statistieken)
        public ObservableCollection<PracticeHistory> GetAllHistory()
        {
            var sortedHistory = _practiceHistoryList
                .OrderByDescending(h => h.Date)
                .ToList();

            return new ObservableCollection<PracticeHistory>(sortedHistory);
        }

        /// <summary>
        /// Merges the practice history of multiple old bar sections into a single new one.
        /// It finds all history records associated with the old section IDs,
        /// updates their BarSectionId and BarSectionRange to the new values, and saves the changes.
        /// </summary>
        /// <param name="oldSectionIds">A list of Guids for the sections being merged.</param>
        /// <param name="newSectionId">The Guid of the new, merged section.</param>
        /// <param name="newBarRange">The bar range string of the new, merged section.</param>
        public void MergeHistoryForSections(List<Guid> oldSectionIds, Guid newSectionId, string newBarRange)
        {
            if (oldSectionIds == null || oldSectionIds.Count == 0)
            {
                return;
            }

            var oldIdsSet = new HashSet<Guid>(oldSectionIds);
            int updatedRecords = 0;

            foreach (var historyItem in _practiceHistoryList)
            {
                if (oldIdsSet.Contains(historyItem.BarSectionId))
                {
                    historyItem.BarSectionId = newSectionId;
                    historyItem.BarSectionRange = newBarRange; // Keep history consistent
                    updatedRecords++;
                }
            }

            if (updatedRecords > 0)
            {
                MLLogManager.Instance.Log($"Merged history for {updatedRecords} records into new section {newSectionId}.", LogLevel.Info);
                SaveHistoryData();
                HistoryChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public int CountSessionsForSectionOnLocalDate(Guid sectionId, DateOnly localDate)
        {
            try
            {
                return _practiceHistoryList
                    .Where(h => h != null && h.BarSectionId == sectionId)
                    .Select(h => DateHelper.ToLocalBrussels(h.Date))
                    .Count(localTs => DateOnly.FromDateTime(localTs) == localDate);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("CountSessionsForSectionOnLocalDate: error while counting", ex);
                return 0;
            }
        }
    }
}
