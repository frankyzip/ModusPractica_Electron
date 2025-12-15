// ============================================================================
// Ebbinghaus Memory Engine - Core Algorithm Implementation
// Port from C# Desktop Application to JavaScript
// Copyright © 2025 Frank De Baere - All Rights Reserved
// ============================================================================

/**
 * EbbinghausConstants - Normative Ebbinghaus policy (single source of truth)
 * 
 * Retention targets (R*):
 * - Difficult: 0.85
 * - Default: 0.80
 * - Easy: 0.70
 * - Mastered: 0.65
 * 
 * Tau clamp: τ ∈ [1, 180] days
 * Interval clamp: [1, 365] days AND t ≤ 5×τ
 * 
 * All clamping happens centrally here; no local caps elsewhere.
 */
class EbbinghausConstants {
    // Ebbinghaus Forgetting Curve Parameters
    static BASE_TAU_HOURS = 72.0;
    static TAU_STANDARD_DEVIATION = 24.0;
    static BASE_TAU_DAYS = this.BASE_TAU_HOURS / 24.0; // 3.0 days
    static MIN_TAU_DAYS = this.BASE_TAU_DAYS / 5.0; // 0.6 dagen
    static MAX_TAU_DAYS = this.BASE_TAU_DAYS * 5.0; // 15 dagen

    // Retention Parameters
    static INITIAL_LEARNING_STRENGTH = 0.80; // 80%
    static ASYMPTOTIC_RETENTION_BASELINE = 0.15; // 15%
    static RETENTION_THRESHOLD = 0.80; // Default R*
    static EASY_RETENTION_THRESHOLD = 0.70; // Easy R*
    static MASTERED_RETENTION_THRESHOLD = 0.65; // Mastered R*
    static MIN_RETENTION_THRESHOLD = 0.50; // 50%
    static OPTIMAL_RETENTION_THRESHOLD = 0.85; // Difficult R*

    // Material-specific Adjustments
    static MUSIC_MATERIAL_FACTOR = 3.0;
    static MOTOR_SKILL_FACTOR = 2.5;
    static REPETITION_STRENGTH_FACTOR = 1.3;

    // Difficulty Modifiers
    static DIFFICULTY_MODIFIERS = {
        DIFFICULT: 0.6, // 40% reductie
        AVERAGE: 1.0, // Geen aanpassing
        EASY: 1.7, // 70% verhoging
        MASTERED: 3.5, // 250% verhoging (alleen voor stage 5+)
        
        // Graduele MASTERED groei
        MASTERED_STAGE_3: 2.0,  // 100% (eerste mastered: 7-10 dagen)
        MASTERED_STAGE_4: 2.5,  // 150% (tweede perfect: 14-21 dagen)
        MASTERED_STAGE_5_PLUS: 3.5 // 250% (derde+ perfect: 30-60+ dagen)
    };

    // Interval Calculation Parameters
    static BASE_EXPANSION_FACTOR = 2.0;
    static MIN_INTERVAL_DAYS = 1; // Beleidsminimum: 1 dag voor consolidatie
    static MAX_INTERVAL_DAYS = 365; // Beleidsmaximum: 1 jaar als veiligheidsplafond
    static PERFORMANCE_ADJUSTMENT_FACTOR = 0.3;

    // Individual Calibration Parameters
    static MIN_SESSIONS_FOR_CALIBRATION = 10;
    static BAYESIAN_LEARNING_RATE = 0.1;
    static CONFIDENCE_INTERVAL = 0.95;

    /**
     * Gets age-adjusted base tau based on scientific research
     * Note: In current version, age adjustment is removed - returns baseline for all ages
     */
    static getAgeAdjustedBaseTau(age) {
        // Leeftijdsaanpassing verwijderd - leeftijd maakt niet veel verschil voor forgetting curves
        return this.BASE_TAU_DAYS;
    }

