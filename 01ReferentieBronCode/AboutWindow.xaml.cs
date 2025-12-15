using System.Windows;

namespace ModusPractica
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        // Constructor for the AboutWindow. This method is called when the window is created.
        public AboutWindow()
        {
            InitializeComponent(); // This method initializes the UI components defined in XAML.

            // Set OS information
            if (TxtOperatingSystem != null)
            {
                TxtOperatingSystem.Text = GetOperatingSystemInfo();
            }

            // Removed runtime build time assignment as requested. Version/build can be set manually in XAML.
        }

        // Event handler for the "Close" button click.
        // This method closes the window when the user clicks the button.
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); // Close the current window.
        }

        // Get detailed operating system information
        private string GetOperatingSystemInfo()
        {
            try
            {
                // Get OS information
                OperatingSystem os = Environment.OSVersion;
                Version version = os.Version;

                string osInfo = $"{os.Platform} {version.Major}.{version.Minor}.{version.Build}";

                // Add more specific Windows version info if possible
                if (os.Platform == PlatformID.Win32NT)
                {
                    if (version.Major == 10 && version.Build >= 22000)
                    {
                        osInfo = "Windows 11";
                    }
                    else if (version.Major == 10)
                    {
                        osInfo = "Windows 10";
                    }
                    else if (version.Major == 6 && version.Minor == 3)
                    {
                        osInfo = "Windows 8.1";
                    }
                    else if (version.Major == 6 && version.Minor == 2)
                    {
                        osInfo = "Windows 8";
                    }
                    else if (version.Major == 6 && version.Minor == 1)
                    {
                        osInfo = "Windows 7";
                    }

                    // Add 64-bit or 32-bit information
                    osInfo += Environment.Is64BitOperatingSystem ? " (64-bit)" : " (32-bit)";
                }

                return osInfo;
            }
            catch (Exception)
            {
                // Return a generic message if there was an error getting OS info
                return "Windows";
            }
        }
    }
}