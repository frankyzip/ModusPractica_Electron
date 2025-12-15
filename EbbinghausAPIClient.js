// ============================================================================
// Ebbinghaus API Client - Server-Side Calculation Interface
// Copyright © 2025 Frank De Baere - Partura Music™
// Licensed under AGPL-3.0-or-later (see LICENSE-AGPL)
// Commercial license available (see LICENSE-COMMERCIAL)
// ============================================================================

/**
 * EbbinghausAPIClient - Handles all communication with server-side Ebbinghaus API
 * 
 * Purpose:
 * - Protects proprietary algorithm by moving calculations server-side
 * - Maintains local-first architecture (user data stays in localStorage)
 * - Provides fallback calculations for offline scenarios
 * - Implements retry logic and error handling
 * 
 * Usage:
 * const apiClient = new EbbinghausAPIClient();
 * const result = await apiClient.calculateInterval(sectionData, sessionResult, ...);
 */
class EbbinghausAPIClient {
    constructor(options = {}) {
        // API configuration
        this.baseURL = options.baseURL || 'https://api.parturamusic.be/v1/ebbinghaus';
        this.apiKey = options.apiKey || this._getAPIKey();
        this.timeout = options.timeout || 10000; // 10 seconds
        
        // Retry configuration
        this.maxRetries = options.maxRetries || 3;
        this.retryDelays = options.retryDelays || [1000, 2000, 5000]; // ms
        
        // Offline queue for failed requests
        this.offlineQueue = [];
        this.offlineQueueKey = 'mp_api_offline_queue';
        
        // Load offline queue from localStorage
        this._loadOfflineQueue();
        
        // Check connectivity
        this._setupConnectivityMonitoring();
        
        console.log('🔌 EbbinghausAPIClient initialized');
    }

    // ========================================================================
    // PUBLIC API METHODS
    // ========================================================================

    /**
     * Calculate next review interval after practice session
     * 
     * @param {Object} params - Calculation parameters
     * @param {Object} params.sectionData - Section information (difficulty, stage, etc.)
     * @param {Object} params.sessionResult - Practice session results
     * @param {Array} params.practiceHistory - Previous practice sessions
     * @param {Object} params.demographics - User age and experience level
     * @param {Object} params.calibrationData - Optional personalized calibration
     * @returns {Promise<Object>} Calculation result with nextReviewDate, tau, etc.
     */
    async calculateInterval(params) {
        const endpoint = '/calculate-interval';
        
        try {
            console.log('📊 Calculating interval via API...');
            
            // Validate required parameters
            this._validateCalculateIntervalParams(params);
            
            // Prepare request payload
            const payload = this._buildCalculateIntervalPayload(params);
            
            // Call API with retry logic
            const result = await this._callAPIWithRetry(endpoint, payload);
            
            console.log('✅ API calculation successful:', {
                nextReview: result.calculation.nextReviewDate,
                interval: result.calculation.intervalDays,
                tau: result.calculation.adjustedTau
            });
            
            return result;
            
        } catch (error) {
            console.error('❌ API calculation failed:', error.message);
            
            // Queue for later retry if network error
            if (this._isNetworkError(error)) {
                this._queueForRetry(endpoint, params);
            }
            
            // Use fallback calculation
            return this._calculateFallback(params);
        }
    }

    /**
     * Initialize a new section with demographic-adjusted tau
     * 
     * @param {Object} params - Initialization parameters
     * @param {Object} params.sectionData - Section difficulty and target repetitions
     * @param {Object} params.demographics - User age and experience level
     * @returns {Promise<Object>} Initial tau and first review date
     */
    async initializeSection(params) {
        const endpoint = '/initialize-section';
        
        try {
            console.log('🆕 Initializing new section via API...');
            
            const payload = {
                sectionData: params.sectionData,
                demographics: params.demographics
            };
            
            const result = await this._callAPIWithRetry(endpoint, payload);
            
            console.log('✅ Section initialized:', {
                initialTau: result.initialization.initialTau,
                firstReview: result.initialization.firstReviewDate
            });
            
            return result;
            
        } catch (error) {
            console.error('❌ Section initialization failed:', error.message);
            
            // Use fallback initialization
            return this._initializeFallback(params);
        }
    }

