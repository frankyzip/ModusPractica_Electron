using System;

namespace ModusPractica
{
    /// <summary>
    /// Normative Ebbinghaus policy (single source of truth):
    /// - Retention targets (R*): Difficult 0.85, Default 0.80, Easy 0.70, Mastered 0.65 (via GetRetentionTargetForDifficulty).
    /// - Tau clamp: τ ∈ [1, 180] (ClampTauToSafeBounds).
    /// - Interval clamp: [1, 365] days AND t ≤ 5×τ (ClampIntervalToScientificBounds).
    /// - Planner schedules ≥ 1 day; Registration may use 0.0 days for same-day extra practice (due date unchanged).
    /// All clamping happens centrally here; no local caps elsewhere.
    /// </summary>
    public static class EbbinghausConstants
    {
        #region Ebbinghaus Vergeetcurve Parameters

        public static readonly double BASE_TAU_HOURS = 72.0;
        public static readonly double TAU_STANDARD_DEVIATION = 24.0;
        public static readonly double BASE_TAU_DAYS = BASE_TAU_HOURS / 24.0;
        public static readonly double MIN_TAU_DAYS = BASE_TAU_DAYS / 5.0; // 0.6 dagen
        public static readonly double MAX_TAU_DAYS = BASE_TAU_DAYS * 5.0; // 15 dagen

        #endregion

        #region Retentie Parameters

        public static readonly double INITIAL_LEARNING_STRENGTH = 0.80; // 80% (zorgt samen met baseline 15% voor ~95% retentie op dag 0)
        public static readonly double ASYMPTOTIC_RETENTION_BASELINE = 0.15; // 15%
        public static readonly double RETENTION_THRESHOLD = 0.80; // 80% (Default R*)
        public const double EASY_RETENTION_THRESHOLD = 0.70; // Easy R*
        public const double MASTERED_RETENTION_THRESHOLD = 0.65; // Mastered R*
        public static readonly double MIN_RETENTION_THRESHOLD = 0.50; // 50%
        public static readonly double OPTIMAL_RETENTION_THRESHOLD = 0.85; // Difficult R*

        #endregion

        #region Materiaal-specifieke Aanpassingen

        public static readonly double MUSIC_MATERIAL_FACTOR = 3.0;
        public static readonly double MOTOR_SKILL_FACTOR = 2.5;
        public static readonly double REPETITION_STRENGTH_FACTOR = 1.3;

        #endregion

        #region Moeilijkheidsgraad Aanpassingen

        public static class DifficultyModifiers
        {
            public static readonly double DIFFICULT = 0.6; // 40% reductie
            public static readonly double AVERAGE = 1.0; // Geen aanpassing
            public static readonly double EASY = 1.7; // 70% verhoging
            public static readonly double MASTERED = 3.5; // 250% verhoging (alleen voor stage 5+)

            // Graduele MASTERED groei gebaseerd op wetenschappelijk onderzoek:
            // Bjork & Bjork (2011), Cepeda et al. (2006), Simmons & Duke (2006)
            // Motor consolidatie bij muziek vereist graduele expansion (3-14 dagen eerst)
            public static readonly double MASTERED_STAGE_3 = 2.0;  // 100% (eerste mastered: 7-10 dagen)
            public static readonly double MASTERED_STAGE_4 = 2.5;  // 150% (tweede perfect: 14-21 dagen)
            public static readonly double MASTERED_STAGE_5_PLUS = 3.5; // 250% (derde+ perfect: 30-60+ dagen)
        }

        #endregion

        #region Interval Berekening Parameters

        public static readonly double BASE_EXPANSION_FACTOR = 2.0;
        public static readonly int MIN_INTERVAL_DAYS = 1; // Beleidsminimum: 1 dag voor consolidatie.
        public static readonly int MAX_INTERVAL_DAYS = 365; // Beleidsmaximum: 1 jaar als veiligheidsplafond.
        public static readonly double PERFORMANCE_ADJUSTMENT_FACTOR = 0.3;

        #endregion

        #region Individuele Kalibratie Parameters

        public static readonly int MIN_SESSIONS_FOR_CALIBRATION = 10;
        public static readonly double BAYESIAN_LEARNING_RATE = 0.1;
        public static readonly double CONFIDENCE_INTERVAL = 0.95;

        #endregion

        #region Demografische Personalisatie Parameters

        /// <summary>
        /// Age-stratified BASE_TAU_DAYS gebaseerd op wetenschappelijk onderzoek:
        /// GECORRIGEERD conform literatuur: Jongeren en ouderen vergeten sneller dan middelbare leeftijd.
        /// - 8-25 jaar: Sneller vergeten → kortere intervallen (2.5 dagen)
        /// - 26-50 jaar: Baseline (3.0 dagen)  
        /// - 51+ jaar: Sneller/gelijk vergeten → kortere intervallen (2.5 dagen)
        /// Conservatieve aanpassingen die snel convergeren naar individuele kalibratie.
        /// </summary>
        public static double GetAgeAdjustedBaseTau(int age)
        {
            // Leeftijdsaanpassing verwijderd gebaseerd op onderzoek - leeftijd maakt niet veel verschil voor forgetting curves
            return BASE_TAU_DAYS; // Altijd baseline gebruiken
        }