    /**
     * Gets experience-adjusted tau multiplier
     * Based on encoding strength hypothesis (Bjork & Bjork, 2011)
     * 
     * Stronger initial encoding → slower decay → longer optimal τ → longer intervals
     */
    static getExperienceAdjustedTau(baseTau, experience) {
        if (!experience || experience.trim() === '') return baseTau;

        const exp = experience.toLowerCase().trim();
        const multipliers = {
            'beginner': 0.8,      // Kortere intervallen (zwakke encoding, sneller vergeten)
            'intermediate': 1.0,   // Baseline (3.0 dagen)
            'advanced': 1.1,       // Langere intervallen (sterke encoding, langzamer vergeten)
            'professional': 1.3    // Langste intervallen (chunking expertise + desirable difficulty)
        };

        const multiplier = multipliers[exp] || 1.0;
        return baseTau * multiplier;
    }

    /**
     * Central method for demographic personalization of BASE_TAU_DAYS
     * Combines age and experience with conservative adjustments
     */
    static getPersonalizedBaseTau(age, experience) {
        try {
            // Step 1: Age-based base tau
            let baseTau = this.getAgeAdjustedBaseTau(age);

            // Step 2: Experience adjustment
            baseTau = this.getExperienceAdjustedTau(baseTau, experience);

            // Safety check and clamping
            baseTau = this.clampTauToSafeBounds(baseTau);

            console.log(`[Ebbinghaus] PersonalizedBaseTau: exp='${experience}' -> τ=${baseTau.toFixed(3)}d`);
            if (typeof window !== 'undefined' && window.MPLog) window.MPLog.debug('Ebbinghaus PersonalizedBaseTau', { profileExperience: experience, tau: baseTau });
            return baseTau;
        } catch (error) {
            console.error(`[Ebbinghaus] GetPersonalizedBaseTau error:`, error);
            return this.BASE_TAU_DAYS;
        }
    }

    /**
     * Calculates adjusted tau with stage-aware MASTERED growth
     * Implements scientifically founded gradual expansion for mastered sections
     */
    static calculateAdjustedTau(difficulty, repetitionCount, practiceScheduleStage = null) {
        if (repetitionCount < 0) {
            console.warn(`[Ebbinghaus] Negative repetition count (${repetitionCount}) corrected to 0`);
            repetitionCount = 0;
        }

        const MAX_SAFE_REPETITIONS = 1000;
        if (repetitionCount > MAX_SAFE_REPETITIONS) {
            console.log(`[Ebbinghaus] High repetition count (${repetitionCount}) clamped to ${MAX_SAFE_REPETITIONS}`);
            repetitionCount = MAX_SAFE_REPETITIONS;
        }

        try {
            let adjustedTau = this.BASE_TAU_DAYS * this.MUSIC_MATERIAL_FACTOR;
            
            if (!isFinite(adjustedTau) || adjustedTau <= 0) {
                console.error(`[Ebbinghaus] Invalid base tau calculation, using fallback`);
                adjustedTau = this.BASE_TAU_DAYS;
            }

            // Stage-aware MASTERED modifier
            let difficultyModifier;
            if (difficulty && difficulty.toLowerCase() === 'mastered' && practiceScheduleStage !== null) {
                if (practiceScheduleStage <= 3) {
                    difficultyModifier = this.DIFFICULTY_MODIFIERS.MASTERED_STAGE_3;
                    console.log(`[Ebbinghaus] Stage ${practiceScheduleStage} - Conservative MASTERED modifier ${difficultyModifier} (7-10 dagen)`);
                } else if (practiceScheduleStage === 4) {
                    difficultyModifier = this.DIFFICULTY_MODIFIERS.MASTERED_STAGE_4;
                    console.log(`[Ebbinghaus] Stage ${practiceScheduleStage} - Moderate MASTERED modifier ${difficultyModifier} (14-21 dagen)`);
                } else {
                    difficultyModifier = this.DIFFICULTY_MODIFIERS.MASTERED_STAGE_5_PLUS;
                    console.log(`[Ebbinghaus] Stage ${practiceScheduleStage} - Full MASTERED modifier ${difficultyModifier} (30-60+ dagen)`);
                }
            } else {
                difficultyModifier = this.getSafeDifficultyModifier(difficulty || 'Average');
            }

            adjustedTau *= difficultyModifier;

            if (!isFinite(adjustedTau) || adjustedTau <= 0) {
                console.error(`[Ebbinghaus] Invalid tau after difficulty adjustment, using fallback`);
                adjustedTau = this.BASE_TAU_DAYS;
            }

            // Apply repetition bonus
            if (repetitionCount > 0) {
                const multiplier = this.calculateEnhancedRepetitionBonus(repetitionCount, difficulty || 'Average');
                adjustedTau *= multiplier;
                console.log(`[Ebbinghaus] Applied repetition bonus - reps: ${repetitionCount}, multiplier: ${multiplier.toFixed(3)}`);
            }

            adjustedTau = this.clampTauToSafeBounds(adjustedTau);

            if (!isFinite(adjustedTau) || adjustedTau <= 0) {
                console.error(`[Ebbinghaus] Final validation failed, using safe fallback`);
                return this.BASE_TAU_DAYS;
            }

            console.log(`[Ebbinghaus] CalculateAdjustedTau: difficulty='${difficulty}', reps=${repetitionCount}, stage=${practiceScheduleStage} -> τ=${adjustedTau.toFixed(3)}d`);
            if (typeof window !== 'undefined' && window.MPLog) window.MPLog.debug('Ebbinghaus CalculateAdjustedTau', { difficulty, repetitionCount, practiceScheduleStage, adjustedTau });
            return adjustedTau;
        } catch (error) {
            console.error(`[Ebbinghaus] CalculateAdjustedTau error:`, error);
            return this.BASE_TAU_DAYS;
        }
    }

