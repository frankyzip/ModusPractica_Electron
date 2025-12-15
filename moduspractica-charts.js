// ModusPractica Web App - Practice Charts Page
// Copyright (c) 2025 Frank De Baere - Partura Music™
// All rights reserved.

// Global state
let currentProfile = null;
let profileData = null;
let selectedPiece = null;
let selectedSection = null;
let chart = null;

const storagePrefix = 'mp_';

// Initialize page
document.addEventListener('DOMContentLoaded', () => {
    loadProfile();
    if (window.MPLog) MPLog.info('DOMContentLoaded: charts page');
    setupEventListeners();
});

// Load active profile
function loadProfile() {
    // Try both 'activeProfile' and 'lastProfile' keys for compatibility
    let activeProfileId = localStorage.getItem(`${storagePrefix}activeProfile`);
    if (!activeProfileId) {
        activeProfileId = localStorage.getItem(`${storagePrefix}lastProfile`);
    }
    
    if (!activeProfileId) {
        showNoProfileMessage();
        return;
    }
    
    const profilesKey = `${storagePrefix}profiles`;
    const profiles = JSON.parse(localStorage.getItem(profilesKey) || '[]');
    
    currentProfile = profiles.find(p => p.id === activeProfileId);
    
    if (!currentProfile) {
        showNoProfileMessage();
        return;
    }
    
    // Load profile data
    const dataKey = `${storagePrefix}${currentProfile.id}_data`;
    profileData = JSON.parse(localStorage.getItem(dataKey) || '{"musicPieces":[],"practiceHistory":[]}');
    try {
        profileData = normalizeProfilePracticeHistory(profileData);
        storageQuotaManager.safeSetItem(dataKey, JSON.stringify(profileData));
    } catch (error) {
        console.warn('Error normalizing practice history on charts load:', error);
        if (window.MPLog) MPLog.warn('Error normalizing practice history on charts load', { error: error && error.message });
    }
    
    // Update UI
    const profileNameEl = document.getElementById('profile-name');
    if (profileNameEl) {
        profileNameEl.textContent = currentProfile.name;
    }
    if (window.MPLog) MPLog.info('Loaded profile for charts', { profileId: currentProfile.id, name: currentProfile.name });
    
    loadMusicPieces();
}

// Setup event listeners
function setupEventListeners() {
    document.getElementById('pieceSelect').addEventListener('change', onPieceChange);
    document.getElementById('maxSessionsSelect').addEventListener('change', redrawChart);
    document.getElementById('includeDeletedCheck').addEventListener('change', redrawChart);
    document.getElementById('tempoOverlayCheck').addEventListener('change', redrawChart);
    
    // Logout button (returns to profile selection)
    const logoutBtn = document.getElementById('logout-btn');
    if (logoutBtn) {
        logoutBtn.addEventListener('click', () => {
            window.location.href = 'moduspractica-app.html';
        });
    }

    let resizeTimer;
    window.addEventListener('resize', () => {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(() => {
            if (selectedSection && chart) {
                drawChart();
            }
        }, 250);
    });
}

// Load music pieces into dropdown
function loadMusicPieces() {
    const pieceSelect = document.getElementById('pieceSelect');
    pieceSelect.innerHTML = '<option value="">Select a piece...</option>';
    
    if (!profileData.musicPieces || profileData.musicPieces.length === 0) {
        pieceSelect.innerHTML += '<option value="" disabled>No pieces found</option>';
        return;
    }
    
    profileData.musicPieces.forEach(piece => {
        const option = document.createElement('option');
        option.value = piece.id;
        option.textContent = `${piece.title} (${piece.composer})`;
        pieceSelect.appendChild(option);
    });
}

// Handle piece selection change
function onPieceChange(event) {
    const pieceId = event.target.value;
    
    if (!pieceId) {
        selectedPiece = null;
        clearSectionList();
        clearChart();
        return;
    }
    
    selectedPiece = profileData.musicPieces.find(p => p.id === pieceId);
    
    if (!selectedPiece) {
        clearSectionList();
        clearChart();
        return;
    }
    
    loadSections();
}