        // Gender multiplier removed - effect size too small (d=0.2-0.3) and inconsistent across studies.
        // Muziek is hybrid skill (motor + declarative), making simple gender split scientifically unsound.

        /// <summary>
        /// Muzikale ervaring-gebaseerde tau aanpassingen (GECORRIGEERD v3.0):
        /// Gebaseerd op encoding strength hypothesis (Bjork & Bjork, 2011; Ericsson et al., 2018):
        /// Sterkere initiële encoding → langzamer verval → langere optimale τ → langere intervallen
        /// 
        /// - Beginner: τ×0.8 (zwakke encoding, sneller verval → KORTERE intervallen, meer oefening)
        /// - Intermediate: τ×1.0 (baseline)
        /// - Advanced: τ×1.1 (sterke encoding, langzamer verval → langere intervallen)
        /// - Professional: τ×1.3 (expertise chunking + "desirable difficulty" → LANGSTE intervallen)
        /// 
        /// Verwachte verbetering: 20-30%
        /// </summary>
        public static double GetExperienceAdjustedTau(double baseTau, string experience)
        {
            if (string.IsNullOrWhiteSpace(experience)) return baseTau;

            var settings = SettingsManager.Instance?.CurrentSettings;
            if (settings != null)
            {
                return experience.ToLower().Trim() switch
                {
                    "beginner" => baseTau * settings.BeginnerTauMultiplier,
                    "intermediate" => baseTau * settings.IntermediateTauMultiplier,
                    "advanced" => baseTau * settings.AdvancedTauMultiplier,
                    "professional" => baseTau * settings.ProfessionalTauMultiplier,
                    _ => baseTau * settings.IntermediateTauMultiplier
                };
            }

            // Fallback: GECORRIGEERDE hardcoded waarden (omkering van v2.x logica)
            return experience.ToLower().Trim() switch
            {
                "beginner" => baseTau * 0.8,     // Kortere intervallen (zwakke encoding, sneller vergeten)
                "intermediate" => baseTau,        // Baseline (3.0 dagen)
                "advanced" => baseTau * 1.1,      // Langere intervallen (sterke encoding, langzamer vergeten)
                "professional" => baseTau * 1.3,  // Langste intervallen (chunking expertise + desirable difficulty)
                _ => baseTau
            };
        }

        /// <summary>
        /// Centrale methode voor demografische personalisatie van BASE_TAU_DAYS.
        /// WETENSCHAPPELIJK GECORRIGEERD (v3.0): 
        /// - Age-adjustments gebaseerd op literatuur (Park et al., 2002; Hedden & Gabrieli, 2004)
        /// - Experience multipliers OMGEKEERD: encoding strength hypothesis (Bjork & Bjork, 2011)
        /// - Gender removed: effect size te klein (d=0.2-0.3), muziek is hybrid skill
        /// Combineert leeftijd en ervaring met conservatieve aanpassingen die snel convergeren naar PMC.
        /// </summary>
        public static double GetPersonalizedBaseTau(int age, string experience)
        {
            try
            {
                // Stap 1: Leeftijd-gebaseerde base tau
                double baseTau = GetAgeAdjustedBaseTau(age);

                // Stap 2: Ervaring aanpassing (GECORRIGEERD: omgekeerde logica)
                baseTau = GetExperienceAdjustedTau(baseTau, experience);

                // Veiligheidscontrole en clamping
                baseTau = ClampTauToSafeBounds(baseTau);

                // Only log if MLLogManager is available to prevent startup issues
                try
                {
                    // Leeftijd wordt niet meer gebruikt in de berekening; enkel ervaring wordt gelogd
                    MLLogManager.Instance?.Log($"PersonalizedBaseTau: exp='{experience}' -> τ={baseTau:F3}d", LogLevel.Debug);
                }
                catch { /* Ignore logging errors during startup */ }

                return baseTau;
            }
            catch (Exception ex)
            {
                try
                {
                    MLLogManager.Instance?.LogError($"GetPersonalizedBaseTau: Error with age={age}, experience='{experience}'", ex);
                }
                catch { /* Ignore logging errors during startup */ }
                return BASE_TAU_DAYS; // Fallback naar originele waarde
            }
        }

        #endregion

        #region Hulpmethoden

