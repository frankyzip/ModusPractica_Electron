using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ModusPractica
{
    /// <summary>
    /// Difficulty does not impose local caps. It only maps to R* via EbbinghausConstants.GetRetentionTargetForDifficulty.
    /// </summary>
    public static class MusicPieceUtils
    {
        public static List<MusicPieceItem> GetAllMusicPieces()
        {
            // AANGEPAST: Haal de lijst direct op uit de static property van MainWindow.
            // De null-coalescing operator (??) zorgt voor veiligheid als de lijst nog niet geïnitialiseerd zou zijn.
            return new List<MusicPieceItem>(MainWindow.MusicPieces ?? new ObservableCollection<MusicPieceItem>());
        }

        public static bool IsMusicPiecePaused(Guid musicPieceId)
        {
            try
            {
                var musicPieces = GetAllMusicPieces();
                var musicPiece = musicPieces.FirstOrDefault(mp => mp.Id == musicPieceId);

                if (musicPiece != null)
                {
                    // Controleer of het stuk gepauzeerd is
                    if (musicPiece.IsPaused && musicPiece.PauseUntilDate.HasValue)
                    {
                        // A piece is paused if the pause date is today or in the future.
                        // This makes the "Pause Until" date inclusive.
                        return musicPiece.PauseUntilDate.Value.Date >= DateTime.Today;
                    }
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance.LogError($"Error checking if music piece {musicPieceId} is paused.", ex);
            }

            return false;
        }
    }
}