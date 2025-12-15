// VOLLEDIGE, CORRECTE EN ROBUUSTE VERSIE

using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ModusPractica
{
    public partial class PracticeTimerWindow : Window
    {
        private DispatcherTimer _uiTimer;
        private bool _isTimerRunning = false;
        private MediaPlayer _mediaPlayer;
        private TimeSpan _timeRemaining;
        private readonly bool _isDurationFixed = false;
        private DateTime _countdownEndTime;
        private Window _ownerWindow;

        // NIEUW: Een publieke vlag die aangeeft of de timer succesvol is afgerond.
        // Deze vervangt de ongeldige DialogResult logica.
        public bool WasCompletedSuccessfully { get; private set; } = false;

        public PracticeTimerWindow()
        {
            InitializeComponent();
            InitializeCommon();
            _isDurationFixed = false;
            if (int.TryParse(TxtMinutes.Text, out int minutes) && minutes > 0)
            {
                _timeRemaining = TimeSpan.FromMinutes(minutes);
            }
            else
            {
                _timeRemaining = TimeSpan.FromMinutes(30);
                TxtMinutes.Text = "30";
            }
            UpdateTimerDisplay(_timeRemaining);
        }

        public PracticeTimerWindow(int durationMinutes) : this()
        {
            _isDurationFixed = true;
            _timeRemaining = TimeSpan.FromMinutes(durationMinutes);
            UpdateTimerDisplay(_timeRemaining);
            if (TxtMinutes != null)
            {
                TxtMinutes.Text = durationMinutes.ToString();
                TxtMinutes.IsEnabled = false;
                var minutesPanel = TxtMinutes.Parent as StackPanel;
                if (minutesPanel != null)
                {
                    minutesPanel.Visibility = Visibility.Collapsed;
                }
            }
        }

        public PracticeTimerWindow(int durationMinutes, Window owner) : this(durationMinutes)
        {
            _ownerWindow = owner;
            this.Owner = owner;
        }

        private void InitializeCommon()
        {
            InitializeAlarmSound();
            _uiTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _uiTimer.Tick += UiTimer_Tick;
            this.Loaded += PracticeTimerWindow_Loaded;
        }

        private void PracticeTimerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var workArea = SystemParameters.WorkArea;
            if (_ownerWindow != null)
            {
                double timerWidth = this.ActualWidth;
                double margin = 15;
                this.Left = workArea.Right - (timerWidth * 2) - margin;
                this.Top = workArea.Bottom - this.ActualHeight;
            }
            else
            {
                this.Left = workArea.Right - this.ActualWidth;
                this.Top = workArea.Bottom - this.ActualHeight;
            }
            if (_isDurationFixed && !_isTimerRunning)
            {
                StartTimer();
            }
        }

        private void InitializeAlarmSound()
        {
            try
            {
                string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sound", "warning.mp3");
                if (File.Exists(soundPath))
                {
                    _mediaPlayer = new MediaPlayer();
                    _mediaPlayer.Open(new Uri(soundPath, UriKind.Absolute));
                    MLLogManager.Instance.Log("Alarm sound 'warning.mp3' loaded successfully.", LogLevel.Info);
                }
                else
                {
                    MLLogManager.Instance.Log("Alarm sound 'warning.mp3' NOT FOUND in Sound folder.", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Failed to initialize alarm sound", ex);
            }
        }

        private void StartTimer()
        {
            if (_timeRemaining.TotalSeconds <= 0)
            {
                if (int.TryParse(TxtMinutes.Text, out int minutes) && minutes > 0)
                {
                    _timeRemaining = TimeSpan.FromMinutes(minutes);
                }
                else
                {
                    MessageBox.Show("Please enter a valid number of minutes.", "Invalid Time", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            _countdownEndTime = DateTime.Now + _timeRemaining;
            _uiTimer.Start();
            _isTimerRunning = true;
            BtnStartPause.Content = "Pause";
            TxtMinutes.IsEnabled = false;
        }

        private void BtnStartPause_Click(object sender, RoutedEventArgs e)
        {
            if (_isTimerRunning)
            {
                _uiTimer.Stop();
                _timeRemaining = _countdownEndTime - DateTime.Now;
                _isTimerRunning = false;
                BtnStartPause.Content = "Resume";
            }
            else
            {
                StartTimer();
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            _uiTimer.Stop();
            _isTimerRunning = false;
            BtnStartPause.Content = "Start";
            if (!_isDurationFixed)
            {
                if (int.TryParse(TxtMinutes.Text, out int minutes) && minutes > 0)
                {
                    _timeRemaining = TimeSpan.FromMinutes(minutes);
                }
                else
                {
                    _timeRemaining = TimeSpan.FromMinutes(30);
                    TxtMinutes.Text = "30";
                }
                TxtMinutes.IsEnabled = true;
            }
            UpdateTimerDisplay(_timeRemaining);
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            _timeRemaining = _countdownEndTime - DateTime.Now;
            UpdateTimerDisplay(_timeRemaining);
            if (_timeRemaining.TotalSeconds <= 0)
            {
                _uiTimer.Stop();
                _isTimerRunning = false;
                PlayAlarmSound();
                if (_isDurationFixed)
                {
                    // AANGEPAST: Zet de vlag op true voordat het venster sluit.
                    this.WasCompletedSuccessfully = true;
                    this.Close();
                }
                else
                {
                    // FIXED: Voeg Owner toe aan MessageBox om modal dialog problemen te voorkomen
                    MessageBox.Show(this, "Time's up!", "Practice Timer", MessageBoxButton.OK, MessageBoxImage.Information);
                    // FIXED: Stop alarm sound after user clicks OK
                    StopAlarmSound();
                    BtnReset_Click(null, null);
                }
            }
        }

        private void UpdateTimerDisplay(TimeSpan time)
        {
            if (time.TotalSeconds < 0)
            {
                time = TimeSpan.Zero;
            }
            TxtTimeDisplay.Text = time.ToString(@"mm\:ss");
        }

        private void PlayAlarmSound()
        {
            if (_mediaPlayer != null)
            {
                MLLogManager.Instance.Log("Playing alarm sound.", LogLevel.Info);
                _mediaPlayer.Position = TimeSpan.Zero;
                _mediaPlayer.Play();
            }
            else
            {
                MLLogManager.Instance.Log("Cannot play alarm sound: MediaPlayer was not initialized (file likely missing).", LogLevel.Warning);
            }
        }

        private void StopAlarmSound()
        {
            if (_mediaPlayer != null)
            {
                try
                {
                    _mediaPlayer.Stop();
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance.LogError("Failed to stop alarm sound", ex);
                }
            }
        }

        private void TxtMinutes_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }
    }
}