        /// <summary>
        /// NEW: Overloaded versie met PracticeScheduleStage voor graduele MASTERED groei.
        /// Implementeert wetenschappelijk gefundeerde graduele expansion voor mastered secties.
        /// Gebaseerd op: Bjork & Bjork (2011), Cepeda et al. (2006), Simmons & Duke (2006)
        /// </summary>
        public static double CalculateAdjustedTau(string difficulty, int repetitionCount, int practiceScheduleStage)
        {
            if (repetitionCount < 0)
            {
                MLLogManager.Instance?.Log($"CalculateAdjustedTau (stage-aware): Negative repetition count ({repetitionCount}) corrected to 0", LogLevel.Warning);
                repetitionCount = 0;
            }
            const int MAX_SAFE_REPETITIONS = 1000;
            if (repetitionCount > MAX_SAFE_REPETITIONS)
            {
                MLLogManager.Instance?.Log($"CalculateAdjustedTau (stage-aware): Extremely high repetition count ({repetitionCount}) clamped to {MAX_SAFE_REPETITIONS}", LogLevel.Debug);
                repetitionCount = MAX_SAFE_REPETITIONS;
            }

            try
            {
                double adjustedTau = BASE_TAU_DAYS * MUSIC_MATERIAL_FACTOR;
                if (double.IsNaN(adjustedTau) || double.IsInfinity(adjustedTau) || adjustedTau <= 0)
                {
                    MLLogManager.Instance?.Log($"CalculateAdjustedTau (stage-aware): Invalid base tau calculation, using fallback", LogLevel.Error);
                    adjustedTau = BASE_TAU_DAYS;
                }

                // NEW: Graduele MASTERED modifier op basis van stage
                double difficultyModifier;
                if (difficulty?.ToLower() == "mastered")
                {
                    if (practiceScheduleStage <= 3)
                    {
                        difficultyModifier = DifficultyModifiers.MASTERED_STAGE_3; // 2.0 - Eerste mastered
                        MLLogManager.Instance?.Log($"CalculateAdjustedTau (stage-aware): Stage {practiceScheduleStage} - Using conservative MASTERED modifier {difficultyModifier} (7-10 dagen)", LogLevel.Info);
                    }
                    else if (practiceScheduleStage == 4)
                    {
                        difficultyModifier = DifficultyModifiers.MASTERED_STAGE_4; // 2.5 - Tweede perfect
                        MLLogManager.Instance?.Log($"CalculateAdjustedTau (stage-aware): Stage {practiceScheduleStage} - Using moderate MASTERED modifier {difficultyModifier} (14-21 dagen)", LogLevel.Info);
                    }
                    else // stage >= 5
                    {
                        difficultyModifier = DifficultyModifiers.MASTERED_STAGE_5_PLUS; // 3.5 - Volledig geconsolideerd
                        MLLogManager.Instance?.Log($"CalculateAdjustedTau (stage-aware): Stage {practiceScheduleStage} - Using full MASTERED modifier {difficultyModifier} (30-60+ dagen)", LogLevel.Info);
                    }
                }
                else
                {
                    difficultyModifier = GetSafeDifficultyModifier(difficulty ?? "Average");
                }

                adjustedTau *= difficultyModifier;
                if (double.IsNaN(adjustedTau) || double.IsInfinity(adjustedTau) || adjustedTau <= 0)
                {
                    MLLogManager.Instance?.Log($"CalculateAdjustedTau (stage-aware): Invalid tau after difficulty adjustment, using fallback", LogLevel.Error);
                    adjustedTau = BASE_TAU_DAYS;
                }

                if (repetitionCount > 0)
                {
                    double multiplier = CalculateEnhancedRepetitionBonus(repetitionCount, difficulty ?? "Average");
                    adjustedTau *= multiplier;
                    MLLogManager.Instance?.Log($"CalculateAdjustedTau (stage-aware): Applied enhanced repetition bonus - reps: {repetitionCount}, difficulty: {difficulty}, multiplier: {multiplier:F3}", LogLevel.Debug);
                }

                adjustedTau = ClampTauToSafeBounds(adjustedTau);
                if (double.IsNaN(adjustedTau) || double.IsInfinity(adjustedTau) || adjustedTau <= 0)
                {
                    MLLogManager.Instance?.Log($"CalculateAdjustedTau (stage-aware): Final validation failed, using safe fallback", LogLevel.Error);
                    return BASE_TAU_DAYS;
                }

                MLLogManager.Instance?.Log($"CalculateAdjustedTau (stage-aware): stage={practiceScheduleStage}, difficulty='{difficulty}', reps={repetitionCount} -> τ={adjustedTau:F3}d", LogLevel.Info);
                return adjustedTau;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError($"CalculateAdjustedTau (stage-aware): Critical error with difficulty='{difficulty}', repetitions={repetitionCount}, stage={practiceScheduleStage}", ex);
                return BASE_TAU_DAYS;
            }
        }