// Load sections for selected piece
function loadSections() {
    const sectionList = document.getElementById('sectionList');
    sectionList.innerHTML = '';
    
    // Check both 'sections' and 'barSections' properties
    const sections = selectedPiece.sections || selectedPiece.barSections || [];
    
    // Filter out archived sections (support both legacy flags and lifecycle states)
    const activeSections = sections.filter(section => {
        const lifecycleStateValue = Number(section.lifecycleState ?? (typeof LifecycleState !== 'undefined' ? LifecycleState.Active : 0));
        const isLifecycleArchived = lifecycleStateValue === (typeof LifecycleState !== 'undefined' ? LifecycleState.Inactive : 2);
        return !section.isArchived && !isLifecycleArchived;
    });
    
    if (activeSections.length === 0) {
        sectionList.innerHTML = '<div class="empty-state" style="padding: 60px 10px;">No sections found</div>';
        clearChart();
        return;
    }
    
    activeSections.forEach(section => {
        const sectionItem = document.createElement('div');
        sectionItem.className = 'section-item';
        sectionItem.textContent = section.barRange || section.range || 'Unknown range';
        sectionItem.dataset.sectionId = section.id;
        sectionItem.onclick = () => selectSection(section.id);
        sectionList.appendChild(sectionItem);
    });
}

// Select a section
function selectSection(sectionId) {
    const sections = selectedPiece.sections || selectedPiece.barSections || [];
    // Filter out archived sections
    const activeSections = sections.filter(section => {
        const lifecycleStateValue = Number(section.lifecycleState ?? (typeof LifecycleState !== 'undefined' ? LifecycleState.Active : 0));
        const isLifecycleArchived = lifecycleStateValue === (typeof LifecycleState !== 'undefined' ? LifecycleState.Inactive : 2);
        return !section.isArchived && !isLifecycleArchived;
    });
    selectedSection = activeSections.find(s => s.id === sectionId);
    
    if (!selectedSection) {
        clearChart();
        return;
    }
    
    // Update UI selection
    document.querySelectorAll('.section-item').forEach(item => {
        item.classList.toggle('selected', item.dataset.sectionId === sectionId);
    });
    
    // Draw chart
    drawChart();
    if (window.MPLog) MPLog.info('Section selected in charts', { pieceId: selectedPiece?.id, sectionId });
}

// Draw the chart
function drawChart() {
    if (!selectedSection) {
        clearChart();
        return;
    }
    
    // Get practice history for this section
    const sectionHistory = (profileData.practiceHistory || []).filter(h => 
        h.barSectionId === selectedSection.id
    );
    
    // Apply deleted filter
    const includeDeleted = document.getElementById('includeDeletedCheck').checked;
    const filteredHistory = includeDeleted 
        ? sectionHistory 
        : sectionHistory.filter(h => !h.isDeleted);
    
    if (filteredHistory.length < 2) {
        showEmptyChart('Not enough practice sessions (need at least 2)');
        if (window.MPLog) MPLog.warn('Not enough sessions for chart', { sectionId: selectedSection?.id, entries: filteredHistory.length });
        return;
    }
    
    // Get max sessions setting
    const maxSessions = parseInt(document.getElementById('maxSessionsSelect').value);
    
    // Show chart
    const container = document.getElementById('chartContainer');
    const canvas = document.getElementById('mainChartCanvas');
    const emptyState = canvas.previousElementSibling;
    emptyState.style.display = 'none';
    canvas.style.display = 'block';
    const measuredWidth = container ? Math.max(container.clientWidth - 20, 0) : 0;
    const targetWidth = measuredWidth || canvas.clientWidth || 1200;
    canvas.width = targetWidth;
    canvas.height = 700;
    
    // Initialize chart
    chart = new SuccessRatioTrendChart('mainChartCanvas', {
        maxSessions: maxSessions,
        highlightLatest: true,
        showLegend: true,
        showTempoOverlay: document.getElementById('tempoOverlayCheck').checked,
        showStartFrictionLine: true,
        frictionMaxAttempts: 5,
        width: targetWidth,
        height: 700
    });
    
    chart.draw(filteredHistory);
    if (window.MPLog) MPLog.info('Chart drawn', { sectionId: selectedSection.id, entries: filteredHistory.length, practiceScheduleStage: selectedSection.practiceScheduleStage, nextReviewDate: selectedSection.nextReviewDate, completedRepetitions: selectedSection.completedRepetitions, targetRepetitions: selectedSection.targetRepetitions });
    
    // Update summary
    updateSummary(filteredHistory);
    
    // Update sessions list
    updateSessionsList(filteredHistory);
}

