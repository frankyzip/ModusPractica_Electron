// ============================================================================
// Memory Stability Manager - Advanced Memory Tracking System
// Based on SuperMemo SM-17+ algorithms and modern cognitive research
// Copyright © 2025 Frank De Baere - All Rights Reserved
// ============================================================================

/**
 * MemoryStabilityManager - Tracks memory stability and difficulty per section
 * 
 * Key Concepts:
 * - Stability (S): How long memory lasts before 50% forgetting probability
 * - Retrievability (R): Current probability of successful recall
 * - Difficulty (D): Inherent difficulty of the memory item
 */
class MemoryStabilityManager {
    constructor(storagePrefix = 'mp_') {
        this.storagePrefix = storagePrefix;
        this.stabilityDatabase = new Map();
        this.profileName = null;

        // SM-17+ Algorithm Constants (scientifically validated)
        this.INITIAL_STABILITY_DAYS = 1.8;        // First recall stability
        this.DEFAULT_DIFFICULTY = 0.3;            // Default difficulty (0.0 = easy, 1.0 = hard)
        this.STABILITY_GROWTH_FACTOR = 1.3;       // How much stability grows on success
        this.DIFFICULTY_ADJUSTMENT_RATE = 0.05;   // How fast difficulty adapts
        this.RETRIEVABILITY_THRESHOLD = 0.8;      // Optimal recall probability
        this.FORGETTING_CURVE_STEEPNESS = -0.233; // Forgetting curve parameter
    }

    /**
     * Initializes for a specific user profile
     */
    initializeForUser(profileName) {
        this.profileName = profileName;
        this.loadStabilityData();
        console.log(`[MemoryStability] Initialized for profile: ${profileName}`);
    }

    /**
     * Updates memory stability based on practice session result
     * This is the core of the SM-17+ algorithm implementation
     */
    updateMemoryStability(barSectionId, practiceResult) {
        try {
            if (!practiceResult || !barSectionId) {
                console.warn('[MemoryStability] Invalid parameters');
                return;
            }

            // Get or create stability data
            let stabilityData = this.stabilityDatabase.get(barSectionId);
            
            if (!stabilityData) {
                stabilityData = {
                    barSectionId: barSectionId,
                    stability: this.INITIAL_STABILITY_DAYS,
                    difficulty: this.DEFAULT_DIFFICULTY,
                    lastReviewDate: this.getCurrentDate(),
                    reviewCount: 0,
                    createdDate: this.getCurrentDate(),
                    lastModifiedDate: this.getCurrentDate()
                };
                this.stabilityDatabase.set(barSectionId, stabilityData);
            }

            // Calculate current retrievability before this session
            const lastReviewDate = new Date(stabilityData.lastReviewDate);
            const sessionDate = new Date(practiceResult.date);
            const daysSinceLastReview = (sessionDate - lastReviewDate) / (1000 * 60 * 60 * 24);

            // Same-day repetition check
            // We check for:
            // 1. Same calendar day (e.g. morning and evening practice)
            // 2. If the day changed (implying sleep/consolidation), we allow the update even if < 12h.
            const isSameCalendarDay = lastReviewDate.getFullYear() === sessionDate.getFullYear() &&
                                    lastReviewDate.getMonth() === sessionDate.getMonth() &&
                                    lastReviewDate.getDate() === sessionDate.getDate();
            
            // MODIFIED: Allow update if day changed (isSameCalendarDay is false), even if daysSinceLastReview < 0.5
            // Scientific rationale: Sleep consolidates memory. A review after sleep (next day) is a valid
            // retrieval attempt on consolidated memory, even if the chronological time is short (e.g. 23:00 -> 08:00).
            if (isSameCalendarDay && stabilityData.reviewCount > 0) {
                console.log(`[MemoryStability] Same-day repetition detected (SameDay: ${isSameCalendarDay}, Days: ${daysSinceLastReview.toFixed(2)}). Skipping stability update to preserve consolidation.`);
                return;
            }

            const retrievability = this.calculateRetrievability(stabilityData.stability, daysSinceLastReview);

            // Determine if this was a successful recall
            const wasSuccessful = this.determineRecallSuccess(practiceResult);

            // Update stability using SM-17+ algorithm
            this.updateStabilityAlgorithm(stabilityData, retrievability, wasSuccessful, practiceResult);

            // Save updated data
            this.saveStabilityData();

            console.log(
                `[MemoryStability] Updated for section ${barSectionId}: ` +
                `S=${stabilityData.stability.toFixed(1)}d, D=${stabilityData.difficulty.toFixed(3)}, ` +
                `R=${retrievability.toFixed(3)}, Success=${wasSuccessful}`
            );
        } catch (error) {
            console.error('[MemoryStability] Error updating stability data:', error);
        }
    }

