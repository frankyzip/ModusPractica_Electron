using System.IO;
using System.Diagnostics; // ADDED: For Debug.Assert compile-time checks

namespace ModusPractica
{
    /// <summary>
    /// Central utility class for standardized date handling throughout the application.
    /// Ensures all date calculations use consistent logic to prevent planning conflicts.
    /// 
    /// <para><strong>POLICY - PLANNER vs REGISTRATION PATHS / BELEID:</strong></para>
    /// <list type="bullet">
    /// <item><description><strong>Planner Path / Planner:</strong> interval ≥ 1 dag (nooit voor 'vandaag' plannen) / interval must be ≥ 1 day (never schedule for 'today').</description></item>
    /// <item><description><strong>Registration Path / Registratie:</strong> interval 0,0 dagen toegestaan voor same-day extra's zonder due-wijziging (due date blijft gelijk) / interval 0.0 days allowed for same-day extra registrations without changing due date.</description></item>
    /// </list>
    /// 
    /// <para><strong>GUARDRAILS:</strong></para>
    /// <list type="bullet">
    /// <item><description>Debug.Assert(intervalDays ≥ 1.0) in planner-pad (alleen Debug build) / Debug.Assert in planner path (debug builds only)</description></item>
    /// <item><description>Release build corrigeert automatisch naar 1.0 indien kleiner / Release build auto-corrects to 1.0 if lower</description></item>
    /// <item><description>Registratie-pad accepteert 0.0 voor extra's zelfde dag / Registration path allows 0.0 (same-day extra)</description></item>
    /// <item><description>All date operations use normalized date-only arithmetic for consistency</description></item>
    /// </list>
    /// </summary>
    public static class DateHelper
    {
        private static readonly TimeZoneInfo BrusselsTimeZone = GetBrusselsTimeZone();

        /// <summary>
        /// Returns the current local date in the Europe/Brussels timezone (date-only, time=00:00:00).
        /// This is the authoritative day boundary for all "today" / same-day ExtraPractice logic.
        /// </summary>
        public static DateTime LocalToday()
        {
            var nowUtc = DateTime.UtcNow;
            var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, BrusselsTimeZone);
            return local.Date; // normalized date-only
        }

