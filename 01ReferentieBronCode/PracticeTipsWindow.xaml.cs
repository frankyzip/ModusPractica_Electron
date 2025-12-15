using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace ModusPractica
{
    /// <summary>
    /// Interaction logic for PracticeTipsWindow.xaml
    /// </summary>
    public partial class PracticeTipsWindow : Window
    {
        public PracticeTipsWindow()
        {
            InitializeComponent();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }
}