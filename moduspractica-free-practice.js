// ============================================================================
// ModusPractica Free Practice - Timer for Unscheduled Practice
// Copyright © 2025 Frank De Baere - All Rights Reserved
// ============================================================================

class FreePracticeTimer {
    constructor() {
        this.startTime = null;
        this.elapsedTime = 0;
        this.timerInterval = null;
        this.isRunning = false;
        this.isManuallyEditing = false;
        this.storagePrefix = 'mp_';
        this.currentProfile = null;
        
        this.init();
    }

    init() {
        console.log('⏱️ Initializing Free Practice Timer...');
        this.loadCurrentProfile();
        this.setupEventListeners();
        this.setupAutoSave();
        this.updateDisplay();
        this.setupCleanup();
        console.log('✅ Timer initialized');
    }

    setupCleanup() {
        // Cleanup timers on page unload to prevent memory leaks
        window.addEventListener('beforeunload', () => this.cleanup());
        window.addEventListener('pagehide', () => this.cleanup());
    }

    cleanup() {
        if (this.timerInterval) {
            clearInterval(this.timerInterval);
            this.timerInterval = null;
            console.log('🧹 Free practice timer cleaned up');
        }
    }

    setupAutoSave() {
        // No auto-save needed for free practice timer
    }

    loadCurrentProfile() {
        const lastProfileId = localStorage.getItem(this.storagePrefix + 'lastProfile');
        if (!lastProfileId) {
            console.warn('⚠️ No active profile found');
            return;
        }

        const profilesJson = localStorage.getItem(this.storagePrefix + 'profiles');
        if (profilesJson) {
            const profiles = JSON.parse(profilesJson);
            this.currentProfile = profiles.find(p => p.id === lastProfileId);
            console.log('👤 Loaded profile:', this.currentProfile?.name);
        }
    }

    setupEventListeners() {
        const btnStart = document.getElementById('btnStart');
        const btnPause = document.getElementById('btnPause');
        const btnReset = document.getElementById('btnReset');
        const btnSave = document.getElementById('btnSave');
        const display = document.getElementById('timerDisplay');

        if (!btnStart || !btnPause || !btnReset || !btnSave || !display) {
            console.error('❌ Could not find required DOM elements');
            console.log('btnStart:', btnStart);
            console.log('btnPause:', btnPause);
            console.log('btnReset:', btnReset);
            console.log('btnSave:', btnSave);
            console.log('timerDisplay:', display);
            return;
        }

        btnStart.addEventListener('click', () => this.start());
        btnPause.addEventListener('click', () => this.pause());
        btnReset.addEventListener('click', () => this.reset());
        btnSave.addEventListener('click', () => this.saveAndClose());
        
        // Manual time editing
        display.addEventListener('click', () => this.enableManualEdit());
        display.addEventListener('blur', () => this.saveManualTime());
        display.addEventListener('keypress', (e) => this.handleTimeKeypress(e));
        
        console.log('✅ Event listeners attached successfully');
    }

    start() {
        if (this.isRunning) return;
        
        this.isRunning = true;
        this.startTime = Date.now() - this.elapsedTime;
        
        this.timerInterval = setInterval(() => {
            this.elapsedTime = Date.now() - this.startTime;
            this.updateDisplay();
        }, 100); // Update every 100ms for smooth display
        
        // Update button states
        document.getElementById('btnStart').disabled = true;
        document.getElementById('btnPause').disabled = false;
        
        console.log('▶️ Timer started');
    }

    pause() {
        if (!this.isRunning) return;
        
        this.isRunning = false;
        clearInterval(this.timerInterval);
        this.timerInterval = null;
        
        // Update button states
        document.getElementById('btnStart').disabled = false;
        document.getElementById('btnPause').disabled = true;
        
        console.log('⏸️ Timer paused at', this.formatTime(this.elapsedTime));
    }

    reset() {
        // Confirm if timer has significant time
        if (this.elapsedTime > 60000) { // More than 1 minute
            if (!confirm('Are you sure you want to reset the timer? Your current time will be lost.')) {
                return;
            }
        }
        
        this.pause();
        this.elapsedTime = 0;
        this.startTime = null;
        this.updateDisplay();
        
        // Update button states
        document.getElementById('btnStart').disabled = false;
        document.getElementById('btnPause').disabled = true;
        
        console.log('🔄 Timer reset');
    }

    async saveAndClose() {
        // Stop timer if running
        if (this.isRunning) {
            this.pause();
        }

        // Check if there's time to save
        if (this.elapsedTime === 0) {
            if (confirm('No time recorded. Close without saving?')) {
                window.close();
            }
            return;
        }

        // Save the practice time
        if (await this.savePracticeTime()) {
            alert(`Practice time saved: ${this.formatTime(this.elapsedTime)}\n\nYour progress has been recorded!`);
            window.close();
        }
    }