    /**
     * Calculates optimal next review date based on memory stability
     */
    calculateOptimalReviewDate(barSectionId) {
        try {
            const stabilityData = this.stabilityDatabase.get(barSectionId);

            if (!stabilityData) {
                // New item - use initial stability
                const currentDate = this.getCurrentDate();
                return this.addDaysToDate(currentDate, this.INITIAL_STABILITY_DAYS);
            }

            // Calculate interval to reach target retrievability
            const targetInterval = this.calculateTargetInterval(
                stabilityData.stability,
                this.RETRIEVABILITY_THRESHOLD
            );

            // Apply difficulty adjustment
            const adjustedInterval = this.applyDifficultyAdjustment(targetInterval, stabilityData.difficulty);

            const lastReviewDate = new Date(stabilityData.lastReviewDate);
            return this.addDaysToDate(lastReviewDate, adjustedInterval);
        } catch (error) {
            console.error(`[MemoryStability] Error calculating optimal review date for section ${barSectionId}:`, error);
            const currentDate = this.getCurrentDate();
            return this.addDaysToDate(currentDate, 1.0);
        }
    }

    /**
     * Gets comprehensive memory statistics for a bar section
     */
    getMemoryStats(barSectionId) {
        const stabilityData = this.stabilityDatabase.get(barSectionId);

        if (!stabilityData) {
            return {
                barSectionId: barSectionId,
                isNew: true,
                stability: this.INITIAL_STABILITY_DAYS,
                difficulty: this.DEFAULT_DIFFICULTY,
                currentRetrievability: 1.0,
                reviewCount: 0,
                lastReviewDate: null,
                daysSinceLastReview: 0.0,
                optimalNextReview: this.addDaysToDate(this.getCurrentDate(), this.INITIAL_STABILITY_DAYS),
                retentionStrength: 0.0,
                learningProgress: 0.0
            };
        }

        const currentDate = this.getCurrentDate();
        const lastReviewDate = new Date(stabilityData.lastReviewDate);
        const daysSinceLastReview = (currentDate - lastReviewDate) / (1000 * 60 * 60 * 24);

        const currentRetrievability = this.calculateRetrievability(
            stabilityData.stability,
            daysSinceLastReview
        );

        return {
            barSectionId: barSectionId,
            isNew: false,
            stability: stabilityData.stability,
            difficulty: stabilityData.difficulty,
            currentRetrievability: currentRetrievability,
            reviewCount: stabilityData.reviewCount,
            lastReviewDate: stabilityData.lastReviewDate,
            daysSinceLastReview: daysSinceLastReview,
            optimalNextReview: this.calculateOptimalReviewDate(barSectionId),
            retentionStrength: this.calculateRetentionStrength(stabilityData),
            learningProgress: this.calculateLearningProgress(stabilityData)
        };
    }

    /**
     * Predicts memory retention curve for visualization
     */
    predictRetentionCurve(barSectionId, daysAhead = 30) {
        const curve = [];
        const stats = this.getMemoryStats(barSectionId);

        const startDate = stats.lastReviewDate ? new Date(stats.lastReviewDate) : this.getCurrentDate();

        for (let day = 0; day <= daysAhead; day++) {
            const retrievability = this.calculateRetrievability(stats.stability, day);
            const date = this.addDaysToDate(startDate, day);
            curve.push({ date, retrievability });
        }

        return curve;
    }