// Update sessions list panel
function updateSessionsList(history) {
    const sessionsPanel = document.getElementById('sessionsPanel');
    const sessionsList = document.getElementById('sessionsList');
    
    if (history.length === 0) {
        sessionsPanel.style.display = 'none';
        return;
    }
    
    sessionsPanel.style.display = 'flex';
    
    // Sort by date (newest first)
    const sorted = [...history].sort((a, b) => new Date(b.date) - new Date(a.date));
    
    // Get max sessions setting
    const maxSessions = parseInt(document.getElementById('maxSessionsSelect').value);
    const displaySessions = sorted.slice(0, maxSessions);
    
    sessionsList.innerHTML = displaySessions.map(session => {
        const date = new Date(session.date);
        const dateStr = date.toLocaleDateString(undefined, { day: '2-digit', month: '2-digit', year: 'numeric' });
        const timeStr = date.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
        
            const reps = session.repetitions || 0;
            // Combined failures using helper (memoryFailures + executionFailures)
            const fails = getCombinedFailures(session);
        const total = reps + fails;
        const successRate = total > 0 ? (reps / total * 100).toFixed(0) : 0;
        
        const durationMin = session.durationMinutes 
            ? session.durationMinutes.toFixed(0) 
            : (session.duration ? (session.duration / 60000).toFixed(0) : '?');
        
        const performance = session.difficulty || 'N/A';
        const performanceClass = performance.toLowerCase();
        
        const tempo = session.achievedTempo > 0 ? `${session.achievedTempo} BPM` : '--';
        const attempts = getExecutionFailures(session);
        const frictionInfo = getStartFrictionInfo(attempts);
        
        return `
            <div class="session-card">
                <div class="session-date">${dateStr} • ${timeStr}</div>
                <div class="session-details">
                    <div class="session-detail-row">
                        <span class="session-label">Success Rate:</span>
                        <span class="session-value">${successRate}%</span>
                    </div>
                    <div class="session-detail-row">
                        <span class="session-label">Repetitions:</span>
                        <span class="session-value">${reps}</span>
                    </div>
                    <div class="session-detail-row">
                        <span class="session-label">Failures:</span>
                        <span class="session-value">${fails}</span>
                    </div>
                    <div class="session-detail-row">
                        <span class="session-label">Attempts Before Success:</span>
                        <span class="session-value">${attempts}</span>
                    </div>
                    <div class="session-detail-row">
                        <span class="session-label">Start Friction:</span>
                        <span class="session-friction ${frictionInfo.className}" title="${frictionInfo.tooltip}">${frictionInfo.label}</span>
                    </div>
                    <div class="session-detail-row">
                        <span class="session-label">Duration:</span>
                        <span class="session-value">${durationMin} min</span>
                    </div>
                    <div class="session-detail-row">
                        <span class="session-label">Tempo:</span>
                        <span class="session-value">${tempo}</span>
                    </div>
                    <div class="session-detail-row">
                        <span class="session-label">Performance:</span>
                        <span class="session-performance ${performanceClass}">${performance}</span>
                    </div>
                </div>
            </div>
        `;
    }).join('');
}