    /**
     * Overloaded version with demographic personalization
     */
    static calculateAdjustedTauPersonalized(difficulty, repetitionCount, age, experience) {
        if (repetitionCount < 0) {
            console.warn(`[Ebbinghaus] Negative repetition count (${repetitionCount}) corrected to 0`);
            repetitionCount = 0;
        }

        const MAX_SAFE_REPETITIONS = 1000;
        if (repetitionCount > MAX_SAFE_REPETITIONS) {
            console.log(`[Ebbinghaus] High repetition count (${repetitionCount}) clamped to ${MAX_SAFE_REPETITIONS}`);
            repetitionCount = MAX_SAFE_REPETITIONS;
        }

        try {
            // Step 1: Start with personalized base tau
            const personalizedBaseTau = this.getPersonalizedBaseTau(age, experience);
            let adjustedTau = personalizedBaseTau * this.MUSIC_MATERIAL_FACTOR;

            if (!isFinite(adjustedTau) || adjustedTau <= 0) {
                console.error(`[Ebbinghaus] Invalid base tau calculation, using fallback`);
                adjustedTau = personalizedBaseTau;
            }

            // Step 2: Difficulty adjustment
            const difficultyModifier = this.getSafeDifficultyModifier(difficulty);
            adjustedTau *= difficultyModifier;

            if (!isFinite(adjustedTau) || adjustedTau <= 0) {
                console.error(`[Ebbinghaus] Invalid tau after difficulty adjustment, using fallback`);
                adjustedTau = personalizedBaseTau;
            }

            // Step 3: Repetition bonus
            if (repetitionCount > 0) {
                const effectiveRepetitions = this.calculateSafeEffectiveRepetitions(repetitionCount);
                const repetitionBonus = this.calculateSafeRepetitionBonus(effectiveRepetitions);
                const multiplier = 1.0 + Math.min(0.5, repetitionBonus * 0.08);
                adjustedTau *= multiplier;
                console.log(`[Ebbinghaus] Applied repetition bonus - reps: ${repetitionCount}, effective: ${effectiveRepetitions.toFixed(2)}, multiplier: ${multiplier.toFixed(3)}`);
            }

            // Step 4: Final clamping
            adjustedTau = this.clampTauToSafeBounds(adjustedTau);

            if (!isFinite(adjustedTau) || adjustedTau <= 0) {
                console.error(`[Ebbinghaus] Final validation failed, using safe fallback`);
                return personalizedBaseTau;
            }

            console.log(`[Ebbinghaus] CalculateAdjustedTau (personalized): exp='${experience}', difficulty='${difficulty}', reps=${repetitionCount} -> τ=${adjustedTau.toFixed(3)}d`);
            if (typeof window !== 'undefined' && window.MPLog) window.MPLog.debug('Ebbinghaus CalculateAdjustedTau (personalized)', { experience, difficulty, repetitionCount, adjustedTau });
            return adjustedTau;
        } catch (error) {
            console.error(`[Ebbinghaus] CalculateAdjustedTau (personalized) error:`, error);
            return this.getPersonalizedBaseTau(age, experience);
        }
    }