    /**
     * Merges memory stability data from multiple old sections into a new merged section
     */
    mergeStabilityData(oldSectionIds, newSectionId) {
        if (!oldSectionIds || oldSectionIds.length === 0 || !newSectionId) {
            console.warn('[MemoryStability] Invalid merge parameters');
            return;
        }

        try {
            const oldStabilityData = [];

            // Collect existing stability data for old sections
            for (const oldId of oldSectionIds) {
                const data = this.stabilityDatabase.get(oldId);
                if (data) {
                    oldStabilityData.push(data);
                }
            }

            if (oldStabilityData.length === 0) {
                console.log(`[MemoryStability] No existing stability data found for ${oldSectionIds.length} sections`);
                return;
            }

            // Calculate merged stability values using weighted averaging
            let totalReviews = oldStabilityData.reduce((sum, d) => sum + d.reviewCount, 0);
            if (totalReviews === 0) totalReviews = 1; // Avoid division by zero

            const mergedStability = oldStabilityData.reduce((sum, d) => 
                sum + d.stability * (d.reviewCount / totalReviews), 0);

            const mergedDifficulty = oldStabilityData.reduce((sum, d) => 
                sum + d.difficulty * (d.reviewCount / totalReviews), 0);

            const mostRecentReview = new Date(Math.max(...oldStabilityData.map(d => 
                new Date(d.lastReviewDate).getTime())));

            const totalReviewCount = oldStabilityData.reduce((sum, d) => sum + d.reviewCount, 0);

            const earliestCreation = new Date(Math.min(...oldStabilityData.map(d => 
                new Date(d.createdDate).getTime())));

            // Create merged stability data
            const mergedData = {
                barSectionId: newSectionId,
                stability: Math.max(mergedStability, this.INITIAL_STABILITY_DAYS),
                difficulty: Math.max(0.0, Math.min(1.0, mergedDifficulty)),
                lastReviewDate: mostRecentReview.toISOString(),
                reviewCount: totalReviewCount,
                createdDate: earliestCreation.toISOString(),
                lastModifiedDate: this.getCurrentDate().toISOString()
            };

            // Add merged data to database
            this.stabilityDatabase.set(newSectionId, mergedData);

            // Remove old stability data
            for (const oldId of oldSectionIds) {
                this.stabilityDatabase.delete(oldId);
            }

            // Save changes
            this.saveStabilityData();

            console.log(
                `[MemoryStability] Successfully merged ${oldStabilityData.length} stability records into new section ${newSectionId}. ` +
                `Merged stats - Stability: ${mergedData.stability.toFixed(1)} days, Difficulty: ${mergedData.difficulty.toFixed(2)}, Reviews: ${mergedData.reviewCount}`
            );
        } catch (error) {
            console.error(`[MemoryStability] Error merging stability data for section ${newSectionId}:`, error);
        }
    }

    // ========================================================================
    // PRIVATE ALGORITHM IMPLEMENTATION
    // ========================================================================