        /// <summary>
        /// Overloaded versie van CalculateAdjustedTau met demografische personalisatie (v3.0).
        /// Gebruikt leeftijd en ervaring voor gepersonaliseerde tau berekening.
        /// Gender removed in v3.0 - effect size too small and scientifically unsound for hybrid skills.
        /// </summary>
        public static double CalculateAdjustedTau(string difficulty, int repetitionCount, int age, string experience)
        {
            if (repetitionCount < 0)
            {
                MLLogManager.Instance?.Log($"CalculateAdjustedTau (personalized): Negative repetition count ({repetitionCount}) corrected to 0", LogLevel.Warning);
                repetitionCount = 0;
            }
            const int MAX_SAFE_REPETITIONS = 1000;
            if (repetitionCount > MAX_SAFE_REPETITIONS)
            {
                MLLogManager.Instance?.Log($"CalculateAdjustedTau (personalized): Extremely high repetition count ({repetitionCount}) clamped to {MAX_SAFE_REPETITIONS}", LogLevel.Debug);
                repetitionCount = MAX_SAFE_REPETITIONS;
            }
            try
            {
                // Stap 1: Start met gepersonaliseerde base tau in plaats van generieke BASE_TAU_DAYS
                double personalizedBaseTau = GetPersonalizedBaseTau(age, experience);
                double adjustedTau = personalizedBaseTau * MUSIC_MATERIAL_FACTOR;

                if (double.IsNaN(adjustedTau) || double.IsInfinity(adjustedTau) || adjustedTau <= 0)
                {
                    try { MLLogManager.Instance?.Log($"CalculateAdjustedTau (personalized): Invalid base tau calculation, using fallback", LogLevel.Error); } catch { }
                    adjustedTau = personalizedBaseTau;
                }

                // Stap 2: Difficulty aanpassing (zoals voorheen)
                double difficultyModifier = GetSafeDifficultyModifier(difficulty);
                adjustedTau *= difficultyModifier;

                if (double.IsNaN(adjustedTau) || double.IsInfinity(adjustedTau) || adjustedTau <= 0)
                {
                    MLLogManager.Instance?.Log($"CalculateAdjustedTau (personalized): Invalid tau after difficulty adjustment, using fallback", LogLevel.Error);
                    adjustedTau = personalizedBaseTau;
                }

                // Stap 3: Repetition bonus (zoals voorheen)
                if (repetitionCount > 0)
                {
                    double effectiveRepetitions = CalculateSafeEffectiveRepetitions(repetitionCount);
                    double repetitionBonus = CalculateSafeRepetitionBonus(effectiveRepetitions);
                    double multiplier = 1.0 + Math.Min(0.5, repetitionBonus * 0.08); // Max 50% bonus
                    adjustedTau *= multiplier;
                    try { MLLogManager.Instance?.Log($"CalculateAdjustedTau (personalized): Applied repetition bonus - reps: {repetitionCount}, effective: {effectiveRepetitions:F2}, bonus: {repetitionBonus:F3}, multiplier: {multiplier:F3}", LogLevel.Debug); } catch { }
                }

                // Stap 4: Final clamping
                adjustedTau = ClampTauToSafeBounds(adjustedTau);

                if (double.IsNaN(adjustedTau) || double.IsInfinity(adjustedTau) || adjustedTau <= 0)
                {
                    MLLogManager.Instance?.Log($"CalculateAdjustedTau (personalized): Final validation failed, using safe fallback", LogLevel.Error);
                    return personalizedBaseTau;
                }

                try { MLLogManager.Instance?.Log($"CalculateAdjustedTau (personalized): exp='{experience}', difficulty='{difficulty}', reps={repetitionCount} -> τ={adjustedTau:F3}d (vs baseline {BASE_TAU_DAYS:F3}d)", LogLevel.Info); } catch { }

                return adjustedTau;
            }
            catch (Exception ex)
            {
                try { MLLogManager.Instance?.LogError($"CalculateAdjustedTau (personalized): Critical error with difficulty='{difficulty}', repetitions={repetitionCount}, age={age}, experience='{experience}'", ex); } catch { }
                return GetPersonalizedBaseTau(age, experience); // Fallback naar gepersonaliseerde baseline
            }
        }