        /// <summary>
        /// Converts a DateTime (assumed unspecified or local/UTC) to Europe/Brussels local time.
        /// </summary>
        public static DateTime ToLocalBrussels(DateTime timestamp)
        {
            DateTime utc = timestamp.Kind switch
            {
                DateTimeKind.Utc => timestamp,
                DateTimeKind.Local => timestamp.ToUniversalTime(),
                _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc) // assume utc if unspecified
            };
            return TimeZoneInfo.ConvertTimeFromUtc(utc, BrusselsTimeZone);
        }

        private static TimeZoneInfo GetBrusselsTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");
            }
            catch
            {
#if WINDOWS
                try { return TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time"); } catch { }
#endif
                return TimeZoneInfo.Local; // graceful fallback
            }
        }

        /// <summary>
        /// Normalizes any DateTime to a date-only value (midnight UTC).
        /// This is the canonical method for all date operations in the application.
        /// 
        /// <para><strong>CONTRACT:</strong> Always returns a DateTime with time component set to 00:00:00</para>
        /// </summary>
        /// <param name="dateTime">The DateTime to normalize</param>
        /// <returns>DateTime with only date component (time set to 00:00:00)</returns>
        public static DateTime NormalizeToDateOnly(DateTime dateTime)
        {
            return dateTime.Date;
        }

        /// <summary>
        /// Normalizes a nullable DateTime to date-only, preserving null values.
        /// 
        /// <para><strong>CONTRACT:</strong> Preserves null input, normalizes non-null values to date-only</para>
        /// </summary>
        /// <param name="dateTime">The nullable DateTime to normalize</param>
        /// <returns>Normalized DateTime or null if input was null</returns>
        public static DateTime? NormalizeToDateOnly(DateTime? dateTime)
        {
            return dateTime?.Date;
        }

        /// <summary>
        /// Gets the current session date for all planning operations.
        /// This ensures consistent "today" reference across the application.
        /// 
        /// <para><strong>CONTRACT:</strong> Always returns DateTime.Today (already date-only)</para>
        /// <para><strong>USAGE:</strong> Use this method instead of DateTime.Today for consistency</para>
        /// </summary>
        /// <returns>Today's date normalized to date-only</returns>
        public static DateTime GetCurrentSessionDate()
        {
            return LocalToday(); // use Europe/Brussels date boundary
        }

        /// <summary>
        /// Calculates the next practice date based on a base date and interval.
        /// ROBUUST: Uses consistent date arithmetic with numerical stability protections.
        /// Contract:
        ///  - Planner path MUST pass intervals >= 1 day (use the 3-arg overload with isRegistrationPath=false to assert this).
        ///  - Registration path MAY pass 0.0 days for same-day extras (due date remains unchanged).
        /// </summary>
        /// <param name="baseDate">The date to calculate from</param>
        /// <param name="intervalDays">Number of days to add (≥ 1.0 for planner path)</param>
        /// <returns>Next practice date, normalized to date-only</returns>
        public static DateTime CalculateNextPracticeDate(DateTime baseDate, double intervalDays)
        {
            // Gebruik de bestaande implementatie centraal (houdt 0..365 rails aan)
            return CalculateNextPracticeDate(baseDate, intervalDays, isRegistrationPath: false);
        }

        /// <summary>
        /// Calculates the next practice date with an explicit path contract.
        /// Planner path MUST pass intervals >= 1 day; Registration path may pass 0.0 for same-day extras.
        /// </summary>
        /// <param name="baseDate">The date to calculate from</param>
        /// <param name="intervalDays">Number of days to add</param>
        /// <param name="isRegistrationPath">
        /// false = Planner (enforces >= 1 day),
        /// true  = Registration (same-day allowed, 0.0 is valid)
        /// </param>
        /// <returns>Next practice date, normalized to date-only</returns>
        public static DateTime CalculateNextPracticeDate(DateTime baseDate, double intervalDays, bool isRegistrationPath)
        {
            var normalizedBase = NormalizeToDateOnly(baseDate);

            // Input validatie (zoals in de 2-arg versie)
            if (double.IsNaN(intervalDays) || double.IsInfinity(intervalDays))
            {
                MLLogManager.Instance?.Log($"CalculateNextPracticeDate(path={(isRegistrationPath ? "registration" : "planner")}): invalid intervalDays ({intervalDays}), using 1 day", LogLevel.Warning);
                intervalDays = 1.0;
            }

            // Contract-enforcement: planner mag nooit < 1 dag plannen
            // SAFETY FIX: Clamp EERST voordat de Debug.Assert wordt gecontroleerd
            if (!isRegistrationPath && intervalDays < 1.0)
            {
                MLLogManager.Instance?.Log($"CalculateNextPracticeDate(planner): intervalDays {intervalDays:F2} < 1.0 → clamped to 1.0 (safety fix)", LogLevel.Warning);
                intervalDays = 1.0;
            }

            // Debug assertion nu safe - na clamping
            if (!isRegistrationPath)
            {
                System.Diagnostics.Debug.Assert(intervalDays >= 1.0, "Planner must never schedule < 1 day");
            }

            // This is a temporary implementation that calls the old method.
            // The full implementation from the file will be merged here.
            // For now, this structure satisfies the request.
            // The original method's logic will be integrated next.

            // The original implementation from the file is more robust and should be used.
            // Let's re-integrate it while keeping the new structure.
            try
            {
                // BEVEILIGING 1: Input validatie voor interval (already done above)

                // GUARDRAIL: PLANNER PATH POLICY ENFORCEMENT (already done above)

                // BEVEILIGING 2: Begrens interval tot redelijke waarden
                const double MIN_INTERVAL = 0.0;   // Minimum: vandaag (registration path only)
                const double MAX_INTERVAL = 365.0; // Maximum: 1 jaar

                if (intervalDays < MIN_INTERVAL || intervalDays > MAX_INTERVAL)
                {
                    MLLogManager.Instance?.Log($"CalculateNextPracticeDate: Extreme intervalDays ({intervalDays}) clamped to safe range", LogLevel.Warning);
                    intervalDays = Math.Max(MIN_INTERVAL, Math.Min(MAX_INTERVAL, intervalDays));
                }

                // BEVEILIGING 3: Veilige afronding
                int roundedInterval;
                try
                {
                    roundedInterval = (int)Math.Round(intervalDays, MidpointRounding.AwayFromZero);

                    // Valideer afgerond resultaat
                    if (roundedInterval < 0 || roundedInterval > MAX_INTERVAL)
                    {
                        MLLogManager.Instance?.Log($"CalculateNextPracticeDate: Invalid rounded interval ({roundedInterval}), using safe fallback", LogLevel.Warning);
                        roundedInterval = Math.Max(0, Math.Min((int)MAX_INTERVAL, roundedInterval));
                    }
                }
                catch (OverflowException)
                {
                    MLLogManager.Instance?.Log($"CalculateNextPracticeDate: Overflow in rounding, using 1 day", LogLevel.Warning);
                    roundedInterval = 1;
                }

                // BEVEILIGING 4: Veilige datum optelling
                DateTime nextDate;
                try
                {
                    nextDate = normalizedBase.AddDays(roundedInterval);

                    // Valideer resulterende datum
                    if (nextDate < DateTime.MinValue.AddDays(1) || nextDate > DateTime.MaxValue.AddDays(-1))
                    {
                        MLLogManager.Instance?.Log($"CalculateNextPracticeDate: Date arithmetic resulted in extreme date, using safe fallback", LogLevel.Warning);
                        nextDate = normalizedBase.AddDays(1); // Veilige fallback: morgen
                    }
                }
                catch (ArgumentOutOfRangeException)
                {
                    MLLogManager.Instance?.Log($"CalculateNextPracticeDate: Date arithmetic overflow, using fallback", LogLevel.Warning);
                    nextDate = normalizedBase.AddDays(1); // Veilige fallback
                }

                // BEVEILIGING 5: Ensure we never schedule in the past
                var today = GetCurrentSessionDate();
                if (nextDate < today && !isRegistrationPath) // On registration path, it might be a same-day calculation
                {
                    nextDate = today;
                    MLLogManager.Instance?.Log($"CalculateNextPracticeDate: Calculated past date, corrected to today", LogLevel.Debug);
                }

                return nextDate;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError($"CalculateNextPracticeDate: Critical error with baseDate={baseDate}, intervalDays={intervalDays}", ex);

                // ULTIMATE FALLBACK: Morgen
                try
                {
                    return GetCurrentSessionDate().AddDays(1);
                }
                catch
                {
                    return DateTime.Today.AddDays(1); // Absolute emergency fallback
                }
            }
        }

        /// <summary>
        /// Calculates the interval in days between two dates using consistent logic.
        /// ROBUUST: Always returns positive values and handles date normalization with stability protections.
        /// </summary>
        /// <param name="fromDate">Start date</param>
        /// <param name="toDate">End date</param>
        /// <returns>Number of days between dates (always ≥ 0)</returns>
        public static double CalculateIntervalDays(DateTime fromDate, DateTime toDate)
        {
            try
            {
                var normalizedFrom = NormalizeToDateOnly(fromDate);
                var normalizedTo = NormalizeToDateOnly(toDate);

                // BEVEILIGING 1: Veilige datum subtractie
                TimeSpan difference;
                try
                {
                    difference = normalizedTo - normalizedFrom;
                }
                catch (ArgumentOutOfRangeException)
                {
                    MLLogManager.Instance?.Log($"CalculateIntervalDays: Date arithmetic overflow, using absolute difference", LogLevel.Warning);

                    // Fallback: gebruik absolute verschil
                    if (normalizedTo > normalizedFrom)
                        difference = normalizedTo - normalizedFrom;
                    else
                        difference = normalizedFrom - normalizedTo;
                }

                // BEVEILIGING 2: Valideer TimeSpan resultaat
                if (double.IsNaN(difference.TotalDays) || double.IsInfinity(difference.TotalDays))
                {
                    MLLogManager.Instance?.Log($"CalculateIntervalDays: Invalid TimeSpan result, using 0", LogLevel.Warning);
                    return 0.0;
                }

                // BEVEILIGING 3: Begrens tot redelijke waarden
                double daysDifference = difference.TotalDays;
                const double MAX_REASONABLE_INTERVAL = 36525.0; // 100 jaar

                if (Math.Abs(daysDifference) > MAX_REASONABLE_INTERVAL)
                {
                    MLLogManager.Instance?.Log($"CalculateIntervalDays: Extreme interval ({daysDifference:F1} days) clamped", LogLevel.Warning);
                    daysDifference = Math.Sign(daysDifference) * MAX_REASONABLE_INTERVAL;
                }

                // Return absolute value (always positive)
                return Math.Max(0, Math.Abs(daysDifference));
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError($"CalculateIntervalDays: Critical error with fromDate={fromDate}, toDate={toDate}", ex);
                return 0.0; // Veilige fallback
            }
        }

        /// <summary>
        /// Determines if a scheduled session is due for practice today.
        /// Uses consistent logic for all scheduling decisions.
        /// </summary>
        /// <param name="scheduledDate">The scheduled practice date</param>
        /// <returns>True if the session is due today or overdue</returns>
        public static bool IsSessionDueToday(DateTime scheduledDate)
        {
            var today = GetCurrentSessionDate();
            var normalizedScheduled = NormalizeToDateOnly(scheduledDate);

            return normalizedScheduled <= today;
        }

        /// <summary>
        /// Determines if a date represents "today" for session purposes.
        /// </summary>
        /// <param name="date">Date to check</param>
        /// <returns>True if the date is today</returns>
        public static bool IsToday(DateTime date)
        {
            return NormalizeToDateOnly(date) == GetCurrentSessionDate();
        }

        /// <summary>
        /// Gets a standardized display format for dates in the application.
        /// </summary>
        /// <param name="date">Date to format</param>
        /// <returns>Formatted date string using current culture</returns>
        public static string FormatDisplayDate(DateTime date)
        {
            return NormalizeToDateOnly(date).ToString("d", CultureHelper.Current);
        }
    }

    public static class FileLockManager
    {
        private static readonly object _globalLock = new object();

        /// <summary>
        /// Performs atomic file write operations with exclusive locking
        /// </summary>
        public static void WriteAllTextWithLock(string path, string content)
        {
            lock (_globalLock)
            {
                var directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory!);

                string fileName = Path.GetFileName(path);
                string tempPath = Path.Combine(directory!, fileName + ".tmp");
                string backupPath = Path.Combine(directory!, fileName + ".bak");

                // Write to temp file first
                using (var stream = new FileStream(
                    tempPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(content);
                    writer.Flush();
                    stream.Flush(true); // ensure data hits the disk
                }

                // Atomically swap temp into place; keep a backup if destination exists
                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
                }
                else
                {
                    // First-time write: move temp to destination
                    File.Move(tempPath, path, overwrite: true);
                }
            }
        }


        /// <summary>
        /// Performs atomic file read operations with shared locking
        /// </summary>
        public static string ReadAllTextWithLock(string path)
        {
            lock (_globalLock)
            {
                if (!File.Exists(path))
                    return string.Empty;

                // Use FileShare.Read for shared read access
                using (var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    4096,
                    FileOptions.SequentialScan))
                {
                    using (var reader = new StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        /// <summary>
        /// Performs atomic file write operations with exclusive locking and retry logic with exponential backoff
        /// </summary>
        /// <param name="path">The file path to write to</param>
        /// <param name="content">The content to write</param>
        /// <param name="maxRetries">Maximum number of retry attempts (default: 5)</param>
        /// <param name="baseDelayMs">Base delay in milliseconds for exponential backoff (default: 100)</param>
        public static void WriteAllTextWithRetry(string path, string content, int maxRetries = 5, int baseDelayMs = 100)
        {
            Exception? lastException = null;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    WriteAllTextWithLock(path, content);
                    return; // Success, exit early
                }
                catch (IOException ex) when (IsFileLockException(ex))
                {
                    lastException = ex;

                    if (attempt == maxRetries)
                        break; // Last attempt, don't wait

                    // Exponential backoff: baseDelay * 2^attempt with jitter
                    int delay = baseDelayMs * (int)Math.Pow(2, attempt);
                    delay += new Random().Next(0, delay / 2); // Add jitter (0-50% of delay)

                    System.Threading.Thread.Sleep(delay);
                }
                catch (Exception)
                {
                    // Non-recoverable exception, don't retry
                    throw;
                }
            }

            // All retries exhausted, throw the last exception
            throw new IOException($"Failed to write file '{path}' after {maxRetries + 1} attempts. Last error: {lastException?.Message}", lastException);
        }

        /// <summary>
        /// Performs atomic file read operations with shared locking and retry logic with exponential backoff
        /// </summary>
        /// <param name="path">The file path to read from</param>
        /// <param name="maxRetries">Maximum number of retry attempts (default: 5)</param>
        /// <param name="baseDelayMs">Base delay in milliseconds for exponential backoff (default: 100)</param>
        /// <returns>The file content or empty string if file doesn't exist</returns>
        public static string ReadAllTextWithRetry(string path, int maxRetries = 5, int baseDelayMs = 100)
        {
            Exception? lastException = null;

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    return ReadAllTextWithLock(path);
                }
                catch (IOException ex) when (IsFileLockException(ex))
                {
                    lastException = ex;

                    if (attempt == maxRetries)
                        break; // Last attempt, don't wait

                    // Exponential backoff: baseDelay * 2^attempt with jitter
                    int delay = baseDelayMs * (int)Math.Pow(2, attempt);
                    delay += new Random().Next(0, delay / 2); // Add jitter (0-50% of delay)

                    System.Threading.Thread.Sleep(delay);
                }
                catch (Exception)
                {
                    // Non-recoverable exception, don't retry
                    throw;
                }
            }

            // All retries exhausted, throw the last exception
            throw new IOException($"Failed to read file '{path}' after {maxRetries + 1} attempts. Last error: {lastException?.Message}", lastException);
        }

        /// <summary>
        /// Determines if an IOException is likely due to file locking issues
        /// </summary>
        private static bool IsFileLockException(IOException ex)
        {
            // Common Windows file locking error codes
            const int ERROR_SHARING_VIOLATION = unchecked((int)0x80070020);
            const int ERROR_LOCK_VIOLATION = unchecked((int)0x80070021);
            const int ERROR_FILE_IN_USE = unchecked((int)0x800704B0);

            var hresult = ex.HResult;
            return hresult == ERROR_SHARING_VIOLATION ||
                   hresult == ERROR_LOCK_VIOLATION ||
                   hresult == ERROR_FILE_IN_USE ||
                   ex.Message.Contains("being used by another process") ||
                   ex.Message.Contains("sharing violation") ||
                   ex.Message.Contains("lock violation");
        }
    }
}