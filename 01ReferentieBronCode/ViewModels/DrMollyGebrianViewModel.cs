using System;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Navigation;

namespace ModusPractica.ViewModels
{
    public class DrMollyGebrianViewModel
    {
        public Action? CloseAction { get; set; }
        public ICommand CloseCommand { get; }
        public ICommand NavigateCommand { get; }

        public DrMollyGebrianViewModel()
        {
            CloseCommand = new RelayCommand(_ => CloseWindow());
            NavigateCommand = new RelayCommand(NavigateToUrl);
        }

        private void CloseWindow()
        {
            CloseAction?.Invoke();
        }

        private void NavigateToUrl(object? parameter)
        {
            if (parameter is string url)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance?.LogError("Failed to navigate to URL", ex);
                }
            }
            else if (parameter is RequestNavigateEventArgs e)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MLLogManager.Instance?.LogError("Failed to navigate to URI", ex);
                }
            }
        }
    }
}