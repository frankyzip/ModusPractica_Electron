// VOLLEDIGE, CORRECTE EN ROBUUSTE VERSIE

using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.ComponentModel; // for CancelEventArgs
using System.Windows.Input;

namespace ModusPractica
{
    /// <summary>
    /// Interaction logic for PlaylistManagerWindow.xaml
    /// Dr. Gebrian's Interleaved Practice Playlist Manager
    /// </summary>
    public partial class PlaylistManagerWindow : Window
    {

        private ObservableCollection<PracticePlaylist> _playlists;
        private ObservableCollection<MusicPieceItem> _allMusicPieces;
        private PracticePlaylist? _currentPlaylist;
        private bool _uiRefreshNeeded = false; // NIEUW: Vlag om onnodige refreshes te beheren
        private bool _hasUnsavedChanges = false; // Track unsaved changes
        private PracticePlaylist? _subscribedPlaylist; // keep track of event subscriptions
        private bool _isSyncingUI = false; // suppress change tracking during programmatic UI updates

        public PlaylistManagerWindow(ObservableCollection<MusicPieceItem> allMusicPieces)
        {
            InitializeComponent();

            // Set culture for proper localization
            this.Language = XmlLanguage.GetLanguage(CultureHelper.Current.IetfLanguageTag);

            _allMusicPieces = allMusicPieces;
            _playlists = new ObservableCollection<PracticePlaylist>();

            InitializeUI();
            LoadExistingPlaylists();

            this.Loaded += PlaylistManagerWindow_Loaded;
            // NIEUW: Koppel de Activated event handler voor betrouwbare UI-updates.
            this.Activated += PlaylistManagerWindow_Activated;
            this.Closing += PlaylistManagerWindow_Closing; // Warn on unsaved changes

            MLLogManager.Instance.Log("Playlist Manager opened", LogLevel.Info);
        }

        // NIEUW: Wordt aangeroepen telkens als het venster focus krijgt.
        private void PlaylistManagerWindow_Activated(object sender, EventArgs e)
        {
            // Als er een refresh nodig is (bv. na het sluiten van een ander venster), voer deze dan uit.
            if (_uiRefreshNeeded)
            {
                RefreshDataAndUI();
                _uiRefreshNeeded = false; // Reset de vlag
                MLLogManager.Instance.Log("PlaylistManagerWindow re-activated and UI refreshed.", LogLevel.Debug);
            }
        }

        private void PlaylistManagerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var workArea = System.Windows.SystemParameters.WorkArea;
            this.Height = workArea.Height;
            this.Top = workArea.Top;
            this.Left = (workArea.Width - this.Width) / 2;

            RefreshDataAndUI(); // Gebruik de nieuwe centrale refresh-methode

            MLLogManager.Instance.Log($"Playlist Manager window loaded. Found {_playlists.Count} playlists, current playlist: {(_currentPlaylist?.Name ?? "none")}", LogLevel.Info);
        }

        private void InitializeUI()
        {
            LbPlaylists.ItemsSource = _playlists;
            CbMusicPieces.ItemsSource = _allMusicPieces;

            // NIEUW: meteen status herberekenen bij user input
            CbBarSections.SelectionChanged += CbBarSections_SelectionChanged;
            TxtDuration.TextChanged += TxtDuration_TextChanged;

            // Mark unsaved when name/description edited
            TxtPlaylistName.TextChanged += (_, __) => MarkUnsaved();
            TxtPlaylistDescription.TextChanged += (_, __) => MarkUnsaved();

            if (_allMusicPieces.Count > 0)
                CbMusicPieces.SelectedIndex = 0;

            TxtDuration.Text = "2";
        }


