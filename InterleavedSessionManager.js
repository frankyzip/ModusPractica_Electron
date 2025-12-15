// ============================================================================
// InterleavedSessionManager.js
// Manages randomized circuit review of today's practiced chunks
// Copyright © 2025 Frank De Baere - Partura Music™
// Licensed under AGPL-3.0-or-later (see LICENSE-AGPL)
// Commercial license available (see LICENSE-COMMERCIAL)
// ============================================================================

/**
 * InterleavedSessionManager handles the Interleaved Practice mode where users
 * review all chunks they practiced today in a randomized circuit.
 * 
 * Based on pedagogical principle: Interleaved practice improves retention
 * compared to blocked practice (studying one topic at a time).
 */
class InterleavedSessionManager {
    constructor(profileData, currentProfile, storagePrefix = 'mp_') {
        this.profileData = profileData;
        this.currentProfile = currentProfile;
        this.storagePrefix = storagePrefix;
        
        // Load queue from sessionStorage
        const queueJson = sessionStorage.getItem('mp_interleaved_queue');
        if (!queueJson) {
            throw new Error('No interleaved queue found in sessionStorage');
        }
        
        this.sectionQueue = JSON.parse(queueJson);
        console.log('🔀 Loaded interleaved queue:', this.sectionQueue);
        
        // Shuffle the queue for randomization
        this.shuffleQueue();
        
        // Current position in circuit
        this.currentIndex = 0;
        this.circuitResults = {}; // { sectionId: { reps, failures, duration, notes } }
        
        // Default time per slot (3 minutes = 180 seconds)
        this.defaultSlotDuration = 180;
        
        // Circuit completion flag
        this.circuitCompleted = false;
        
        console.log(`✅ InterleavedSessionManager initialized with ${this.sectionQueue.length} chunks`);
    }
    
    /**
     * Fisher-Yates shuffle algorithm
     */
    shuffleQueue() {
        for (let i = this.sectionQueue.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [this.sectionQueue[i], this.sectionQueue[j]] = [this.sectionQueue[j], this.sectionQueue[i]];
        }
        console.log('🎲 Queue shuffled:', this.sectionQueue);
    }
    
    /**
     * Get current section ID
     */
    getCurrentSectionId() {
        return this.sectionQueue[this.currentIndex];
    }
    
    /**
     * Get current section data (piece + section)
     */
    getCurrentSection() {
        const sectionId = this.getCurrentSectionId();
        
        // Find section in all pieces
        for (const piece of this.profileData.musicPieces) {
            if (piece.barSections) {
                const section = piece.barSections.find(s => s.id === sectionId);
                if (section) {
                    return { piece, section };
                }
            }
        }
        
        return null;
    }
    
    /**
     * Save current chunk results
     */
    saveCurrentResults(data) {
        const sectionId = this.getCurrentSectionId();
        
        // Initialize if not exists
        if (!this.circuitResults[sectionId]) {
            this.circuitResults[sectionId] = {
                repetitions: 0,
                failures: 0,
                durationSeconds: 0,
                notes: '',
                attempts: 0
            };
        }
        
        // Accumulate results (user might practice same chunk multiple times in circuit)
        const result = this.circuitResults[sectionId];
        result.repetitions += data.repetitions || 0;
        result.failures += data.failures || 0;
        result.durationSeconds += data.durationSeconds || 0;
        result.notes = data.notes || result.notes; // Keep most recent notes
        result.attempts++;
        
        console.log(`💾 Saved results for section ${sectionId}:`, result);
    }
    
    /**
     * Move to next chunk in circuit
     * Returns true if more chunks remain, false if circuit complete
     */
    nextChunk() {
        this.currentIndex++;
        
        if (this.currentIndex >= this.sectionQueue.length) {
            // Circuit complete - optionally loop back
            console.log('🏁 Circuit completed!');
            this.circuitCompleted = true;
            return false;
        }
        
        return true;
    }
    
