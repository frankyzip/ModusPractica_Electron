using System;

namespace ModusPractica.Infrastructure
{
    /// <summary>
    /// App-brede events. Eenvoudig houden en thread-safe genoeg voor onze use-case.
    /// </summary>
    public static class AppEvents
    {
        public static event Action? ScheduledSessionsChanged;

        public static void RaiseScheduledSessionsChanged()
        {
            try { ScheduledSessionsChanged?.Invoke(); }
            catch { /* nooit een refresh laten crashen */ }
        }
    }
}