        private void LoadExistingPlaylists()
        {
            try
            {
                string profileFolder = GetProfileFolder();
                string playlistsFolder = Path.Combine(profileFolder, "Playlists");

                MLLogManager.Instance.Log($"Loading playlists from: {playlistsFolder}", LogLevel.Debug);

                if (!Directory.Exists(playlistsFolder))
                {
                    Directory.CreateDirectory(playlistsFolder);
                    return;
                }

                var playlistFiles = Directory.GetFiles(playlistsFolder, "*.json");
                MLLogManager.Instance.Log($"Found {playlistFiles.Length} playlist files", LogLevel.Debug);

                foreach (var filePath in playlistFiles)
                {
                    try
                    {
                        string jsonContent = File.ReadAllText(filePath);
                        if (string.IsNullOrWhiteSpace(jsonContent))
                        {
                            MLLogManager.Instance.Log($"Skipping empty playlist file: {Path.GetFileName(filePath)}", LogLevel.Warning);
                            continue;
                        }
                        var playlist = System.Text.Json.JsonSerializer.Deserialize<PracticePlaylist>(jsonContent);
                        if (playlist != null)
                        {
                            _playlists.Add(playlist);
                        }
                    }
                    catch (Exception ex)
                    {
                        MLLogManager.Instance.LogError($"Failed to load playlist from {Path.GetFileName(filePath)}", ex);
                    }
                }

                MLLogManager.Instance.Log($"Successfully loaded {_playlists.Count} existing playlists", LogLevel.Info);

                if (_playlists.Any())
                {
                    var mostRecentPlaylist = _playlists.OrderByDescending(p => p.LastUsedAt).FirstOrDefault();
                    if (mostRecentPlaylist != null)
                    {
                        _currentPlaylist = mostRecentPlaylist;
                        LbPlaylists.SelectedItem = _currentPlaylist;
                        MLLogManager.Instance.Log($"Auto-selecting most recent playlist: {mostRecentPlaylist.Name}", LogLevel.Info);
                    }
                }
                else
                {
                    CreateNewPlaylist();
                }

                RefreshDataAndUI();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Failed to load existing playlists", ex);
                MessageBox.Show($"Error loading playlists: {ex.Message}", "Load Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                if (!_playlists.Any()) CreateNewPlaylist();
            }
        }

        // METHODE AANGEPAST: Omgedoopt tot CreateNewPlaylist en logica gestroomlijnd
        private void CreateNewPlaylist()
        {
            _currentPlaylist = new PracticePlaylist
            {
                Name = $"Practice Session {DateTime.Now:MMM dd, HH:mm}",
                Description = "Focused practice session"
            };

            LbPlaylists.SelectedItem = null;
            TxtDuration.Text = "2";
            RefreshDataAndUI();
            MLLogManager.Instance.Log("UI prepared for new playlist entry.", LogLevel.Info);
            MarkUnsaved();
            SubscribeToCurrentPlaylistChanges();
        }

        // METHODE AANGEPAST: Roept nu de centrale CreateNewPlaylist aan
        private void BtnNewPlaylist_Click(object sender, RoutedEventArgs e)
        {
            CreateNewPlaylist();
        }

        /// <summary>
        /// Auto-generates an interleaved practice playlist from selected music pieces
        /// </summary>
        private void BtnAutoGenerateInterleaved_Click(object sender, RoutedEventArgs e)
        {
            // Create a piece selection dialog
            var pieceSelectionWindow = new Window
            {
                Title = "Select Pieces for Interleaved Practice",
                Width = 500,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = System.Windows.Media.Brushes.White
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Title
            var titleText = new TextBlock
            {
                Text = "🎯 Select pieces for interleaved practice:",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(20, 20, 20, 10)
            };
            Grid.SetRow(titleText, 0);
            grid.Children.Add(titleText);

            // ListBox for piece selection
            var listBox = new ListBox
            {
                Margin = new Thickness(20, 0, 20, 10),
                SelectionMode = SelectionMode.Multiple
            };
            listBox.ItemsSource = _allMusicPieces;
            listBox.DisplayMemberPath = "Title";
            Grid.SetRow(listBox, 1);
            grid.Children.Add(listBox);

            // Duration selection
            var durationPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(20, 0, 20, 20)
            };
            Grid.SetRow(durationPanel, 2);
            grid.Children.Add(durationPanel);

            var durationLabel = new TextBlock
            {
                Text = "Duration per section:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            durationPanel.Children.Add(durationLabel);

            var durationComboBox = new ComboBox
            {
                Width = 80,
                SelectedIndex = 1 // Default to 2 minutes
            };
            durationComboBox.ItemsSource = new[] { 1, 2, 3, 5, 10 };
            durationPanel.Children.Add(durationComboBox);

            var durationUnitLabel = new TextBlock
            {
                Text = "minutes",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0)
            };
            durationPanel.Children.Add(durationUnitLabel);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(20)
            };
            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Margin = new Thickness(0, 0, 10, 0),
                IsCancel = true
            };
            cancelButton.Click += (s, args) => pieceSelectionWindow.DialogResult = false;
            buttonPanel.Children.Add(cancelButton);

