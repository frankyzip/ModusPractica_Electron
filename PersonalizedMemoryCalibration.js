// ============================================================================
// Personalized Memory Calibration - Bayesian Parameter Adjustment
// Learns from individual practice patterns to personalize τ-parameters
// Copyright © 2025 Frank De Baere - All Rights Reserved
// ============================================================================

/**
 * PersonalizedMemoryCalibration - Learns individual forgetting patterns
 * Uses Bayesian learning to adapt tau parameters based on actual performance
 */
class PersonalizedMemoryCalibration {
    constructor(storagePrefix = 'mp_') {
        this.storagePrefix = storagePrefix;
        this.calibrationData = null;
        this.profileName = null;
    }

    /**
     * Initializes calibration system for a specific profile
     */
    initializeCalibrationSystem(profileName) {
        try {
            this.profileName = profileName;
            this.loadCalibrationData();
            
            console.log(`[PMC] Initialized calibration system for profile: ${profileName}`);
        } catch (error) {
            console.error('[PMC] Failed to initialize calibration system:', error);
            this.calibrationData = this.createEmptyCalibrationData();
        }
    }

    /**
     * Calculates personalized τ-value based on individual history
     */
    getPersonalizedTau(difficulty, repetitions) {
        // Start with scientific basis
        const baseTau = EbbinghausConstants.calculateAdjustedTau(difficulty, repetitions);

        // Apply personal adjustment based on calibration
        const personalAdjustment = this.calculatePersonalAdjustment(difficulty, repetitions);

        const personalizedTau = baseTau * personalAdjustment;

        console.log(`[PMC] Personal Tau Calculation: Base Tau: ${baseTau.toFixed(2)} days, Personal Adjustment: ${personalAdjustment.toFixed(2)}x, Personalized Tau: ${personalizedTau.toFixed(2)} days`);

        return personalizedTau;
    }

    /**
     * Updates calibration from a practice session
     */
    updateCalibrationFromSession(session, section) {
        if (!session) {
            console.warn('[PMC] Session is null; skipping calibration update');
            return;
        }

        // Determine difficulty (fallback to Average)
        let difficulty = 'Average';
        try {
            if (section && section.difficulty) {
                difficulty = section.difficulty;
            }
        } catch (error) {
            // Ignore - difficulty stays "Average"
        }

        try {
            // 1. Calculate prediction accuracy
            const predictionAccuracy = section ? 
                this.calculatePredictionAccuracy(session, section) : 1.0;

            // 2. Update personal parameters for this difficulty
            this.updatePersonalParameters(difficulty, predictionAccuracy);

            // 3. Persist changes
            this.saveCalibrationData();

            console.log(
                `[PMC] Updated calibration. Diff=${difficulty}, ` +
                `Outcome='${session.sessionOutcome}', Reps=${session.repetitions}, ` +
                `PredAccuracy=${predictionAccuracy.toFixed(3)}`
            );
        } catch (error) {
            console.error('[PMC] Error during calibration update:', error);
        }
    }

    /**
     * Gets calibration statistics for debugging/UI
     */
    getCalibrationStats() {
        if (!this.calibrationData) {
            return {
                totalSessions: 0,
                isCalibrated: false,
                difficultyAdjustments: {}
            };
        }

        return {
            totalSessions: this.calibrationData.totalSessions,
            isCalibrated: this.hasSufficientDataForCalibration(),
            difficultyAdjustments: Object.fromEntries(
                Object.entries(this.calibrationData.difficultyAdjustments).map(([key, value]) => [
                    key,
                    {
                        factor: value.adjustmentFactor,
                        confidence: value.confidence,
                        sessions: value.sessionCount
                    }
                ])
            )
        };
    }

    // ========================================================================
    // PRIVATE HELPER METHODS
    // ========================================================================

    /**
     * Calculates personal adjustment factor based on collected data
     * Supports rapid calibration with lower session requirements
     */
    calculatePersonalAdjustment(difficulty, repetitions) {
        // Check for rapid calibration phase (first 5 sessions get immediate but conservative adjustments)
        const isRapidPhase = this.calibrationData.totalSessions <= 5;

        if (!this.hasSufficientDataForCalibration() && !isRapidPhase) {
            return 1.0; // Use standard values if insufficient data
        }

        // Find relevant calibration data for this difficulty
        const difficultyKey = difficulty.toLowerCase();
        const adjustment = this.calibrationData.difficultyAdjustments[difficultyKey];

        if (!adjustment) {
            return 1.0;
        }

        let confidenceFactor;

        if (isRapidPhase) {
            // Rapid calibration: lower confidence but still apply adjustments
            confidenceFactor = Math.min(0.6, adjustment.sessionCount / 3.0); // Max 60% confidence in rapid phase
            console.log(`[PMC] Rapid calibration active for ${difficulty} (sessions=${adjustment.sessionCount}, confidence=${confidenceFactor.toFixed(2)})`);
        } else {
            // Normal calibration: gradual confidence buildup
            confidenceFactor = Math.min(1.0, this.calibrationData.totalSessions / 25.0); // Reduced from 50 to 25
        }

        const personalizedFactor = 1.0 + (adjustment.adjustmentFactor - 1.0) * confidenceFactor;

        return Math.max(0.3, Math.min(3.0, personalizedFactor)); // Safe bounds
    }

