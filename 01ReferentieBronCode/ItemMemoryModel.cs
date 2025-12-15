using System;
using System.Collections.Concurrent;

namespace ModusPractica
{
    /// <summary>
    /// Per-item geheugenstaat voor adaptieve planning.
    /// TauDays (τ) representeert de huidige schatting van de tijdconstante voor dit item.
    /// Wordt incrementieel bijgewerkt op basis van echte succes/mis uitkomsten.
    /// </summary>
    public class ItemMemoryState
    {
        public string ItemId { get; init; } = string.Empty;
        public double TauDays { get; set; }
        public int ReviewCount { get; set; }
        public DateTime? LastReview { get; set; }
        public double? LastPredictedRetention { get; set; }
        public double? LastPlannedIntervalDays { get; set; }
        public double? EaseLike { get; set; } // optioneel: kan later gebruikt worden
    }

    /// <summary>
    /// Centraal in-memory model voor per-item τ / stability.
    /// Thread-safe via ConcurrentDictionary. (Persistente opslag kan later worden toegevoegd.)
    /// </summary>
    public static class ItemMemoryModel
    {
        private static readonly ConcurrentDictionary<string, ItemMemoryState> _states = new();

        // Parameters voor update – voorlopig hier, kan later naar configuratie.
        private const double ETA = 0.20;   // smoothing factor
        private const double ALPHA = 0.35; // omhoog bij correct
        private const double BETA = 0.45;  // omlaag bij fout
        private const double MARGIN = 0.05; // retentiemarge
        private const double MIN_TAU = 1.0;
        private const double MAX_TAU = 180.0;

        /// <summary>
        /// Haalt de state op of maakt een nieuwe met initTau.
        /// </summary>
        public static ItemMemoryState GetOrCreate(string itemId, Func<double> initTauFactory)
        {
            if (string.IsNullOrWhiteSpace(itemId)) throw new ArgumentException("itemId is null/empty");
            return _states.GetOrAdd(itemId, id => new ItemMemoryState
            {
                ItemId = id,
                TauDays = EbbinghausConstants.ClampTauToSafeBounds(initTauFactory()),
                ReviewCount = 0
            });
        }

        /// <summary>
        /// Update τ op basis van een review uitkomst.
        /// </summary>
        /// <param name="itemId">Unieke ID (bijv. piece+bar of pieceId)</param>
        /// <param name="intervalDays">Tijdsduur sinds vorige review in dagen</param>
        /// <param name="correct">Resultaat (true = correct/herinnerd)</param>
        /// <param name="targetRetention">Doel R* (0-1)</param>
        public static ItemMemoryState Update(string itemId, double intervalDays, bool correct, double targetRetention, Func<double> initTauFactory)
        {
            MLLogManager.Instance?.Log($"[DEBUG] ItemMemoryModel.Update called: itemId='{itemId}', interval={intervalDays:F2}d, correct={correct}, targetR={targetRetention:F3}", LogLevel.Info);

            var state = GetOrCreate(itemId, initTauFactory);
            double oldTau = state.TauDays;
            intervalDays = Math.Max(0.1, intervalDays);
            targetRetention = Math.Clamp(targetRetention, 0.50, 0.95);

            MLLogManager.Instance?.Log($"[DEBUG] ItemMemoryModel state: isNew={state.ReviewCount == 0}, reviewCount={state.ReviewCount}, oldTau={oldTau:F3}", LogLevel.Info);

            double predictedRetention = Math.Exp(-intervalDays / oldTau);
            double targetRatio = -Math.Log(targetRetention); // ≈0.223 bij 0.80
            double observedRatio = intervalDays / oldTau;    // = -ln(predictedRetention)

            double proposedTau = oldTau;
            if (correct)
            {
                double delta = observedRatio - targetRatio; // positief => interval relatief langer dan target ⇒ τ omhoog
                double adj = ALPHA * delta;
                adj = Math.Clamp(adj, -0.15, 0.50); // beperk extreme verandering
                proposedTau = oldTau * (1.0 + adj);
            }
            else
            {
                double severity = 1.0;
                if (predictedRetention < targetRetention - MARGIN)
                    severity = 0.7; // we voorspelden al lage retentie; demp reductie
                double down = BETA * severity;
                proposedTau = oldTau * (1.0 - down);
            }

            double newTau = (1 - ETA) * oldTau + ETA * proposedTau; // smoothing
            newTau = Math.Max(MIN_TAU, Math.Min(MAX_TAU, newTau));

            MLLogManager.Instance?.Log($"[DEBUG] ItemMemoryModel calc: predR={predictedRetention:F3}, targetRatio={targetRatio:F3}, obsRatio={observedRatio:F3}, proposedTau={proposedTau:F3}, newTau={newTau:F3}", LogLevel.Info);

            state.TauDays = newTau;
            state.ReviewCount++;
            state.LastReview = DateTime.UtcNow;
            state.LastPredictedRetention = predictedRetention;
            state.LastPlannedIntervalDays = PlanNextIntervalInternal(newTau, targetRetention);

            MLLogManager.Instance?.Log(
                $"[ItemTauUpdate] item='{itemId}' rev#{state.ReviewCount} interval={intervalDays:F2}d correct={correct} predR={predictedRetention:F3} τ_old={oldTau:F3} τ_new={newTau:F3} nextInterval={state.LastPlannedIntervalDays:F2}d",
                LogLevel.Info);

            return state;
        }

        /// <summary>
        /// Plant volgende interval voor gegeven τ en targetRetention.
        /// </summary>
        public static double PlanNextInterval(double tauDays, double targetRetention)
        {
            return PlanNextIntervalInternal(tauDays, targetRetention);
        }

        private static double PlanNextIntervalInternal(double tauDays, double targetRetention)
        {
            tauDays = EbbinghausConstants.ClampTauToSafeBounds(tauDays);
            targetRetention = Math.Clamp(targetRetention, 0.50, 0.95);
            double raw = -tauDays * Math.Log(targetRetention);
            var (clamped, _) = EbbinghausConstants.ClampIntervalToScientificBounds(raw, tauDays);
            return clamped;
        }
    }
}
