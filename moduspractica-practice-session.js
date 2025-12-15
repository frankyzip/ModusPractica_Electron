// ModusPractica Web App - Practice Session Interface
// Copyright (c) 2025 Frank De Baere - Partura Music™
// All rights reserved.

// Global state
let currentProfile = null;
let currentPiece = null;
let currentSection = null;
let sectionId = null;
const storagePrefix = 'mp_';

// Adaptive learning systems (global instances)
let adaptiveTauManager = null;
let personalizedMemoryCalibration = null;
let memoryStabilityManager = null;

// Debug helpers (available in console for developers)
window.showCalibrationReport = function() {
    if (personalizedMemoryCalibration) {
        console.log(personalizedMemoryCalibration.getDetailedCalibrationReport());
    } else {
        console.warn('Calibration system not initialized');
    }
};

window.showMemoryStats = function(sectionId = null) {
    if (!memoryStabilityManager) {
        console.warn('Memory stability system not initialized');
        return;
    }
    
    const targetId = sectionId || currentSection?.id;
    if (!targetId) {
        console.warn('No section ID provided or current section not set');
        return;
    }
    
    const stats = memoryStabilityManager.getMemoryStats(targetId);
    console.log(
        `\n${'='.repeat(60)}\n` +
        `🧠 MEMORY STABILITY STATS\n` +
        `${'='.repeat(60)}\n` +
        `Section ID: ${targetId}\n` +
        `Status: ${stats.isNew ? 'NEW' : 'TRACKED'}\n` +
        `Stability (S): ${stats.stability.toFixed(1)} days\n` +
        `Difficulty (D): ${stats.difficulty.toFixed(3)}\n` +
        `Current Retrievability (R): ${stats.currentRetrievability.toFixed(3)}\n` +
        `Review Count: ${stats.reviewCount}\n` +
        `Days Since Last Review: ${stats.daysSinceLastReview?.toFixed(1) || 'N/A'}\n` +
        `Retention Strength: ${stats.retentionStrength?.toFixed(1) || 'N/A'}%\n` +
        `Learning Progress: ${stats.learningProgress?.toFixed(1) || 'N/A'}%\n` +
        `${'='.repeat(60)}\n`
    );
};

window.exportAdaptiveData = function() {
    const data = {
        calibration: personalizedMemoryCalibration?.exportCalibrationData(),
        memoryStability: memoryStabilityManager ? 
            Array.from(memoryStabilityManager.stabilityDatabase.values()) : []
    };
    console.log('Adaptive Learning Data:', data);
    return data;
};

// Intensity Module Debug Helper (available in console for developers)
window.showIntensityReport = function() {
    if (typeof IntensityModule === 'undefined') {
        console.error('IntensityModule not loaded. Include IntensityModule.js in HTML.');
        return;
    }
    
    // Get section history
    const profileData = JSON.parse(localStorage.getItem(`mp_${currentProfile.id}_data`) || '{"practiceHistory":[]}');
    const sectionHistory = profileData.practiceHistory ? 
        profileData.practiceHistory.filter(h => h.barSectionId === currentSection.id && !h.isDeleted) : [];
    
    // Generate and log report
    const report = IntensityModule.generateIntensityReport(
        correctRepetitions,
        failedAttempts,
        sectionHistory
    );
    
    IntensityModule.logIntensityReport(report);
    return report;
};

// Timer state
let timerInterval = null;
let startTime = null;
let pausedTime = 0;
let totalElapsedSeconds = 0;
let isRunning = false;
let isManuallyEditing = false;

// Evaluation state
let selectedPerformance = null;

// Tracking metrics state
let targetRepetitions = 6;
let failedAttempts = 0;
let correctRepetitions = 0;
let streakResets = 0; // Automatic counter: memory retrieval failures

// Dr. Gebrian Overlearning state
let errorsBeforeFirstCorrect = 0; // E: fouten vóór allereerste correcte rep
let hasAchievedFirstCorrect = false; // Flag: hebben we de eerste correcte rep al bereikt?
let gebrianTargetLocked = false; // Flag: is target al berekend en vastgezet?
let overlearningIntensity = 100; // 50% of 100% overlearning (standaard 100%)

// Intensity Module state
let intensityModuleEnabled = true; // Default enabled
let currentTDS = 0;
let currentPhase = '';
let currentOLQ = null;
let adaptiveTimePerCR = null; // Adaptive calibration: learned T̄_CR per user
let adaptiveCalibrationSessions = 0; // How many sessions were used to learn T̄_CR
let userManuallySetTarget = false; // Track if user manually adjusted target reps

// Micro-Break state (Molly Gebrian methodiek)
let repsSinceLastBreak = 0; // Teller voor herhalingen sinds laatste break
let enableMicroBreaks = true; // Default enabled
let microBreakNotificationActive = false; // Track of pop-up actief is
let microBreakTimeoutId = null; // Timer reference om automatische sluiting te beheren

// Energy/Context state
let currentEnergy = 'Normal'; // Default

// Session completion and auto-save state
let sessionCompleted = false;
let autoSaveInterval = null;
let isSaving = false;
let returnDateParam = null;

// Popup helpers -------------------------------------------------------------
function isPopupContext() {
    return !!(window.opener && !window.opener.closed);
}

function notifyPracticeSessionEvent(eventName, payload = {}) {
    if (!isPopupContext()) return;
    try {
        window.opener.postMessage({
            type: 'MP_PRACTICE_SESSION_EVENT',
            event: eventName,
            payload
        }, window.location.origin);
    } catch (error) {
        console.warn('Unable to notify opener window:', error);
    }
}

function exitPracticeSession(eventName, payload = {}, fallbackUrl = 'moduspractica-dashboard.html') {
    if (eventName) {
        notifyPracticeSessionEvent(eventName, payload);
    }

    if (isPopupContext()) {
        window.close();
    } else if (fallbackUrl) {
        window.location.href = fallbackUrl;
    }
}

// =========================
// HELPER FUNCTIONS
// =========================

/**
 * Get memory zone name based on practice schedule stage
 * Stage 0-2: EXPLORATION (discovering and learning the material)
 * Stage 3-5: CONSOLIDATION (solidifying knowledge, transition phase)
 * Stage 6-8: MASTERY (well-learned, maintenance mode)
 * Stage 9+: OVERLEARNING (peak retention, extended intervals)
 */
function getMemoryZoneName(stage) {
    if (stage === null || stage === undefined || stage < 3) {
        return 'EXPLORATION';
    } else if (stage >= 3 && stage <= 5) {
        return 'CONSOLIDATION';
    } else if (stage >= 6 && stage <= 8) {
        return 'MASTERY';
    } else {
        return 'OVERLEARNING';
    }
}

// Interleaved Practice state
let interleavedMode = false;
let interleavedManager = null;

// Initialize page
document.addEventListener('DOMContentLoaded', () => {
    // Get URL parameters
    const urlParams = new URLSearchParams(window.location.search);
    const mode = urlParams.get('mode');
    sectionId = urlParams.get('section');
    const rawReturnDate = urlParams.get('returnDate');
    returnDateParam = (rawReturnDate && /^\d{4}-\d{2}-\d{2}$/.test(rawReturnDate)) ? rawReturnDate : null;

    // Check if Interleaved Practice mode is active
    if (mode === 'interleaved') {
        interleavedMode = true;
        console.log('🔀 Interleaved Practice mode activated');
    }

    // For interleaved mode, we don't need a section ID from URL
    if (!interleavedMode && !sectionId) {
        alert('No section ID provided');
        window.location.href = 'moduspractica-dashboard.html';
        return;
    }

    // Load profile and section data
    if (interleavedMode) {
        loadInterleavedSession();
    } else {
        loadProfileAndSection();
    }
    
    if (window.MPLog) MPLog.info('DOMContentLoaded: practice session', { 
        sectionId: sectionId,
        mode: interleavedMode ? 'interleaved' : 'normal'
    });
    
    // Track page view
    if (window.ga4Tracker) {
        if (interleavedMode) {
            window.ga4Tracker.trackPageView('Interleaved Practice Session');
        } else {
            window.ga4Tracker.trackPageView('Practice Session');
        }
    }
    
    // Try to load draft (only for normal mode)
    if (!interleavedMode) {
        loadDraft();
        startAutoSave();
    }
    
    // Add unsaved changes indicator to body
    document.body.insertAdjacentHTML('beforeend', 
        '<div class="unsaved-indicator" id="unsavedIndicator">💾 Unsaved Changes</div>'
    );
});

// Cleanup function to prevent memory leaks
function cleanupTimers(performFinalSave = false) {
    // Perform final save BEFORE stopping timers if requested
    if (performFinalSave && !sessionCompleted) {
        const hasData = totalElapsedSeconds > 0 || failedAttempts > 0 || 
                        correctRepetitions > 0 || streakResets > 0;
        if (hasData) {
            console.log('🔒 Performing final save before cleanup...');
            saveDraft(true); // Synchronous final save
        }
    }
    
    if (timerInterval) {
        clearInterval(timerInterval);
        timerInterval = null;
    }
    if (autoSaveInterval) {
        clearInterval(autoSaveInterval);
        autoSaveInterval = null;
    }
    console.log('🧹 Timers cleaned up');
}

// Warn user before leaving with unsaved data
window.addEventListener('beforeunload', (e) => {
    const hasData = totalElapsedSeconds > 0 || 
                    failedAttempts > 0 || 
                    correctRepetitions > 0 || 
                    streakResets > 0;    // Always cleanup timers on page unload (with final save if needed)
    cleanupTimers(true);

    if (hasData && !sessionCompleted) {
        e.preventDefault();
        e.returnValue = '';
        return 'You have unsaved practice data. Are you sure you want to leave?';
    }
});

// Also cleanup on pagehide (for mobile browsers)
window.addEventListener('pagehide', () => cleanupTimers(true));

// Load profile and section data
function loadProfileAndSection() {
    // Get current profile
    const lastProfileId = localStorage.getItem('mp_lastProfile');
    if (!lastProfileId) {
        alert('No active profile found');
        window.location.href = 'moduspractica-app.html';
        return;
    }

    // Load profiles
    const profiles = JSON.parse(localStorage.getItem('mp_profiles') || '[]');
    currentProfile = profiles.find(p => p.id === lastProfileId);

    if (!currentProfile) {
        alert('Profile not found');
        window.location.href = 'moduspractica-app.html';
        return;
    }

    // Load profile data
    let profileData = JSON.parse(localStorage.getItem(`mp_${currentProfile.id}_data`) || '{"musicPieces":[]}');
    // Normalize existing practice history structure to new fields; persist normalized data
    try {
        profileData = normalizeProfilePracticeHistory(profileData);
        storageQuotaManager.safeSetItem(`mp_${currentProfile.id}_data`, JSON.stringify(profileData));
    } catch (error) {
        // Non-fatal: normalization should not break loading
        console.warn('Could not normalize practice history fields:', error);
    }

    // Find the section in all pieces
    for (const piece of profileData.musicPieces) {
        const section = piece.barSections?.find(s => s.id === sectionId);
        if (section) {
            currentPiece = piece;
            currentSection = section;
            break;
        }
    }

    if (!currentSection) {
        alert('Section not found');
        window.location.href = 'moduspractica-dashboard.html';
        return;
    }

    // Initialize adaptive learning systems for this profile
    try {
        adaptiveTauManager = new AdaptiveTauManager();
        personalizedMemoryCalibration = new PersonalizedMemoryCalibration(storagePrefix);
        memoryStabilityManager = new MemoryStabilityManager(storagePrefix);
        
        // Initialize with profile data
        personalizedMemoryCalibration.initializeCalibrationSystem(currentProfile.id);
        memoryStabilityManager.initializeForUser(currentProfile.id);
        
        console.log('[AdaptiveLearning] Initialized adaptive systems for profile:', currentProfile.name);
        if (window.MPLog) MPLog.info('[AdaptiveLearning] Initialized adaptive systems', { profileId: currentProfile.id, name: currentProfile.name });
    } catch (error) {
        console.error('[AdaptiveLearning] Failed to initialize adaptive systems:', error);
        if (window.MPLog) MPLog.error('[AdaptiveLearning] Failed to initialize adaptive systems', { error: error && error.message });
        // Continue without adaptive systems (will use fallback calculations)
    }
    
    // Check if section is inactive (lifecycleState === 2)
    if (currentSection.lifecycleState === 2) {
        alert('This chunk is currently inactive and cannot be practiced. Please activate it first in the piece details page.');
        window.location.href = `moduspractica-piece-detail.html?id=${currentPiece.id}`;
        return;
    }
    
    // Load Intensity Module settings
    try {
        loadIntensityModuleSettings();
    } catch (error) {
        console.error('Failed to load Intensity Module settings:', error);
        // Continue without intensity module
    }

    // Render the page
    renderSessionInfo();
}

