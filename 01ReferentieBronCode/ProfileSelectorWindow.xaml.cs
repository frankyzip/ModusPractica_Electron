using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Windows.Documents;
using System.Windows.Navigation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using WinForms = System.Windows.Forms;

namespace ModusPractica
{
    public partial class ProfileSelectorWindow : Window
    {
        private string _selectedProfile = string.Empty;
        private string _customRootPath = string.Empty;
        private List<string> _availableProfiles = new List<string>();

        public string SelectedProfile => _selectedProfile;
        public string CustomRootPath => _customRootPath;
        public bool UseCustomRoot => !string.IsNullOrWhiteSpace(_customRootPath);

        public ProfileSelectorWindow()
        {
            try
            {
                InitializeComponent();
                LoadSavedConfiguration();
                RefreshProfileList();
                UpdateLocationPreview();
                // Ensure initial button state reflects missing location/selection
                UpdateButtonsEnabled();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error initializing profile selector:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    "Initialization Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                throw;
            }
        }

        private void LoadSavedConfiguration()
        {
            try
            {
                var config = ProfileConfiguration.Load();

                // Restore last used custom root if available
                if (!string.IsNullOrWhiteSpace(config.CustomRootPath) && Directory.Exists(config.CustomRootPath))
                {
                    _customRootPath = config.CustomRootPath;
                    TxtCustomPath?.SetCurrentValue(TextBox.TextProperty, _customRootPath);
                }

                // Do not preselect last used profile; require explicit user selection
                _selectedProfile = string.Empty;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.Log($"Could not load saved profile configuration: {ex.Message}", LogLevel.Warning);
            }
        }

