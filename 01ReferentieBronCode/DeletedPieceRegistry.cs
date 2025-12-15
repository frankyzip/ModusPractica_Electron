using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ModusPractica
{
    /// <summary>
    /// Keeps track of permanently deleted music pieces so that re-created entries can be distinguished.
    /// </summary>
    public static class DeletedPieceRegistry
    {
        private const string Prefix = "New - ";
        private static readonly object _syncRoot = new object();
        private static readonly List<DeletedPieceRecord> _records = new();
        private static string? _registryPath;

        public static void Initialize(string profileFolder)
        {
            if (string.IsNullOrWhiteSpace(profileFolder))
            {
                return;
            }

            lock (_syncRoot)
            {
                _registryPath = Path.Combine(profileFolder, "deleted_pieces.json");
                LoadRecords();
            }
        }

        public static void RecordDeletion(string? title, string? composer)
        {
            string normalizedTitle = (title ?? string.Empty).Trim();
            string normalizedComposer = (composer ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(normalizedTitle) && string.IsNullOrEmpty(normalizedComposer))
            {
                return;
            }

            lock (_syncRoot)
            {
                if (_registryPath == null)
                {
                    return;
                }

                var record = _records.FirstOrDefault(r =>
                    string.Equals(r.Title, normalizedTitle, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.Composer, normalizedComposer, StringComparison.OrdinalIgnoreCase));

                if (record == null)
                {
                    record = new DeletedPieceRecord
                    {
                        Title = normalizedTitle,
                        Composer = normalizedComposer,
                        DeletionCount = 1,
                        LastDeletedUtc = DateTime.UtcNow
                    };
                    _records.Add(record);
                }
                else
                {
                    record.DeletionCount++;
                    record.LastDeletedUtc = DateTime.UtcNow;
                }

                SaveRecords();
            }
        }

        public static string ApplyPrefixIfNeeded(string? title, string? composer)
        {
            string normalizedTitle = (title ?? string.Empty).Trim();
            string normalizedComposer = (composer ?? string.Empty).Trim();

            lock (_syncRoot)
            {
                bool hadDeletion = _records.Any(r =>
                    string.Equals(r.Title, normalizedTitle, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(r.Composer, normalizedComposer, StringComparison.OrdinalIgnoreCase));

                if (!hadDeletion)
                {
                    return normalizedTitle;
                }

                if (normalizedTitle.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return normalizedTitle;
                }

                return string.Concat(Prefix, normalizedTitle);
            }
        }

        private static void LoadRecords()
        {
            _records.Clear();

            if (string.IsNullOrWhiteSpace(_registryPath))
            {
                return;
            }

            try
            {
                if (!File.Exists(_registryPath))
                {
                    return;
                }

                string content = FileLockManager.ReadAllTextWithLock(_registryPath);
                if (string.IsNullOrWhiteSpace(content))
                {
                    return;
                }

                var items = JsonSerializer.Deserialize<List<DeletedPieceRecord>>(content);
                if (items != null)
                {
                    _records.AddRange(items.Where(r => !string.IsNullOrWhiteSpace(r.Title)));
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("DeletedPieceRegistry: Failed to load registry.", ex);
            }
        }

        private static void SaveRecords()
        {
            if (string.IsNullOrWhiteSpace(_registryPath))
            {
                return;
            }

            try
            {
                string json = JsonSerializer.Serialize(_records, new JsonSerializerOptions { WriteIndented = true });
                FileLockManager.WriteAllTextWithLock(_registryPath, json);
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("DeletedPieceRegistry: Failed to save registry.", ex);
            }
        }

        private sealed class DeletedPieceRecord
        {
            public string Title { get; set; } = string.Empty;
            public string Composer { get; set; } = string.Empty;
            public int DeletionCount { get; set; }
            public DateTime LastDeletedUtc { get; set; }
        }
    }
}