    static getSafeDifficultyModifier(difficulty) {
        try {
            const diff = (difficulty || 'average').toLowerCase();
            const modifiers = {
                'difficult': this.DIFFICULTY_MODIFIERS.DIFFICULT,
                'easy': this.DIFFICULTY_MODIFIERS.EASY,
                'mastered': this.DIFFICULTY_MODIFIERS.MASTERED,
                'average': this.DIFFICULTY_MODIFIERS.AVERAGE
            };

            let modifier = modifiers[diff] || this.DIFFICULTY_MODIFIERS.AVERAGE;

            if (!isFinite(modifier) || modifier <= 0) {
                console.warn(`[Ebbinghaus] Invalid modifier for '${difficulty}', using default`);
                return this.DIFFICULTY_MODIFIERS.AVERAGE;
            }

            return Math.max(0.1, Math.min(10.0, modifier));
        } catch (error) {
            return this.DIFFICULTY_MODIFIERS.AVERAGE;
        }
    }

    static calculateSafeEffectiveRepetitions(repetitionCount) {
        try {
            if (repetitionCount <= 0) return 0.0;
            
            const logInput = Math.max(1.0, repetitionCount + 1.0);
            const logResult = Math.log2(logInput);

            if (!isFinite(logResult)) {
                console.warn(`[Ebbinghaus] Invalid log calculation for ${repetitionCount}, using linear approximation`);
                return Math.min(10.0, repetitionCount * 0.1);
            }

            return Math.min(20.0, Math.max(0.0, logResult));
        } catch (error) {
            console.error(`[Ebbinghaus] CalculateSafeEffectiveRepetitions error:`, error);
            return Math.min(5.0, repetitionCount * 0.05);
        }
    }

    static calculateSafeRepetitionBonus(effectiveRepetitions) {
        try {
            if (effectiveRepetitions <= 0) return 0.0;

            const exponent = Math.min(2.0, Math.max(0.0, 0.5));
            const powerResult = Math.pow(effectiveRepetitions, exponent);

            if (!isFinite(powerResult)) {
                console.warn(`[Ebbinghaus] Invalid power calculation, using linear approximation`);
                return Math.min(5.0, effectiveRepetitions * 0.5);
            }

            const bonusBase = Math.min(10.0, Math.max(0.0, powerResult));
            const bonus = bonusBase * this.REPETITION_STRENGTH_FACTOR;

            return Math.min(6.0, Math.max(0.0, bonus));
        } catch (error) {
            console.error(`[Ebbinghaus] CalculateSafeRepetitionBonus error:`, error);
            return Math.min(2.0, effectiveRepetitions * 0.3);
        }
    }

