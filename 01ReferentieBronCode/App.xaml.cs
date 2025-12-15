// --- NIEUW: Voeg deze 'using'-statements toe ---
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ModusPractica
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // --- NIEUW: Een publieke, statische referentie naar de timer ---
        public static PracticeTimerWindow? TimerWindow { get; private set; }
        // --- EINDE NIEUW ---

        // --- NIEUWE METHODE ---
        // Deze methode start de PracticeTimerWindow op zijn eigen, onafhankelijke UI-thread.
        private void StartTimerWindowThread()
        {
            Thread timerThread = new Thread(() =>
            {
                // Maak een nieuwe instantie van het venster.
                TimerWindow = new PracticeTimerWindow();

                // Zorgt ervoor dat de thread van de timer correct wordt afgesloten 
                // wanneer de gebruiker de timer-window zelf sluit.
                TimerWindow.Closed += (s, e) => TimerWindow.Dispatcher.InvokeShutdown();

                // Toon het venster.
                TimerWindow.Show();

                // Start een nieuwe 'event loop' voor dit venster.
                // Dit is de cruciale stap die het venster onafhankelijk en responsief houdt.
                System.Windows.Threading.Dispatcher.Run();
            });

            // Configureer de thread.
            timerThread.SetApartmentState(ApartmentState.STA); // Vereist voor WPF UI-elementen.
            timerThread.IsBackground = true; // Zorgt ervoor dat de thread sluit als de hoofd-app sluit.
            timerThread.Start();
        }
        // --- EINDE NIEUWE METHODE ---

        protected override async void OnStartup(StartupEventArgs e)
        {
            // Global exception handling to avoid hard crashes and improve diagnostics
            this.DispatcherUnhandledException += (s, args) =>
            {
                try
                {
                    MLLogManager.Instance.LogError("Unhandled UI exception", args.Exception);
                    MessageBox.Show(
                        $"An unexpected error occurred:\n\n{args.Exception.Message}\n\nThe application will try to continue.",
                        "Unexpected Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    // Last resort: log to console if logging fails
                    Console.WriteLine($"Failed to handle UI exception: {ex}");
                }
                finally
                {
                    args.Handled = true; // prevent app from crashing
                }
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args2) =>
            {
                try
                {
                    if (args2.ExceptionObject is Exception ex)
                    {
                        MLLogManager.Instance.LogError("Unhandled non-UI exception", ex);
                    }
                    else
                    {
                        MLLogManager.Instance.Log("Unhandled non-UI exception (non-Exception type)", LogLevel.Error);
                    }
                }
                catch (Exception ex)
                {
                    // Avoid crash in handler: fallback to console
                    Console.WriteLine($"Failed to handle unhandled exception: {ex}");
                }
            };

            TaskScheduler.UnobservedTaskException += (s, args3) =>
            {
                try
                {
                    MLLogManager.Instance.LogError("Unobserved task exception", args3.Exception);
                }
                catch (Exception ex)
                {
                    // Fallback for unobserved task exception logging failure
                    Console.WriteLine($"Failed to log unobserved task exception: {ex}");
                }
                finally
                {
                    args3.SetObserved();
                }
            };

            // --- Start de rest van de applicatie ---
            var stopwatch = Stopwatch.StartNew();
            var splashScreen = new SplashScreenWindow();
            splashScreen.Show();

            base.OnStartup(e);

            // Wacht even zodat het splashscreen zichtbaar blijft.
            await Task.Delay(1000);

            // STAP 1: Laat gebruiker profiel kiezen.
            // Gebruik tijdelijk OnExplicitShutdown zodat het sluiten van deze pre-startup window
            // de applicatie NIET afsluit voordat MainWindow is ingesteld.
            var previousShutdownMode = this.ShutdownMode;
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var profileSelector = new ProfileSelectorWindow();

            // Nu kunnen we veilig de splashscreen sluiten en ProfileSelector tonen
            splashScreen.Close();

            // Toon modaal zodat we eenvoudig het resultaat kunnen bepalen
            bool result = profileSelector.ShowDialog() == true;

            if (!result)
            {
                // User cancelled - exit application
                Shutdown();
                return;
            }

            // Herstel gewenste ShutdownMode voor de rest van de app
            this.ShutdownMode = previousShutdownMode;

            // Set active profile and custom root if selected
            ActiveUserSession.ProfileName = profileSelector.SelectedProfile;
            if (profileSelector.UseCustomRoot && !string.IsNullOrWhiteSpace(profileSelector.CustomRootPath))
            {
                DataPathProvider.SetCustomRoot(profileSelector.CustomRootPath);
            }

            // Disable adaptive/ML features explicitly (safe removal mode)
            RetentionFeatureFlags.Configure(
                useAdaptiveSystems: true,
                useMemoryStability: true,
                usePMC: false,
                enableDiagnosticLogging: false
            );

            // STAP 2: Initialiseer alle managers voor het actieve profiel
            MLLogManager.Instance.InitializeForUser(ActiveUserSession.ProfileName);
            SettingsManager.Instance.InitializeForUser(ActiveUserSession.ProfileName);
            PracticeHistoryManager.Instance.InitializeForUser(ActiveUserSession.ProfileName);
            ScheduledPracticeSessionManager.Instance.InitializeForUser(ActiveUserSession.ProfileName);

            // Apply user settings for adaptive systems
            RetentionFeatureFlags.Configure(useAdaptiveSystems: SettingsManager.Instance.CurrentSettings.UseAdaptiveSystems);

            // AUDIT FIX: Initialiseer MemoryStabilityManager bij startup (no-op when disabled)
            MemoryStabilityManager.Instance.InitializeForUser(ActiveUserSession.ProfileName);

            // --- De rest van de opstartlogica ---

            var logManager = MLLogManager.Instance;
            logManager.Log($"Application startup for profile '{ActiveUserSession.ProfileName}'.", LogLevel.Info);

            SettingsManager.Instance.LoadSettings();

            logManager.Log($"App: Loaded ShowPostSessionTips at startup: {SettingsManager.Instance.CurrentSettings.ShowPostSessionTips}", LogLevel.Info);

            string savedCultureName = SettingsManager.Instance.CurrentSettings.SelectedCultureName;
            if (!string.IsNullOrEmpty(savedCultureName))
            {
                try
                {
                    CultureHelper.Current = new System.Globalization.CultureInfo(savedCultureName);
                    logManager.Log($"App: CultureHelper set to '{savedCultureName}'.", LogLevel.Info);
                }
                catch (System.Globalization.CultureNotFoundException)
                {
                    logManager.Log($"App: Saved culture '{savedCultureName}' not found. Using system default via CultureHelper.", LogLevel.Warning);
                }
            }
            else
            {
                logManager.Log("App: No specific culture saved. CultureHelper using system default.", LogLevel.Info);
            }

            try
            {
                logManager.Log("App: Application initialization complete", LogLevel.Info);

                var scheduledSessionManager = ScheduledPracticeSessionManager.Instance;
                logManager.Log("App: ScheduledPracticeSessionManager initialized", LogLevel.Info);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing managers: {ex.Message}");
                logManager.LogError("App: Error initializing managers", ex);
            }

            // --- Logica voor splash screen duur ---
            stopwatch.Stop();
            var loadingTime = stopwatch.Elapsed;
            var minimumDisplayTime = TimeSpan.FromSeconds(1);

            if (loadingTime < minimumDisplayTime)
            {
                var remainingTime = minimumDisplayTime - loadingTime;
                await Task.Delay(remainingTime);
            }

            // --- Start het hoofdscherm ---
            var mainWindow = new MainWindow();

            // Initialiseer de data voor het hoofdscherm met het geselecteerde profiel.
            mainWindow.InitializeDataForUser(ActiveUserSession.ProfileName);

            // --- NIEUW: VOER EENMALIGE DATAMIGRATIE UIT VOOR PERFORMANCE SCORES ---
            PerformDataMigration();
            // --- EINDE NIEUW ---

            this.MainWindow = mainWindow;

            mainWindow.Show();

            // --- AANPASSING ---
            // Start de klok niet langer automatisch op. De gebruiker kan dit doen via Ctrl+T.
            // StartTimerWindowThread();
            // --- EINDE AANPASSING ---
        }

        private void PerformDataMigration()
        {
            // Check if the migration has already been done for this profile.
            if (SettingsManager.Instance.CurrentSettings.HasMigratedPerformanceScores)
            {
                return;
            }

            try
            {
                MLLogManager.Instance.Log("Starting one-time data migration for performance scores...", LogLevel.Info);

                var historyManager = PracticeHistoryManager.Instance;
                var allHistory = historyManager.GetAllHistory();
                int migratedCount = 0;

                // Simple performance score calculation based on existing data
                foreach (var session in allHistory)
                {
                    // Only calculate if the score hasn't been set before (is 0).
                    if (session.PerformanceScore == 0.0f)
                    {
                        // Simple heuristic: base score on outcome
                        float score = 5.0f; // Default average
                        if (session.SessionOutcome != null)
                        {
                            var outcome = session.SessionOutcome.ToLower();
                            if (outcome.Contains("targetreached")) score = 8.0f;
                            else if (outcome.Contains("frustration")) score = 3.0f;
                            else if (outcome.Contains("partial")) score = 6.0f;
                        }
                        session.PerformanceScore = score;
                        migratedCount++;
                    }
                }

                if (migratedCount > 0)
                {
                    // Save all the updated history items back to the JSON file.
                    historyManager.SaveHistoryData();
                    MLLogManager.Instance.Log($"Migration complete. Calculated scores for {migratedCount} historical sessions.", LogLevel.Info);
                }

                // Mark the migration as complete and save the setting.
                SettingsManager.Instance.CurrentSettings.HasMigratedPerformanceScores = true;
                SettingsManager.Instance.SaveSettings();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("A critical error occurred during performance score data migration.", ex);
                MessageBox.Show("An error occurred during data migration. Please check the logs.", "Migration Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
