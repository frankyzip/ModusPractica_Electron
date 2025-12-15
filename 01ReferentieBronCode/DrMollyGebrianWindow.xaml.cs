using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using ModusPractica.ViewModels;

namespace ModusPractica
{
    /// <summary>
    /// Interaction logic for DrMollyGebrianWindow.xaml
    /// </summary>
    public partial class DrMollyGebrianWindow : Window
    {
        private readonly DrMollyGebrianViewModel _viewModel;

        public DrMollyGebrianWindow()
        {
            // Initializes all UI elements defined in XAML
            InitializeComponent();
            
            // Initialiseer de ViewModel
            _viewModel = new DrMollyGebrianViewModel();
            _viewModel.CloseAction = () => this.Close();
            this.DataContext = _viewModel;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch
            {
                // Eventueel: log of toon een melding.
            }
            e.Handled = true;
        }

        // Event handler for the "Close" button.
        // This method executes when the user clicks the button.
        private void BtnSluiten_Click(object sender, RoutedEventArgs e)
        {
            // Closes the window and returns to the previous window
            this.Close();
        }
    }
}