    async savePracticeTime() {
        if (!this.currentProfile) {
            alert('No active profile found. Please select a profile first.');
            return false;
        }

        try {
            // Load profile data
            const dataKey = this.storagePrefix + this.currentProfile.id + '_data';
            const profileData = JSON.parse(localStorage.getItem(dataKey) || '{"musicPieces":[],"practiceHistory":[]}');

            // Ensure practiceHistory array exists
            if (!profileData.practiceHistory) {
                profileData.practiceHistory = [];
            }

            // Calculate duration in minutes
            const durationMinutes = this.elapsedTime / (1000 * 60);

            // Create history entry for free practice
            const historyEntry = {
                id: this.generateGUID(),
                musicPieceId: null,
                musicPieceTitle: 'Free Practice',
                barSectionId: null,
                barSectionRange: 'N/A',
                date: new Date().toISOString(),
                durationMinutes: durationMinutes,
                repetitions: 0,
                difficulty: 'Easy',
                notes: 'Unscheduled practice session',
                attemptsTillSuccess: 0,
                totalFailures: 0,
                sessionOutcome: 'FreePractice',
                targetRepetitions: 0,
                isDeleted: false
            };

            // Add to history
            profileData.practiceHistory.push(historyEntry);

            // Limit history to prevent storage overflow
            const maxHistoryRecords = 5000;
            if (profileData.practiceHistory.length > maxHistoryRecords) {
                profileData.practiceHistory.sort((a, b) => new Date(b.date) - new Date(a.date));
                profileData.practiceHistory = profileData.practiceHistory.slice(0, maxHistoryRecords);
            }

            // Update statistics
            if (!profileData.statistics) {
                profileData.statistics = { totalSessions: 0, totalPracticeTime: 0 };
            }
            profileData.statistics.totalSessions = (profileData.statistics.totalSessions || 0) + 1;
            profileData.statistics.totalPracticeTime = (profileData.statistics.totalPracticeTime || 0) + durationMinutes;

            // Save back to localStorage with quota check
            try {
                storageQuotaManager.safeSetItem(dataKey, JSON.stringify(profileData));
            } catch (error) {
                if (error.name === 'QuotaExceededError') {
                    console.warn('Storage quota exceeded, attempting cleanup...');
                    storageQuotaManager.emergencyCleanup();
                    try {
                        storageQuotaManager.safeSetItem(dataKey, JSON.stringify(profileData));
                    } catch (retryError) {
                        alert('⚠️ Opslag vol! Exporteer je data en ruim oude profielen op.');
                        throw retryError;
                    }
                }
            }
            
            // Mark as having unsaved changes
            sessionStorage.setItem(this.storagePrefix + 'hasUnsavedChanges', 'true');

            console.log('💾 Free practice time saved:', {
                duration: this.formatTime(this.elapsedTime),
                minutes: durationMinutes.toFixed(2),
                profile: this.currentProfile.name
            });
            
            // Data already saved to localStorage above
            return true;
        } catch (error) {
            console.error('❌ Error saving practice time:', error);
            alert('Failed to save practice time. Please try again.');
            return false;
        }
    }

    // GUID generation now in moduspractica-utils.js
    generateGUID() {
        return generateGUID(); // Delegate to global utility function
    }

    updateDisplay() {
        const display = document.getElementById('timerDisplay');
        display.textContent = this.formatTime(this.elapsedTime);
    }

    formatTime(milliseconds) {
        const totalSeconds = Math.floor(milliseconds / 1000);
        const hours = Math.floor(totalSeconds / 3600);
        const minutes = Math.floor((totalSeconds % 3600) / 60);
        const seconds = totalSeconds % 60;
        
        return `${this.pad(hours)}:${this.pad(minutes)}:${this.pad(seconds)}`;
    }

    pad(num) {
        return num.toString().padStart(2, '0');
    }

    // ========================================================================
    // MANUAL TIME EDITING
    // ========================================================================

    enableManualEdit() {
        // Pause timer if running
        if (this.isRunning) {
            this.pause();
        }
        
        this.isManuallyEditing = true;
        const display = document.getElementById('timerDisplay');
        
        // Focus the element so user can edit
        display.focus();
        
        console.log('✏️ Manual edit mode enabled');
    }

    handleTimeKeypress(event) {
        // Allow Enter to finish editing
        if (event.key === 'Enter') {
            event.preventDefault();
            document.getElementById('timerDisplay').blur();
            return;
        }
        
        // Allow only numbers and colon
        if (!/[\d:]/.test(event.key)) {
            event.preventDefault();
        }
    }

    saveManualTime() {
        if (!this.isManuallyEditing) return;
        
        this.isManuallyEditing = false;
        const display = document.getElementById('timerDisplay');
        const timeText = display.textContent.trim();
        
        // Parse the time format HH:MM:SS
        const parts = timeText.split(':');
        
        if (parts.length !== 3) {
            alert('Invalid time format. Please use HH:MM:SS (e.g., 00:15:30)');
            this.updateDisplay();
            return;
        }
        
        const hours = parseInt(parts[0]) || 0;
        const minutes = parseInt(parts[1]) || 0;
        const seconds = parseInt(parts[2]) || 0;
        
        // Validate ranges
        if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59 || seconds < 0 || seconds > 59) {
            alert('Invalid time values. Hours: 0-23, Minutes: 0-59, Seconds: 0-59');
            this.updateDisplay();
            return;
        }
        
        // Calculate total milliseconds
        const newElapsedTime = (hours * 3600 + minutes * 60 + seconds) * 1000;
        
        // Update timer state
        this.elapsedTime = newElapsedTime;
        this.startTime = Date.now() - newElapsedTime;
        
        // Update display
        this.updateDisplay();
        
        console.log('⏱️ Manual time set:', {
            hours, minutes, seconds,
            totalMs: newElapsedTime,
            formatted: timeText
        });
    }
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    new FreePracticeTimer();
});