// Load Interleaved Practice session
function loadInterleavedSession() {
    console.log('🔀 Loading Interleaved Practice session...');
    
    // Load current profile
    const lastProfileId = localStorage.getItem('mp_lastProfile');
    if (!lastProfileId) {
        alert('No active profile found');
        window.location.href = 'moduspractica-app.html';
        return;
    }

    const profiles = JSON.parse(localStorage.getItem('mp_profiles') || '[]');
    currentProfile = profiles.find(p => p.id === lastProfileId);

    if (!currentProfile) {
        alert('Profile not found');
        window.location.href = 'moduspractica-app.html';
        return;
    }

    // Load profile data
    let profileData = JSON.parse(localStorage.getItem(`mp_${currentProfile.id}_data`) || '{"musicPieces":[]}');
    
    // Initialize InterleavedSessionManager
    try {
        interleavedManager = new InterleavedSessionManager(profileData, currentProfile, storagePrefix);
    } catch (error) {
        console.error('Failed to initialize InterleavedSessionManager:', error);
        alert('Kon Interleaved Practice sessie niet initialiseren. Ga terug naar dashboard.');
        window.location.href = 'moduspractica-dashboard.html';
        return;
    }
    
    // Load first section
    const firstSection = interleavedManager.getCurrentSection();
    if (!firstSection) {
        alert('Eerste chunk niet gevonden. Ga terug naar dashboard.');
        window.location.href = 'moduspractica-dashboard.html';
        return;
    }
    
    currentPiece = firstSection.piece;
    currentSection = firstSection.section;
    sectionId = currentSection.id;
    
    // Initialize adaptive learning systems
    try {
        adaptiveTauManager = new AdaptiveTauManager();
        personalizedMemoryCalibration = new PersonalizedMemoryCalibration(storagePrefix);
        memoryStabilityManager = new MemoryStabilityManager(storagePrefix);
        
        personalizedMemoryCalibration.initializeCalibrationSystem(currentProfile.id);
        memoryStabilityManager.initializeForUser(currentProfile.id);
        
        console.log('[InterleavedMode] Initialized adaptive systems');
    } catch (error) {
        console.warn('[InterleavedMode] Could not initialize adaptive systems:', error);
    }
    
    // Render UI in interleaved mode
    renderInterleavedSessionInfo();
    adaptUIForInterleavedMode();
}

// Render session info for interleaved mode
function renderInterleavedSessionInfo() {
    const progress = interleavedManager.getProgress();
    
    document.getElementById('pieceTitle').textContent = currentPiece.title;
    document.getElementById('chunkInfo').textContent = `Interleaved Circuit - Chunk ${progress.current} of ${progress.total}`;
    
    if (currentSection.description) {
        document.getElementById('chunkDescription').textContent = `${currentSection.barRange}: ${currentSection.description}`;
        document.getElementById('chunkDescription').style.display = 'block';
    } else {
        document.getElementById('chunkDescription').textContent = currentSection.barRange;
        document.getElementById('chunkDescription').style.display = 'block';
    }
    
    // Memory zone
    const stage = currentSection.practiceScheduleStage || 0;
    const zoneName = getMemoryZoneName(stage);
    document.getElementById('memoryZone').textContent = zoneName;
    
    // Last practice
    const lastPractice = currentSection.lastPracticeDate 
        ? new Date(currentSection.lastPracticeDate).toLocaleDateString(undefined) 
        : 'Never';
    document.getElementById('lastPractice').textContent = lastPractice;
    
    // Initialize tracking metrics
    updateTrackingMetrics();
    updateSuccessRateDisplay();
}

// Adapt UI for interleaved mode
function adaptUIForInterleavedMode() {
    console.log('🎨 Adapting UI for Interleaved Mode...');
    
    // Hide Intensity Module (not used in retrieval practice)
    const intensityDisplay = document.getElementById('intensityDisplay');
    if (intensityDisplay) {
        intensityDisplay.style.display = 'none';
    }
    
    // Hide target repetitions and Dr. Gebrian controls (not relevant for quick reviews)
    const targetRepsGroup = document.getElementById('targetReps')?.closest('.counter-group');
    if (targetRepsGroup) {
        targetRepsGroup.style.display = 'none';
    }
    
    // Hide overlearning intensity toggle
    const intensityToggle = document.getElementById('intensity50Btn')?.closest('.counter-group');
    if (intensityToggle) {
        intensityToggle.style.display = 'none';
    }
    
    // Change "Complete Session" button to "Next Chunk ⏭️"
    const btnComplete = document.getElementById('btnComplete');
    if (btnComplete) {
        btnComplete.textContent = 'Next Chunk ⏭️';
        btnComplete.onclick = moveToNextInterleavedChunk;
    }
    
    // Add "Finish Circuit 🏁" button
    const actionButtons = document.querySelector('.action-buttons');
    if (actionButtons && !document.getElementById('btnFinishCircuit')) {
        const finishBtn = document.createElement('button');
        finishBtn.id = 'btnFinishCircuit';
        finishBtn.className = 'btn btn-primary';
        finishBtn.textContent = 'Finish Circuit 🏁';
        finishBtn.onclick = finishInterleavedCircuit;
        actionButtons.appendChild(finishBtn);
    }
    
    // Set timer to countdown mode (default 3 minutes)
    totalElapsedSeconds = interleavedManager.defaultSlotDuration;
    updateTimerDisplay();
    
    console.log('✅ UI adapted for Interleaved Mode');
}

// Move to next chunk in interleaved circuit
function moveToNextInterleavedChunk() {
    console.log('⏭️ Moving to next chunk...');
    
    // Save current chunk results
    const currentResults = {
        repetitions: correctRepetitions,
        failures: failedAttempts,
        durationSeconds: Math.max(0, interleavedManager.defaultSlotDuration - totalElapsedSeconds),
        notes: document.getElementById('sessionNotes')?.value || ''
    };
    
    interleavedManager.saveCurrentResults(currentResults);
    
    // Move to next chunk
    const hasMore = interleavedManager.nextChunk();
    
    if (!hasMore) {
        // Circuit complete
        alert('🎉 Circuit voltooid! Alle chunks zijn gereviewd.\n\nKlik op "Finish Circuit" om de resultaten op te slaan.');
        document.getElementById('btnComplete').disabled = true;
        return;
    }
    
    // Load next section
    const nextSection = interleavedManager.getCurrentSection();
    if (!nextSection) {
        console.error('Could not load next section');
        return;
    }
    
    currentPiece = nextSection.piece;
    currentSection = nextSection.section;
    sectionId = currentSection.id;
    
    // Reset tracking metrics
    failedAttempts = 0;
    correctRepetitions = 0;
    streakResets = 0;
    totalElapsedSeconds = interleavedManager.defaultSlotDuration;
    document.getElementById('sessionNotes').value = '';
    
    // Stop timer
    if (isRunning) {
        pauseTimer();
    }
    
    // Re-render UI
    renderInterleavedSessionInfo();
    updateTimerDisplay();
    updateTrackingMetrics();
    
    console.log('✅ Moved to next chunk');
}

// Finish interleaved circuit and save all results
function finishInterleavedCircuit() {
    console.log('🏁 Finishing interleaved circuit...');
    
    // Save current chunk results (if any time was used)
    const timeUsed = interleavedManager.defaultSlotDuration - totalElapsedSeconds;
    if (timeUsed > 0 || failedAttempts > 0 || correctRepetitions > 0) {
        const currentResults = {
            repetitions: correctRepetitions,
            failures: failedAttempts,
            durationSeconds: timeUsed,
            notes: document.getElementById('sessionNotes')?.value || ''
        };
        interleavedManager.saveCurrentResults(currentResults);
    }
    
    // Save all circuit results
    const entriesCount = interleavedManager.finishCircuit();
    
    // Cleanup timers
    cleanupTimers(false); // Don't save draft (already saved via finishCircuit)
    
    // Show confirmation
    alert(`✅ Interleaved Circuit opgeslagen!\n\n${entriesCount} chunk${entriesCount === 1 ? '' : 's'} geoefend en bijgewerkt.`);
    
    // Redirect back to dashboard
    exitPracticeSession('interleaved_circuit_completed', { entriesCount }, 'moduspractica-dashboard.html');
}

// Render session info
function renderSessionInfo() {
    document.getElementById('pieceTitle').textContent = currentPiece.title;
    document.getElementById('chunkInfo').textContent = `Chunk: ${currentSection.barRange}`;
    
    if (currentSection.description) {
        document.getElementById('chunkDescription').textContent = currentSection.description;
        document.getElementById('chunkDescription').style.display = 'block';
    } else {
        document.getElementById('chunkDescription').style.display = 'none';
    }

    // Initialize target repetitions: start at 6 (default baseline)
    // Target will be automatically calculated when first correct rep is achieved
    targetRepetitions = 6;
    document.getElementById('targetReps').textContent = targetRepetitions;
    
    // Display memory zone instead of raw stage number
    const stage = currentSection.practiceScheduleStage || 0;
    const zoneName = getMemoryZoneName(stage);
    document.getElementById('memoryZone').textContent = zoneName;
    
    // Initialize tempo fields
    if (currentSection.targetTempo && currentSection.targetTempo > 0) {
        document.getElementById('targetTempo').value = currentSection.targetTempo;
    }
    
    // Show last achieved tempo and suggestion if available
    const profileData = JSON.parse(localStorage.getItem(`mp_${currentProfile.id}_data`) || '{"practiceHistory":[]}');
    if (profileData.practiceHistory) {
        const sectionHistory = profileData.practiceHistory
            .filter(h => h.barSectionId === currentSection.id && !h.isDeleted && h.achievedTempo > 0)
            .sort((a, b) => new Date(b.date) - new Date(a.date));
        
        if (sectionHistory.length > 0) {
            const lastAchieved = sectionHistory[0].achievedTempo;
            document.getElementById('lastAchievedTempo').textContent = `${lastAchieved} BPM`;
            document.getElementById('lastAchievedTempoRow').style.display = 'flex';
            
            // AUTO-FILL: Pre-fill achieved tempo with last value for consistency
            // User can override if they achieved different tempo this session
            document.getElementById('achievedTempo').value = lastAchieved;
            
            // Show progressive suggestion (+5 BPM, capped at target)
            const sectionTarget = currentSection.targetTempo || 0;
            if (sectionTarget > 0 && lastAchieved < sectionTarget) {
                const suggested = Math.min(lastAchieved + 5, sectionTarget);
                document.getElementById('tempoSuggestion').textContent = `${suggested} BPM`;
                document.getElementById('tempoSuggestionRow').style.display = 'flex';
            }
        }
    }
    
    const lastPractice = currentSection.lastPracticeDate 
        ? new Date(currentSection.lastPracticeDate).toLocaleDateString(undefined) 
        : 'Never';
    document.getElementById('lastPractice').textContent = lastPractice;

    // Load previous session notes if available
    loadPreviousNotes();

    // Initialize tracking metrics
    updateTrackingMetrics();
    updateSuccessRateDisplay();
}

// Load previous session notes
function loadPreviousNotes() {
    // Find all practice sessions for this section
    if (!currentPiece.practiceSessions || currentPiece.practiceSessions.length === 0) {
        return;
    }

    // Filter sessions for this specific section
    const sectionSessions = currentPiece.practiceSessions.filter(s => s.sectionId === currentSection.id);
    
    if (sectionSessions.length === 0) {
        return;
    }

    // Sort by date (most recent first)
    sectionSessions.sort((a, b) => new Date(b.date) - new Date(a.date));

    // Get the most recent session with notes
    const recentSessionWithNotes = sectionSessions.find(s => s.notes && s.notes.trim().length > 0);
    
    if (recentSessionWithNotes) {
        document.getElementById('sessionNotes').value = recentSessionWithNotes.notes;
        console.log('Loaded previous notes from session:', recentSessionWithNotes.date);
    }
}

// Energy Level functionality
function setEnergy(level) {
    currentEnergy = level;
    
    // Update UI
    document.querySelectorAll('.energy-btn').forEach(btn => {
        btn.classList.remove('selected');
    });
    
    const selectedBtnId = `energy${level}`;
    document.getElementById(selectedBtnId).classList.add('selected');
    
    console.log(`Energy set to: ${level}`);
}

// Tracking Metrics Functions
function updateTrackingMetrics() {
    document.getElementById('failedAttempts').textContent = failedAttempts;
    document.getElementById('correctRepetitions').textContent = correctRepetitions;
    document.getElementById('streakResets').textContent = streakResets;
    
    // Update Dr. Gebrian target label if locked
    if (gebrianTargetLocked) {
        updateGebrianTargetLabel();
    }
    
    // Update Intensity Module display if enabled
    if (intensityModuleEnabled && typeof IntensityModule !== 'undefined') {
        updateIntensityDisplay();
    }
    
    updateUnsavedIndicator();
}

// Calculate Gebrian target based on selected intensity
function computeGebrianTarget(errorsCount, intensity) {
    const minimumTarget = 6;
    if (intensity === 50) {
        if (errorsCount <= minimumTarget) {
            return minimumTarget;
        }
        const extraErrors = errorsCount - minimumTarget;
        return minimumTarget + Math.floor(extraErrors / 2);
    }
    return Math.max(minimumTarget, errorsCount);
}

function adjustFailedAttempts(delta) {
    failedAttempts = Math.max(0, failedAttempts + delta);
    
    // Dr. Gebrian: Tel fouten vóór eerste correcte rep (STAP 1)
    if (!hasAchievedFirstCorrect && delta > 0) {
        errorsBeforeFirstCorrect++;
        
        // LIVE TARGET CALCULATIE: Update target direct terwijl fouten oplopen
        // Molly's formule: 100% -> MAX(6, E), 50% -> 6 + floor(max(0, E - 6)/2) (geen groei tot 6 fouten)
        const liveTarget = computeGebrianTarget(errorsBeforeFirstCorrect, overlearningIntensity);
        
        // Update target alleen als gebruiker niet handmatig heeft aangepast
        if (!userManuallySetTarget) {
            targetRepetitions = liveTarget;
            document.getElementById('targetReps').textContent = targetRepetitions;
            console.log(`[Gebrian LIVE] E=${errorsBeforeFirstCorrect} → T=${liveTarget} (${overlearningIntensity}% intensity)`);
        }
    }
    
    // Note: streakResets is incremented separately in resetCorrectReps() 
    // to maintain semantic separation between execution errors and memory failures
    
    updateTrackingMetrics();
    updateSuccessRateDisplay();
    if (window.MPLog) MPLog.info('Failed attempts adjusted', { delta, failedAttempts, errorsBeforeFirstCorrect, liveTarget: targetRepetitions });
}