            var generateButton = new Button
            {
                Content = "Generate Playlist",
                Width = 120,
                IsDefault = true
            };
            generateButton.Click += (s, args) =>
            {
                var selectedPieces = listBox.SelectedItems.Cast<MusicPieceItem>().ToList();
                var selectedDuration = (int)durationComboBox.SelectedItem;

                if (selectedPieces.Any())
                {
                    try
                    {
                        // Generate the interleaved playlist with selected duration
                        var newPlaylist = PracticePlaylist.AutoGenerateInterleavedPlaylist(selectedPieces, selectedDuration);

                        // Add to playlists collection
                        _playlists.Add(newPlaylist);
                        _currentPlaylist = newPlaylist;
                        LbPlaylists.SelectedItem = newPlaylist;

                        RefreshDataAndUI();
                        MarkUnsaved();

                        MLLogManager.Instance.Log($"Auto-generated interleaved playlist: {newPlaylist.Name} with {newPlaylist.Items.Count} sections ({selectedDuration} min each)", LogLevel.Info);

                        pieceSelectionWindow.DialogResult = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error generating playlist: {ex.Message}", "Generation Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        MLLogManager.Instance.LogError("Failed to auto-generate interleaved playlist", ex);
                    }
                }
                else
                {
                    MessageBox.Show("Please select at least one music piece.", "No Selection",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            buttonPanel.Children.Add(generateButton);

            pieceSelectionWindow.Content = grid;
            pieceSelectionWindow.ShowDialog();
        }

        private void BtnDeletePlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (LbPlaylists.SelectedItem is not PracticePlaylist selectedPlaylist) return;

            var result = MessageBox.Show(
                $"Are you sure you want to delete the playlist '{selectedPlaylist.Name}'?", "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _playlists.Remove(selectedPlaylist);

                    string filePath = GetPlaylistFilePath(selectedPlaylist);
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    if (_currentPlaylist?.Id == selectedPlaylist.Id)
                    {
                        _currentPlaylist = _playlists.FirstOrDefault();
                        LbPlaylists.SelectedItem = _currentPlaylist;
                    }

                    RefreshDataAndUI();
                    MLLogManager.Instance.Log($"Deleted playlist: {selectedPlaylist.Name}", LogLevel.Info);
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance.LogError($"Failed to delete playlist: {selectedPlaylist.Name}", ex);
                    MessageBox.Show($"Error deleting playlist: {ex.Message}", "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LbPlaylists_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LbPlaylists.SelectedItem is PracticePlaylist selectedPlaylist)
            {
                _currentPlaylist = selectedPlaylist;
                _currentPlaylist.MarkAsUsed();
                RefreshDataAndUI();
                SubscribeToCurrentPlaylistChanges();
                MLLogManager.Instance.Log($"Loaded playlist: {_currentPlaylist.Name}", LogLevel.Info);
            }
        }

        private void CbMusicPieces_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CbMusicPieces.SelectedItem is MusicPieceItem selectedPiece)
            {
                CbBarSections.ItemsSource = selectedPiece.BarSections;
                CbBarSections.IsEnabled = true;
                CbBarSections.SelectedIndex = -1;
            }
            else
            {
                CbBarSections.ItemsSource = null;
                CbBarSections.IsEnabled = false;
            }
            UpdateAddButtonState();
        }

        private void BtnAddSection_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPlaylist == null)
            {
                MessageBox.Show("Please create or select a playlist first.", "No Playlist Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (CbMusicPieces.SelectedItem is not MusicPieceItem selectedPiece)
            {
                MessageBox.Show("Please select a music piece.", "No Music Piece Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!int.TryParse(TxtDuration.Text, out int duration) || duration < 1)
            {
                MessageBox.Show("Please enter a valid duration (minimum 1 minute).", "Invalid Duration", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BarSection? selectedSection = CbBarSections.SelectedItem as BarSection;

            if (selectedSection != null)
            {
                _currentPlaylist.AddBarSection(selectedPiece, selectedSection, duration);
                MLLogManager.Instance.Log($"Added section '{selectedSection.BarRange}' from '{selectedPiece.Title}' to playlist '{_currentPlaylist.Name}'", LogLevel.Info);
                MarkUnsaved();
            }
            else
            {
                MessageBox.Show("Please select a specific bar section to add.", "No Section Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RefreshDataAndUI();
            CbBarSections.SelectedIndex = -1;
            TxtDuration.Text = "2";
            UpdateAddButtonState();
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PlaylistItem item && _currentPlaylist != null)
            {
                int currentIndex = _currentPlaylist.Items.IndexOf(item);
                if (currentIndex > 0)
                {
                    _currentPlaylist.MoveItem(item, currentIndex - 1);
                    RefreshDataAndUI();
                    MarkUnsaved();
                }
            }
        }

        private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PlaylistItem item && _currentPlaylist != null)
            {
                _currentPlaylist.RemoveItem(item);
                RefreshDataAndUI();
                MarkUnsaved();
            }
        }

        private void BtnReshuffleOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentPlaylist == null || _currentPlaylist.Items.Count <= 1)
                {
                    MessageBox.Show("Need at least two items to reshuffle.", "Nothing to reshuffle", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Preserve current selection for a nicer UX
                var selected = LbPlaylistItems.SelectedItem as PlaylistItem;

                // Shuffle items
                var shuffled = _currentPlaylist.Items.OrderBy(_ => Guid.NewGuid()).ToList();

                // Rewrite collection with new order and corrected indices
                _currentPlaylist.Items.Clear();
                for (int i = 0; i < shuffled.Count; i++)
                {
                    shuffled[i].OrderIndex = i;
                    _currentPlaylist.Items.Add(shuffled[i]);
                }

                // Restore selection if possible
                if (selected != null && _currentPlaylist.Items.Contains(selected))
                {
                    LbPlaylistItems.SelectedItem = selected;
                }

                RefreshDataAndUI();
                MarkUnsaved();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Failed to reshuffle order.", ex);
                MessageBox.Show("Failed to reshuffle the order. Please try again.", "Reshuffle Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSavePlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPlaylist == null)
            {
                MessageBox.Show("No playlist to save.", "Nothing to Save", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (string.IsNullOrWhiteSpace(TxtPlaylistName.Text))
            {
                MessageBox.Show("Please enter a playlist name.", "Missing Name", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPlaylistName.Focus();
                return;
            }

            try
            {
                _currentPlaylist.Name = TxtPlaylistName.Text.Trim();
                _currentPlaylist.Description = TxtPlaylistDescription.Text.Trim();

                string filePath = GetPlaylistFilePath(_currentPlaylist);
                string jsonContent = System.Text.Json.JsonSerializer.Serialize(_currentPlaylist, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                File.WriteAllText(filePath, jsonContent);

                if (!_playlists.Contains(_currentPlaylist))
                {
                    _playlists.Add(_currentPlaylist);
                }

                RefreshDataAndUI(refreshPlaylistList: true);

                MessageBox.Show($"Playlist '{_currentPlaylist.Name}' saved successfully!", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                MLLogManager.Instance.Log($"Saved playlist: {_currentPlaylist.Name}", LogLevel.Info);
                _hasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Failed to save playlist", ex);
                MessageBox.Show($"Error saving playlist: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // METHODE AANGEPAST: Gebruikt nu de UI-vlag voor de refresh.
        private void BtnStartPlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPlaylist == null || _currentPlaylist.Items.Count == 0)
            {
                MessageBox.Show("Please create a playlist with at least one section.", "Empty Playlist", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                _currentPlaylist.Name = TxtPlaylistName.Text.Trim();
                _currentPlaylist.Description = TxtPlaylistDescription.Text.Trim();

                var playlistPracticeWindow = new PlaylistPracticeWindow(_currentPlaylist, _allMusicPieces) { Owner = this };

                // Stel de vlag in zodat de UI ververst wordt wanneer dit venster weer focus krijgt.
                _uiRefreshNeeded = true;

                playlistPracticeWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Failed to start playlist practice session", ex);
                MessageBox.Show($"Error starting practice session: {ex.Message}", "Start Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateAddButtonState()
        {
            BtnAddSection.IsEnabled = _currentPlaylist != null &&
                                      CbMusicPieces.SelectedItem != null &&
                                      CbBarSections.SelectedItem != null && // Vereis nu een sectie
                                      int.TryParse(TxtDuration.Text, out int duration) && duration >= 1;
        }

        // METHODE AANGEPAST: Logica verplaatst naar RefreshDataAndUI.
        private void UpdateEmptyStateVisibility()
        {
            bool hasPlaylists = _playlists.Count > 0;
            TxtEmptyPlaylists.Visibility = hasPlaylists ? Visibility.Collapsed : Visibility.Visible;
            LbPlaylists.Visibility = hasPlaylists ? Visibility.Visible : Visibility.Collapsed;

            bool hasItems = _currentPlaylist != null && _currentPlaylist.Items.Count > 0;
            TxtEmptyItems.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;

            // De luidruchtige log-regel is hier verwijderd.
        }

        // NIEUWE CENTRALE REFRESH-METHODE
        private void RefreshDataAndUI(bool refreshPlaylistList = false)
        {
            _isSyncingUI = true;
            try
            {
                // Bewaar de selectie
                var selectedItem = LbPlaylists.SelectedItem;

                if (refreshPlaylistList)
                {
                    LbPlaylists.ItemsSource = null;
                    LbPlaylists.ItemsSource = _playlists;
                }

                // Herstel de selectie indien mogelijk
                if (selectedItem != null && _playlists.Contains(selectedItem))
                {
                    LbPlaylists.SelectedItem = selectedItem;
                }
                else if (_currentPlaylist != null && _playlists.Contains(_currentPlaylist))
                {
                    LbPlaylists.SelectedItem = _currentPlaylist;
                }

                // Update de details van de geselecteerde playlist
                if (_currentPlaylist != null)
                {
                    TxtPlaylistName.Text = _currentPlaylist.Name;
                    TxtPlaylistDescription.Text = _currentPlaylist.Description;
                    LbPlaylistItems.ItemsSource = null;
                    LbPlaylistItems.ItemsSource = _currentPlaylist.Items;
                }
                else
                {
                    TxtPlaylistName.Text = string.Empty;
                    TxtPlaylistDescription.Text = string.Empty;
                    LbPlaylistItems.ItemsSource = null;
                }

                // Update alle knoppen en zichtbaarheidsstatussen
                UpdateAddButtonState();
                UpdateEmptyStateVisibility();
                BtnDeletePlaylist.IsEnabled = _currentPlaylist != null && _playlists.Contains(_currentPlaylist);
                BtnStartPlaylist.IsEnabled = _currentPlaylist != null && _currentPlaylist.Items.Count > 0;
                BtnSavePlaylist.IsEnabled = _currentPlaylist != null && !string.IsNullOrWhiteSpace(TxtPlaylistName.Text);
            }
            finally
            {
                // UI sync finished – do not consider these changes as user edits
                _isSyncingUI = false;
            }
        }

        private string GetProfileFolder()
        {
            string profileName = ActiveUserSession.ProfileName;
            if (string.IsNullOrEmpty(profileName))
            {
                MLLogManager.Instance.Log("Warning: No active user profile found, using 'Default' profile", LogLevel.Warning);
                profileName = "Default";
            }
            string profileFolder = DataPathProvider.GetProfileFolder(profileName);
            MLLogManager.Instance.Log($"Using profile folder: {profileFolder}", LogLevel.Debug);
            return profileFolder;
        }

        private string GetPlaylistFilePath(PracticePlaylist playlist)
        {
            string playlistsFolder = Path.Combine(GetProfileFolder(), "Playlists");
            return Path.Combine(playlistsFolder, $"{playlist.Id}.json");
        }

        private void CbBarSections_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateAddButtonState();
        }

        private void TxtDuration_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateAddButtonState();
        }

        // --- Duration editing helpers for playlist items ---
        // Software comments are in English as requested.

        private const int MinItemMinutes = 1;
        private const int MaxItemMinutes = 60;

        // Decrease duration by 1 minute (floor at MinItemMinutes)
        private void BtnDecDuration_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is PlaylistItem item)
                {
                    int original = item.DurationMinutes;
                    item.DurationMinutes = Math.Max(MinItemMinutes, original - 1);
                    if (item.DurationMinutes != original)
                    {
                        SavePlaylists_Auto("Decreased");
                    }
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error decreasing playlist item duration.", ex);
            }
        }

        // Increase duration by 1 minute (cap at MaxItemMinutes)
        private void BtnIncDuration_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is PlaylistItem item)
                {
                    int original = item.DurationMinutes;
                    item.DurationMinutes = Math.Min(MaxItemMinutes, original + 1);
                    if (item.DurationMinutes != original)
                    {
                        SavePlaylists_Auto("Increased");
                    }
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error increasing playlist item duration.", ex);
            }
        }

        // Allow only digits in the duration textbox; coerce range on commit.
        private void DurationNumeric_OnlyDigits(object sender, TextCompositionEventArgs e)
        {
            // Only digits allowed
            e.Handled = !e.Text.All(char.IsDigit);
        }

        // OPTIONAL (nice-to-have): coerce range after focus leaves the box.
        // Hook this in XAML with: LostFocus="DurationNumeric_LostFocus"
        private void DurationNumeric_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is PlaylistItem item)
            {
                int parsed = item.DurationMinutes;
                parsed = Math.Min(MaxItemMinutes, Math.Max(MinItemMinutes, parsed));
                if (parsed != item.DurationMinutes)
                    item.DurationMinutes = parsed;

                SavePlaylists_Auto("Edited");
            }
        }

        // Centralized auto-save after an inline change
        private void SavePlaylists_Auto(string reason)
        {
            try
            {
                // Call your existing save routine for playlists.
                // If your method name differs, replace with the correct one.
                SavePlaylists(); // <-- this should already exist in your window

                MLLogManager.Instance.Log($"Playlist durations updated ({reason}). Changes saved.", LogLevel.Info);
                _hasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Failed to auto-save playlist after duration change.", ex);
                MessageBox.Show("Failed to save playlist changes. Please try again.",
                                "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Centralized save routine for playlists, used by auto-save and manual save
        private void SavePlaylists()
        {
            if (_currentPlaylist == null)
                return;
            if (string.IsNullOrWhiteSpace(_currentPlaylist.Name))
                return;
            try
            {
                _currentPlaylist.Name = TxtPlaylistName.Text.Trim();
                _currentPlaylist.Description = TxtPlaylistDescription.Text.Trim();
                string filePath = GetPlaylistFilePath(_currentPlaylist);
                string jsonContent = System.Text.Json.JsonSerializer.Serialize(_currentPlaylist, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                File.WriteAllText(filePath, jsonContent);
                if (!_playlists.Contains(_currentPlaylist))
                {
                    _playlists.Add(_currentPlaylist);
                }
                RefreshDataAndUI(refreshPlaylistList: true);
                MLLogManager.Instance.Log($"Playlist '{_currentPlaylist.Name}' auto-saved.", LogLevel.Info);
                _hasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError("Error auto-saving playlist", ex);
            }
        }

        private void MarkUnsaved()
        {
            if (_isSyncingUI)
                return;
            _hasUnsavedChanges = true;
        }

        // Subscribe to current playlist changes so any edit (including DurationMinutes typing) marks unsaved
        private void SubscribeToCurrentPlaylistChanges()
        {
            try
            {
                if (_subscribedPlaylist != null)
                {
                    _subscribedPlaylist.Items.CollectionChanged -= PlaylistItems_CollectionChanged;
                    foreach (var it in _subscribedPlaylist.Items)
                        it.PropertyChanged -= PlaylistItem_PropertyChanged;
                }

                if (_currentPlaylist != null)
                {
                    _currentPlaylist.Items.CollectionChanged += PlaylistItems_CollectionChanged;
                    foreach (var it in _currentPlaylist.Items)
                        it.PropertyChanged += PlaylistItem_PropertyChanged;
                    _subscribedPlaylist = _currentPlaylist;
                }
                else
                {
                    _subscribedPlaylist = null;
                }
            }
            catch { /* best-effort */ }
        }

        private void PlaylistItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Manage per-item PropertyChanged subscriptions and mark unsaved
            if (e.OldItems != null)
            {
                foreach (var obj in e.OldItems)
                    if (obj is PlaylistItem oldItem)
                        oldItem.PropertyChanged -= PlaylistItem_PropertyChanged;
            }
            if (e.NewItems != null)
            {
                foreach (var obj in e.NewItems)
                    if (obj is PlaylistItem newItem)
                        newItem.PropertyChanged += PlaylistItem_PropertyChanged;
            }
            MarkUnsaved();
        }

        private void PlaylistItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Any change to an item is considered unsaved
            MarkUnsaved();
        }

        private void PlaylistManagerWindow_Closing(object? sender, CancelEventArgs e)
        {
            try
            {
                if (!_hasUnsavedChanges)
                    return;

                // If name is empty, force user to decide before closing
                if (string.IsNullOrWhiteSpace(TxtPlaylistName.Text))
                {
                    var nameMissing = MessageBox.Show(
                        "You have unsaved changes. Please enter a name before saving, or cancel to keep editing.",
                        "Unsaved Changes",
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Warning);
                    if (nameMissing == MessageBoxResult.OK)
                    {
                        // Don't close; allow user to enter a name
                        e.Cancel = true;
                        TxtPlaylistName.Focus();
                    }
                    return;
                }

                var result = MessageBox.Show(
                    "You have unsaved changes to this Interleaved Practice. Save before closing?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // Try to save; if something prevents saving, keep the window open
                    try
                    {
                        SavePlaylists();
                        _hasUnsavedChanges = false;
                    }
                    catch
                    {
                        e.Cancel = true;
                    }
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
                // If No, just allow closing without saving
            }
            catch
            {
                // In case of any unexpected error, allow closing to avoid trapping the user
            }
        }
    }
}