// Update summary statistics
function updateSummary(history) {
    const summaryBar = document.getElementById('summaryBar');
    const startFrictionChip = document.getElementById('startFrictionChip');
    summaryBar.style.display = 'flex';
    
    if (history.length === 0) {
        document.getElementById('currentRatio').textContent = '--';
        document.getElementById('avg7Ratio').textContent = '--';
        document.getElementById('zoneText').textContent = '--';
        document.getElementById('stageValue').textContent = '--';
        document.getElementById('sectionDuration').textContent = '--';
        document.getElementById('pieceDuration').textContent = '--';
        startFrictionChip.textContent = '--';
        startFrictionChip.className = 'summary-chip';
        startFrictionChip.removeAttribute('title');
        return;
    }
    
    // Sort by date
    const sorted = [...history].sort((a, b) => new Date(a.date) - new Date(b.date));
    
    // Calculate current session ratio
    const latest = sorted[sorted.length - 1];
    const currentReps = latest.repetitions || 0;
    // Combine streak-based memory failures with execution failed attempts for display
        const currentFails = getCombinedFailures(latest);
    const currentRatio = (currentReps + currentFails) > 0 
        ? currentReps / (currentReps + currentFails) 
        : 0;
    
    // Calculate 7-session average (cumulative)
    const last7 = sorted.slice(-7);
    let totalReps = 0;
    let totalFails = 0;
    last7.forEach(h => {
        totalReps += h.repetitions || 0;
            totalFails += getCombinedFailures(h);
    });
    const avg7Ratio = (totalReps + totalFails) > 0 
        ? totalReps / (totalReps + totalFails) 
        : 0;
    
    // Determine zone
    const zone = getZoneName(avg7Ratio);
    const zoneColor = getZoneColor(zone);
    
    // Get current stage from section
    const currentStage = selectedSection.practiceScheduleStage || 0;
    
    // Calculate total practice duration for this section (sum all sessions in minutes)
    let sectionMinutes = 0;
    history.forEach(h => {
        if (h.durationMinutes) {
            sectionMinutes += h.durationMinutes;
        } else if (h.duration) {
            sectionMinutes += h.duration / 60000; // Convert milliseconds to minutes
        }
    });
    
    // Calculate total practice duration for entire piece (all sections)
    let pieceMinutes = 0;
    if (selectedPiece && selectedPiece.practiceSessions) {
        selectedPiece.practiceSessions.forEach(session => {
            if (session.durationSeconds) {
                pieceMinutes += session.durationSeconds / 60; // Convert seconds to minutes
            } else if (session.duration) {
                pieceMinutes += session.duration / 60000; // Convert milliseconds to minutes
            }
        });
    }
    
    // Format section duration
    const sectionHours = Math.floor(sectionMinutes / 60);
    const sectionMins = Math.round(sectionMinutes % 60);
    const sectionDurationText = sectionHours > 0 
        ? `${sectionHours}h ${sectionMins}m` 
        : `${sectionMins}m`;
    
    // Format piece duration
    const pieceHours = Math.floor(pieceMinutes / 60);
    const pieceMins = Math.round(pieceMinutes % 60);
    const pieceDurationText = pieceHours > 0 
        ? `${pieceHours}h ${pieceMins}m` 
        : `${pieceMins}m`;
    
    // Update UI
    document.getElementById('currentRatio').textContent = `${(currentRatio * 100).toFixed(0)}%`;
    document.getElementById('avg7Ratio').textContent = `${(avg7Ratio * 100).toFixed(0)}%`;
    
    const zoneElement = document.getElementById('zoneText');
    zoneElement.textContent = zone;
    zoneElement.style.color = zoneColor;
    
    const attempts = getExecutionFailures(latest);
    const friction = getStartFrictionInfo(attempts);
    startFrictionChip.textContent = friction.label;
    startFrictionChip.className = `summary-chip ${friction.className}`;
    startFrictionChip.title = friction.tooltip;
    
    document.getElementById('stageValue').textContent = currentStage;
    document.getElementById('sectionDuration').textContent = sectionDurationText;
    document.getElementById('pieceDuration').textContent = pieceDurationText;
}