    /**
     * Core SM-17+ algorithm for updating memory stability
     */
    updateStabilityAlgorithm(stabilityData, retrievability, wasSuccessful, practiceResult) {
        stabilityData.reviewCount++;
        stabilityData.lastReviewDate = new Date(practiceResult.date).toISOString();

        if (wasSuccessful) {
            // Successful recall - increase stability
            const stabilityIncrease = this.calculateStabilityIncrease(
                stabilityData.stability,
                retrievability,
                stabilityData.difficulty
            );

            stabilityData.stability *= stabilityIncrease;

            // Slightly reduce difficulty (it's getting easier)
            stabilityData.difficulty = Math.max(0.01,
                stabilityData.difficulty - this.DIFFICULTY_ADJUSTMENT_RATE);
        } else {
            // Failed recall - reset stability
            let penaltyFactor = 0.3; // Default: reduce stability to 30%
            
            // CONTEXT-AWARENESS: If Energy Level was LOW, soften the penalty
            if (practiceResult.energyLevel === 'Low') {
                penaltyFactor = 0.9; // Reduce stability to 90% instead of 30%
                console.log('[MemoryStability] Low Energy Context Detected: Softening failure penalty (0.3 -> 0.9)');
            }

            // Don't make it worse than slightly below initial stability
            stabilityData.stability = Math.max(
                this.INITIAL_STABILITY_DAYS * 0.8,
                stabilityData.stability * penaltyFactor
            );

            // Increase difficulty (it's harder than we thought)
            // If low energy, increase difficulty less aggressively
            const difficultyIncrease = practiceResult.energyLevel === 'Low' 
                ? this.DIFFICULTY_ADJUSTMENT_RATE // Normal increase
                : this.DIFFICULTY_ADJUSTMENT_RATE * 2; // Aggressive increase

            stabilityData.difficulty = Math.min(0.99,
                stabilityData.difficulty + difficultyIncrease);
        }

        // Apply performance-based fine-tuning
        this.applyPerformanceAdjustments(stabilityData, practiceResult);

        stabilityData.lastModifiedDate = this.getCurrentDate().toISOString();
    }

    /**
     * Calculates how much stability should increase on successful recall
     */
    calculateStabilityIncrease(currentStability, retrievability, difficulty) {
        // Lower retrievability = harder recall = more stability gain
        const retrievabilityFactor = Math.pow(1.0 - retrievability + 0.1, 0.5);

        // Higher difficulty = less stability gain
        const difficultyFactor = 1.0 - (difficulty * 0.3);

        // Base growth factor adjusted by conditions
        return this.STABILITY_GROWTH_FACTOR * retrievabilityFactor * difficultyFactor;
    }

    /**
     * Calculates current retrievability based on forgetting curve
     * R(t) = e^(t * ln(0.5) / S) where S is stability
     */
    calculateRetrievability(stability, daysSinceReview) {
        if (daysSinceReview <= 0) return 1.0;
        if (stability <= 0) return 0.1;

        // Forgetting curve: R(t) = e^(t * ln(0.5) / S)
        const exponent = daysSinceReview * Math.log(0.5) / stability;
        return Math.max(0.01, Math.exp(exponent));
    }

    /**
     * Determines if a practice session represents successful recall
     */
    determineRecallSuccess(practiceResult) {
        // Multiple success criteria
        const hasGoodPerformance = practiceResult.performanceScore >= 6.0;
        const hasSuccessfulReps = practiceResult.repetitions > 0;
        const hasTargetReached = practiceResult.sessionOutcome && 
            practiceResult.sessionOutcome.includes('TargetReached');
        const hasReasonableTime = practiceResult.duration >= 60000; // 1 minute in ms

        // Success if multiple criteria are met
        let successCriteria = 0;
        if (hasGoodPerformance) successCriteria++;
        if (hasSuccessfulReps) successCriteria++;
        if (hasTargetReached) successCriteria++;
        if (hasReasonableTime) successCriteria++;

        return successCriteria >= 2; // At least 2 out of 4 criteria
    }

    /**
     * Calculates target interval to reach desired retrievability
     */
    calculateTargetInterval(stability, targetRetrievability) {
        if (targetRetrievability >= 1.0) return 0.0;
        if (targetRetrievability <= 0.01) return stability * 10; // Very long interval

        // Solve for t in: R(t) = e^(t * ln(0.5) / S) = targetRetrievability
        // t = S * ln(targetRetrievability) / ln(0.5)
        const logTarget = Math.log(targetRetrievability);
        if (!isFinite(logTarget)) {
            console.error('Invalid targetRetrievability for log calculation:', targetRetrievability);
            return stability * 2; // Reasonable fallback
        }
        
        const interval = stability * logTarget / Math.log(0.5);
        
        if (!isFinite(interval)) {
            console.error('Infinity detected in interval calculation. stability:', stability, 'targetRetrievability:', targetRetrievability);
            return stability * 2; // Fallback
        }
        
        return Math.max(0.1, Math.min(stability * 5, interval)); // Reasonable bounds
    }