function resetFailedAttempts() {
    if (!confirm('⚠️ Are you sure you want to reset Failed Attempts to 0?\n\nThis will also reset the Gebrian target calculation.\n\nThis cannot be undone.')) {
        return;
    }
    failedAttempts = 0;
    
    // Reset Dr. Gebrian state omdat fouten count gereset wordt
    errorsBeforeFirstCorrect = 0;
    hasAchievedFirstCorrect = false;
    gebrianTargetLocked = false;
    targetRepetitions = 6;
    document.getElementById('targetReps').textContent = targetRepetitions;
    
    // Verwijder uitleg label
    const container = document.getElementById('targetReps').parentElement;
    const explanation = container?.querySelector('.gebrian-explanation');
    if (explanation) explanation.remove();
    
    console.log('[Gebrian] Failed attempts gereset - target terug naar 6');
    
    updateTrackingMetrics();
    updateSuccessRateDisplay();
    if (window.MPLog) MPLog.info('Failed attempts reset to 0, Gebrian state reset');
}

function adjustCorrectReps(delta) {
    const previousCorrectReps = correctRepetitions;
    correctRepetitions = Math.max(0, correctRepetitions + delta);
    
    // Verberg Micro-Break pop-up bij elke gebruikersactie (+ of -)
    if (microBreakNotificationActive) {
        hideMicroBreakNotification();
        console.log('[Micro-Break] Pop-up verborgen door gebruikersactie');
    }
    
    // Dr. Gebrian: Bereken target bij allereerste correcte rep (STAP 2)
    if (!hasAchievedFirstCorrect && correctRepetitions > 0 && delta > 0) {
        hasAchievedFirstCorrect = true;
        
        // Bereken target op basis van gekozen intensity
        const calculatedTarget = computeGebrianTarget(errorsBeforeFirstCorrect, overlearningIntensity);
        
        // Lock de target (tenzij gebruiker handmatig heeft aangepast)
        if (!userManuallySetTarget) {
            targetRepetitions = calculatedTarget;
            gebrianTargetLocked = true;
            document.getElementById('targetReps').textContent = targetRepetitions;
            
            console.log(
                `\n${'='.repeat(60)}\n` +
                `🎯 DR. GEBRIAN TARGET BEREKEND\n` +
                `${'='.repeat(60)}\n` +
                `Fouten voor eerste correcte rep (E): ${errorsBeforeFirstCorrect}\n` +
                `Overlearning Intensity: ${overlearningIntensity}%\n` +
                `Berekend doel (T): ${overlearningIntensity === 50 ? `6 + floor(max(0, ${errorsBeforeFirstCorrect} - 6)/2)` : `MAX(6, ${errorsBeforeFirstCorrect})`} = ${calculatedTarget}\n` +
                `Target is nu LOCKED - geen aanpassingen meer tijdens sessie\n` +
                `${'='.repeat(60)}\n`
            );
        }
        
        // Update UI met uitleg
        updateGebrianTargetLabel();
    }
    
    // MICRO-BREAK WORKFLOW: Verhoog teller bij succesvolle herhaling
    if (delta > 0 && correctRepetitions > 0) {
        repsSinceLastBreak++;
        console.log(`[Micro-Break] Reps sinds laatste break: ${repsSinceLastBreak}`);
        
        // Check of we Micro-Break moeten tonen (elke 3 herhalingen)
        if (enableMicroBreaks && repsSinceLastBreak >= 3) {
            showMicroBreakNotification();
        }
    }
    
    updateTrackingMetrics();
    updateSuccessRateDisplay();
    if (window.MPLog) MPLog.info('Correct repetitions adjusted', { delta, correctRepetitions, hasAchievedFirstCorrect, targetRepetitions, repsSinceLastBreak });
}

function resetCorrectReps() {
    // When resetting correct reps (streak broken), increment streak resets
    // This represents a memory retrieval failure (Ebbinghaus), not just an execution error
    // streakResets is used as totalFailures in Ebbinghaus calculations
    if (correctRepetitions > 0) {
        streakResets++;  // Automatic counter: memory failure affects interval calculation
    }
    correctRepetitions = 0;
    
    // Dr. Gebrian STAP 3: Bij fout -> reset teller naar 0, maar TARGET BLIJFT GELIJK!
    console.log(`[Gebrian] Streak reset - teller terug naar 0, doel blijft ${targetRepetitions}`);
    
    // MICRO-BREAK RESET: Reset break teller en verberg pop-up
    repsSinceLastBreak = 0;
    hideMicroBreakNotification();
    console.log(`[Micro-Break] Reset - teller en notificatie gereset`);
    
    updateTrackingMetrics();
    updateSuccessRateDisplay();
    if (window.MPLog) MPLog.info('Correct repetitions reset, streakResets incremented', { streakResets, targetStaysAt: targetRepetitions, repsSinceLastBreak });
}

// Note: streakResets is automatically incremented in resetCorrectReps()
// No manual adjustment functions needed - it's a read-only automatic counter

// Set Overlearning Intensity (50% or 100%)
function setOverlearningIntensity(intensity) {
    overlearningIntensity = intensity;
    
    // Update UI Elements
    const btn50 = document.getElementById('intensity50Btn');
    const btn100 = document.getElementById('intensity100Btn');
    
    // Desktop Accent Kleur (Blauw)
    const activeColor = '#2563eb'; 
    
    if (btn50 && btn100) {
        if (intensity === 50) {
            // 50% Actief
            btn50.style.background = activeColor;
            btn50.style.color = 'white';
            btn50.style.fontWeight = '700';
            
            // 100% Inactief
            btn100.style.background = '';
            btn100.style.color = '';
            btn100.style.fontWeight = '';
        } else {
            // 50% Inactief
            btn50.style.background = '';
            btn50.style.color = '';
            btn50.style.fontWeight = '';
            
            // 100% Actief
            btn100.style.background = activeColor;
            btn100.style.color = 'white';
            btn100.style.fontWeight = '700';
        }
    }
    
    console.log(`[Gebrian] Overlearning intensity set to: ${intensity}%`);
    
    // Herbereken target als de eerste succesvolle poging al is geweest
    // en de gebruiker het doel niet handmatig heeft aangepast.
    if (hasAchievedFirstCorrect && !userManuallySetTarget) {
        const calculatedTarget = computeGebrianTarget(errorsBeforeFirstCorrect, intensity);
        targetRepetitions = calculatedTarget;
        
        const targetEl = document.getElementById('targetReps');
        if (targetEl) targetEl.textContent = targetRepetitions;

        // Update de blauwe uitleg box indien aanwezig
        if (typeof updateGebrianTargetLabel === 'function') {
            updateGebrianTargetLabel();
        }
    }
}

// Update Gebrian target label met uitleg
function updateGebrianTargetLabel() {
    const targetElement = document.getElementById('targetReps');
    if (!targetElement) return;
    
    const container = targetElement.parentElement;
    if (!container) return;
    
    // Verwijder oude uitleg als die er is (infobordje niet meer tonen)
    const existingExplanation = container.querySelector('.gebrian-explanation');
    if (existingExplanation) {
        existingExplanation.remove();
    }
    
    // Infobordje niet meer tonen - functie behoudt alleen cleanup logica
}

// Micro-Break Notification (Molly Gebrian methodiek)
function showMicroBreakNotification() {
    // Controleer of notificatie al actief is
    if (microBreakNotificationActive) return;
    
    const notification = document.getElementById('microBreakNotification');
    if (!notification) {
        console.warn('[Micro-Break] Notificatie-element ontbreekt in DOM');
        return;
    }

    // Reset animatie door element opnieuw te forceren
    notification.classList.remove('visible');
    // Force repaint zodat CSS-animatie opnieuw kan triggeren
    void notification.offsetWidth;

    notification.classList.add('visible');
    notification.setAttribute('aria-hidden', 'false');
    microBreakNotificationActive = true;
    if (microBreakTimeoutId) {
        clearTimeout(microBreakTimeoutId);
    }
    microBreakTimeoutId = setTimeout(() => {
        hideMicroBreakNotification();
    }, 5000);
    
    // Reset de teller zodat de melding na volgende 3 reps opnieuw verschijnt
    repsSinceLastBreak = 0;
    
    console.log('[Micro-Break] Notificatie getoond - wacht op gebruikersactie');
}

function hideMicroBreakNotification() {
    const notification = document.getElementById('microBreakNotification');
    if (!notification) {
        microBreakNotificationActive = false;
        if (microBreakTimeoutId) {
            clearTimeout(microBreakTimeoutId);
            microBreakTimeoutId = null;
        }
        return;
    }

    notification.classList.remove('visible');
    notification.setAttribute('aria-hidden', 'true');
    microBreakNotificationActive = false;
    if (microBreakTimeoutId) {
        clearTimeout(microBreakTimeoutId);
        microBreakTimeoutId = null;
    }
    console.log('[Micro-Break] Notificatie verborgen');
}

function adjustTargetReps(delta) {
    targetRepetitions = Math.max(1, Math.min(100, targetRepetitions + delta));
    document.getElementById('targetReps').textContent = targetRepetitions;
    userManuallySetTarget = true; // Mark that user manually adjusted the target
    gebrianTargetLocked = false; // User override: unlock Gebrian auto-calculation
    
    // Verwijder Gebrian uitleg bij handmatige aanpassing
    const container = document.getElementById('targetReps').parentElement;
    const explanation = container?.querySelector('.gebrian-explanation');
    if (explanation) explanation.remove();
    
    console.log('[Gebrian] Gebruiker heeft target handmatig aangepast - Gebrian auto-berekening uitgeschakeld');
    
    updateSuccessRateDisplay();
    updateIntensityDisplay();
}

function resetTargetReps() {
    // Reset naar standaard 6 (baseline default)
    targetRepetitions = 6;
    document.getElementById('targetReps').textContent = targetRepetitions;
    userManuallySetTarget = false; // Reset flag - allow auto-adjustment again
    
    // Reset Dr. Gebrian state
    errorsBeforeFirstCorrect = 0;
    hasAchievedFirstCorrect = false;
    gebrianTargetLocked = false;
    
    // Verwijder uitleg label
    const container = document.getElementById('targetReps').parentElement;
    const explanation = container?.querySelector('.gebrian-explanation');
    if (explanation) explanation.remove();
    
    console.log('[Gebrian] Target gereset naar 6 - ready voor nieuwe berekening');
    
    updateSuccessRateDisplay();
    updateIntensityDisplay();
}

function calculateSuccessRate() {
    const totalAttempts = correctRepetitions + failedAttempts + streakResets;
    if (totalAttempts === 0) {
        return { percentage: 0, average: 0 };
    }
    
    const successfulAttempts = correctRepetitions;
    const percentage = Math.round((successfulAttempts / totalAttempts) * 100);
    const average = totalAttempts > 0 ? Math.round(totalAttempts / Math.max(1, correctRepetitions)) : 0;
    
    return { percentage, average };
}

function updateSuccessRateDisplay() {
    // Success rate display has been removed from UI - this function is now a no-op
    // Kept for backward compatibility to prevent errors in other parts of the code
    return;
}

// Validate tempo input to prevent typos and provide feedback
function validateTempoInput() {
    try {
        const targetInput = document.getElementById('targetTempo');
        const achievedInput = document.getElementById('achievedTempo');
        const warningRow = document.getElementById('tempoWarningRow');
        const warningText = document.getElementById('tempoWarningText');
        
        if (!targetInput || !achievedInput || !warningRow || !warningText) {
            console.warn('Tempo elements not found, skipping validation');
            return;
        }
        
        const targetTempo = parseInt(targetInput.value) || 0;
        const achievedTempo = parseInt(achievedInput.value) || 0;
    
    // Clear warning by default
    warningRow.style.display = 'none';
    
    // Skip validation if fields are empty
    if (targetTempo === 0 && achievedTempo === 0) return;
    
    // Get last achieved tempo for comparison
    const profileData = JSON.parse(localStorage.getItem(`mp_${currentProfile.id}_data`) || '{"practiceHistory":[]}');
    let lastAchievedTempo = 0;
    
    if (profileData.practiceHistory) {
        const sectionHistory = profileData.practiceHistory
            .filter(h => h.barSectionId === currentSection.id && !h.isDeleted && h.achievedTempo > 0)
            .sort((a, b) => new Date(b.date) - new Date(a.date));
        
        if (sectionHistory.length > 0) {
            lastAchievedTempo = sectionHistory[0].achievedTempo;
        }
    }
    
    // Validation 1: Achieved > Target (likely typo)
    if (achievedTempo > 0 && targetTempo > 0 && achievedTempo > targetTempo + 10) {
        warningText.textContent = `Achieved tempo (${achievedTempo}) is higher than target (${targetTempo}). Is this correct?`;
        warningRow.style.display = 'flex';
        return;
    }
    
    // Validation 2: Large jump from last session (>30 BPM increase)
    if (achievedTempo > 0 && lastAchievedTempo > 0) {
        const difference = achievedTempo - lastAchievedTempo;
        if (difference > 30) {
            warningText.textContent = `Big jump from last session (+${difference} BPM). Double-check this value.`;
            warningRow.style.display = 'flex';
            return;
        }
        if (difference < -30) {
            warningText.textContent = `Large decrease from last session (${difference} BPM). Is everything OK?`;
            warningRow.style.display = 'flex';
            return;
        }
    }
    
    // Validation 3: Unrealistic tempo values
    if (targetTempo > 0 && (targetTempo < 30 || targetTempo > 300)) {
        warningText.textContent = `Target tempo ${targetTempo} BPM seems unusual (range: 30-300 BPM).`;
        warningRow.style.display = 'flex';
        return;
    }
    
    if (achievedTempo > 0 && (achievedTempo < 30 || achievedTempo > 300)) {
        warningText.textContent = `Achieved tempo ${achievedTempo} BPM seems unusual (range: 30-300 BPM).`;
        warningRow.style.display = 'flex';
        return;
    }
    } catch (error) {
        console.error('Error in validateTempoInput:', error);
        // Silently fail - tempo validation is not critical
    }
}