        /// <summary>
        /// Originele CalculateAdjustedTau methode (backward compatibility).
        /// Voor nieuwe implementaties, gebruik de overloaded versie met demografische parameters.
        /// </summary>
        public static double CalculateAdjustedTau(string difficulty, int repetitionCount)
        {
            // Probeer demografische personalisatie te gebruiken indien beschikbaar
            try
            {
                var settings = SettingsManager.Instance?.CurrentSettings;
                if (settings != null)
                {
                    // Leeftijd wordt niet langer gebruikt; personalisatie gebeurt op basis van ervaring
                    MLLogManager.Instance?.Log($"CalculateAdjustedTau: Using personalized version (experience='{settings.MusicalExperience}')", LogLevel.Debug);
                    return CalculateAdjustedTau(difficulty, repetitionCount, settings.Age, settings.MusicalExperience);
                }
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError($"CalculateAdjustedTau: Error accessing user settings for personalization, falling back to standard calculation", ex);
            }

            // Fallback naar originele implementatie
            MLLogManager.Instance?.Log($"CalculateAdjustedTau: Using standard (non-personalized) calculation", LogLevel.Debug);

            if (repetitionCount < 0)
            {
                MLLogManager.Instance?.Log($"CalculateAdjustedTau: Negative repetition count ({repetitionCount}) corrected to 0", LogLevel.Warning);
                repetitionCount = 0;
            }
            const int MAX_SAFE_REPETITIONS = 1000;
            if (repetitionCount > MAX_SAFE_REPETITIONS)
            {
                MLLogManager.Instance?.Log($"CalculateAdjustedTau: Extremely high repetition count ({repetitionCount}) clamped to {MAX_SAFE_REPETITIONS}", LogLevel.Debug);
                repetitionCount = MAX_SAFE_REPETITIONS;
            }
            try
            {
                double adjustedTau = BASE_TAU_DAYS * MUSIC_MATERIAL_FACTOR;
                if (double.IsNaN(adjustedTau) || double.IsInfinity(adjustedTau) || adjustedTau <= 0)
                {
                    MLLogManager.Instance?.Log($"CalculateAdjustedTau: Invalid base tau calculation, using fallback", LogLevel.Error);
                    adjustedTau = BASE_TAU_DAYS;
                }
                double difficultyModifier = GetSafeDifficultyModifier(difficulty);
                adjustedTau *= difficultyModifier;
                if (double.IsNaN(adjustedTau) || double.IsInfinity(adjustedTau) || adjustedTau <= 0)
                {
                    MLLogManager.Instance?.Log($"CalculateAdjustedTau: Invalid tau after difficulty adjustment, using fallback", LogLevel.Error);
                    adjustedTau = BASE_TAU_DAYS;
                }
                if (repetitionCount > 0)
                {
                    // Use the enhanced repetition bonus calculation
                    double multiplier = CalculateEnhancedRepetitionBonus(repetitionCount, difficulty);
                    adjustedTau *= multiplier;
                    MLLogManager.Instance?.Log($"CalculateAdjustedTau: Applied enhanced repetition bonus - reps: {repetitionCount}, difficulty: {difficulty}, multiplier: {multiplier:F3}", LogLevel.Debug);
                }
                adjustedTau = ClampTauToSafeBounds(adjustedTau); // τ-policy enforced [1,180]
                if (double.IsNaN(adjustedTau) || double.IsInfinity(adjustedTau) || adjustedTau <= 0)
                {
                    MLLogManager.Instance?.Log($"CalculateAdjustedTau: Final validation failed, using safe fallback", LogLevel.Error);
                    return BASE_TAU_DAYS;
                }
                return adjustedTau;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError($"CalculateAdjustedTau: Critical error with difficulty='{difficulty}', repetitions={repetitionCount}", ex);
                return BASE_TAU_DAYS; // Fallback
            }
        }

        private static double GetSafeDifficultyModifier(string difficulty)
        {
            try
            {
                double modifier = difficulty?.ToLower() switch
                {
                    "difficult" => DifficultyModifiers.DIFFICULT,
                    "easy" => DifficultyModifiers.EASY,
                    "mastered" => DifficultyModifiers.MASTERED,
                    _ => DifficultyModifiers.AVERAGE
                };
                if (double.IsNaN(modifier) || double.IsInfinity(modifier) || modifier <= 0)
                {
                    MLLogManager.Instance?.Log($"GetSafeDifficultyModifier: Invalid modifier for '{difficulty}', using default", LogLevel.Warning);
                    return DifficultyModifiers.AVERAGE;
                }
                return Math.Max(0.1, Math.Min(10.0, modifier));
            }
            catch
            {
                return DifficultyModifiers.AVERAGE;
            }
        }

        private static double CalculateSafeEffectiveRepetitions(int repetitionCount)
        {
            try
            {
                if (repetitionCount <= 0) return 0.0;
                double logInput = Math.Max(1.0, repetitionCount + 1.0);
                double logResult = Math.Log(logInput) / Math.Log(2.0);
                if (double.IsNaN(logResult) || double.IsInfinity(logResult))
                {
                    MLLogManager.Instance?.Log($"CalculateSafeEffectiveRepetitions: Invalid log calculation for {repetitionCount}, using linear approximation", LogLevel.Warning);
                    return Math.Min(10.0, repetitionCount * 0.1);
                }
                return Math.Min(20.0, Math.Max(0.0, logResult));
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError($"CalculateSafeEffectiveRepetitions: Error with repetitions={repetitionCount}", ex);
                return Math.Min(5.0, repetitionCount * 0.05);
            }
        }