    /**
     * Skip current chunk
     */
    skipChunk() {
        console.log(`⏭️ Skipping chunk ${this.currentIndex + 1}`);
        return this.nextChunk();
    }
    
    /**
     * Get progress info
     */
    getProgress() {
        return {
            current: this.currentIndex + 1,
            total: this.sectionQueue.length,
            percentage: Math.round(((this.currentIndex + 1) / this.sectionQueue.length) * 100)
        };
    }
    
    /**
     * Finish circuit and save all results to practice history
     */
    finishCircuit() {
        console.log('🏁 Finishing interleaved circuit...');
        
        const now = new Date().toISOString();
        const historyEntries = [];
        
        // Create practice history entry for each section practiced
        for (const [sectionId, result] of Object.entries(this.circuitResults)) {
            // Find piece and section
            let piece = null;
            let section = null;
            
            for (const p of this.profileData.musicPieces) {
                if (p.barSections) {
                    const s = p.barSections.find(sec => sec.id === sectionId);
                    if (s) {
                        piece = p;
                        section = s;
                        break;
                    }
                }
            }
            
            if (!piece || !section) {
                console.warn(`⚠️ Could not find section ${sectionId}, skipping history entry`);
                continue;
            }
            
            // Create history entry
            const entry = {
                id: this.generateGuid(),
                date: now,
                musicPieceId: piece.id,
                barSectionId: section.id,
                repetitions: result.repetitions,
                memoryFailures: 0, // Interleaved mode doesn't track streak resets
                executionFailures: result.failures,
                durationMinutes: result.durationSeconds / 60,
                notes: result.notes || '',
                sessionType: 'interleaved', // Tag for analytics
                isDeleted: false
            };
            
            historyEntries.push(entry);
            
            // Update section's lastPracticeDate
            section.lastPracticeDate = now;
            
            // Update memory stability (counts as valid retrieval practice)
            if (window.memoryStabilityManager) {
                try {
                    const performanceScore = result.repetitions / (result.repetitions + result.failures);
                    window.memoryStabilityManager.recordReview(
                        sectionId,
                        performanceScore,
                        0 // daysLate = 0 since this is retrieval practice, not scheduled
                    );
                } catch (error) {
                    console.warn(`Failed to update memory stability for ${sectionId}:`, error);
                }
            }
        }
        
        // Add all entries to practice history
        if (!this.profileData.practiceHistory) {
            this.profileData.practiceHistory = [];
        }
        this.profileData.practiceHistory.push(...historyEntries);
        
        // Update statistics
        if (!this.profileData.statistics) {
            this.profileData.statistics = { totalSessions: 0, totalPracticeTime: 0 };
        }
        this.profileData.statistics.totalSessions += historyEntries.length;
        
        const totalMinutes = historyEntries.reduce((sum, entry) => sum + entry.durationMinutes, 0);
        this.profileData.statistics.totalPracticeTime += totalMinutes;
        
        // Save to localStorage
        this.saveProfileData();
        
        console.log(`✅ Saved ${historyEntries.length} interleaved practice entries`);
        
        // Clear sessionStorage queue
        sessionStorage.removeItem('mp_interleaved_queue');
        
        // Track completion
        if (window.ga4Tracker) {
            window.ga4Tracker.trackEvent('interleaved_circuit_completed', {
                chunks_practiced: historyEntries.length,
                total_duration_minutes: Math.round(totalMinutes)
            });
        }
        
        return historyEntries.length;
    }
    
    /**
     * Save profile data to localStorage
     */
    saveProfileData() {
        if (!this.currentProfile || !this.profileData) return;
        
        const key = `${this.storagePrefix}${this.currentProfile.id}_data`;
        
        try {
            window.storageQuotaManager.safeSetItem(key, JSON.stringify(this.profileData));
        } catch (error) {
            console.error('Failed to save profile data:', error);
            alert('⚠️ Could not save circuit results due to storage limit. Please free up space.');
        }
    }
    
    /**
     * Generate GUID for history entries
     */
    generateGuid() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
            const r = Math.random() * 16 | 0;
            const v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }
}