// Timer functions
function startTimer() {
    if (isRunning) return;

    isRunning = true;
    startTime = Date.now() - pausedTime;

    timerInterval = setInterval(() => {
        const elapsed = Date.now() - startTime;
        
        // For interleaved mode: countdown from initial value
        if (interleavedMode && interleavedManager) {
            const initialSeconds = interleavedManager.defaultSlotDuration;
            totalElapsedSeconds = Math.max(0, initialSeconds - Math.floor(elapsed / 1000));
        } else {
            // Normal mode: count up
            totalElapsedSeconds = Math.floor(elapsed / 1000);
        }
        
        updateTimerDisplay();
    }, 100);

    updateButtonStates();
    if (window.MPLog) MPLog.info('Timer started', { totalElapsedSeconds, mode: interleavedMode ? 'countdown' : 'normal' });
}

function pauseTimer() {
    if (!isRunning) return;

    isRunning = false;
    pausedTime = Date.now() - startTime;
    clearInterval(timerInterval);
    updateButtonStates();
    if (window.MPLog) MPLog.info('Timer paused', { totalElapsedSeconds });
}

function stopTimer() {
    pauseTimer();
    // Also stop auto-save when timer is stopped
    if (autoSaveInterval) {
        clearInterval(autoSaveInterval);
        autoSaveInterval = null;
    }
    // Don't reset - keep time for session save
    updateButtonStates();
    if (window.MPLog) MPLog.info('Timer stopped', { totalElapsedSeconds });
}

