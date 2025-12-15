using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading; // For DispatcherTimer

namespace ModusPractica
{
    public partial class FreePracticeWindow : Window
    {
        private DispatcherTimer _timer;
        private Stopwatch _stopwatch;
        private TimeSpan _totalElapsedTime;

        public FreePracticeWindow()
        {
            InitializeComponent();
            _stopwatch = new Stopwatch();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += Timer_Tick;

            UpdateTimerButtonStates();
            TxtTimerDisplay.Text = "00:00:00";
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateTimerDisplay();
        }

        private void UpdateTimerDisplay()
        {
            TimeSpan currentElapsed = _totalElapsedTime + _stopwatch.Elapsed;
            TxtTimerDisplay.Text = currentElapsed.ToString(@"hh\:mm\:ss");
        }

        private void BtnStartTimer_Click(object sender, RoutedEventArgs e)
        {
            _stopwatch.Start();
            _timer.Start();
            UpdateTimerButtonStates();
            UpdateTimerDisplay();
        }

        private void BtnPauseTimer_Click(object sender, RoutedEventArgs e)
        {
            _stopwatch.Stop();
            _timer.Stop();
            _totalElapsedTime += _stopwatch.Elapsed;
            _stopwatch.Reset();
            UpdateTimerButtonStates();
            UpdateTimerDisplay();
        }

        private void BtnStopTimer_Click(object sender, RoutedEventArgs e)
        {
            _stopwatch.Stop();
            _timer.Stop();
            _totalElapsedTime += _stopwatch.Elapsed;
            _stopwatch.Reset();
            UpdateTimerButtonStates();
            UpdateTimerDisplay();
        }

        private void UpdateTimerButtonStates()
        {
            BtnStartTimer.IsEnabled = !_stopwatch.IsRunning;
            BtnPauseTimer.IsEnabled = _stopwatch.IsRunning;
            BtnStopTimer.IsEnabled = _stopwatch.IsRunning || _totalElapsedTime > TimeSpan.Zero;
            BtnSave.IsEnabled = _totalElapsedTime > TimeSpan.Zero; // Only enable save if some time has elapsed
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Ensure timer is stopped before saving
            if (_stopwatch.IsRunning)
            {
                BtnStopTimer_Click(null, null); // This will stop, add elapsed, and reset stopwatch
            }

            if (_totalElapsedTime.TotalSeconds <= 0)
            {
                MessageBox.Show("Registreer eerst oefentijd voordat je opslaat.", "Geen tijd geregistreerd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Create a new PracticeHistory entry for free practice
                PracticeHistory freePracticeSession = new PracticeHistory
                {
                    Id = Guid.NewGuid(),
                    Date = DateTime.Now,
                    MusicPieceId = Guid.Empty, // Special ID for free practice
                    BarSectionId = Guid.Empty,  // Special ID for free practice
                    MusicPieceTitle = "Vrije oefening", // Descriptive title
                    BarSectionRange = "Algemeen", // Descriptive range
                    Duration = _totalElapsedTime, // Use the measured elapsed time
                    Notes = "Vrije oefensessie.", // Default note for free practice
                    SessionOutcome = "FreePractice", // Custom outcome for identification
                };

                PracticeHistoryManager.Instance.AddPracticeHistory(freePracticeSession); // This saves the history
                MLLogManager.Instance.Log($"Recorded free practice session: {_totalElapsedTime.TotalMinutes:F2} minutes. Description: '{freePracticeSession.Notes}'", LogLevel.Info);

                MessageBox.Show($"Succesvol {_totalElapsedTime.TotalMinutes:F2} minuten vrije oefening geregistreerd.", "Sessie opgeslagen", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error recording free practice session.", ex);
                MessageBox.Show($"Er is een fout opgetreden: {ex.Message}", "Fout", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            // Stop timer if running before closing
            if (_stopwatch.IsRunning)
            {
                _stopwatch.Stop();
                _timer.Stop();
            }
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Ensure timer is stopped when window is closed by 'X' button
            if (_timer != null && _timer.IsEnabled)
            {
                _timer.Stop();
            }
            if (_stopwatch != null && _stopwatch.IsRunning)
            {
                _stopwatch.Stop();
            }
            base.OnClosed(e);
        }

        private void NumberValidationTextBox(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9]+$");
        }
    }
}