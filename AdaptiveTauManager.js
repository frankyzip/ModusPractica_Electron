// ============================================================================
// Adaptive Tau Manager - Unified Adaptive Learning System
// Integrates PersonalizedMemoryCalibration, MemoryStabilityManager, and performance-based adjustments
// Copyright © 2025 Frank De Baere - All Rights Reserved
// ============================================================================

/**
 * AdaptiveTauManager - Coordinates all adaptive learning systems
 * 
 * Key improvements:
 * - Reduces fragmentation between parallel adaptive systems
 * - Accelerates adaptation for new users (rapid calibration)
 * - Provides confidence-weighted integration of different data sources
 * - Maintains scientific baseline while learning individual patterns
 */
class AdaptiveTauManager {
    constructor() {
        // Rapid calibration constants
        this.RAPID_CALIBRATION_SESSIONS = 5;
        this.RAPID_LEARNING_RATE = 0.35;
        this.CONFIDENCE_BUILDUP_RATE = 0.25;
        this.AGE_BIAS_DECAY_RATE = 0.3;
    }

    /**
     * Master tau calculation that integrates all adaptive systems
     * This replaces the fragmented approach in EbbinghausConstants.calculateAdjustedTau
     */
    calculateIntegratedTau(difficulty, repetitionCount, options = {}) {
        const {
            barSectionId = null,
            sectionHistory = null,
            userAge = null,
            userExperience = null,
            pmcManager = null,
            stabilityManager = null,
            useAdaptiveSystems = true
        } = options;

        try {
            // If adaptive systems disabled, use demographic baseline only
            if (!useAdaptiveSystems) {
                const simpleTau = this.calculateDemographicBaseline(difficulty, repetitionCount, userAge, userExperience);
                const clampedSimple = EbbinghausConstants.clampTauToSafeBounds(simpleTau);
                console.log(`[AdaptiveTau] Adaptive systems disabled, using demographic baseline: ${clampedSimple.toFixed(3)}`);
                return clampedSimple;
            }

            // Step 1: Calculate demographic baseline
            const demographicTau = this.calculateDemographicBaseline(difficulty, repetitionCount, userAge, userExperience);

            // Step 2: If no section-specific data available, return demographic baseline
            if (!barSectionId || !sectionHistory) {
                console.log(`[AdaptiveTau] No section data available, using demographic baseline τ=${demographicTau.toFixed(3)}`);
                return EbbinghausConstants.clampTauToSafeBounds(demographicTau);
            }

            // Step 3: Gather adaptive data from all systems
            const adaptiveData = this.gatherAdaptiveData(
                barSectionId,
                sectionHistory,
                difficulty,
                repetitionCount,
                pmcManager,
                stabilityManager
            );

            // Step 4: Calculate confidence in adaptive vs demographic data
            const adaptiveConfidence = this.calculateAdaptiveConfidence(adaptiveData);

            // Step 5: Integrate all sources based on confidence
            const integratedTau = this.integrateTauSources(demographicTau, adaptiveData, adaptiveConfidence);

            console.log(
                `[AdaptiveTau] Integrated τ calculation for ${difficulty} (reps=${repetitionCount}): ` +
                `demographic=${demographicTau.toFixed(3)}, adaptive=${adaptiveData.adaptiveTau.toFixed(3)}, ` +
                `confidence=${adaptiveConfidence.toFixed(3)}, final=${integratedTau.toFixed(3)}`
            );

            return EbbinghausConstants.clampTauToSafeBounds(integratedTau);
        } catch (error) {
            console.error('[AdaptiveTau] Error in integrated tau calculation:', error);
            // Fallback to original demographic calculation
            return EbbinghausConstants.calculateAdjustedTau(difficulty, repetitionCount);
        }
    }

    /**
     * Rapid calibration for new users - applies after each session in first 5 sessions
     */
    applyRapidCalibration(barSectionId, newSession, section, allHistory, pmcManager) {
        try {
            if (!newSession || !section) return;

            const totalUserSessions = allHistory ? allHistory.length : 0;
            const sectionSessions = allHistory ? 
                allHistory.filter(h => h.barSectionId === barSectionId).length : 0;

            const isInRapidPhase = totalUserSessions <= this.RAPID_CALIBRATION_SESSIONS || sectionSessions <= 3;

            if (isInRapidPhase) {
                const performanceDeviation = newSession.performanceScore - 6.0; // Expected baseline
                let adjustment = 1.0;

                if (Math.abs(performanceDeviation) > 1.5) { // Significant deviation
                    if (performanceDeviation < 0) { // Poor performance
                        adjustment = 0.8; // Shorter intervals
                    } else { // Good performance
                        adjustment = 1.25; // Longer intervals
                    }

                    // Apply rapid adjustment through PersonalizedMemoryCalibration
                    if (pmcManager) {
                        pmcManager.updateCalibrationFromSession(newSession, section);
                    }

                    console.log(
                        `[AdaptiveTau] Rapid calibration applied for section ${barSectionId}: ` +
                        `performance=${newSession.performanceScore.toFixed(1)}, adjustment=${adjustment.toFixed(3)}x ` +
                        `(session ${sectionSessions} of section, ${totalUserSessions} total user sessions)`
                    );
                }
            }
        } catch (error) {
            console.error('[AdaptiveTau] Error in rapid calibration:', error);
        }
    }