function updateTimerDisplay() {
    // Don't update if user is manually editing
    if (isManuallyEditing) return;

    // For interleaved mode, show countdown instead of count-up
    let displaySeconds = totalElapsedSeconds;
    if (interleavedMode && interleavedManager) {
        // Countdown mode: show remaining time
        displaySeconds = Math.max(0, displaySeconds);
    }

    const hours = Math.floor(displaySeconds / 3600);
    const minutes = Math.floor((displaySeconds % 3600) / 60);
    const seconds = displaySeconds % 60;

    const display = `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
    document.getElementById('timerDisplay').textContent = display;
    
    // In countdown mode, alert when time runs out
    if (interleavedMode && displaySeconds === 0 && isRunning) {
        pauseTimer();
        alert('⏰ Tijd is op! Je kunt doorgaan naar de volgende chunk of meer tijd nemen.');
    }
}

function updateButtonStates() {
    const btnStart = document.getElementById('btnStart');
    const btnPause = document.getElementById('btnPause');
    const btnStop = document.getElementById('btnStop');
    const btnComplete = document.getElementById('btnComplete');

    if (isRunning) {
        btnStart.disabled = true;
        btnPause.disabled = false;
        btnStop.disabled = false;
        btnComplete.disabled = false;
    } else if (totalElapsedSeconds > 0) {
        btnStart.disabled = false;
        btnPause.disabled = true;
        btnStop.disabled = false;
        btnComplete.disabled = false;
    } else {
        btnStart.disabled = false;
        btnPause.disabled = true;
        btnStop.disabled = true;
        btnComplete.disabled = true;
    }
}

// Complete session
function completeSession() {
    // Stop timer if running
    if (isRunning) {
        pauseTimer();
    }

    // Validate minimum time (30 seconds threshold to avoid noise)
    if (totalElapsedSeconds < 30) {
        const confirmShort = confirm(
            '⚠️ Short Session Detected\n\n' +
            'This session is less than 30 seconds long.\n' +
            'Very short sessions can distort your statistics.\n\n' +
            'Do you want to save it anyway?\n' +
            '[OK] Save it\n' +
            '[Cancel] Discard session'
        );
        
        if (!confirmShort) {
            if (window.MPLog) MPLog.info('Short session discarded by user', { duration: totalElapsedSeconds });
            // Treat as cancel
            cancelSession();
            return;
        }
    }

    // Show evaluation modal
    openEvaluationModal();
    if (window.MPLog) MPLog.info('Session completed (entering evaluation)', { totalElapsedSeconds, correctRepetitions, failedAttempts, streakResets });
}

// Cancel session
function cancelSession() {
    // For interleaved mode, ask if user wants to exit entire circuit
    if (interleavedMode) {
        const confirmExit = confirm(
            '🔀 Interleaved Circuit verlaten?\n\n' +
            'Als je nu annuleert, gaan alle circuit resultaten verloren.\n\n' +
            'Wil je het circuit echt verlaten?\n\n' +
            '[OK = Ja, verlaat circuit] [Annuleer = Blijf]'
        );
        
        if (!confirmExit) return;
        
        // Clear interleaved queue
        sessionStorage.removeItem('mp_interleaved_queue');
        
        // Cleanup and exit
        cleanupTimers(false);
        sessionCompleted = true;
        exitPracticeSession('interleaved_circuit_cancelled', {}, 'moduspractica-dashboard.html');
        return;
    }
    
    // Normal mode: Check if session has any recorded data
    const hasData = totalElapsedSeconds > 0 || 
                    failedAttempts > 0 || 
                    correctRepetitions > 0 ||
                    streakResets > 0 ||
                    document.getElementById('sessionNotes').value.trim() !== '';

    if (hasData) {
        if (!confirm('⚠️ You have unsaved practice data!\n\nAre you sure you want to cancel? All progress will be lost.\n\n[OK = Yes, cancel] [Cancel = No, stay]')) {
            return;
        }
    }
    
    // Safely clear draft when canceling (important: do this BEFORE navigation)
    try {
        clearDraft();
        if (window.MPLog) MPLog.info('Draft cleared on cancel', { draftKey: `mp_draft_${currentPiece.id}_${sectionId}` });
    } catch (error) {
        console.error('Error clearing draft on cancel:', error);
        if (window.MPLog) MPLog.error('Error clearing draft on cancel', { error: error && error.message });
        // Continue anyway - canceling should always succeed
    }
    
    // Mark session as completed to prevent beforeunload warning
    sessionCompleted = true;
    
    // Track session cancellation if there was data
    if (hasData && window.ga4Tracker) {
        try {
            window.ga4Tracker.trackPracticeSessionCancelled(
                currentPiece?.title || 'Unknown',
                currentSection?.barRange || 'Unknown',
                totalElapsedSeconds
            );
        } catch (error) {
            console.error('Error tracking cancellation:', error);
            // Continue anyway
        }
    }
    if (window.MPLog) MPLog.info('Session cancelled', { profileId: currentProfile?.id, sectionId: currentSection?.id, totalElapsedSeconds });

    exitPracticeSession('practice-session-cancelled', {
        pieceId: currentPiece?.id,
        sectionId: currentSection?.id,
        durationSeconds: totalElapsedSeconds,
        hadData: hasData,
        returnDate: returnDateParam
    });
}

// Evaluation modal functions
function openEvaluationModal() {
    const modal = document.getElementById('evaluationModal');
    modal.classList.add('active');
    
    // Show special message if this is an incomplete session (0 correct reps after extended time)
    const isIncomplete = correctRepetitions === 0 && totalElapsedSeconds >= 120;
    const existingWarning = document.getElementById('incompleteSessionWarning');
    
    if (isIncomplete && !existingWarning) {
        const modalHeader = modal.querySelector('.modal-header');
        const warningDiv = document.createElement('div');
        warningDiv.id = 'incompleteSessionWarning';
        warningDiv.style.cssText = 'margin-top: 15px; padding: 12px; background: rgba(255, 152, 0, 0.1); border-left: 4px solid #ff9800; border-radius: 6px;';
        warningDiv.innerHTML = `
            <div style="display: flex; align-items: start; gap: 10px;">
                <div style="font-size: 24px;">⚠️</div>
                <div>
                    <div style="font-weight: 600; color: #e65100; margin-bottom: 5px;">Passage too difficult?</div>
                    <div style="font-size: 13px; color: #666; line-height: 1.5;">
                        You practiced for ${Math.floor(totalElapsedSeconds / 60)} minutes without a successful repetition.
                        This is normal for challenging passages. The session is saved, and you will get another chance tomorrow (1 day).
                        <strong>No stage penalty</strong> – sometimes it is better to pause and restart with a fresh mind.
                    </div>
                </div>
            </div>
        `;
        modalHeader.appendChild(warningDiv);
    } else if (!isIncomplete && existingWarning) {
        // Remove warning if it exists but session is not incomplete
        existingWarning.remove();
    }
    
    // Load and display trend chart
    displayTrendChart();
}

function closeEvaluationModal() {
    document.getElementById('evaluationModal').classList.remove('active');
    selectedPerformance = null;
    
    // Clear selections
    document.querySelectorAll('.eval-option').forEach(opt => opt.classList.remove('selected'));
    document.getElementById('btnSaveEvaluation').disabled = true;
    
    // Hide chart section
    document.getElementById('chartSection').style.display = 'none';
}

// Display success ratio trend chart
function displayTrendChart() {
    try {
        // Get practice history for this section
        const profileData = JSON.parse(localStorage.getItem(`mp_${currentProfile.id}_data`) || '{"practiceHistory":[]}');
        
        if (!profileData.practiceHistory) {
            console.log('No practice history available');
            return;
        }
        
        // Filter history for current section
        const sectionHistory = profileData.practiceHistory.filter(h => 
            h.barSectionId === currentSection.id && !h.isDeleted
        );
        
        const historyForChart = [...sectionHistory];
        const previewEntry = buildPreviewHistoryEntry();
        if (previewEntry) {
            historyForChart.push(previewEntry);
        }
        
        console.log(`Found ${sectionHistory.length} practice sessions for this section`);
        if (window.MPLog) MPLog.info('Found practice history', { profileId: currentProfile?.id, sectionId: currentSection?.id, count: sectionHistory.length });
        if (previewEntry) {
            console.log('Including current session preview in trend chart');
            if (window.MPLog) MPLog.debug('Including current session preview in trend chart');
        }
        
        // Only show chart if we have at least 2 sessions
        if (historyForChart.length >= 2) {
            const chartSection = document.getElementById('chartSection');
            chartSection.style.display = 'block';
            if (window.MPLog) MPLog.info('Rendering trend chart', { entries: historyForChart.length });
            
            // Initialize and draw chart
            const chart = new SuccessRatioTrendChart('trendChartCanvas', {
                maxSessions: 7,
                highlightLatest: true,
                showLegend: true,
                width: 420,
                height: 280
            });
            
            chart.draw(historyForChart);
            
            console.log('✅ Trend chart displayed successfully');
        } else {
            console.log('Not enough sessions to display chart (need at least 2)');
            document.getElementById('chartSection').style.display = 'none';
        }
    } catch (error) {
        console.error('Error displaying trend chart:', error);
        document.getElementById('chartSection').style.display = 'none';
    }
}

// Provide a synthetic history entry so the chart reflects the in-progress session
function buildPreviewHistoryEntry() {
    const hasAttempts = correctRepetitions > 0 || streakResets > 0 || failedAttempts > 0;
    if (totalElapsedSeconds === 0 && !hasAttempts) {
        return null;
    }

    return {
        repetitions: correctRepetitions,
        // Include both memory failures (streakResets) and execution failed attempts, so the
        // preview matches the final saved session and the charts' success-rate calculation.
        totalFailures: streakResets,
        attemptsTillSuccess: failedAttempts,
        date: new Date().toISOString()
    };
}

function selectEvaluation(performance) {
    selectedPerformance = performance;

    // Update UI
    document.querySelectorAll('.eval-option').forEach(opt => opt.classList.remove('selected'));
    event.currentTarget.classList.add('selected');
    document.getElementById('btnSaveEvaluation').disabled = false;
}

function saveEvaluation() {
    if (!selectedPerformance) {
        alert('Please select a performance rating');
        return;
    }
    
    // Validate data integrity before saving
    if (!currentProfile || !currentPiece || !currentSection) {
        alert('⚠️ Data integrity error!\n\nSession data is incomplete. Your practice data cannot be saved.\n\nPlease take a screenshot of your practice metrics and contact support.');
        console.error('Save aborted: Missing data', {
            hasProfile: !!currentProfile,
            hasPiece: !!currentPiece,
            hasSection: !!currentSection
        });
        return;
    }
    
    // Validate that there was actual practice activity
    const totalAttempts = correctRepetitions + failedAttempts + streakResets;
    
    // Allow saving even with 0 correct reps IF there was time spent practicing
    // This handles the common case where a passage is too difficult and user struggles for 10+ minutes
    const hasMinimalActivity = totalAttempts > 0 || totalElapsedSeconds >= 120; // 2+ minutes
    
    if (!hasMinimalActivity) {
        alert('⚠️ No practice activity detected!\n\nYou must either:\n' +
              '• Record at least one attempt (use counters), OR\n' +
              '• Practice for at least 2 minutes\n\n' +
              `Current stats:\n` +
              `- Time: ${Math.floor(totalElapsedSeconds / 60)}m ${totalElapsedSeconds % 60}s\n` +
              `- Correct: ${correctRepetitions}\n` +
              `- Failed: ${failedAttempts}\n` +
              `- Streak resets: ${streakResets}\n\n` +
              'Please practice the section and then save.');
        console.warn('Save aborted: No practice activity', {
            totalElapsedSeconds,
            correctRepetitions,
            failedAttempts,
            streakResets,
            totalAttempts
        });
        return;
    }
    
    // Detect incomplete session (struggled but didn't succeed)
    const isIncompleteSession = correctRepetitions === 0 && totalElapsedSeconds >= 120;
    if (isIncompleteSession) {
        console.log('⚠️ Incomplete session detected: 0 correct reps after extended practice time');
    }

    // Wrap entire save operation in try-catch for error recovery
    try {
        // Calculate final success rate
        const { percentage: successRate, average: avgAttempts } = calculateSuccessRate();
        
        // Determine session outcome
        let sessionOutcome;
        if (correctRepetitions === 0 && totalElapsedSeconds >= 120) {
            sessionOutcome = 'Incomplete'; // Struggled but didn't achieve any successful repetitions
        } else if (correctRepetitions >= targetRepetitions) {
            sessionOutcome = 'TargetReached';
        } else {
            sessionOutcome = 'PartialProgress';
        }

        // Create practice session object
        const session = {
            id: generateGUID(),
            sectionId: currentSection.id,
            pieceId: currentPiece.id,
            pieceTitle: currentPiece.title,
            sectionRange: currentSection.barRange,
            date: new Date().toISOString(),
            durationSeconds: totalElapsedSeconds,
            performance: selectedPerformance,
            notes: document.getElementById('sessionNotes').value.trim(),
            completedRepetitions: correctRepetitions,
            // Tracking metrics for Ebbinghaus calculations
            failedAttempts: failedAttempts,
            correctRepetitions: correctRepetitions,
            streakResets: streakResets,
            totalFailures: streakResets, // Use streakResets as totalFailures for Ebbinghaus
            executionFailures: failedAttempts, // execution-related failures (attempts till success)
            successRate: successRate,
            averageAttempts: avgAttempts,
            targetRepetitions: targetRepetitions, // Use the adjusted value from session
            sessionOutcome: sessionOutcome, // Track outcome for analysis
            energyLevel: currentEnergy // Save energy level
        };

        // Save session to piece
        if (!currentPiece.practiceSessions) {
            currentPiece.practiceSessions = [];
        }
        currentPiece.practiceSessions.push(session);

        // Update section properties
        updateSectionAfterPractice(session);

        // Save practice history for statistics (matching C# PracticeHistory structure)
        savePracticeHistory(session);

        // Save to localStorage (this validates and saves everything)
        saveData();
        
        // Save adaptive time calibration for Intensity Module
        saveAdaptiveTimeCalibration();
        
        // Mark session as completed (disable beforeunload warning)
        sessionCompleted = true;
        
        // Clear draft after successful save
        clearDraft();
        
        // Track practice session completion
        if (window.ga4Tracker) {
            window.ga4Tracker.trackPracticeSessionCompleted(
                currentPiece.title,
                currentSection.barRange,
                totalElapsedSeconds,
                session.performance,
                session.successRate
            );
        }
        if (window.MPLog) MPLog.info('Practice session saved', { profileId: currentProfile?.id, pieceId: currentPiece?.id, sectionId: currentSection?.id, sessionId: session.id, durationSeconds: session.durationSeconds });

        exitPracticeSession('practice-session-completed', {
            pieceId: currentPiece.id,
            sectionId: currentSection.id,
            nextReviewDate: currentSection.nextReviewDate,
            sessionId: session.id,
            durationSeconds: session.durationSeconds,
            returnDate: returnDateParam
        });
        
    } catch (error) {
        console.error('Failed to save practice session:', error);
        if (window.MPLog) MPLog.error('Failed to save practice session', { error: error && error.message });
        
        // Show detailed error message to user
        alert(
            '⚠️ Failed to save practice session!\n\n' +
            'Error: ' + error.message + '\n\n' +
            'Your practice metrics:\n' +
            `- Duration: ${Math.floor(totalElapsedSeconds / 60)}m ${totalElapsedSeconds % 60}s\n` +
            `- Correct Repetitions: ${correctRepetitions}\n` +
            `- Failed Attempts: ${failedAttempts}\n` +
            `- Streak Resets: ${streakResets}\n\n` +
            'Please take a screenshot and try to export your profile data from the dashboard.\n\n' +
            'Click OK to return to dashboard (session will NOT be saved).'
        );
        
        // Clear draft to prevent corruption
        clearDraft();
        
        // Mark as completed to prevent further issues
        sessionCompleted = true;
        
        // Return to dashboard
        exitPracticeSession('practice-session-error', {
            pieceId: currentPiece?.id,
            sectionId: currentSection?.id,
            error: error?.message || 'unknown'
        });
    }
}

// Update section after practice
function updateSectionAfterPractice(session) {
    // Calculate actual interval (for overdue tracking)
    let actualIntervalDays = 0;
    const today = new Date();
    const todayNormalized = new Date(today.getFullYear(), today.getMonth(), today.getDate());
    
    if (currentSection.nextReviewDate) {
        const scheduledDateObj = new Date(currentSection.nextReviewDate);
        const scheduledDate = new Date(scheduledDateObj.getFullYear(), scheduledDateObj.getMonth(), scheduledDateObj.getDate());
        actualIntervalDays = Math.max(0, (todayNormalized - scheduledDate) / (1000 * 60 * 60 * 24));
        
        if (actualIntervalDays > 0) {
            console.log(`[Overdue] Section was ${actualIntervalDays.toFixed(0)} day(s) overdue - this will be factored into next interval calculation`);
        }
    }
    
    // Update last practice date
    currentSection.lastPracticeDate = session.date;

    // Update target repetitions if changed during session
    if (targetRepetitions !== currentSection.targetRepetitions) {
        currentSection.targetRepetitions = targetRepetitions;
    }
    
    // Update target tempo if set during session - with safe fallback
    try {
        const targetTempoEl = document.getElementById('targetTempo');
        if (targetTempoEl && targetTempoEl.value) {
            const targetTempo = parseInt(targetTempoEl.value) || 0;
            if (targetTempo > 0) {
                currentSection.targetTempo = targetTempo;
            }
        }
    } catch (error) {
        console.warn('Error updating target tempo, skipping:', error);
    }

    // Update completed repetitions
    const previousCompleted = currentSection.completedRepetitions || 0;
    currentSection.completedRepetitions = previousCompleted + session.completedRepetitions;

    // Update status
    if (currentSection.status === 'New') {
        currentSection.status = 'Active';
    }

    // Initialize stage if not set
    const currentStage = currentSection.practiceScheduleStage || 0;
    
    // Calculate next review date based on stage and session outcome
    let intervalDays;
    
    // Special handling for incomplete sessions (0 correct reps after extended practice)
    if (session.sessionOutcome === 'Incomplete') {
        // Keep interval very short (1 day) regardless of stage
        // User needs to try again soon, but not immediately (allow rest)
        intervalDays = 1.0;
        console.log(`[Incomplete Session] Stage ${currentStage}: Fixed 1-day interval (passage too difficult, retry tomorrow)`);
    } else if (currentStage < 3) {
        // Foundation phase (Stages 0, 1, 2): Fixed 1-day intervals
        // This ensures 3 consecutive days of practice for new sections
        intervalDays = 1.0;
        console.log(`[Foundation] Stage ${currentStage}: Fixed 1-day interval (consecutive days)`);
    } else {
        // Ebbinghaus phase (Stage 3+): Calculate optimal interval
        intervalDays = calculateEbbinghausInterval(session.performance, currentStage, actualIntervalDays);
        console.log(`[Ebbinghaus] Stage ${currentStage}: Calculated ${intervalDays} days (actual interval: ${actualIntervalDays.toFixed(1)} days)`);
    }
    
    const nextDate = new Date();
    nextDate.setDate(nextDate.getDate() + Math.round(intervalDays));
    currentSection.nextReviewDate = nextDate.toISOString();

    // Update stage if target repetitions reached (but NOT for incomplete sessions)
    if (session.sessionOutcome !== 'Incomplete' && currentSection.completedRepetitions >= currentSection.targetRepetitions) {
        if (window.MPLog) MPLog.info('Stage increment condition met', { sectionId: currentSection.id, completedReps: currentSection.completedRepetitions, targetReps: currentSection.targetRepetitions, prevStage: currentStage });
        currentSection.practiceScheduleStage = (currentSection.practiceScheduleStage || 0) + 1;
        if (window.MPLog) MPLog.info('Stage incremented', { sectionId: currentSection.id, newStage: currentSection.practiceScheduleStage });
        currentSection.completedRepetitions = 0; // Reset for next stage
        
        // Reset target repetitions to baseline (6) for the new stage
        // This prevents high targets from difficult sessions carrying over to the new stage
        currentSection.targetRepetitions = 6;
    } else {
        if (window.MPLog) MPLog.debug('Stage increment condition NOT met', { sectionId: currentSection.id, completedReps: currentSection.completedRepetitions, targetReps: currentSection.targetRepetitions, stage: currentSection.practiceScheduleStage, sessionOutcome: session.sessionOutcome });
    }

    // Log for debugging
    const prevStage = currentStage;
    console.log('Section updated:', {
        range: currentSection.barRange,
        completedReps: currentSection.completedRepetitions,
        targetReps: currentSection.targetRepetitions,
        stage: currentSection.practiceScheduleStage,
        nextReview: currentSection.nextReviewDate,
        performance: session.performance,
        intervalDays: intervalDays
    });
    if (window.MPLog) MPLog.info('Section updated after practice', {
        sectionId: currentSection.id,
        prevStage, prevCompleted: previousCompleted,
        newCompleted: currentSection.completedRepetitions,
        targetReps: currentSection.targetRepetitions,
        stageBeforeIncrement: prevStage,
        stageAfter: currentSection.practiceScheduleStage,
        intervalDays: intervalDays,
        nextReview: currentSection.nextReviewDate,
        performance: session.performance
    });
}

// Ebbinghaus interval calculation for stage 3+
// Uses Ebbinghaus formula with performance-based adjustment
// actualIntervalDays: how many days have actually passed since last scheduled review (for logging only)
function calculateEbbinghausInterval(performance, stage, actualIntervalDays = 0) {
    // Base tau for the section's difficulty
    const difficulty = currentSection.difficulty || 'Average';
    const repetitionCount = currentSection.completedRepetitions || 0;
    
    // Load profile data for adaptive systems
    const profileData = JSON.parse(localStorage.getItem(`mp_${currentProfile.id}_data`) || '{"musicPieces":[],"practiceHistory":[]}');
    
    // Get section history for adaptive learning
    const sectionHistory = profileData.practiceHistory ? 
        profileData.practiceHistory.filter(h => h.barSectionId === currentSection.id && !h.isDeleted) : [];
    
    // Calculate integrated tau using global adaptive managers
    let tau;
    try {
        if (adaptiveTauManager && personalizedMemoryCalibration && memoryStabilityManager) {
            // Calculate integrated tau (uses all adaptive systems)
            tau = adaptiveTauManager.calculateIntegratedTau(difficulty, repetitionCount, {
                barSectionId: currentSection.id,
                sectionHistory: sectionHistory,
                userAge: currentProfile.age,
                userExperience: currentProfile.experience,
                pmcManager: personalizedMemoryCalibration,
                stabilityManager: memoryStabilityManager,
                useAdaptiveSystems: true
            });
            
            console.log(`[AdaptiveTau] Integrated τ=${tau.toFixed(3)} (demographic + PMC + stability + performance)`);
        } else {
            // Fallback if managers not initialized
            console.warn('[AdaptiveTau] Adaptive managers not initialized, using basic calculation');
            if (window.MPLog) MPLog.warn('[AdaptiveTau] Adaptive managers not initialized, using basic calculation');
            tau = EbbinghausConstants.calculateAdjustedTau(difficulty, repetitionCount, stage);
        }
    } catch (error) {
        console.warn('[AdaptiveTau] Error in adaptive systems, falling back to basic calculation:', error);
        if (window.MPLog) MPLog.warn('[AdaptiveTau] Error in adaptive systems, falling back to basic calculation', { error: error && error.message });
        // Fallback to basic calculation
        tau = EbbinghausConstants.calculateAdjustedTau(difficulty, repetitionCount, stage);
    }
    
    // Performance-based retention target
    // Lower retention target = longer intervals (more forgetting acceptable)
    // Higher retention target = shorter intervals (less forgetting acceptable)
    let retentionTarget;
    switch (performance) {
        case 'Poor':
            retentionTarget = 0.90; // Need high retention → shorter interval
            break;
        case 'Fair':
            retentionTarget = 0.85; // Moderate retention → moderate interval
            break;
        case 'Good':
            retentionTarget = 0.80; // Standard retention → standard interval
            break;
        case 'Excellent':
            retentionTarget = 0.70; // Lower retention acceptable → longer interval
            break;
        default:
            retentionTarget = 0.80;
    }
    
    // Ebbinghaus formula: t = -τ * ln(R)
    // where R is the retention target
    // Guard against invalid retention values that would cause Infinity
    if (retentionTarget <= 0 || retentionTarget >= 1) {
        console.error('Invalid retentionTarget:', retentionTarget, 'for piece:', pieceId);
        retentionTarget = Math.max(0.01, Math.min(0.99, retentionTarget));
    }
    let rawInterval = -tau * Math.log(retentionTarget);
    
    // Additional safety check for Infinity result
    if (!isFinite(rawInterval)) {
        console.error('Infinity detected in rawInterval calculation. tau:', tau, 'retentionTarget:', retentionTarget);
        rawInterval = 1.0; // Fallback to 1 day
    }
    
    // Performance-based interval adjustment (matching C# AdjustForPerformanceScientific)
    // This is where overdue effects are handled: if you practiced late but still performed well,
    // your interval increases. If you practiced late and performed poorly, interval decreases.
    const performanceScore = performanceToScore(performance); // Convert to 0-10 scale
    const adjustmentFactor = calculatePerformanceAdjustment(performanceScore);
    const adjustedInterval = rawInterval * adjustmentFactor;
    
    // Clamp to scientific bounds [1, 365] and t ≤ 5×τ
    // Note: clampIntervalToScientificBounds returns an object { clampedIntervalDays, reason }
    const clampResult = EbbinghausConstants.clampIntervalToScientificBounds(adjustedInterval, tau);
    const clampedInterval = clampResult.clampedIntervalDays;
    
    if (actualIntervalDays > 0) {
        console.log(`[Overdue] Section was ${actualIntervalDays.toFixed(0)} days late. Performance: ${performance} (score ${performanceScore.toFixed(1)}) → adjustment factor: ${adjustmentFactor.toFixed(2)}x`);
    }
    console.log(`[Ebbinghaus] performance=${performance}, R*=${retentionTarget}, τ=${tau.toFixed(2)}, raw=${rawInterval.toFixed(2)}d, adjusted=${adjustedInterval.toFixed(2)}d → ${clampedInterval.toFixed(2)} days (reason: ${clampResult.reason})`);
    
    return clampedInterval;
}

// Convert performance string to numeric score (0-10 scale, matching C# PerformanceScore)
function performanceToScore(performance) {
    switch (performance) {
        case 'Poor': return 2.5;      // Struggle, needs much shorter interval
        case 'Fair': return 5.0;      // Some difficulty, needs shorter interval
        case 'Good': return 7.5;      // Solid performance, standard interval
        case 'Excellent': return 9.5; // Strong performance, can extend interval
        default: return 5.0;          // Neutral
    }
}

// Calculate performance-based interval adjustment factor
// Based on C# AdjustForPerformanceScientific logic
// Returns factor in range [0.3, 2.5]:
// - Poor performance (score ~2.5) → factor ~0.4-0.6 (shorter interval)
// - Fair performance (score ~5.0) → factor ~1.0 (no change)
// - Good performance (score ~7.5) → factor ~1.3-1.5 (longer interval)
// - Excellent performance (score ~9.5) → factor ~1.8-2.2 (much longer interval)
function calculatePerformanceAdjustment(performanceScore) {
    // Normalize to 0-1 range
    const normalized = Math.max(0.0, Math.min(1.0, performanceScore / 10.0));
    
    // Sigmoid mapping (C# uses steepness=6.0, midpoint=0.5)
    const steepness = 6.0;
    const midpoint = 0.5;
    const x = (normalized - midpoint) * steepness;
    
    // Sigmoid function with overflow protection
    let sigmoid;
    if (x > 50) sigmoid = 1.0;
    else if (x < -50) sigmoid = 0.0;
    else sigmoid = 1.0 / (1.0 + Math.exp(-x));
    
    // Map sigmoid [0,1] to adjustment range [0.4, 2.0]
    const sigmoidFactor = 0.4 + (sigmoid * 1.6);
    
    // Confidence modifier (boosts high performance more)
    let confidenceFactor;
    if (normalized <= 0.5) {
        confidenceFactor = 0.6 + (normalized * 0.8);
    } else {
        const exponent = (normalized - 0.5) * 2.0;
        const exponentialGain = Math.pow(exponent, 1.5);
        confidenceFactor = 1.0 + (exponentialGain * 0.8);
    }
    
    // Cognitive load factor (penalizes very poor performance)
    let cognitiveLoadFactor;
    if (normalized < 0.3) {
        const overloadSeverity = (0.3 - normalized) / 0.3;
        cognitiveLoadFactor = 0.3 + (0.4 * (1.0 - overloadSeverity));
    } else if (normalized < 0.7) {
        cognitiveLoadFactor = 0.8 + (normalized * 0.4);
    } else {
        cognitiveLoadFactor = 1.0 + ((normalized - 0.7) * 0.5);
    }
    
    // Combine factors (matching C# weights: 50% sigmoid, 30% confidence, 20% cognitive load)
    const combinedFactor = (sigmoidFactor * 0.5) + (confidenceFactor * 0.3) + (cognitiveLoadFactor * 0.2);
    
    // Clamp to safe range [0.3, 2.5]
    return Math.max(0.3, Math.min(2.5, combinedFactor));
}

// Save practice history for statistics (matches C# PracticeHistory structure)
function savePracticeHistory(session) {
    const profileData = JSON.parse(localStorage.getItem(`mp_${currentProfile.id}_data`) || '{"musicPieces":[],"practiceHistory":[]}');
    
    // Ensure practiceHistory array exists
    if (!profileData.practiceHistory) {
        profileData.practiceHistory = [];
    }
    
    // Get tempo values (optional tracking) - with safe fallback
    let targetTempo = 0;
    let achievedTempo = 0;
    
    try {
        const targetTempoEl = document.getElementById('targetTempo');
        const achievedTempoEl = document.getElementById('achievedTempo');
        
        if (targetTempoEl && targetTempoEl.value) {
            targetTempo = parseInt(targetTempoEl.value) || 0;
        }
        if (achievedTempoEl && achievedTempoEl.value) {
            achievedTempo = parseInt(achievedTempoEl.value) || 0;
        }
    } catch (error) {
        console.warn('Error reading tempo values, using 0:', error);
        targetTempo = 0;
        achievedTempo = 0;
    }
    
    // Create history entry (matching C# PracticeHistory properties)
    const historyEntry = {
        id: generateGUID(),
        musicPieceId: currentPiece.id,
        musicPieceTitle: currentPiece.title,
        barSectionId: currentSection.id,
        barSectionRange: currentSection.barRange,
        date: session.date,
        duration: session.durationSeconds * 1000, // Duration in milliseconds for adaptive systems
        durationMinutes: session.durationSeconds / 60, // Convert to minutes for statistics
        repetitions: session.completedRepetitions,
        difficulty: session.performance, // Using performance as difficulty indicator
        performanceScore: performanceToScore(session.performance), // Numeric score (0-10) for adaptive systems
        notes: session.notes,
        attemptsTillSuccess: session.failedAttempts,
        totalFailures: session.totalFailures,
        // New explicit fields for clarity:
        memoryFailures: session.totalFailures, // memory retrieval failures (streakResets)
        executionFailures: session.failedAttempts, // execution-related failures (attempts till success)
        sessionOutcome: session.successRate >= 0.7 ? 'TargetReached' : 'Incomplete',
        targetRepetitions: session.targetRepetitions,
        targetTempo: targetTempo,  // BPM goal for this session (0 = not tracked)
        achievedTempo: achievedTempo,  // Highest successful BPM during session (0 = not tracked)
        energyLevel: session.energyLevel, // NEW: Physiological Context Factor
        isDeleted: false
    };
    
    // Add to history
    profileData.practiceHistory.push(historyEntry);
    if (window.MPLog) MPLog.info('Practice history appended', { profileId: currentProfile?.id, sectionId: currentSection?.id, sessionId: historyEntry.id, repetitions: historyEntry.repetitions, successRate: historyEntry.successRate });
    
    // Apply adaptive learning updates after session (using global managers)
    try {
        if (adaptiveTauManager && personalizedMemoryCalibration && memoryStabilityManager) {
            // Apply rapid calibration (first 5 sessions)
            adaptiveTauManager.applyRapidCalibration(
                currentSection.id,
                historyEntry,
                currentSection,
                profileData.practiceHistory,
                personalizedMemoryCalibration
            );
            
            // Log PMC calibration stats for debugging
            const pmcStats = personalizedMemoryCalibration.getCalibrationStats();
            if (pmcStats && pmcStats.totalSessions > 0) {
                console.log(
                    `[PMC] Calibration stats: ` +
                    `Total sessions=${pmcStats.totalSessions}, ` +
                    `Calibrated=${pmcStats.isCalibrated}, ` +
                    `Adjustments=${Object.keys(pmcStats.difficultyAdjustments).length} difficulties tracked`
                );
                
                // Log per-difficulty adjustments
                Object.entries(pmcStats.difficultyAdjustments).forEach(([diff, data]) => {
                    const trend = data.factor < 0.95 ? '⚡ Faster forgetting' : 
                                 data.factor > 1.05 ? '🧠 Stronger memory' : 
                                 '✓ Standard';
                    console.log(
                        `[PMC]   ${diff}: ${data.factor.toFixed(3)}x ` +
                        `(${(data.confidence * 100).toFixed(0)}% confidence, ${data.sessions} sessions) ${trend}`
                    );
                });
            }
            
            // Capture pre-update memory stats for accurate logging of what was used for calculation
            const preUpdateMemoryStats = memoryStabilityManager.getMemoryStats(currentSection.id);

            // Update memory stability tracking
            memoryStabilityManager.updateMemoryStability(
                currentSection.id,
                historyEntry
            );
            
            // Log memory stats for debugging
            const memoryStats = memoryStabilityManager.getMemoryStats(currentSection.id);
            if (memoryStats && !memoryStats.isNew) {
                console.log(
                    `[MemoryStability] Current stats: ` +
                    `S=${memoryStats.stability.toFixed(1)}d, ` +
                    `D=${memoryStats.difficulty.toFixed(3)}, ` +
                    `R=${memoryStats.currentRetrievability.toFixed(3)}, ` +
                    `Reviews=${memoryStats.reviewCount}, ` +
                    `Progress=${memoryStats.learningProgress.toFixed(1)}%`
                );
            }
            
            // Get current difficulty adjustment for this section
            const sectionDifficulty = (currentSection.difficulty || 'Average').toLowerCase();
            const diffAdj = pmcStats?.difficultyAdjustments[sectionDifficulty];
            const adjustmentInfo = diffAdj ? 
                `${diffAdj.factor.toFixed(3)}x (${(diffAdj.confidence * 100).toFixed(0)}% confidence)` : 
                'Not yet calibrated';
            
            // Show comprehensive learning summary
            console.log(
                `\n${'='.repeat(60)}\n` +
                `📊 ADAPTIVE LEARNING SUMMARY\n` +
                `${'='.repeat(60)}\n` +
                `Section: ${currentSection.barRange}\n` +
                `Performance: ${historyEntry.difficulty} (${historyEntry.performanceScore.toFixed(1)}/10)\n` +
                `Stage: ${currentSection.practiceScheduleStage || 0}\n` +
                `Difficulty: ${currentSection.difficulty || 'Average'}\n` +
                `${'─'.repeat(60)}\n` +
                `📚 Personalized Memory Calibration (PMC):\n` +
                `  Total Sessions: ${pmcStats?.totalSessions || 0}\n` +
                `  Calibration: ${pmcStats?.isCalibrated ? '✅ Active' : '⏳ Learning'}\n` +
                `  This Difficulty Adjustment: ${adjustmentInfo}\n` +
                `${'─'.repeat(60)}\n` +
                `🧠 Memory Stability (SM-17+):\n` +
                `  Stability (S): ${preUpdateMemoryStats?.isNew ? 'NEW' : preUpdateMemoryStats?.stability.toFixed(1) + 'd'} ➔ ${memoryStats?.stability.toFixed(1)}d\n` +
                `  Difficulty (D): ${memoryStats?.isNew ? 'N/A (0.30)' : memoryStats?.difficulty.toFixed(3)}\n` +
                `  Retrievability (R): ${preUpdateMemoryStats?.isNew ? 'N/A' : preUpdateMemoryStats?.currentRetrievability.toFixed(3)} (was ${preUpdateMemoryStats?.daysSinceLastReview.toFixed(1)} days ago)\n` +
                `  Review Count: ${memoryStats?.reviewCount || 0}\n` +
                `  Learning Progress: ${memoryStats?.isNew ? 'N/A' : memoryStats?.learningProgress.toFixed(1) + '%'}\n` +
                `${'='.repeat(60)}\n`
            );
            
            console.log('[AdaptiveLearning] ✅ Post-session updates completed (PMC + MemoryStability)');
            if (window.MPLog) MPLog.info('AdaptiveLearning post-session updates completed', { sectionId: currentSection?.id, sessionId: historyEntry.id });
            if (window.MPLog) MPLog.debug('AdaptiveLearning Details', { pmcStats, memoryStats, preUpdateMemoryStats });
        } else {
            console.warn('[AdaptiveLearning] ⚠️ Adaptive managers not available for post-session updates');
        }
    } catch (error) {
        console.warn('[AdaptiveLearning] ❌ Error in post-session updates:', error);
    }
    
    // Limit history to prevent storage overflow (keep last 5000 entries like C# version)
    const maxHistoryRecords = 5000;
    if (profileData.practiceHistory.length > maxHistoryRecords) {
        // Sort by date (most recent first) and keep only the most recent
        profileData.practiceHistory.sort((a, b) => new Date(b.date) - new Date(a.date));
        profileData.practiceHistory = profileData.practiceHistory.slice(0, maxHistoryRecords);
    }
    
    // Update statistics
    if (!profileData.statistics) {
        profileData.statistics = { totalSessions: 0, totalPracticeTime: 0 };
    }
    profileData.statistics.totalSessions = (profileData.statistics.totalSessions || 0) + 1;
    profileData.statistics.totalPracticeTime = (profileData.statistics.totalPracticeTime || 0) + historyEntry.durationMinutes;
    
    // Save back to localStorage with quota check
    try {
        // Snapshot for saved data validation (logs only)
        try {
            const savedPiece = profileData.musicPieces[pieceIndex];
            const savedSection = savedPiece.barSections.find(s => s.id === currentSection.id);
            if (window.MPLog) MPLog.debug('SavingData snapshot (pre-save)', {
                pieceId: currentPiece.id,
                sectionId: currentSection.id,
                savedStage: savedSection?.practiceScheduleStage,
                savedCompleted: savedSection?.completedRepetitions,
                savedTarget: savedSection?.targetRepetitions
            });
        } catch (err) {
            // ignore debug snapshot errors
        }
        storageQuotaManager.safeSetItem(`mp_${currentProfile.id}_data`, JSON.stringify(profileData));
    } catch (error) {
        if (error.name === 'QuotaExceededError') {
            console.warn('Storage quota exceeded, attempting cleanup...');
            storageQuotaManager.emergencyCleanup();
            try {
                storageQuotaManager.safeSetItem(`mp_${currentProfile.id}_data`, JSON.stringify(profileData));
            } catch (retryError) {
                alert('⚠️ Opslag vol! Exporteer je data en ruim oude profielen op.');
                throw retryError;
            }
        } else {
            throw error;
        }
    }
    
    // Mark as having unsaved changes
    sessionStorage.setItem(storagePrefix + 'hasUnsavedChanges', 'true');
    
    console.log('Practice history saved:', historyEntry);
}

// Save data to localStorage
function saveData() {
    // Validate that we have the required data to save
    if (!currentProfile || !currentProfile.id) {
        console.error('Cannot save: currentProfile is invalid');
        throw new Error('Invalid profile data - cannot save session');
    }
    
    if (!currentPiece || !currentPiece.id) {
        console.error('Cannot save: currentPiece is invalid');
        throw new Error('Invalid piece data - cannot save session');
    }
    
    if (!currentSection || !currentSection.id) {
        console.error('Cannot save: currentSection is invalid');
        throw new Error('Invalid section data - cannot save session');
    }
    
    // Load fresh data from localStorage to avoid overwriting concurrent changes
    const profileData = JSON.parse(localStorage.getItem(`mp_${currentProfile.id}_data`) || '{"musicPieces":[]}');
    
    // Ensure musicPieces array exists
    if (!profileData.musicPieces) {
        profileData.musicPieces = [];
    }
    
    // Find the piece in the fresh data
    const pieceIndex = profileData.musicPieces.findIndex(p => p.id === currentPiece.id);
    
    if (pieceIndex === -1) {
        console.error('Cannot save: piece not found in profile data');
        throw new Error('Piece not found - data may be corrupted. Please export your data and contact support.');
    }
    
    // Update the piece with our current changes
    profileData.musicPieces[pieceIndex] = currentPiece;
    
    // Validate the data structure before saving
    if (!profileData.musicPieces[pieceIndex].barSections || 
        !Array.isArray(profileData.musicPieces[pieceIndex].barSections)) {
        console.error('Cannot save: barSections is missing or invalid');
        throw new Error('Invalid data structure - barSections missing');
    }

    // Save back to localStorage with quota check
    try {
        storageQuotaManager.safeSetItem(`mp_${currentProfile.id}_data`, JSON.stringify(profileData));
        console.log('✅ Data saved successfully');
        // Confirm saved snapshot for debugging
        try {
            const confirmData = JSON.parse(localStorage.getItem(`mp_${currentProfile.id}_data`));
            const confirmPiece = confirmData.musicPieces.find(p => p.id === currentPiece.id);
            const confirmSection = confirmPiece && confirmPiece.barSections ? confirmPiece.barSections.find(s => s.id === currentSection.id) : null;
            if (window.MPLog) MPLog.debug('SavedData snapshot (post-save)', { pieceId: currentPiece.id,
                sectionId: currentSection.id,
                savedStage: confirmSection?.practiceScheduleStage,
                savedCompleted: confirmSection?.completedRepetitions,
                savedTarget: confirmSection?.targetRepetitions,
                nextReview: confirmSection?.nextReviewDate
            });
        } catch (err) {
            // Ignore errors during post-save confirmation
        }
        if (window.MPLog) MPLog.info('Data saved to localStorage', {
            profileId: currentProfile.id,
            pieceId: currentPiece.id,
            sectionId: currentSection.id,
            practiceScheduleStage: currentSection.practiceScheduleStage,
            completedRepetitions: currentSection.completedRepetitions,
            targetRepetitions: currentSection.targetRepetitions,
            nextReviewDate: currentSection.nextReviewDate
        });
    } catch (error) {
        if (error.name === 'QuotaExceededError') {
            console.warn('Storage quota exceeded, attempting cleanup...');
            storageQuotaManager.emergencyCleanup();
            try {
                storageQuotaManager.safeSetItem(`mp_${currentProfile.id}_data`, JSON.stringify(profileData));
                console.log('✅ Data saved after cleanup');
            } catch (retryError) {
                alert('⚠️ Opslag vol! Exporteer je data en ruim oude profielen op.');
                throw retryError;
            }
        } else {
            console.error('Save failed:', error);
            throw error;
        }
    }
    
    // Mark as having unsaved changes
    sessionStorage.setItem(storagePrefix + 'hasUnsavedChanges', 'true');
}

// Generate GUID
// GUID generation now in moduspractica-utils.js

// Close modal on ESC key
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        const modal = document.getElementById('evaluationModal');
        if (modal.classList.contains('active')) {
            closeEvaluationModal();
        }
    }
});

// Close modal on backdrop click
document.getElementById('evaluationModal').addEventListener('click', (e) => {
    if (e.target.id === 'evaluationModal') {
        closeEvaluationModal();
    }
});

// Manual time editing functions
function enableManualEdit() {
    // Pause timer if running
    if (isRunning) {
        pauseTimer();
    }
    
    isManuallyEditing = true;
    const display = document.getElementById('timerDisplay');
    
    // Don't select all text - let user position cursor naturally
    // Just focus the element so user can click and edit individual digits
    display.focus();
}

function handleTimeKeypress(event) {
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

function saveManualTime() {
    if (!isManuallyEditing) return;
    
    isManuallyEditing = false;
    const display = document.getElementById('timerDisplay');
    const timeText = display.textContent.trim();
    
    // Parse the time format HH:MM:SS
    const parts = timeText.split(':');
    
    if (parts.length !== 3) {
        alert('⚠️ Invalid time format!\n\nPlease use HH:MM:SS format (e.g., 00:15:30)');
        updateTimerDisplay();
        return;
    }
    
    // Validate that all parts are valid numbers
    const hours = parseInt(parts[0], 10);
    const minutes = parseInt(parts[1], 10);
    const seconds = parseInt(parts[2], 10);
    
    if (isNaN(hours) || isNaN(minutes) || isNaN(seconds)) {
        alert('⚠️ Invalid time values!\n\nAll time components must be numbers.\n\nFormat: HH:MM:SS (e.g., 00:15:30)');
        updateTimerDisplay();
        return;
    }
    
    // Validate ranges
    if (hours < 0 || hours > 23) {
        alert('⚠️ Invalid hours!\n\nHours must be between 0 and 23.\n\nYou entered: ' + hours);
        updateTimerDisplay();
        return;
    }
    
    if (minutes < 0 || minutes > 59) {
        alert('⚠️ Invalid minutes!\n\nMinutes must be between 0 and 59.\n\nYou entered: ' + minutes);
        updateTimerDisplay();
        return;
    }
    
    if (seconds < 0 || seconds > 59) {
        alert('⚠️ Invalid seconds!\n\nSeconds must be between 0 and 59.\n\nYou entered: ' + seconds);
        updateTimerDisplay();
        return;
    }
    
    // Calculate total seconds
    const newTotalSeconds = (hours * 3600) + (minutes * 60) + seconds;
    
    // Validate reasonable practice session duration (max 6 hours)
    const maxSeconds = 6 * 3600; // 6 hours
    if (newTotalSeconds > maxSeconds) {
        alert('⚠️ Duration too long!\n\nMaximum practice session duration is 6 hours.\n\nYou entered: ' + 
              hours + 'h ' + minutes + 'm ' + seconds + 's\n\n' +
              'If you really practiced this long, please split it into multiple sessions.');
        updateTimerDisplay();
        return;
    }
    
    // Warn if duration is suspiciously short (less than 10 seconds) but allow it
    if (newTotalSeconds > 0 && newTotalSeconds < 10) {
        const proceed = confirm('⚠️ Very short duration!\n\nYou entered: ' + 
                               hours + 'h ' + minutes + 'm ' + seconds + 's\n\n' +
                               'This is less than 10 seconds.\n\nContinue anyway?');
        if (!proceed) {
            updateTimerDisplay();
            return;
        }
    }
    
    // Update timer state
    totalElapsedSeconds = newTotalSeconds;
    pausedTime = newTotalSeconds * 1000; // Convert to milliseconds
    
    // Update display
    updateTimerDisplay();
    
    // Update button states
    updateButtonStates();
    
    console.log('Manual time set:', {
        hours, minutes, seconds,
        totalSeconds: totalElapsedSeconds,
        formatted: timeText
    });
}

// Auto-save draft functions
function startAutoSave() {
    autoSaveInterval = setInterval(() => {
        if (totalElapsedSeconds > 0 || failedAttempts > 0 || correctRepetitions > 0 || streakResets > 0) {
            if (window.MPLog) MPLog.debug('Auto-save triggered', { totalElapsedSeconds, failedAttempts, correctRepetitions, streakResets });
            saveDraft();
        }
    }, 30000); // Every 30 seconds
    if (window.MPLog) MPLog.info('Auto-save started for practice session', { intervalSeconds: 30 });
}

function saveDraft(isFinalSave = false) {
    // Prevent concurrent saves (skip unless final save)
    if (isSaving && !isFinalSave) {
        console.log('⏭️ Skipping save - another save in progress');
        return;
    }
    
    isSaving = true;
    if (window.MPLog) MPLog.info('Saving draft', { isFinalSave });
    
    const draftKey = `mp_draft_${currentPiece.id}_${sectionId}`;
    const draft = {
        timestamp: new Date().toISOString(),
        totalElapsedSeconds: totalElapsedSeconds,
        failedAttempts: failedAttempts,
        correctRepetitions: correctRepetitions,
        streakResets: streakResets,
        targetRepetitions: targetRepetitions,
        notes: document.getElementById('sessionNotes').value,
        targetTempo: document.getElementById('targetTempo').value,
        achievedTempo: document.getElementById('achievedTempo').value,
        isRunning: isRunning,
        energyLevel: currentEnergy, // Save current energy level
        // Dr. Gebrian state
        errorsBeforeFirstCorrect: errorsBeforeFirstCorrect,
        hasAchievedFirstCorrect: hasAchievedFirstCorrect,
        gebrianTargetLocked: gebrianTargetLocked,
        overlearningIntensity: overlearningIntensity
    };
    
    try {
        storageQuotaManager.safeSetItem(draftKey, JSON.stringify(draft));
        if (isFinalSave) {
            console.log('💾 Final draft saved:', new Date().toLocaleTimeString());
            if (window.MPLog) MPLog.info('Final draft saved', { time: new Date().toISOString() });
        } else {
            console.log('💾 Draft auto-saved:', new Date().toLocaleTimeString());
            if (window.MPLog) MPLog.debug('Draft auto-saved', { time: new Date().toISOString() });
        }
    } catch (error) {
        // Draft save is niet kritiek - log only
        console.warn('Could not save draft (storage full):', error.message);
    } finally {
        isSaving = false;
    }
}

function loadDraft() {
    const draftKey = `mp_draft_${currentPiece.id}_${sectionId}`;
    const draftJson = localStorage.getItem(draftKey);
    
    if (!draftJson) return;
    
    try {
        const draft = JSON.parse(draftJson);
        const draftAge = Date.now() - new Date(draft.timestamp).getTime();
        
        // Only load if draft is less than 24 hours old
        if (draftAge < 24 * 60 * 60 * 1000) {
            const draftAgeMinutes = Math.round(draftAge / (1000 * 60));
            console.log(`📝 Found draft from ${draftAgeMinutes} minutes ago`);
            if (window.MPLog) MPLog.info('Found draft', { draftAgeMinutes });
            
            if (confirm('📝 Found unsaved practice session draft from ' + 
                       new Date(draft.timestamp).toLocaleString(undefined) + 
                       '\n\nDo you want to restore it?\n\n[OK = Yes] [Cancel = No]')) {
                
                console.log('✅ User chose to restore draft');
                if (window.MPLog) MPLog.info('Draft restore chosen', { draftAge: draftAgeMinutes });
                totalElapsedSeconds = draft.totalElapsedSeconds || 0;
                failedAttempts = draft.failedAttempts || 0;
                correctRepetitions = draft.correctRepetitions || 0;
                streakResets = draft.streakResets || 0;
                
                // Restore Dr. Gebrian state
                errorsBeforeFirstCorrect = draft.errorsBeforeFirstCorrect || 0;
                hasAchievedFirstCorrect = draft.hasAchievedFirstCorrect || false;
                gebrianTargetLocked = draft.gebrianTargetLocked || false;
                overlearningIntensity = draft.overlearningIntensity || 100;
                
                // Restore UI state for intensity buttons
                if (overlearningIntensity === 50) {
                    setOverlearningIntensity(50);
                } else {
                    setOverlearningIntensity(100);
                }
                const draftTargetReps = draft.targetRepetitions && draft.targetRepetitions > 0
                    ? draft.targetRepetitions
                    : 6;
                const defaultTargetReps = currentSection.targetRepetitions || 6;
                targetRepetitions = draftTargetReps;
                // If draft target differs from default, user likely adjusted it manually
                userManuallySetTarget = (draftTargetReps !== defaultTargetReps);
                pausedTime = totalElapsedSeconds * 1000;
                
                document.getElementById('sessionNotes').value = draft.notes || '';
                document.getElementById('targetReps').textContent = targetRepetitions;
                
                // Restore tempo from draft (has priority over auto-fill)
                if (draft.targetTempo) {
                    document.getElementById('targetTempo').value = draft.targetTempo;
                }
                if (draft.achievedTempo) {
                    document.getElementById('achievedTempo').value = draft.achievedTempo;
                }

                // Restore energy level from draft
                if (draft.energyLevel) {
                    setEnergy(draft.energyLevel);
                }
                
                updateTimerDisplay();
                updateTrackingMetrics();
                updateSuccessRateDisplay();
                updateButtonStates();
                
                if (draft.isRunning) {
                    alert('ℹ️ Timer was running when draft was saved. Please restart manually if needed.');
                }
                
                console.log('✅ Draft restored successfully');
                if (window.MPLog) MPLog.info('Draft restored successfully', { draftKey });
            } else {
                // User declined, clean up old draft
                console.log('❌ User declined draft restore - removing draft');
                if (window.MPLog) MPLog.info('Draft restore declined - removed draft', { draftKey });
                localStorage.removeItem(draftKey);
            }
        } else {
            // Clean up old draft (older than 24 hours)
            localStorage.removeItem(draftKey);
            console.log('Old draft removed (>24h)');
            if (window.MPLog) MPLog.info('Old draft removed (>24h)', { draftKey });
        }
    } catch (e) {
        console.error('Error loading draft:', e);
        if (window.MPLog) MPLog.error('Error loading draft', { error: e && e.message });
        localStorage.removeItem(draftKey);
    }
}

function clearDraft() {
    if (!currentPiece || !sectionId) return;
    const draftKey = `mp_draft_${currentPiece.id}_${sectionId}`;
    localStorage.removeItem(draftKey);
    console.log('Draft cleared');
}

// Update unsaved changes indicator
function updateUnsavedIndicator() {
    const indicator = document.getElementById('unsavedIndicator');
    if (!indicator) return;
    
    const hasChanges = totalElapsedSeconds > 0 || 
                       failedAttempts > 0 || 
                       correctRepetitions > 0 ||
                       streakResets > 0 ||
                       document.getElementById('sessionNotes')?.value.trim() !== '';
    
    if (hasChanges && !sessionCompleted) {
        indicator.classList.add('visible');
    } else {
        indicator.classList.remove('visible');
    }
}

// ============================================================================
// INTENSITY MODULE FUNCTIONS
// ============================================================================

// Load Intensity Module settings
function loadIntensityModuleSettings() {
    try {
        if (!currentProfile) {
            console.warn('[IntensityModule] No current profile, skipping settings load');
            return;
        }
        
        const settingsKey = `mp_${currentProfile.id}_intensitySettings`;
        let settingsJson = localStorage.getItem(settingsKey);

        // Backward compatibility: check for older/legacy storage key used elsewhere in the app
        if (!settingsJson) {
            const legacyProfileId = localStorage.getItem('mp_currentProfile');
            if (legacyProfileId && legacyProfileId !== currentProfile.id) {
                const legacyKey = `mp_${legacyProfileId}_intensitySettings`;
                const legacyJson = localStorage.getItem(legacyKey);
                if (legacyJson) {
                    // Migrate legacy settings to the current profile key and use them
                    try {
                        localStorage.setItem(settingsKey, legacyJson);
                        // Optional: keep legacy key for compatibility and do not delete silently
                        console.log(`[IntensityModule] Migrated settings from ${legacyKey} to ${settingsKey}`);
                        settingsJson = legacyJson;
                    } catch (e) {
                        console.warn('[IntensityModule] Failed migrating legacy settings:', e);
                    }
                }
            }
        }
        
        if (settingsJson) {
            const settings = JSON.parse(settingsJson);
            intensityModuleEnabled = settings.enabled !== undefined ? settings.enabled : true;
            enableMicroBreaks = settings.enableMicroBreaks !== undefined ? settings.enableMicroBreaks : true;
        } else {
            // Default: enabled for new profiles
            intensityModuleEnabled = true;
            enableMicroBreaks = true;
        }
        
        // Load adaptive time per CR calibration
        if (sectionId) {
            // Preferred aggregated storage (mp_[profile]_adaptiveTimeCalibration)
            const aggregatedKey = `mp_${currentProfile.id}_adaptiveTimeCalibration`;
            const aggregatedJson = localStorage.getItem(aggregatedKey);

            if (aggregatedJson) {
                try {
                    const aggregatedData = JSON.parse(aggregatedJson);
                    if (aggregatedData && aggregatedData[sectionId] && aggregatedData[sectionId].avgTimePerCR) {
                        adaptiveTimePerCR = aggregatedData[sectionId].avgTimePerCR;
                        adaptiveCalibrationSessions = Number(aggregatedData[sectionId].sessionCount || 0);
                        console.log(`[IntensityModule] Loaded adaptive T̄_CR (aggregated): ${adaptiveTimePerCR.toFixed(1)}s`);
                    }
                } catch (parseError) {
                    console.warn('[IntensityModule] Failed to parse aggregated calibration data:', parseError);
                }
            }

            // Legacy per-section storage fallback
            if (!adaptiveTimePerCR) {
                const legacyKey = `mp_${currentProfile.id}_section_${sectionId}_timeCalibration`;
                const legacyJson = localStorage.getItem(legacyKey);
                if (legacyJson) {
                    try {
                        const legacyData = JSON.parse(legacyJson);
                        if (legacyData && legacyData.avgTimePerCR) {
                            adaptiveTimePerCR = legacyData.avgTimePerCR;
                            adaptiveCalibrationSessions = Number(legacyData.sessionCount || 0);
                            console.log(`[IntensityModule] Loaded adaptive T̄_CR (legacy): ${adaptiveTimePerCR.toFixed(1)}s`);
                        }
                    } catch (legacyError) {
                        console.warn('[IntensityModule] Failed to parse legacy calibration data:', legacyError);
                    }
                }
            }
        }
        
        console.log(`[IntensityModule] Settings loaded - Enabled: ${intensityModuleEnabled}, Micro-Breaks: ${enableMicroBreaks}`);
    } catch (error) {
        console.error('[IntensityModule] Error loading settings:', error);
        intensityModuleEnabled = true; // Fallback to enabled
        enableMicroBreaks = true; // Fallback to enabled
    }
    
    // Show/hide intensity display based on settings
    updateIntensityDisplayVisibility();
}

// Update Intensity Module display visibility
function updateIntensityDisplayVisibility() {
    const intensityDisplay = document.getElementById('intensityDisplay');
    if (intensityDisplay) {
        intensityDisplay.style.display = intensityModuleEnabled ? 'block' : 'none';
    }
}

// Update Intensity Module display with current TDS, phase, and OLQ
function updateIntensityDisplay() {
    if (!intensityModuleEnabled || typeof IntensityModule === 'undefined') {
        return;
    }

    try {
        currentTDS = IntensityModule.calculateTDS(correctRepetitions, failedAttempts);
        const phaseKey = IntensityModule.getLearningPhase(currentTDS);
        const olqData = IntensityModule.calculateOLQ(currentTDS, {
            failedAttempts,
            correctRepetitions
        });

        currentPhase = phaseKey;
        currentOLQ = olqData;

        // DISABLED: Dr. Gebrian methode beheert nu de target repetitions
        // Intensity Module mag NIET meer automatisch de target aanpassen
        // Target wordt ONE TIME berekend bij eerste correcte rep: T = MAX(5, E)
        if (currentOLQ && currentOLQ.recommended !== targetRepetitions) {
            console.log(`[IntensityModule] OLQ recommends ${currentOLQ.recommended} reps, but Dr. Gebrian target is locked at ${targetRepetitions}`);
        }

        const phaseDisplay = {
            INITIAL_ACQUISITION: { emoji: '🎯', label: 'Initial Acquisition', barColor: '#e74c3c' },
            REFINEMENT: { emoji: '🌱', label: 'Refinement', barColor: '#e67e22' },
            CONSOLIDATION: { emoji: '🔥', label: 'Consolidation', barColor: '#f1c40f' },
            MASTERY: { emoji: '📈', label: 'Mastery', barColor: '#27ae60' },
            OVERLEARNING: { emoji: '🎓', label: 'Overlearning', barColor: '#8e44ad' }
        };

        const displayInfo = phaseDisplay[phaseKey] || { emoji: '⚪', label: 'Unknown', barColor: '#95a5a6' };

        const tdsValue = document.getElementById('tdsValue');
        const tdsBar = document.getElementById('tdsBar');
        const phaseBadge = document.getElementById('phaseBadge');
        const olqProgress = document.getElementById('olqProgress');
        const olqProgressBar = document.getElementById('olqProgressBar');
        const olqTarget = document.getElementById('olqTarget');

        // Calculate Display TDS (Failure Rate) instead of Success Rate
        // User requested TDS to reflect difficulty (errors), so we show % Failed
        const totalAttempts = correctRepetitions + failedAttempts;
        let displayTDS = 0;
        if (totalAttempts > 0) {
            displayTDS = (failedAttempts / totalAttempts) * 100;
        }

        if (tdsValue) {
            tdsValue.textContent = `${Math.round(displayTDS)}%`;
        }

        if (tdsBar) {
            const width = Math.max(0, Math.min(100, Math.round(displayTDS)));
            tdsBar.style.width = `${width}%`;
            tdsBar.style.background = displayInfo.barColor;
        }

        if (phaseBadge) {
            phaseBadge.textContent = `${displayInfo.emoji} ${displayInfo.label}`;
        }

        const targetForDisplay = Math.max(1, targetRepetitions);

        if (olqProgress) {
            olqProgress.textContent = `${correctRepetitions}/${targetForDisplay}`;
        }

        if (olqProgressBar) {
            const ratio = targetForDisplay > 0 ? (correctRepetitions / targetForDisplay) : 0;
            const progress = Math.max(0, Math.min(100, Math.round(ratio * 100)));
            olqProgressBar.style.width = `${progress}%`;

            if (progress < 50) {
                olqProgressBar.style.background = '#e74c3c';
            } else if (progress < 100) {
                olqProgressBar.style.background = '#f39c12';
            } else {
                olqProgressBar.style.background = '#27ae60';
            }
        }

        if (olqTarget) {
            // Always show a clean target without warnings
            olqTarget.textContent = `Target: ${targetForDisplay} reps`;
        }

        // Estimated Duration display (uses adaptive time per CR if available; otherwise phase defaults)
        const durationEl = document.getElementById('durationEstimate');
        const durationDetailsEl = document.getElementById('durationDetails');
        if (durationEl) {
            let minutes = 0;
            try {
                if (adaptiveTimePerCR && targetForDisplay > 0) {
                    const seconds = adaptiveTimePerCR * targetForDisplay;
                    minutes = Math.max(0, Math.round(seconds / 60));
                } else if (typeof IntensityModule !== 'undefined') {
                    const est = IntensityModule.predictSessionDuration(targetForDisplay, currentTDS || 0);
                    minutes = Math.max(0, est?.durationMinutes || 0);
                }
            } catch (e) {
                console.warn('[IntensityModule] Could not compute duration estimate:', e);
            }
            durationEl.textContent = minutes > 0 ? `Estimated Duration: ~ ${minutes} min` : 'Estimated Duration: —';

            // Optional range and confidence indicator
            if (durationDetailsEl) {
                if (minutes > 0) {
                    const band = Math.max(1, Math.round(minutes * 0.2)); // ±20%, min 1 minute
                    const minM = Math.max(1, minutes - band);
                    const maxM = minutes + band;
                    let conf = 'Low';
                    if (adaptiveCalibrationSessions >= 8) conf = 'High';
                    else if (adaptiveCalibrationSessions >= 3) conf = 'Medium';
                    durationDetailsEl.textContent = `Range: ${minM}–${maxM} min · Confidence: ${conf}`;
                } else {
                    durationDetailsEl.textContent = '';
                }
            }
        }

    } catch (error) {
        console.error('[IntensityModule] Error updating display:', error);
    }
}

// Save adaptive time per CR calibration
function saveAdaptiveTimeCalibration() {
    if (!currentSection || !currentProfile) return;
    
    try {
        // Get section history
        const profileData = JSON.parse(localStorage.getItem(`mp_${currentProfile.id}_data`) || '{}');
        const sectionHistory = profileData.practiceHistory ? 
            profileData.practiceHistory.filter(h => h.barSectionId === currentSection.id && !h.isDeleted) : [];
        
        // Calculate average time per CR using IntensityModule
        if (typeof IntensityModule !== 'undefined') {
            const avgTime = (typeof IntensityModule.calculateAverageTimePerCRRobust === 'function')
                ? IntensityModule.calculateAverageTimePerCRRobust(sectionHistory)
                : IntensityModule.calculateAverageTimePerCR(sectionHistory);
            
            if (avgTime > 0) {
                const calibrationKey = `mp_${currentProfile.id}_section_${currentSection.id}_timeCalibration`;
                const calibration = {
                    avgTimePerCR: avgTime,
                    sessionCount: sectionHistory.length,
                    lastUpdated: new Date().toISOString()
                };
                
                localStorage.setItem(calibrationKey, JSON.stringify(calibration));
                adaptiveTimePerCR = avgTime;
                adaptiveCalibrationSessions = sectionHistory.length;
                
                console.log(`[IntensityModule] Saved adaptive T̄_CR: ${avgTime.toFixed(1)}s (${sectionHistory.length} sessions)`);
            }
        }
    } catch (error) {
        console.error('[IntensityModule] Error saving adaptive time calibration:', error);
    }
}