    /**
     * Applies difficulty-based adjustment to intervals
     */
    applyDifficultyAdjustment(baseInterval, difficulty) {
        // Higher difficulty = shorter intervals
        const difficultyMultiplier = 1.0 - (difficulty * 0.4); // 0.6 to 1.0 range
        return Math.max(0.5, baseInterval * difficultyMultiplier);
    }

    /**
     * Fine-tunes stability based on practice session performance details
     */
    applyPerformanceAdjustments(stabilityData, practiceResult) {
        // Performance-based fine adjustment
        if (practiceResult.performanceScore >= 8.0) {
            // Excellent performance - slight stability bonus
            stabilityData.stability *= 1.05;
        } else if (practiceResult.performanceScore <= 4.0) {
            // Poor performance - slight stability penalty
            stabilityData.stability *= 0.95;
        }

        // Duration-based adjustment
        const sessionMinutes = practiceResult.duration / (1000 * 60);
        if (sessionMinutes < 2.0) {
            // Very short session - might not be consolidated well
            stabilityData.stability *= 0.98;
        } else if (sessionMinutes > 15.0) {
            // Long session - good consolidation
            stabilityData.stability *= 1.02;
        }
    }

    /**
     * Calculates overall retention strength (0-100)
     */
    calculateRetentionStrength(stabilityData) {
        // Combine stability and review count for overall strength
        const stabilityScore = Math.min(100, stabilityData.stability * 5); // 20 days = 100%
        const experienceScore = Math.min(100, stabilityData.reviewCount * 10); // 10 reviews = 100%
        const difficultyScore = (1.0 - stabilityData.difficulty) * 100;

        // Weighted average
        return (stabilityScore * 0.5 + experienceScore * 0.3 + difficultyScore * 0.2);
    }

    /**
     * Calculates learning progress (0-100)
     */
    calculateLearningProgress(stabilityData) {
        // Progress based on stability growth from initial value
        const stabilityProgress = Math.min(100,
            (stabilityData.stability / this.INITIAL_STABILITY_DAYS - 1.0) * 20);

        // Progress based on difficulty reduction
        const difficultyProgress = (this.DEFAULT_DIFFICULTY - stabilityData.difficulty) * 200;

        // Progress based on review experience
        const experienceProgress = Math.min(100, stabilityData.reviewCount * 5);

        return Math.max(0, (stabilityProgress + difficultyProgress + experienceProgress) / 3);
    }

    // ========================================================================
    // DATA PERSISTENCE
    // ========================================================================

    loadStabilityData() {
        try {
            if (!this.profileName) {
                this.stabilityDatabase = new Map();
                return;
            }

            const key = `${this.storagePrefix}${this.profileName}_stability`;
            const jsonContent = localStorage.getItem(key);

            if (jsonContent) {
                const dataList = JSON.parse(jsonContent);
                this.stabilityDatabase = new Map(dataList.map(d => [d.barSectionId, d]));
                console.log(`[MemoryStability] Loaded ${this.stabilityDatabase.size} memory stability records`);
            } else {
                this.stabilityDatabase = new Map();
            }
        } catch (error) {
            console.error('[MemoryStability] Error loading memory stability data:', error);
            this.stabilityDatabase = new Map();
        }
    }

    saveStabilityData() {
        try {
            if (!this.profileName) {
                console.warn('[MemoryStability] Cannot save: missing profile name');
                return;
            }

            const key = `${this.storagePrefix}${this.profileName}_stability`;
            const dataList = Array.from(this.stabilityDatabase.values());
            const jsonContent = JSON.stringify(dataList);

            localStorage.setItem(key, jsonContent);
        } catch (error) {
            console.error('[MemoryStability] Error saving memory stability data:', error);
        }
    }

    // ========================================================================
    // UTILITY METHODS
    // ========================================================================

    getCurrentDate() {
        return new Date();
    }

    addDaysToDate(date, days) {
        const result = new Date(date);
        result.setDate(result.getDate() + days);
        return result;
    }
}

// Export for use in other modules
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { MemoryStabilityManager };
}