        private static double CalculateSafeRepetitionBonus(double effectiveRepetitions)
        {
            try
            {
                if (effectiveRepetitions <= 0) return 0.0;
                double exponent = Math.Min(2.0, Math.Max(0.0, 0.5));
                double powerResult = Math.Pow(effectiveRepetitions, exponent);
                if (double.IsNaN(powerResult) || double.IsInfinity(powerResult))
                {
                    MLLogManager.Instance?.Log($"CalculateSafeRepetitionBonus: Invalid power calculation, using linear approximation", LogLevel.Warning);
                    return Math.Min(5.0, effectiveRepetitions * 0.5);
                }
                double bonusBase = Math.Min(10.0, Math.Max(0.0, powerResult));
                double bonus = bonusBase * REPETITION_STRENGTH_FACTOR;
                return Math.Min(6.0, Math.Max(0.0, bonus));
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError($"CalculateSafeRepetitionBonus: Error with effectiveRepetitions={effectiveRepetitions}", ex);
                return Math.Min(2.0, effectiveRepetitions * 0.3);
            }
        }

        /// <summary>
        /// Clamps tau to the normative range [1, 180] days. Do not apply local caps. Always use this helper.
        /// </summary>
        public static double ClampTauToSafeBounds(double tau)
        {
            const double ABSOLUTE_MIN_TAU = 1.0; // τ-policy lower bound [1, 180]
            const double ABSOLUTE_MAX_TAU = 180.0; // τ-policy upper bound [1, 180]
            if (double.IsNaN(tau) || double.IsInfinity(tau))
            {
                MLLogManager.Instance?.Log($"ClampTauToSafeBounds: Invalid tau value, using default", LogLevel.Warning);
                return BASE_TAU_DAYS;
            }
            double clampedTau = Math.Max(ABSOLUTE_MIN_TAU, Math.Min(ABSOLUTE_MAX_TAU, tau));
            if (Math.Abs(clampedTau - tau) > 0.001)
            {
                MLLogManager.Instance?.Log($"ClampTauToSafeBounds: Tau clamped from {tau:F3} to {clampedTau:F3}", LogLevel.Debug);
            }
            return clampedTau;
        }

        public static double CalculateRetention(double daysSincePractice, double tau)
        {
            return CalculateRetention(daysSincePractice, tau, 0, "average", "average");
        }

        /// <summary>
        /// Enhanced retention calculation with motor skill plateau and individual variability
        /// </summary>
        public static double CalculateRetention(double daysSincePractice, double tau, int repetitionCount, string difficulty, string experience)
        {
            try
            {
                if (double.IsNaN(daysSincePractice) || double.IsInfinity(daysSincePractice))
                {
                    MLLogManager.Instance?.Log($"CalculateRetention: Invalid daysSincePractice, using 0", LogLevel.Warning);
                    daysSincePractice = 0;
                }
                if (double.IsNaN(tau) || double.IsInfinity(tau) || tau <= 0)
                {
                    MLLogManager.Instance?.Log($"CalculateRetention: Invalid tau value ({tau}), using default", LogLevel.Warning);
                    tau = BASE_TAU_DAYS;
                }
                daysSincePractice = Math.Max(0, Math.Min(1000, daysSincePractice));
                tau = ClampTauToSafeBounds(tau);

                // IMPROVEMENT 1: Motor Skills Plateau Phase
                // Research shows motor skills have a consolidation period in the first 6-12 hours
                double adjustedTime = ApplyMotorSkillsPlateau(daysSincePractice);

                // IMPROVEMENT 2: Enhanced Repetition Effects
                // More realistic repetition bonus based on spacing effect research
                double repetitionMultiplier = CalculateEnhancedRepetitionBonus(repetitionCount, difficulty);

                // IMPROVEMENT 3: Individual Variability
                // Adjust based on user experience and learning characteristics
                double individualityFactor = CalculateIndividualVariabilityFactor(experience, difficulty);

                // Apply adjustments to tau
                double adjustedTau = tau * repetitionMultiplier * individualityFactor;

                double exponent = -adjustedTime / adjustedTau;
                const double MIN_SAFE_EXPONENT = -50.0;
                if (exponent < MIN_SAFE_EXPONENT)
                {
                    MLLogManager.Instance?.Log($"CalculateRetention: Extreme negative exponent ({exponent:F2}), using asymptotic baseline", LogLevel.Debug);
                    return ASYMPTOTIC_RETENTION_BASELINE;
                }

                double expResult;
                try
                {
                    expResult = Math.Exp(exponent);
                    if (double.IsNaN(expResult) || double.IsInfinity(expResult))
                    {
                        MLLogManager.Instance?.Log($"CalculateRetention: Invalid exponential result, using safe approximation", LogLevel.Warning);
                        expResult = exponent < -10 ? 0.0 : Math.Max(0.0, 1.0 + exponent);
                    }
                }
                catch (OverflowException)
                {
                    MLLogManager.Instance?.Log($"CalculateRetention: Exponential overflow, using safe approximation", LogLevel.Warning);
                    expResult = exponent > 0 ? 1.0 : 0.0;
                }

                double retention = (INITIAL_LEARNING_STRENGTH * expResult) + ASYMPTOTIC_RETENTION_BASELINE;
                if (double.IsNaN(retention) || double.IsInfinity(retention))
                {
                    MLLogManager.Instance?.Log($"CalculateRetention: Invalid retention calculation, using fallback", LogLevel.Error);
                    retention = Math.Max(ASYMPTOTIC_RETENTION_BASELINE, INITIAL_LEARNING_STRENGTH * Math.Max(0, 1.0 - (adjustedTime / (adjustedTau * 2))));
                }

                retention = Math.Max(0.0, Math.Min(1.0, retention));
                if (daysSincePractice == 0 && retention < INITIAL_LEARNING_STRENGTH * 0.9)
                {
                    MLLogManager.Instance?.Log($"CalculateRetention: Inconsistent retention for day 0, correcting", LogLevel.Warning);
                    retention = INITIAL_LEARNING_STRENGTH + ASYMPTOTIC_RETENTION_BASELINE;
                }

                return retention;
            }
            catch (Exception ex)
            {
                MLLogManager.Instance?.LogError($"CalculateRetention: Critical error with days={daysSincePractice}, tau={tau}", ex);
                if (daysSincePractice <= 0) return INITIAL_LEARNING_STRENGTH + ASYMPTOTIC_RETENTION_BASELINE;
                if (daysSincePractice >= 365) return ASYMPTOTIC_RETENTION_BASELINE;
                double t = Math.Max(0, Math.Min(1, daysSincePractice / 365.0));
                return (INITIAL_LEARNING_STRENGTH + ASYMPTOTIC_RETENTION_BASELINE) * (1 - t) + ASYMPTOTIC_RETENTION_BASELINE * t;
            }
        }