    /**
     * Calculates how accurate our interval prediction was
     */
    calculatePredictionAccuracy(session, section) {
        // Calculate expected retention based on interval
        const lastPracticeDate = section.lastPracticeDate ? new Date(section.lastPracticeDate) : new Date(session.date);
        const sessionDate = new Date(session.date);
        const daysSincePractice = (sessionDate - lastPracticeDate) / (1000 * 60 * 60 * 24);

        if (daysSincePractice <= 0) return 1.0;

        const expectedTau = EbbinghausConstants.calculateAdjustedTau(
            section.difficulty,
            section.completedRepetitions || 0
        );
        const expectedRetention = EbbinghausConstants.calculateRetention(
            daysSincePractice,
            expectedTau
        );

        // Estimate actual retention based on session performance
        const actualRetention = this.estimateActualRetention(session);

        // Calculate accuracy (1.0 = perfect, 0.0 = completely wrong)
        const accuracy = 1.0 - Math.abs(expectedRetention - actualRetention);
        return Math.max(0.0, Math.min(1.0, accuracy));
    }

    /**
     * Estimates actual retention based on session performance
     * Uses multiple factors for accurate estimation
     */
    estimateActualRetention(session) {
        // Use performanceScore if available (most reliable)
        if (session.performanceScore !== undefined && session.performanceScore !== null) {
            // Map performance score (0-10) to retention (0-1)
            // Poor (2.5) → 0.3, Fair (5.0) → 0.6, Good (7.5) → 0.8, Excellent (9.5) → 0.95
            const retention = 0.2 + (session.performanceScore / 10.0) * 0.75;
            return Math.max(0.1, Math.min(1.0, retention));
        }

        // Fallback: estimate based on efficiency metrics
        if (!session.duration || session.duration <= 0) return 0.5;

        const durationMinutes = session.duration / (1000 * 60); // Convert ms to minutes
        
        // Avoid division by zero
        if (durationMinutes <= 0) return 0.5;
        
        const repetitionsPerMinute = session.repetitions / durationMinutes;

        // Normalize to 0-1 scale (0.5-2.0 rep/min = normal range)
        let normalizedEfficiency = Math.max(0.1, Math.min(1.0, repetitionsPerMinute / 2.0));

        // Adjustment based on attempts till success (execution failures)
        const executionFails = getExecutionFailures(session);
        if (executionFails && executionFails > 0) {
            const attemptsAdjustment = 1.0 / Math.sqrt(executionFails);
            normalizedEfficiency *= attemptsAdjustment;
        }

        // Adjustment based on memory failures (streak resets)
        const memoryFails = getMemoryFailures(session);
        if (memoryFails && memoryFails > 0) {
            const failureRatio = memoryFails / (session.repetitions + memoryFails);
            normalizedEfficiency *= (1.0 - failureRatio * 0.5); // Failures reduce efficiency
        }

        return Math.max(0.1, Math.min(1.0, normalizedEfficiency));
    }

    /**
     * Updates personal parameters with Bayesian learning
     */
    updatePersonalParameters(difficulty, accuracy) {
        const difficultyKey = difficulty.toLowerCase();

        if (!this.calibrationData.difficultyAdjustments[difficultyKey]) {
            this.calibrationData.difficultyAdjustments[difficultyKey] = {
                adjustmentFactor: 1.0,
                confidence: 0.0,
                sessionCount: 0
            };
        }

        const adjustment = this.calibrationData.difficultyAdjustments[difficultyKey];

        // Bayesian update with learning rate
        const learningRate = EbbinghausConstants.BAYESIAN_LEARNING_RATE;
        
        let targetAdjustment;
        if (accuracy < 0.5) {
            targetAdjustment = accuracy < 0.3 ? 0.7 : 0.85; // Too fast forgetting -> shorter tau
        } else {
            targetAdjustment = accuracy > 0.8 ? 1.3 : 1.15; // Too good retention -> longer tau
        }

        // Gradual adjustment towards target
        adjustment.adjustmentFactor += learningRate * (targetAdjustment - adjustment.adjustmentFactor);
        adjustment.sessionCount++;
        adjustment.confidence = Math.min(1.0, adjustment.sessionCount / 20.0);

        this.calibrationData.totalSessions++;
    }

    /**
     * Checks if there is sufficient data for reliable calibration
     * Reduced minimum sessions for faster adaptation
     */
    hasSufficientDataForCalibration() {
        const ACCELERATED_MIN_SESSIONS = 5; // Reduced from 10 to 5
        return this.calibrationData.totalSessions >= ACCELERATED_MIN_SESSIONS;
    }