// Get zone name from ratio
function getZoneName(ratio) {
    if (ratio < 0.60) return 'Too Hard';
    if (ratio < 0.80) return 'Exploration';
    if (ratio < 0.90) return 'Consolidation';
    if (ratio < 0.95) return 'Mastery';
    return 'Overlearning';
}

// Get zone color
function getZoneColor(zone) {
    switch (zone) {
        case 'Too Hard': return '#C62828';
        case 'Exploration': return '#F57F17';
        case 'Consolidation': return '#E65100';
        case 'Mastery': return '#2E7D32';
        case 'Overlearning': return '#616161';
        default: return '#666';
    }
}

// Determine start friction category based on attempts before first success
function getStartFrictionInfo(attempts) {
    const safeAttempts = Number.isFinite(attempts) ? Math.max(0, Math.floor(attempts)) : 0;
    if (safeAttempts >= 4) {
        return {
            label: 'High',
            className: 'friction-high',
            tooltip: `${safeAttempts} attempts before first success`
        };
    }
    if (safeAttempts >= 2) {
        return {
            label: 'Moderate',
            className: 'friction-moderate',
            tooltip: `${safeAttempts} attempts before first success`
        };
    }
    return {
        label: 'Low',
        className: 'friction-low',
        tooltip: safeAttempts === 0
            ? 'Immediate success or not recorded'
            : '1 attempt before first success'
    };
}

// Redraw chart with new settings
function redrawChart() {
    if (window.MPLog) MPLog.debug('Redraw chart triggered', { selectedSectionId: selectedSection?.id, maxSessions: document.getElementById('maxSessionsSelect')?.value, includeDeleted: document.getElementById('includeDeletedCheck')?.checked });
    if (selectedSection) {
        drawChart();
    }
}

// Clear section list
function clearSectionList() {
    const sectionList = document.getElementById('sectionList');
    sectionList.innerHTML = '<div class="empty-state" style="padding: 60px 10px;">Select a piece first</div>';
    selectedSection = null;
}

// Clear chart and show empty state
function clearChart() {
    const canvas = document.getElementById('mainChartCanvas');
    const emptyState = canvas.previousElementSibling;
    canvas.style.display = 'none';
    emptyState.style.display = 'block';
    
    document.getElementById('summaryBar').style.display = 'none';
    document.getElementById('sessionsPanel').style.display = 'none';
}

// Show empty chart message
function showEmptyChart(message) {
    const canvas = document.getElementById('mainChartCanvas');
    const emptyState = canvas.previousElementSibling;
    canvas.style.display = 'none';
    emptyState.style.display = 'block';
    emptyState.innerHTML = `<p style="color: #999; font-style: italic;">${message}</p>`;
    
    document.getElementById('summaryBar').style.display = 'none';
    document.getElementById('sessionsPanel').style.display = 'none';
}

// Show no profile message
function showNoProfileMessage() {
    const profileNameEl = document.getElementById('profile-name');
    if (profileNameEl) {
        profileNameEl.textContent = 'No profile';
    }
    document.getElementById('pieceSelect').disabled = true;
    
    const chartContainer = document.getElementById('chartContainer');
    chartContainer.innerHTML = `
        <div class="empty-state">
            <p style="margin-bottom: 20px; font-size: 18px; color: #c62828;">⚠️ No Active Profile Found</p>
            <p style="margin-bottom: 15px; color: #666;">Please select or create a profile first to view practice charts.</p>
            <button onclick="window.location.href='moduspractica-app.html'" 
                    style="padding: 12px 24px; background: linear-gradient(135deg, #7a2d17 0%, #a0522d 100%); 
                           color: white; border: none; border-radius: 8px; font-weight: 600; cursor: pointer; 
                           font-size: 16px; margin-top: 10px;">
                Go to Profile Selection
            </button>
        </div>
    `;
    if (window.MPLog) MPLog.warn('Charts: no active profile found');
}