    /**
     * Clamps tau to the normative range [1, 180] days
     */
    static clampTauToSafeBounds(tau) {
        const ABSOLUTE_MIN_TAU = 1.0;
        const ABSOLUTE_MAX_TAU = 180.0;

        if (!isFinite(tau)) {
            console.warn(`[Ebbinghaus] Invalid tau value, using default`);
            return this.BASE_TAU_DAYS;
        }

        const clampedTau = Math.max(ABSOLUTE_MIN_TAU, Math.min(ABSOLUTE_MAX_TAU, tau));

        if (Math.abs(clampedTau - tau) > 0.001) {
            console.log(`[Ebbinghaus] Tau clamped from ${tau.toFixed(3)} to ${clampedTau.toFixed(3)}`);
        }

        return clampedTau;
    }

    /**
     * Calculates retention using Ebbinghaus forgetting curve
     * Enhanced with motor skill plateau and individual variability
     */
    static calculateRetention(daysSincePractice, tau, repetitionCount = 0, difficulty = 'average', experience = 'intermediate') {
        try {
            if (!isFinite(daysSincePractice)) {
                console.warn(`[Ebbinghaus] Invalid daysSincePractice, using 0`);
                daysSincePractice = 0;
            }

            if (!isFinite(tau) || tau <= 0) {
                console.warn(`[Ebbinghaus] Invalid tau value (${tau}), using default`);
                tau = this.BASE_TAU_DAYS;
            }

            daysSincePractice = Math.max(0, Math.min(1000, daysSincePractice));
            tau = this.clampTauToSafeBounds(tau);

            // Motor skills plateau phase - slower initial decline for first 6-12 hours
            const adjustedTime = this.applyMotorSkillsPlateau(daysSincePractice);

            // Enhanced repetition effects based on spacing effect research
            const repetitionMultiplier = this.calculateEnhancedRepetitionBonus(repetitionCount, difficulty);

            // Individual variability based on experience and learning characteristics
            const individualityFactor = this.calculateIndividualVariabilityFactor(experience, difficulty);

            // Apply adjustments to tau
            const adjustedTau = tau * repetitionMultiplier * individualityFactor;

            let exponent = -adjustedTime / adjustedTau;
            const MIN_SAFE_EXPONENT = -50.0;

            if (exponent < MIN_SAFE_EXPONENT) {
                console.log(`[Ebbinghaus] Extreme negative exponent (${exponent.toFixed(2)}), using asymptotic baseline`);
                return this.ASYMPTOTIC_RETENTION_BASELINE;
            }

            let expResult;
            try {
                expResult = Math.exp(exponent);
                if (!isFinite(expResult)) {
                    console.warn(`[Ebbinghaus] Invalid exponential result, using safe approximation`);
                    expResult = exponent < -10 ? 0.0 : Math.max(0.0, 1.0 + exponent);
                }
            } catch (error) {
                console.warn(`[Ebbinghaus] Exponential overflow, using safe approximation`);
                expResult = exponent > 0 ? 1.0 : 0.0;
            }

            let retention = (this.INITIAL_LEARNING_STRENGTH * expResult) + this.ASYMPTOTIC_RETENTION_BASELINE;

            if (!isFinite(retention)) {
                console.error(`[Ebbinghaus] Invalid retention calculation, using fallback`);
                retention = Math.max(
                    this.ASYMPTOTIC_RETENTION_BASELINE,
                    this.INITIAL_LEARNING_STRENGTH * Math.max(0, 1.0 - (adjustedTime / (adjustedTau * 2)))
                );
            }

            retention = Math.max(0.0, Math.min(1.0, retention));

            if (daysSincePractice === 0 && retention < this.INITIAL_LEARNING_STRENGTH * 0.9) {
                console.warn(`[Ebbinghaus] Inconsistent retention for day 0, correcting`);
                retention = this.INITIAL_LEARNING_STRENGTH + this.ASYMPTOTIC_RETENTION_BASELINE;
            }

            return retention;
        } catch (error) {
            console.error(`[Ebbinghaus] CalculateRetention error:`, error);
            if (daysSincePractice <= 0) return this.INITIAL_LEARNING_STRENGTH + this.ASYMPTOTIC_RETENTION_BASELINE;
            if (daysSincePractice >= 365) return this.ASYMPTOTIC_RETENTION_BASELINE;
            
            const t = Math.max(0, Math.min(1, daysSincePractice / 365.0));
            return (this.INITIAL_LEARNING_STRENGTH + this.ASYMPTOTIC_RETENTION_BASELINE) * (1 - t) + 
                   this.ASYMPTOTIC_RETENTION_BASELINE * t;
        }
    }