    /**
     * Loads calibration data from localStorage
     */
    loadCalibrationData() {
        try {
            if (!this.profileName) {
                this.calibrationData = this.createEmptyCalibrationData();
                return;
            }

            const key = `${this.storagePrefix}${this.profileName}_calibration`;
            const jsonContent = localStorage.getItem(key);

            if (jsonContent) {
                this.calibrationData = JSON.parse(jsonContent);
                console.log(`[PMC] Loaded calibration data for profile: ${this.profileName}`);
            } else {
                this.calibrationData = this.createEmptyCalibrationData();
                console.log(`[PMC] No existing calibration data found, initialized new data`);
            }
        } catch (error) {
            console.error('[PMC] Error loading calibration data:', error);
            this.calibrationData = this.createEmptyCalibrationData();
        }
    }

    /**
     * Saves calibration data to localStorage
     */
    saveCalibrationData() {
        try {
            if (!this.profileName || !this.calibrationData) {
                console.warn('[PMC] Cannot save calibration data: missing profile or data');
                return;
            }

            const key = `${this.storagePrefix}${this.profileName}_calibration`;
            this.calibrationData.lastCalibrationUpdate = new Date().toISOString();
            
            const jsonContent = JSON.stringify(this.calibrationData);
            localStorage.setItem(key, jsonContent);

            console.log(`[PMC] Saved calibration data for profile: ${this.profileName}`);
        } catch (error) {
            console.error('[PMC] Error saving calibration data:', error);
        }
    }

    /**
     * Creates empty calibration data structure
     */
    createEmptyCalibrationData() {
        return {
            totalSessions: 0,
            difficultyAdjustments: {
                // Will be populated as sessions are recorded
                // Keys: 'difficult', 'average', 'easy', 'mastered'
                // Values: { adjustmentFactor, confidence, sessionCount }
            },
            lastCalibrationUpdate: new Date().toISOString()
        };
    }

    /**
     * Resets calibration data (useful for debugging or user request)
     */
    resetCalibration() {
        this.calibrationData = this.createEmptyCalibrationData();
        this.saveCalibrationData();
        console.log('[PMC] Calibration data reset');
    }

    /**
     * Gets detailed calibration report for diagnostics
     */
    getDetailedCalibrationReport() {
        if (!this.calibrationData) {
            return 'No calibration data available';
        }

        const difficulties = ['difficult', 'average', 'easy', 'mastered'];
        let report = `\n${'='.repeat(60)}\n`;
        report += `📊 PERSONALIZED MEMORY CALIBRATION REPORT\n`;
        report += `${'='.repeat(60)}\n`;
        report += `Profile: ${this.profileName || 'Unknown'}\n`;
        report += `Total Sessions: ${this.calibrationData.totalSessions}\n`;
        report += `Calibration Status: ${this.hasSufficientDataForCalibration() ? '✅ Active' : '⏳ Learning'}\n`;
        report += `Last Update: ${new Date(this.calibrationData.lastCalibrationUpdate).toLocaleString()}\n`;
        report += `${'='.repeat(60)}\n`;

        difficulties.forEach(diff => {
            const adj = this.calibrationData.difficultyAdjustments[diff];
            if (adj && adj.sessionCount > 0) {
                report += `\n${diff.toUpperCase()}:\n`;
                report += `  Adjustment Factor: ${adj.adjustmentFactor.toFixed(3)}x\n`;
                report += `  Confidence: ${(adj.confidence * 100).toFixed(1)}%\n`;
                report += `  Sessions: ${adj.sessionCount}\n`;
                
                // Interpret adjustment
                let interpretation = '';
                if (adj.adjustmentFactor < 0.9) {
                    interpretation = '⚡ Faster forgetting → Shorter intervals';
                } else if (adj.adjustmentFactor > 1.1) {
                    interpretation = '🧠 Stronger memory → Longer intervals';
                } else {
                    interpretation = '✓ Standard retention';
                }
                report += `  Interpretation: ${interpretation}\n`;
            }
        });

        report += `\n${'='.repeat(60)}\n`;
        return report;
    }

    /**
     * Exports calibration data for backup/analysis
     */
    exportCalibrationData() {
        if (!this.calibrationData) {
            return null;
        }

        return {
            version: '1.0',
            exportDate: new Date().toISOString(),
            profileName: this.profileName,
            calibrationData: JSON.parse(JSON.stringify(this.calibrationData))
        };
    }

    /**
     * Imports calibration data from backup
     */
    importCalibrationData(exportedData) {
        try {
            if (!exportedData || !exportedData.calibrationData) {
                throw new Error('Invalid calibration data format');
            }

            this.calibrationData = exportedData.calibrationData;
            this.saveCalibrationData();
            
            console.log('[PMC] Successfully imported calibration data');
            return true;
        } catch (error) {
            console.error('[PMC] Error importing calibration data:', error);
            return false;
        }
    }
}

// Export for use in other modules
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { PersonalizedMemoryCalibration };
}
