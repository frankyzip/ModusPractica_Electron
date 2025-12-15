using System;

namespace ModusPractica
{
    /// <summary>
    /// Event arguments for view models that request their associated window or dialog to close.
    /// </summary>
    public sealed class RequestCloseEventArgs : EventArgs
    {
        public RequestCloseEventArgs(bool? dialogResult, MusicPieceItem? createdPiece = null, MusicPieceItem? restoredPiece = null)
        {
            DialogResult = dialogResult;
            CreatedPiece = createdPiece;
            RestoredPiece = restoredPiece;
        }

        public bool? DialogResult { get; }

        public MusicPieceItem? CreatedPiece { get; }

        public MusicPieceItem? RestoredPiece { get; }
    }
}
