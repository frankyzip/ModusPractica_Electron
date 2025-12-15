using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ModusPractica
{
    /// <summary>
    /// Interaction logic for NewMusicPieceWindow.xaml
    /// </summary>
    public partial class NewMusicPieceWindow : Window
    {
        private readonly NewMusicPieceViewModel _viewModel;
        private bool _isSelecting = false;

        public NewMusicPieceWindow(List<MusicPieceItem> allMusicPieces)
        {
            InitializeComponent();

            _viewModel = new NewMusicPieceViewModel(allMusicPieces);
            _viewModel.RequestClose += ViewModel_RequestClose;

            DataContext = _viewModel;

            // Add event handlers for autocomplete behavior
            TxtTitle.PreviewKeyDown += ComboBox_PreviewKeyDown;
            TxtComposer.PreviewKeyDown += ComboBox_PreviewKeyDown;

            // Handle selection changes
            TxtTitle.SelectionChanged += ComboBox_SelectionChanged;
            TxtComposer.SelectionChanged += ComboBox_SelectionChanged;

            // Hook into the text input through Loaded event to get TextBox
            TxtTitle.Loaded += (s, e) => AttachTextBoxHandlers(TxtTitle);
            TxtComposer.Loaded += (s, e) => AttachTextBoxHandlers(TxtComposer);

            Loaded += (_, _) => TxtTitle.Focus();
        }

        public MusicPieceItem? CreatedMusicPiece => _viewModel.CreatedMusicPiece;

        // Archiving/restoring removed: no RestoredMusicPiece

        private void ViewModel_RequestClose(object? sender, RequestCloseEventArgs e)
        {
            if (e.DialogResult.HasValue)
            {
                DialogResult = e.DialogResult;
            }
            else
            {
                Close();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel.RequestClose -= ViewModel_RequestClose;
            base.OnClosed(e);
        }

        private void ComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox == null) return;

            // Close dropdown on Enter or Tab
            if (e.Key == Key.Enter || e.Key == Key.Tab)
            {
                comboBox.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox == null) return;

            // When an item is selected from the dropdown, close it
            if (e.AddedItems.Count > 0 && comboBox.IsDropDownOpen)
            {
                _isSelecting = true;
                comboBox.IsDropDownOpen = false;
                var selectedText = e.AddedItems[0]?.ToString();
                if (!string.IsNullOrWhiteSpace(selectedText))
                {
                    // Update ViewModel directly to avoid re-entrant filtering
                    if (ReferenceEquals(comboBox, TxtTitle))
                    {
                        _viewModel.SetTitleFromSelection(selectedText!);
                    }
                    else if (ReferenceEquals(comboBox, TxtComposer))
                    {
                        _viewModel.SetComposerFromSelection(selectedText!);
                    }
                }

                // Reset flag after a short delay
                Dispatcher.BeginInvoke(new Action(() => _isSelecting = false), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        private void AttachTextBoxHandlers(ComboBox comboBox)
        {
            if (comboBox == null) return;

            // Find the TextBox inside the ComboBox template
            var textBox = FindVisualChild<TextBox>(comboBox);
            if (textBox != null)
            {
                textBox.TextChanged += (s, e) => OnComboBoxTextChanged(comboBox, textBox);
                // Prevent automatic text selection
                textBox.PreviewTextInput += (s, e) =>
                {
                    // Don't let ComboBox auto-complete interfere
                    _isSelecting = false;
                };
            }
        }

        private void OnComboBoxTextChanged(ComboBox comboBox, TextBox textBox)
        {
            if (comboBox == null || _isSelecting) return;

            string text = comboBox.Text ?? string.Empty;

            // Store cursor position before opening dropdown
            int cursorPos = textBox.SelectionStart;

            // Always keep dropdown open when there are items to show and we're not empty
            if (!string.IsNullOrEmpty(text) && comboBox.Items.Count > 0)
            {
                comboBox.SetCurrentValue(ComboBox.IsDropDownOpenProperty, true);

                // Restore cursor position and clear any selection
                textBox.SelectionStart = cursorPos;
                textBox.SelectionLength = 0;
            }
            else if (string.IsNullOrEmpty(text))
            {
                // Close dropdown if empty
                comboBox.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }

            return null;
        }
    }
}
