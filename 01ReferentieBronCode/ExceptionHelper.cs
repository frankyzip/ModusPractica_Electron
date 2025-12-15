using System;
using System.Windows;

namespace ModusPractica
{
    public static class ExceptionHelper
    {
        /// <summary>
        /// Centrale exception handler: logt en toont optioneel een gebruikersvriendelijke melding.
        /// </summary>
        /// <param name="ex">De opgetreden exception</param>
        /// <param name="context">Contextinformatie (bijv. methodenaam)</param>
        /// <param name="showUserMessage">Toon een MessageBox aan de gebruiker</param>
        public static void HandleException(Exception ex, string? context = null, bool showUserMessage = true)
        {
            string logMessage = context == null ? $"Exception: {ex.Message}" : $"Exception in {context}: {ex.Message}";
            MLLogManager.Instance?.LogError(logMessage, ex);

            if (showUserMessage)
            {
                string userMessage = "Er is een onverwachte fout opgetreden. Probeer het opnieuw of neem contact op met support.";
                if (!string.IsNullOrWhiteSpace(context))
                    userMessage += $"\n\nContext: {context}";
                MessageBox.Show(userMessage, "Fout", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