        /// <summary>
        /// Applies motor skills plateau effect - slower initial decline for first 6-12 hours
        /// </summary>
        private static double ApplyMotorSkillsPlateau(double daysSincePractice)
        {
            const double PLATEAU_DURATION = 0.4; // 0.4 days = ~9.6 hours
            const double PLATEAU_STRENGTH = 0.6; // Reduce effective time by 40% during plateau

            if (daysSincePractice <= PLATEAU_DURATION)
            {
                // Gradual transition from full plateau to normal decay
                double plateauFactor = 1.0 - (PLATEAU_STRENGTH * (1.0 - daysSincePractice / PLATEAU_DURATION));
                return daysSincePractice * plateauFactor;
            }
            return daysSincePractice;
        }

        /// <summary>
        /// Enhanced repetition bonus based on empirical research on spaced repetition
        /// </summary>
        private static double CalculateEnhancedRepetitionBonus(int repetitionCount, string difficulty)
        {
            if (repetitionCount <= 0) return 1.0;

            // Research-based: logarithmic scaling with diminishing returns
            double baseMultiplier = 1.0 + Math.Log(1.0 + repetitionCount) * 0.15; // Max ~1.6x for 10 reps

            // Difficulty adjustment - harder material benefits more from repetition
            double difficultyBonus = difficulty?.ToLower() switch
            {
                "difficult" => 1.3, // 30% more benefit from repetition
                "mastered" => 0.7,  // Less benefit as already well-learned
                "easy" => 0.9,      // Slightly less benefit
                _ => 1.0            // Average difficulty
            };

            return Math.Min(2.0, baseMultiplier * difficultyBonus); // Cap at 2.0x
        }

        /// <summary>
        /// Individual variability based on user characteristics
        /// </summary>
        private static double CalculateIndividualVariabilityFactor(string experience, string difficulty)
        {
            double experienceFactor = experience?.ToLower() switch
            {
                "beginner" => 0.8,      // Faster forgetting
                "intermediate" => 1.0,   // Average
                "advanced" => 1.2,       // Slower forgetting
                "expert" => 1.4,         // Much slower forgetting
                _ => 1.0                 // Default
            };

            // Age factor could be added here based on user settings
            // More experienced musicians have better memory consolidation
            try
            {
                var settings = SettingsManager.Instance.CurrentSettings;
                double ageFactor = 1.0;
                if (settings != null && settings.Age > 0)
                {
                    // Slight memory decline with age, but experience compensates
                    ageFactor = Math.Max(0.85, 1.1 - (settings.Age - 20) * 0.005);
                }
                experienceFactor *= ageFactor;
            }
            catch
            {
                // Ignore age factor if settings unavailable
            }

            return Math.Max(0.6, Math.Min(1.8, experienceFactor)); // Reasonable bounds
        }

        public static bool IsOptimalInterval(double predictedRetention, double targetQuality = -1)
        {
            if (targetQuality < 0) targetQuality = RETENTION_THRESHOLD;
            return predictedRetention >= targetQuality && predictedRetention <= OPTIMAL_RETENTION_THRESHOLD;
        }