        private void RefreshProfileList()
        {
            try
            {
                _availableProfiles.Clear();
                LbProfiles.Items.Clear();

                string rootPath = GetEffectiveRootPath();

                // Only show profiles if a custom location has been selected
                if (string.IsNullOrWhiteSpace(rootPath))
                {
                    return;
                }

                string profilesFolder = Path.Combine(rootPath, "Profiles");

                if (Directory.Exists(profilesFolder))
                {
                    var profileDirs = Directory.GetDirectories(profilesFolder)
                        .Select(Path.GetFileName)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .OrderBy(name => name)
                        .ToList();

                    foreach (var profile in profileDirs)
                    {
                        _availableProfiles.Add(profile!);
                        LbProfiles.Items.Add(profile);
                    }
                }

                // No auto-selection; user must choose a profile before Start is enabled
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading profiles: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private string GetEffectiveRootPath()
        {
            if (!string.IsNullOrWhiteSpace(_customRootPath))
            {
                return _customRootPath;
            }

            // Return empty string if no custom path is set (user must select one)
            return string.Empty;
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new WinForms.FolderBrowserDialog
                {
                    Description = "Select a folder for your Modus Practica data",
                    ShowNewFolderButton = true
                };

                // Pre-select current path if available
                if (!string.IsNullOrWhiteSpace(_customRootPath) && Directory.Exists(_customRootPath))
                {
                    dialog.SelectedPath = _customRootPath;
                }

                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    string selectedPath = dialog.SelectedPath;

                    // Validate write permissions
                    if (!IsPathWritable(selectedPath))
                    {
                        MessageBox.Show(
                            "You do not have write access to this folder. Please choose a different location.",
                            "Access Denied",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    _customRootPath = Path.Combine(selectedPath, "ModusPractica");
                    TxtCustomPath?.SetCurrentValue(TextBox.TextProperty, _customRootPath);

                    RefreshProfileList();
                    UpdateLocationPreview();
                    UpdateButtonsEnabled();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error selecting folder: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool IsPathWritable(string path)
        {
            try
            {
                // Test write by creating a temporary file
                string testFile = Path.Combine(path, $"_mp_write_test_{Guid.NewGuid()}.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void UpdateLocationPreview()
        {
            if (TxtLocationPreview == null) return;

            string effectivePath = GetEffectiveRootPath();
            // Render as a clickable hyperlink
            TxtLocationPreview.Inlines.Clear();
            TxtLocationPreview.Inlines.Add(new Run("Data will be stored in: "));

            if (!string.IsNullOrWhiteSpace(effectivePath))
            {
                string profilesPath = Path.Combine(effectivePath, "Profiles");
                var link = new Hyperlink(new Run(profilesPath));
                try
                {
                    var uri = new Uri(profilesPath, UriKind.Absolute);
                    link.NavigateUri = uri;
                }
                catch { /* ignore malformed paths */ }
                link.ToolTip = "Open in File Explorer";
                link.RequestNavigate += (s, e) =>
                {
                    try
                    {
                        var target = e.Uri != null ? e.Uri.LocalPath : profilesPath;
                        if (string.IsNullOrWhiteSpace(target)) return;

                        if (!Directory.Exists(target))
                        {
                            var parent = Path.GetDirectoryName(target);
                            if (!string.IsNullOrEmpty(parent)) target = parent;
                        }

                        Process.Start(new ProcessStartInfo
                        {
                            FileName = target,
                            UseShellExecute = true,
                            Verb = "open"
                        });
                        e.Handled = true;
                    }
                    catch { /* ignore */ }
                };
                TxtLocationPreview.Inlines.Add(link);
            }
            else
            {
                TxtLocationPreview.Inlines.Add(new Run("(not set)"));
            }
        }

        private void LbProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedProfile = LbProfiles.SelectedItem as string ?? string.Empty;
            BtnDeleteProfile?.SetCurrentValue(IsEnabledProperty, !string.IsNullOrWhiteSpace(_selectedProfile) && _selectedProfile != "Default");
            // Enable OK only when both a profile is selected and a data location is chosen
            BtnOk?.SetCurrentValue(IsEnabledProperty, !string.IsNullOrWhiteSpace(_selectedProfile) && !string.IsNullOrWhiteSpace(_customRootPath));
        }

        private void BtnNewProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Guard: require a data location before creating a new profile
                if (string.IsNullOrWhiteSpace(_customRootPath))
                {
                    MessageBox.Show(
                        "Please choose a data location first via 'Browse' or 'Open Profiles'.",
                        "No location selected",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    BtnBrowse?.Focus();
                    return;
                }

                var dialog = new InputDialog("New Profile", "Enter a name for the new profile:");
                if (dialog.ShowDialog() == true)
                {
                    string profileName = dialog.ResponseText.Trim();

                    if (string.IsNullOrWhiteSpace(profileName))
                    {
                        MessageBox.Show(
                            "Profile name cannot be empty.",
                            "Invalid Name",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    // Validate profile name (no invalid path characters)
                    if (profileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    {
                        MessageBox.Show(
                            "Profile name contains invalid characters.",
                            "Invalid Name",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    if (_availableProfiles.Contains(profileName, StringComparer.OrdinalIgnoreCase))
                    {
                        MessageBox.Show(
                            "A profile with this name already exists.",
                            "Duplicate Name",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    // Create profile folder structure
                    string rootPath = GetEffectiveRootPath();
                    string profileFolder = Path.Combine(rootPath, "Profiles", profileName);
                    Directory.CreateDirectory(profileFolder);

                    // Refresh list and select new profile
                    RefreshProfileList();
                    LbProfiles?.SetCurrentValue(Selector.SelectedItemProperty, profileName);

                    MessageBox.Show(
                        $"Profile '{profileName}' has been created.",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    UpdateButtonsEnabled();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Fout bij het aanmaken van profiel: {ex.Message}",
                    "Fout",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnOpenProfiles_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new WinForms.FolderBrowserDialog
                {
                    Description = "Select an existing 'Profiles' folder (e.g., on USB)",
                    ShowNewFolderButton = false
                };

                // Preselect current Profiles path if possible
                var currentRoot = GetEffectiveRootPath();
                var currentProfiles = !string.IsNullOrWhiteSpace(currentRoot) ? Path.Combine(currentRoot, "Profiles") : string.Empty;
                if (!string.IsNullOrWhiteSpace(currentProfiles) && Directory.Exists(currentProfiles))
                {
                    dialog.SelectedPath = currentProfiles;
                }

                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    var chosen = dialog.SelectedPath;
                    // Accept if it's a Profiles folder or its parent contains a Profiles subfolder
                    string? root = null;
                    if (string.Equals(Path.GetFileName(chosen), "Profiles", StringComparison.OrdinalIgnoreCase))
                    {
                        root = Path.GetDirectoryName(chosen);
                    }
                    else if (Directory.Exists(Path.Combine(chosen, "Profiles")))
                    {
                        root = chosen;
                    }

                    if (string.IsNullOrWhiteSpace(root))
                    {
                        MessageBox.Show("Please select a folder that is the 'Profiles' folder or a folder that contains a 'Profiles' subfolder.",
                            "Invalid Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // Validate write access to root (or its parent)
                    if (!IsPathWritable(root))
                    {
                        MessageBox.Show("You do not have write access to this location.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // IMPORTANT: Use the folder that directly contains the 'Profiles' subfolder
                    // as the custom app root. Do NOT append 'ModusPractica' here, because we're
                    // opening an existing data location which may not be named 'ModusPractica'.
                    _customRootPath = root!;

                    TxtCustomPath?.SetCurrentValue(TextBox.TextProperty, _customRootPath);
                    RefreshProfileList();
                    UpdateLocationPreview();
                    UpdateButtonsEnabled();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Profiles folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_selectedProfile) || _selectedProfile == "Default")
                    return;

                // Special handling: If trying to delete while ProfileSelector is open,
                // we can only delete if it's a different profile than what might be loaded
                var result = MessageBox.Show(
                    $"Are you sure you want to delete the profile '{_selectedProfile}'?\n\n" +
                    "ALL data for this profile will be permanently deleted!\n\n" +
                    "Note: You can only delete profiles that are not currently in use.",
                    "Confirm Deletion",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    string rootPath = GetEffectiveRootPath();
                    string profileFolder = Path.Combine(rootPath, "Profiles", _selectedProfile);

                    if (Directory.Exists(profileFolder))
                    {
                        try
                        {
                            // Force garbage collection to release any file handles
                            GC.Collect();
                            GC.WaitForPendingFinalizers();

                            // Try immediate deletion
                            Directory.Delete(profileFolder, true);

                            RefreshProfileList();

                            MessageBox.Show(
                                $"Profile '{_selectedProfile}' has been deleted.",
                                "Deleted",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            var dlg = new ProfileInUseDialog(profileFolder) { Owner = this };
                            dlg.ShowDialog();
                            if (dlg.Result == ProfileInUseDialogResult.OpenAnotherProfile)
                            {
                                // Bring the listbox into focus so the user can select another profile
                                LbProfiles.Focus();
                            }
                        }
                        catch (IOException)
                        {
                            var dlg = new ProfileInUseDialog(profileFolder) { Owner = this };
                            dlg.ShowDialog();
                            if (dlg.Result == ProfileInUseDialogResult.OpenAnotherProfile)
                            {
                                LbProfiles.Focus();
                            }
                        }
                    }
                    else
                    {
                        // Folder doesn't exist, just refresh the list
                        RefreshProfileList();
                        MessageBox.Show(
                            $"Profile '{_selectedProfile}' folder not found. It may have been already deleted.",
                            "Not Found",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error deleting profile: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_selectedProfile))
                {
                    MessageBox.Show(
                        "Please select a profile first.",
                        "No Selection",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Validate custom location is selected
                if (string.IsNullOrWhiteSpace(_customRootPath))
                {
                    MessageBox.Show(
                        "Please choose a custom location first.",
                        "No Location",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!IsPathWritable(Path.GetDirectoryName(_customRootPath) ?? _customRootPath))
                {
                    MessageBox.Show(
                        "You do not have write permission for this location.",
                        "Access Denied",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // Save configuration
                var config = new ProfileConfiguration
                {
                    LastUsedProfile = _selectedProfile,
                    CustomRootPath = _customRootPath
                };
                ProfileConfiguration.Save(config);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error saving configuration: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    /// <summary>
    /// Simple input dialog for profile name
    /// </summary>
    public class InputDialog : Window
    {
        private TextBox _textBox;

        public string ResponseText => _textBox.Text;

        public InputDialog(string title, string message)
        {
            Title = title;
            Width = 400;
            Height = 180;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid { Margin = new Thickness(15) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var textBlock = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(textBlock, 0);
            grid.Children.Add(textBlock);

            _textBox = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                Height = 30,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(_textBox, 1);
            grid.Children.Add(_textBox);

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnOk = new Button
            {
                Content = "OK",
                Width = 80,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            btnOk.Click += (s, e) => { DialogResult = true; Close(); };

            var btnCancel = new Button
            {
                Content = "Cancel",
                Width = 80,
                IsCancel = true
            };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };

            panel.Children.Add(btnOk);
            panel.Children.Add(btnCancel);
            Grid.SetRow(panel, 2);
            grid.Children.Add(panel);

            Content = grid;

            Loaded += (s, e) => _textBox.Focus();
        }
    }

    public partial class ProfileSelectorWindow
    {
        /// <summary>
        /// Central place to enable/disable buttons based on current state
        /// </summary>
        private void UpdateButtonsEnabled()
        {
            bool hasLocation = !string.IsNullOrWhiteSpace(_customRootPath);
            bool hasSelection = !string.IsNullOrWhiteSpace(_selectedProfile);
            try
            {
                BtnNewProfile?.SetCurrentValue(UIElement.IsEnabledProperty, hasLocation);
                BtnOk?.SetCurrentValue(UIElement.IsEnabledProperty, hasLocation && hasSelection);
                // Toggle inline location warning
                TxtLocationWarning?.SetCurrentValue(UIElement.VisibilityProperty, hasLocation ? Visibility.Collapsed : Visibility.Visible);
            }
            catch
            {
                // ignore if controls are not yet initialized
            }
        }
    }
}
