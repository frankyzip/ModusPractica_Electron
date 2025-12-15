namespace ModusPractica
{
    /// <summary>
    /// Dialoogservice voor de Practice Sessions-tab zodat de viewmodel geen directe afhankelijkheid heeft van MessageBox of windows.
    /// </summary>
    public interface IPracticeSessionDialogService
    {
        void ShowInformation(string message, string title);
        void ShowError(string message, string title);
        bool ConfirmDeletion(string message, string title);
        bool? ShowSessionEditor(MusicPieceItem musicPiece, BarSection barSection, PracticeHistory session);
    }
}