    /**
     * Applies motor skills plateau effect - slower initial decline for first 6-12 hours
     */
    static applyMotorSkillsPlateau(daysSincePractice) {
        const PLATEAU_DURATION = 0.4; // 0.4 days = ~9.6 hours
        const PLATEAU_STRENGTH = 0.6; // Reduce effective time by 40% during plateau

        if (daysSincePractice <= PLATEAU_DURATION) {
            // Gradual transition from full plateau to normal decay
            const plateauFactor = 1.0 - (PLATEAU_STRENGTH * (1.0 - daysSincePractice / PLATEAU_DURATION));
            return daysSincePractice * plateauFactor;
        }

        return daysSincePractice;
    }

    /**
     * Enhanced repetition bonus based on empirical research on spaced repetition
     */
    static calculateEnhancedRepetitionBonus(repetitionCount, difficulty) {
        if (repetitionCount <= 0) return 1.0;

        // Research-based: logarithmic scaling with diminishing returns
        const baseMultiplier = 1.0 + Math.log(1.0 + repetitionCount) * 0.15; // Max ~1.6x for 10 reps

        // Difficulty adjustment - harder material benefits more from repetition
        const diff = (difficulty || 'average').toLowerCase();
        const difficultyBonus = {
            'difficult': 1.3,  // 30% more benefit from repetition
            'mastered': 0.7,   // Less benefit as already well-learned
            'easy': 0.9,       // Slightly less benefit
            'average': 1.0
        }[diff] || 1.0;

        return Math.min(2.0, baseMultiplier * difficultyBonus);
    }

    /**
     * Individual variability based on user characteristics
     */
    static calculateIndividualVariabilityFactor(experience, difficulty) {
        const exp = (experience || 'intermediate').toLowerCase();
        const experienceFactor = {
            'beginner': 0.8,      // Faster forgetting
            'intermediate': 1.0,  // Average
            'advanced': 1.2,      // Slower forgetting
            'expert': 1.4,        // Much slower forgetting
            'professional': 1.4
        }[exp] || 1.0;

        return Math.max(0.6, Math.min(1.8, experienceFactor));
    }

    /**
     * Checks if the predicted retention is in the optimal range
     */
    static isOptimalInterval(predictedRetention, targetQuality = -1) {
        if (targetQuality < 0) targetQuality = this.RETENTION_THRESHOLD;
        return predictedRetention >= targetQuality && predictedRetention <= this.OPTIMAL_RETENTION_THRESHOLD;
    }

    /**
     * Clamps an interval to the normative range [1, 365] days and ensures it is <= 5 * tau
     * Returns {clampedIntervalDays, reason}
     */
    static clampIntervalToScientificBounds(intervalDays, tau = null, stability = null) {
        const minDays = this.MIN_INTERVAL_DAYS;
        const SAFETY_MAX_INTERVAL_DAYS = 365;
        let reason = 'none';
        const original = intervalDays;

        if (!isFinite(intervalDays) || intervalDays <= 0.0) {
            intervalDays = minDays;
            reason = 'invalid→min';
        }

        if (intervalDays < minDays) {
            intervalDays = minDays;
            reason = 'min_consolidation';
        }

        if (intervalDays > SAFETY_MAX_INTERVAL_DAYS) {
            intervalDays = SAFETY_MAX_INTERVAL_DAYS;
            reason = 'safety_max_365';
        }

        if (tau !== null) {
            const tauCap = tau * 5.0;
            if (intervalDays > tauCap) {
                intervalDays = tauCap;
                reason = reason === 'none' ? 'cap_5x_tau' : reason + '+cap_5x_tau';
            }
        }

        const tauText = tau !== null ? tau.toFixed(3) : 'n/a';
        const sText = stability !== null ? stability.toFixed(3) : 'n/a';
        const level = reason === 'none' ? 'debug' : 'warning';

        console.log(`[Ebbinghaus][Clamp] interval ${original.toFixed(2)}d -> ${intervalDays.toFixed(2)}d reason=${reason} (τ=${tauText}, S=${sText})`);
        if (typeof window !== 'undefined' && window.MPLog) window.MPLog.debug('Ebbinghaus ClampInterval', { original, clamped: intervalDays, reason, tau: tauText, stability: sText });

        return { clampedIntervalDays: intervalDays, reason };
    }