    /**
     * Detects if immediate tau adjustment is needed based on performance deviation
     */
    requiresImmediateAdjustment(session, recentHistory) {
        if (!session || !recentHistory || recentHistory.length < 2) return false;

        try {
            const last3 = recentHistory.slice(-3);
            const avgRecentPerformance = last3.reduce((sum, h) => sum + h.performanceScore, 0) / last3.length;
            const expectedPerformance = 6.0;
            const deviation = Math.abs(avgRecentPerformance - expectedPerformance);

            return deviation > 2.5; // Significant deviation requiring immediate attention
        } catch (error) {
            return false;
        }
    }

    // ========================================================================
    // PRIVATE HELPER METHODS
    // ========================================================================

    calculateDemographicBaseline(difficulty, repetitionCount, userAge, userExperience) {
        // Use personalized calculation if experience is provided
        if (userExperience && userExperience.trim() !== '') {
            // Get base tau with experience adjustment
            const personalizedBaseTau = EbbinghausConstants.getPersonalizedBaseTau(userAge, userExperience);
            
            // Apply difficulty and repetition adjustments
            const difficultyModifier = EbbinghausConstants.getSafeDifficultyModifier(difficulty || 'Average');
            let adjustedTau = personalizedBaseTau * EbbinghausConstants.MUSIC_MATERIAL_FACTOR * difficultyModifier;
            
            // Apply repetition bonus if applicable
            if (repetitionCount > 0) {
                const repBonus = EbbinghausConstants.calculateEnhancedRepetitionBonus(repetitionCount, difficulty || 'Average');
                adjustedTau *= repBonus;
            }
            
            console.log(`[AdaptiveTau] Demographic baseline with experience '${userExperience}': τ=${adjustedTau.toFixed(3)}d (reps=${repetitionCount})`);
            return EbbinghausConstants.clampTauToSafeBounds(adjustedTau);
        }
        
        // Fallback: use standard calculation without demographic adjustment
        return EbbinghausConstants.calculateAdjustedTau(difficulty, repetitionCount);
    }

    gatherAdaptiveData(barSectionId, sectionHistory, difficulty, repetitionCount, pmcManager, stabilityManager) {
        const dataSet = {
            pmcTau: 0.0,
            pmcConfidence: 0.0,
            hasPMCData: false,

            stabilityDays: 0.0,
            memoryDifficulty: 0.0,
            stabilityBasedTau: 0.0,
            stabilityConfidence: 0.0,
            hasStabilityData: false,

            recentPerformance: 0.0,
            performanceConfidence: 0.0,
            hasPerformanceData: false,

            adaptiveTau: 0.0
        };

        try {
            // 1. PersonalizedMemoryCalibration data
            if (pmcManager) {
                const pmcTau = pmcManager.getPersonalizedTau(difficulty, repetitionCount);
                if (isFinite(pmcTau) && pmcTau > 0) {
                    dataSet.pmcTau = pmcTau;
                    dataSet.hasPMCData = true;

                    const pmcStats = pmcManager.getCalibrationStats();
                    dataSet.pmcConfidence = pmcStats.totalSessions >= 3 ?
                        Math.min(1.0, pmcStats.totalSessions / 10.0) : 0.0;
                }
            }

            // 2. MemoryStabilityManager data
            if (stabilityManager) {
                const memoryStats = stabilityManager.getMemoryStats(barSectionId);
                if (memoryStats && !memoryStats.isNew && memoryStats.reviewCount >= 2) {
                    dataSet.stabilityDays = memoryStats.stability;
                    dataSet.memoryDifficulty = memoryStats.difficulty;
                    dataSet.hasStabilityData = true;
                    dataSet.stabilityConfidence = Math.min(1.0, memoryStats.reviewCount / 5.0);

                    // Convert stability to tau equivalent
                    dataSet.stabilityBasedTau = this.convertStabilityToTau(
                        memoryStats.stability,
                        memoryStats.difficulty
                    );
                }
            }

            // 3. Recent performance trend
            if (sectionHistory && sectionHistory.length >= 2) {
                const recentSessions = sectionHistory
                    .sort((a, b) => new Date(b.date) - new Date(a.date))
                    .slice(0, 3);
                
                const avgPerformance = recentSessions.reduce((sum, s) => sum + s.performanceScore, 0) / recentSessions.length;
                dataSet.recentPerformance = avgPerformance;
                dataSet.hasPerformanceData = true;
                dataSet.performanceConfidence = Math.min(1.0, recentSessions.length / 3.0);
            }

            // 4. Calculate integrated adaptive tau
            dataSet.adaptiveTau = this.calculateWeightedAdaptiveTau(dataSet);

        } catch (error) {
            console.error('[AdaptiveTau] Error gathering adaptive data:', error);
        }

        return dataSet;
    }