    /**
     * Batch calculate intervals for multiple sections
     * 
     * @param {Object} params - Batch parameters
     * @param {Object} params.demographics - User demographics
     * @param {Array} params.sections - Array of section calculation requests
     * @returns {Promise<Object>} Batch results
     */
    async batchCalculate(params) {
        const endpoint = '/batch-calculate';
        
        try {
            console.log(`📦 Batch calculating ${params.sections.length} sections...`);
            
            const payload = {
                demographics: params.demographics,
                sections: params.sections
            };
            
            const result = await this._callAPIWithRetry(endpoint, payload);
            
            console.log(`✅ Batch calculation complete: ${result.results.length} sections processed`);
            
            return result;
            
        } catch (error) {
            console.error('❌ Batch calculation failed:', error.message);
            
            // Fallback: calculate each section individually
            return this._batchCalculateFallback(params);
        }
    }

    /**
     * Check API health status
     * 
     * @returns {Promise<Object>} Health status
     */
    async checkHealth() {
        try {
            const response = await fetch(`${this.baseURL}/health`, {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json'
                },
                signal: AbortSignal.timeout(5000)
            });
            
            if (!response.ok) {
                throw new Error(`Health check failed: ${response.status}`);
            }
            
            const data = await response.json();
            console.log('💚 API healthy:', data);
            return data;
            
        } catch (error) {
            console.warn('⚠️ API health check failed:', error.message);
            return { status: 'unavailable', error: error.message };
        }
    }

    /**
     * Process offline queue (retry failed requests)
     * 
     * @returns {Promise<Object>} Processing results
     */
    async processOfflineQueue() {
        if (this.offlineQueue.length === 0) {
            console.log('✅ Offline queue is empty');
            return { processed: 0, failed: 0 };
        }
        
        console.log(`🔄 Processing ${this.offlineQueue.length} queued requests...`);
        
        const results = {
            processed: 0,
            failed: 0,
            errors: []
        };
        
        // Process each queued request
        const queue = [...this.offlineQueue];
        this.offlineQueue = [];
        
        for (const item of queue) {
            try {
                const result = await this.calculateInterval(item.params);
                results.processed++;
                
                // Emit event for UI update
                this._emitQueueProcessed(item, result);
                
            } catch (error) {
                results.failed++;
                results.errors.push({
                    item: item,
                    error: error.message
                });
                
                // Re-queue if still network error
                if (this._isNetworkError(error)) {
                    this.offlineQueue.push(item);
                }
            }
        }
        
        this._saveOfflineQueue();
        
        console.log(`✅ Queue processed: ${results.processed} success, ${results.failed} failed`);
        return results;
    }

    // ========================================================================
    // PRIVATE HELPER METHODS
    // ========================================================================

    /**
     * Call API with retry logic
     */
    async _callAPIWithRetry(endpoint, payload, retryCount = 0) {
        try {
            return await this._callAPI(endpoint, payload);
            
        } catch (error) {
            // Check if we should retry
            if (retryCount < this.maxRetries && this._isRetryableError(error)) {
                const delay = this.retryDelays[retryCount] || 5000;
                console.log(`🔄 Retrying API call in ${delay}ms (attempt ${retryCount + 1}/${this.maxRetries})...`);
                
                await this._sleep(delay);
                return this._callAPIWithRetry(endpoint, payload, retryCount + 1);
            }
            
            throw error;
        }
    }

    /**
     * Make actual API call
     */
    async _callAPI(endpoint, payload) {
        const url = `${this.baseURL}${endpoint}`;
        
        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-API-Key': this.apiKey
            },
            body: JSON.stringify(payload),
            signal: AbortSignal.timeout(this.timeout)
        });
        
        if (!response.ok) {
            const errorData = await response.json().catch(() => ({}));
            throw new APIError(
                errorData.error?.message || `HTTP ${response.status}`,
                response.status,
                errorData.error?.code
            );
        }
        
        const data = await response.json();
        
        if (!data.success) {
            throw new APIError(
                data.error?.message || 'API returned success=false',
                500,
                data.error?.code
            );
        }
        
        return data;
    }

    /**
     * Build payload for calculate-interval endpoint
     */
    _buildCalculateIntervalPayload(params) {
        return {
            sectionData: {
                barSectionId: params.sectionData.barSectionId,
                difficulty: params.sectionData.difficulty,
                practiceScheduleStage: params.sectionData.practiceScheduleStage || 0,
                targetRepetitions: (params.sectionData.targetRepetitions && params.sectionData.targetRepetitions > 0)
                    ? params.sectionData.targetRepetitions
                    : 6,
                lastTau: params.sectionData.lastTau,
                memoryStabilityS: params.sectionData.memoryStabilityS || 1.0
            },
            sessionResult: {
                date: params.sessionResult.date || new Date().toISOString(),
                duration: params.sessionResult.duration,
                performance: params.sessionResult.performance,
                repetitionsAchieved: params.sessionResult.repetitionsAchieved,
                failedAttempts: params.sessionResult.failedAttempts,
                totalFailures: params.sessionResult.totalFailures,
                memoryFailures: params.sessionResult.memoryFailures || params.sessionResult.totalFailures,
                executionFailures: params.sessionResult.executionFailures || params.sessionResult.attemptsTillSuccess || params.sessionResult.failedAttempts,
                notes: params.sessionResult.notes || ''
            },
            practiceHistory: params.practiceHistory || [],
            demographics: params.demographics || {},
            calibrationData: params.calibrationData || null
        };
    }

    /**
     * Validate required parameters
     */
    _validateCalculateIntervalParams(params) {
        if (!params.sectionData) {
            throw new Error('sectionData is required');
        }
        if (!params.sessionResult) {
            throw new Error('sessionResult is required');
        }
        if (!params.sectionData.difficulty) {
            throw new Error('sectionData.difficulty is required');
        }
        if (!params.sessionResult.performance) {
            throw new Error('sessionResult.performance is required');
        }
    }

    /**
     * Fallback calculation when API unavailable
     * Uses simplified Ebbinghaus logic based on previous interval
     */
    _calculateFallback(params) {
        console.warn('⚠️ Using fallback calculation (API unavailable)');
        
        const { sectionData, sessionResult, practiceHistory } = params;
        
        // Get last interval from history
        const lastInterval = this._getLastInterval(practiceHistory);
        
        // Calculate new interval based on performance
        let intervalMultiplier = 2.0; // Default: double interval
        
        switch (sessionResult.performance) {
            case 'PERFECT':
                intervalMultiplier = 2.5;
                break;
            case 'GOOD':
                intervalMultiplier = 2.0;
                break;
            case 'STRUGGLED':
                intervalMultiplier = 1.3;
                break;
            default:
                intervalMultiplier = 1.5;
        }
        
        // Calculate new interval (capped at 30 days for safety)
        let newInterval = Math.round(lastInterval * intervalMultiplier);
        newInterval = Math.max(1, Math.min(newInterval, 30));
        
        // Calculate next review date
        const nextReviewDate = new Date();
        nextReviewDate.setDate(nextReviewDate.getDate() + newInterval);
        nextReviewDate.setHours(0, 0, 0, 0);
        
        return {
            success: true,
            calculation: {
                nextReviewDate: nextReviewDate.toISOString(),
                intervalDays: newInterval,
                adjustedTau: sectionData.lastTau || 3.0,
                targetRetention: 0.80,
                newStage: sectionData.practiceScheduleStage || 0,
                memoryStabilityS: sectionData.memoryStabilityS || 1.0,
                fallbackMode: true
            },
            factors: {
                demographicTau: 3.0,
                experienceFactor: 1.0,
                difficultyModifier: 1.0,
                stabilityAdjustment: 1.0,
                confidenceScore: 0.5
            },
            recommendations: {
                suggestedDifficulty: sectionData.difficulty,
                estimatedRetention: 0.75,
                confidenceInterval: [0.70, 0.80]
            },
            metadata: {
                calculatedAt: new Date().toISOString(),
                algorithmVersion: '2.0.0-fallback',
                fallback: true
            }
        };
    }

    /**
     * Fallback initialization for new sections
     */
    _initializeFallback(params) {
        console.warn('⚠️ Using fallback initialization');
        
        const { demographics } = params;
        
        // Calculate experience-adjusted base tau
        let baseTau = 3.0; // Base tau in days
        
        if (demographics?.experienceLevel) {
            switch (demographics.experienceLevel.toUpperCase()) {
                case 'BEGINNER':
                    baseTau *= 0.85;
                    break;
                case 'INTERMEDIATE':
                    baseTau *= 1.0;
                    break;
                case 'ADVANCED':
                    baseTau *= 1.15;
                    break;
                case 'PROFESSIONAL':
                    baseTau *= 1.30;
                    break;
            }
        }
        
        // First interval = 3 days (conservative for new sections)
        const initialInterval = 3;
        const firstReviewDate = new Date();
        firstReviewDate.setDate(firstReviewDate.getDate() + initialInterval);
        firstReviewDate.setHours(0, 0, 0, 0);
        
        return {
            success: true,
            initialization: {
                initialTau: baseTau,
                firstReviewDate: firstReviewDate.toISOString(),
                initialInterval: initialInterval,
                targetRetention: 0.80,
                initialStage: 0,
                fallbackMode: true
            },
            metadata: {
                calculatedAt: new Date().toISOString(),
                algorithmVersion: '2.0.0-fallback',
                fallback: true
            }
        };
    }

    /**
     * Fallback batch calculation
     */
    async _batchCalculateFallback(params) {
        console.warn('⚠️ Using fallback batch calculation');
        
        const results = [];
        
        for (const section of params.sections) {
            const result = this._calculateFallback({
                sectionData: section.sectionData,
                sessionResult: section.sessionResult,
                practiceHistory: section.practiceHistory,
                demographics: params.demographics
            });
            
            results.push({
                sectionId: section.sectionId,
                ...result
            });
        }
        
        return {
            success: true,
            results: results,
            metadata: {
                calculatedAt: new Date().toISOString(),
                processedCount: results.length,
                fallback: true
            }
        };
    }

    /**
     * Get last interval from practice history
     */
    _getLastInterval(history) {
        if (!history || history.length === 0) {
            return 3; // Default: 3 days
        }
        
        const lastSession = history[history.length - 1];
        return lastSession.interval || 3;
    }

    /**
     * Check if error is retryable
     */
    _isRetryableError(error) {
        return this._isNetworkError(error) || 
               error.statusCode === 429 || // Rate limit
               error.statusCode >= 500;    // Server error
    }

    /**
     * Check if error is network-related
     */
    _isNetworkError(error) {
        return error.name === 'AbortError' ||
               error.name === 'TypeError' ||
               error.message.includes('fetch') ||
               error.message.includes('network') ||
               error.message.includes('timeout');
    }

    /**
     * Queue failed request for later retry
     */
    _queueForRetry(endpoint, params) {
        this.offlineQueue.push({
            endpoint: endpoint,
            params: params,
            timestamp: new Date().toISOString(),
            retryCount: 0
        });
        
        this._saveOfflineQueue();
        console.log(`📥 Queued request for later retry (queue size: ${this.offlineQueue.length})`);
    }

    /**
     * Save offline queue to localStorage
     */
    _saveOfflineQueue() {
        try {
            localStorage.setItem(this.offlineQueueKey, JSON.stringify(this.offlineQueue));
        } catch (error) {
            console.error('Failed to save offline queue:', error);
        }
    }

    /**
     * Load offline queue from localStorage
     */
    _loadOfflineQueue() {
        try {
            const queueJson = localStorage.getItem(this.offlineQueueKey);
            if (queueJson) {
                this.offlineQueue = JSON.parse(queueJson);
                console.log(`📥 Loaded ${this.offlineQueue.length} queued requests from storage`);
            }
        } catch (error) {
            console.error('Failed to load offline queue:', error);
            this.offlineQueue = [];
        }
    }

    /**
     * Setup connectivity monitoring
     */
    _setupConnectivityMonitoring() {
        // Monitor online/offline events
        window.addEventListener('online', () => {
            console.log('🌐 Connection restored');
            this.processOfflineQueue();
        });
        
        window.addEventListener('offline', () => {
            console.log('📡 Connection lost - switching to fallback mode');
        });
    }

    /**
     * Emit custom event when queue item is processed
     */
    _emitQueueProcessed(item, result) {
        const event = new CustomEvent('ebbinghaus-queue-processed', {
            detail: { item, result }
        });
        window.dispatchEvent(event);
    }

    /**
     * Get API key from environment or localStorage
     */
    _getAPIKey() {
        // Try localStorage first (set during deployment)
        const storedKey = localStorage.getItem('mp_api_key');
        if (storedKey) return storedKey;
        
        // Development fallback
        if (window.location.hostname === 'localhost') {
            return 'dev-api-key-12345';
        }
        
        throw new Error('API key not configured');
    }

    /**
     * Sleep helper for retry delays
     */
    _sleep(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }
}

/**
 * Custom error class for API errors
 */
class APIError extends Error {
    constructor(message, statusCode, errorCode) {
        super(message);
        this.name = 'APIError';
        this.statusCode = statusCode;
        this.errorCode = errorCode;
    }
}

// Export for use in other modules
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { EbbinghausAPIClient, APIError };
}
