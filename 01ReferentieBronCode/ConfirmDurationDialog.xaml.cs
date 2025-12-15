using System;
using System.Windows;

namespace ModusPractica
{
    public partial class ConfirmDurationDialog : Window
    {
        public bool KeepFullDuration { get; private set; } = true;

        public ConfirmDurationDialog(TimeSpan inactiveDuration)
        {
            InitializeComponent();
            TxtInactiveTime.Text = $"{inactiveDuration.TotalMinutes:F0}";
        }

        private void BtnKeepTime_Click(object sender, RoutedEventArgs e)
        {
            this.KeepFullDuration = true;
            this.DialogResult = true;
            this.Close();
        }

        private void BtnAdjustTime_Click(object sender, RoutedEventArgs e)
        {
            this.KeepFullDuration = false;
            this.DialogResult = true;
            this.Close();
        }
    }
}