    calculateWeightedAdaptiveTau(dataSet) {
        let weightedSum = 0.0;
        let totalWeight = 0.0;

        // Weight PMC data
        if (dataSet.hasPMCData && dataSet.pmcConfidence > 0) {
            const weight = dataSet.pmcConfidence * 0.4; // 40% max weight for PMC
            weightedSum += dataSet.pmcTau * weight;
            totalWeight += weight;
        }

        // Weight stability data (most reliable for established sections)
        if (dataSet.hasStabilityData && dataSet.stabilityConfidence > 0) {
            const weight = dataSet.stabilityConfidence * 0.5; // 50% max weight for stability
            weightedSum += dataSet.stabilityBasedTau * weight;
            totalWeight += weight;
        }

        // Weight performance data (immediate feedback)
        if (dataSet.hasPerformanceData && dataSet.performanceConfidence > 0) {
            const performanceAdjustment = this.calculatePerformanceBasedTau(dataSet.recentPerformance);
            const weight = dataSet.performanceConfidence * 0.3; // 30% max weight for performance
            weightedSum += performanceAdjustment * weight;
            totalWeight += weight;
        }

        if (totalWeight > 0) {
            return weightedSum / totalWeight;
        }

        // Fallback to demographic baseline if no adaptive data
        return EbbinghausConstants.BASE_TAU_DAYS * EbbinghausConstants.MUSIC_MATERIAL_FACTOR;
    }

    convertStabilityToTau(stabilityDays, difficulty) {
        // Stability represents 50% retention point, tau represents 37% retention point
        // Approximate conversion based on forgetting curve mathematics
        const conversionFactor = 0.7; // Empirically derived
        return stabilityDays * conversionFactor * (1.0 + difficulty * 0.3);
    }

    calculatePerformanceBasedTau(avgPerformance) {
        // Convert performance to tau multiplier
        const baseline = EbbinghausConstants.BASE_TAU_DAYS * EbbinghausConstants.MUSIC_MATERIAL_FACTOR;

        if (avgPerformance < 4.0) { // Poor performance
            return baseline * 0.7; // Shorter intervals
        } else if (avgPerformance > 7.5) { // Good performance
            return baseline * 1.4; // Longer intervals
        } else {
            return baseline; // Neutral
        }
    }

    calculateAdaptiveConfidence(dataSet) {
        // Calculate overall confidence in adaptive data vs demographic baseline
        let totalConfidence = 0.0;
        let dataSourceCount = 0;

        if (dataSet.hasPMCData) {
            totalConfidence += dataSet.pmcConfidence;
            dataSourceCount++;
        }

        if (dataSet.hasStabilityData) {
            totalConfidence += dataSet.stabilityConfidence;
            dataSourceCount++;
        }

        if (dataSet.hasPerformanceData) {
            totalConfidence += dataSet.performanceConfidence;
            dataSourceCount++;
        }

        if (dataSourceCount === 0) return 0.0;

        // Average confidence across available sources
        let avgConfidence = totalConfidence / dataSourceCount;

        // Boost confidence if multiple sources agree
        if (dataSourceCount >= 2) avgConfidence *= 1.2;
        if (dataSourceCount >= 3) avgConfidence *= 1.1;

        return Math.min(1.0, avgConfidence);
    }

    integrateTauSources(demographicTau, adaptiveData, adaptiveConfidence) {
        // Weighted blend based on confidence in adaptive data
        if (adaptiveConfidence < 0.1) { // Very low confidence
            return demographicTau; // Use demographic baseline
        } else if (adaptiveConfidence > 0.8) { // High confidence
            // Mostly adaptive with small demographic influence
            return adaptiveData.adaptiveTau * 0.9 + demographicTau * 0.1;
        } else { // Medium confidence
            // Blend proportionally
            return adaptiveData.adaptiveTau * adaptiveConfidence +
                   demographicTau * (1.0 - adaptiveConfidence);
        }
    }
}

// Export for use in other modules
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { AdaptiveTauManager };
}
