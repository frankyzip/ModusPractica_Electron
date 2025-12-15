using System.Windows;

namespace ModusPractica
{
    /// <summary>
    /// Interaction logic for PracticeOutcomeDialog.xaml.
    /// A custom dialog to ask the user for the reason for ending a session
    /// when the target was not met, but a struggle was detected.
    /// </summary>
    public partial class PracticeOutcomeDialog : Window
    {
        /// <summary>
        /// Gets the outcome selected by the user.
        /// Possible values: "Continue", "Frustration", "TimeConstraint".
        /// </summary>
        public string SelectedOutcome { get; private set; }

        public PracticeOutcomeDialog(string coachingMessage)
        {
            InitializeComponent();

            // Set the coaching message provided by the calling window
            TxtCoachingMessage.Text = coachingMessage;

            // Default outcome in case the window is closed without a button press
            SelectedOutcome = "Continue";
        }

        private void BtnContinue_Click(object sender, RoutedEventArgs e)
        {
            // User wants to continue practicing.
            SelectedOutcome = "Continue";
            this.DialogResult = true;
            this.Close();
        }

        private void BtnSaveFrustration_Click(object sender, RoutedEventArgs e)
        {
            // User is stopping because the passage was too difficult or frustrating.
            SelectedOutcome = "Frustration";
            this.DialogResult = true;
            this.Close();
        }

        private void BtnSaveTime_Click(object sender, RoutedEventArgs e)
        {
            // User is stopping due to external reasons like lack of time.
            SelectedOutcome = "TimeConstraint";
            this.DialogResult = true;
            this.Close();
        }
    }
}