        /// <summary>
        /// Clamps an interval to the normative range [1, 365] days and ensures it is <= 5 * tau.
        /// Do not apply local caps. Always use this helper.
        /// Returns (clampedDays, reason). Reasons: none, invalid→min, min_consolidation, safety_max_365, cap_5x_tau.
        /// </summary>
        public static (double ClampedIntervalDays, string Reason) ClampIntervalToScientificBounds(
            double intervalDays,
            double? tau = null,
            double? stability = null)
        {
            double minDays = MIN_INTERVAL_DAYS; // Beleidsminimum: 1 dag.
            const int SAFETY_MAX_INTERVAL_DAYS = 365; // Beleidsmaximum: 1 jaar.
            string reason = "none";
            double original = intervalDays;
            if (double.IsNaN(intervalDays) || double.IsInfinity(intervalDays) || intervalDays <= 0.0)
            {
                intervalDays = minDays; reason = "invalid→min";
            }
            if (intervalDays < minDays)
            {
                intervalDays = minDays; reason = "min_consolidation";
            }
            if (intervalDays > SAFETY_MAX_INTERVAL_DAYS)
            {
                intervalDays = SAFETY_MAX_INTERVAL_DAYS; reason = "safety_max_365";
            }
            if (tau.HasValue)
            {
                var tauCap = tau.Value * 5.0; // Interval-policy: extra cap t ≤ 5×τ.
                if (intervalDays > tauCap)
                {
                    intervalDays = tauCap;
                    reason = reason == "none" ? "cap_5x_tau" : reason + "+cap_5x_tau";
                }
            }
            string tauText = tau.HasValue ? tau.Value.ToString("F3") : "n/a";
            string sText = stability.HasValue ? stability.Value.ToString("F3") : "n/a";
            var level = reason == "none" ? LogLevel.Debug : LogLevel.Warning;
            MLLogManager.Instance?.Log($"[Clamp] interval {original:F2}d -> {intervalDays:F2}d reason={reason} (τ={tauText}, S={sText})", level);
            return (intervalDays, reason);
        }

        /// <summary>
        /// Geeft het retentiedoel (R*) terug voor een gegeven difficulty.
        /// Input (case-insensitive): difficult|hard|challenging → 0.85; easy|simple → 0.70; mastered|review|maintain → 0.65; anders/leeg → 0.80.
        /// R*-tabel: Difficult 0.85 | Average 0.80 | Easy 0.70 | Mastered 0.65.
        /// Do not apply local caps. Always use this helper.
        /// UPDATED: Now uses Settings values if available.
        /// </summary>
        public static double GetRetentionTargetForDifficulty(string? difficulty)
        {
            var settings = SettingsManager.Instance?.CurrentSettings;

            if (string.IsNullOrWhiteSpace(difficulty))
                return settings?.AverageRetentionTarget ?? RETENTION_THRESHOLD; // Default 0.80

            var d = difficulty.Trim().ToLowerInvariant();
            if (d is "difficult" or "hard" or "challenging")
                return settings?.DifficultRetentionTarget ?? OPTIMAL_RETENTION_THRESHOLD; // 0.85
            if (d is "easy" or "simple")
                return settings?.EasyRetentionTarget ?? EASY_RETENTION_THRESHOLD; // 0.70
            if (d is "mastered" or "review" or "maintain")
                return settings?.MasteredRetentionTarget ?? MASTERED_RETENTION_THRESHOLD; // 0.65
            return settings?.AverageRetentionTarget ?? RETENTION_THRESHOLD; // 0.80
        }

        /// <summary>
        /// Applies global interval multiplier from settings.
        /// </summary>
        public static double ApplyGlobalMultiplier(double interval)
        {
            var settings = SettingsManager.Instance?.CurrentSettings;
            return interval * (settings?.GlobalIntervalMultiplier ?? 1.0);
        }

        /// <summary>
        /// Gets experience-based tau multiplier from settings.
        /// </summary>
        public static double GetExperienceTauMultiplierFromSettings(string experience)
        {
            var settings = SettingsManager.Instance?.CurrentSettings;
            if (settings == null) return 1.0;

            return experience?.ToLower().Trim() switch
            {
                "beginner" => settings.BeginnerTauMultiplier,
                "intermediate" => settings.IntermediateTauMultiplier,
                "advanced" => settings.AdvancedTauMultiplier,
                "professional" => settings.AdvancedTauMultiplier, // Use advanced for professional
                _ => settings.IntermediateTauMultiplier // Default to intermediate
            };
        }

        /// <summary>
        /// Gets performance penalty threshold from settings.
        /// </summary>
        public static double GetPerformancePenaltyThreshold()
        {
            var settings = SettingsManager.Instance?.CurrentSettings;
            return settings?.PerformancePenaltyThreshold ?? 5.0;
        }

        /// <summary>
        /// Gets frustration cooldown days from settings.
        /// </summary>
        public static double GetFrustrationCooldownDays(bool isManual = false)
        {
            var settings = SettingsManager.Instance?.CurrentSettings;
            if (settings == null) return isManual ? 2.0 : 3.0;

            return isManual ? settings.ManualFrustrationCooldownDays : settings.FrustrationCooldownDays;
        }

        #endregion
    }
}