    /**
     * Returns the retention target (R*) for a given difficulty
     */
    static getRetentionTargetForDifficulty(difficulty) {
        if (!difficulty || difficulty.trim() === '') {
            return this.RETENTION_THRESHOLD; // Default 0.80
        }

        const d = difficulty.trim().toLowerCase();

        if (d === 'difficult' || d === 'hard' || d === 'challenging') {
            return this.OPTIMAL_RETENTION_THRESHOLD; // 0.85
        }

        if (d === 'easy' || d === 'simple') {
            return this.EASY_RETENTION_THRESHOLD; // 0.70
        }

        if (d === 'mastered' || d === 'review' || d === 'maintain') {
            return this.MASTERED_RETENTION_THRESHOLD; // 0.65
        }

        return this.RETENTION_THRESHOLD; // 0.80
    }

    /**
     * Calculates optimal interval to reach target retention
     * Solves: R(t) = R* for t
     */
    static calculateOptimalInterval(tau, targetRetention, repetitionCount = 0, difficulty = 'average', experience = 'intermediate') {
        try {
            // Apply same adjustments as in retention calculation
            const repetitionMultiplier = this.calculateEnhancedRepetitionBonus(repetitionCount, difficulty);
            const individualityFactor = this.calculateIndividualVariabilityFactor(experience, difficulty);
            const adjustedTau = tau * repetitionMultiplier * individualityFactor;

            // Solve for t in: R(t) = (L₀ * e^(-t/τ)) + B = R*
            // e^(-t/τ) = (R* - B) / L₀
            // -t/τ = ln((R* - B) / L₀)
            // t = -τ * ln((R* - B) / L₀)

            const retention_minus_baseline = targetRetention - this.ASYMPTOTIC_RETENTION_BASELINE;
            
            if (retention_minus_baseline <= 0) {
                // Target is at or below baseline - very long interval
                return this.MAX_INTERVAL_DAYS;
            }

            const fraction = retention_minus_baseline / this.INITIAL_LEARNING_STRENGTH;
            
            if (fraction <= 0 || fraction > 1) {
                console.warn(`[Ebbinghaus] Invalid fraction for interval calculation: ${fraction}`);
                return 1.0;
            }

            const interval = -adjustedTau * Math.log(fraction);

            if (!isFinite(interval) || interval < 0) {
                console.warn(`[Ebbinghaus] Invalid interval calculation, using minimum`);
                return this.MIN_INTERVAL_DAYS;
            }

            // Clamp to scientific bounds
            const { clampedIntervalDays, reason } = this.clampIntervalToScientificBounds(interval, tau);
            if (typeof window !== 'undefined' && window.MPLog) window.MPLog.debug('Ebbinghaus CalculateOptimalInterval', { tau, targetRetention, intervalRaw: interval, intervalClamped: clampedIntervalDays, reason });
            return clampedIntervalDays;
        } catch (error) {
            console.error(`[Ebbinghaus] CalculateOptimalInterval error:`, error);
            return this.MIN_INTERVAL_DAYS;
        }
    }
}

// Export for use in other modules
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { EbbinghausConstants };
}
