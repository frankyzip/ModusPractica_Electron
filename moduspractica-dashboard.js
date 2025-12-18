// ============================================================================
// ModusPractica Dashboard - Main Application Logic
// Copyright © 2025 Frank De Baere - All Rights Reserved
// ============================================================================

class ModusPracticaDashboard {
    constructor() {
        this.storagePrefix = 'mp_';
        this.currentProfile = null;
        this.profileData = null;
        this.musicPieces = [];
        this.filteredPieces = [];
        this.selectedColor = null;
        this.selectedTitleFilter = null;
        this.selectedCalendarTitleFilter = null;
        
        // Calendar state
        this.currentMonth = new Date();
        this.currentMonth.setDate(1);
        this.scheduledSessions = [];
        this.currentView = 'pieces'; // 'pieces', 'calendar', or 'piece-detail'
        this.isCalendarExpanded = true; // controls expanded/collapsed state of all weeks in calendar
        
        // Initialize Ebbinghaus Memory System
        this.pmcManager = new PersonalizedMemoryCalibration(this.storagePrefix);
        this.stabilityManager = new MemoryStabilityManager(this.storagePrefix);
        this.adaptiveTauManager = new AdaptiveTauManager();
        
        // Available colors (limited to 10 with strong visual contrast)
        this.availableColors = [
            { name: 'SkyBlue',      value: '#4FC3F7' },
            { name: 'LeafGreen',    value: '#81C784' },
            { name: 'SunYellow',    value: '#FFD54F' },
            { name: 'CoralOrange',  value: '#FF8A65' },
            { name: 'DeepPurple',   value: '#9575CD' },
            { name: 'RubyRed',      value: '#E57373' },
            { name: 'Teal',         value: '#4DB6AC' },
            { name: 'Indigo',       value: '#5C6BC0' },
            { name: 'Pink',         value: '#F06292' },
            { name: 'Lime',         value: '#DCE775' }
        ];
        
        this.init();
    }

            getPieceLifecycleState(piece) {
                if (!piece) return 0;
                if (piece.lifecycleState === undefined || piece.lifecycleState === null) {
                    return piece.isPaused ? 2 : 0;
                }
                return piece.lifecycleState;
            }

            isPieceArchived(piece) {
                return this.getPieceLifecycleState(piece) === 2;
            }

            getPieceLifecycleLabel(state) {
                switch (state) {
                    case 2:
                        return 'Archived';
                    default:
                        return 'Active';
                }
            }

            ensurePieceLifecycleDefaults() {
                if (!Array.isArray(this.musicPieces)) return;

                let changesApplied = false;
                this.musicPieces.forEach(piece => {
                    if (piece.lifecycleState === undefined || piece.lifecycleState === null) {
                        piece.lifecycleState = piece.isPaused ? 2 : 0;
                        changesApplied = true;
                    }

                    const desiredPauseFlag = this.isPieceArchived(piece);
                    if (piece.isPaused !== desiredPauseFlag) {
                        piece.isPaused = desiredPauseFlag;
                        changesApplied = true;
                    }
                });

                if (changesApplied) {
                    this.saveProfileData();
                }
            }

    // ========================================================================
    // INITIALIZATION
    // ========================================================================

    async init() {
        console.log('🎵 Initializing ModusPractica Dashboard...');
        if (window.MPLog) MPLog.info('Dashboard initializing');
        
        // Load current profile
        this.loadCurrentProfile();
        
        // Initialize Ebbinghaus memory systems for this profile
        if (this.currentProfile) {
            this.pmcManager.initializeCalibrationSystem(this.currentProfile.id);
            this.stabilityManager.initializeForUser(this.currentProfile.id);
            console.log('✅ Ebbinghaus memory systems initialized');
        }
        
        // Setup event listeners
        this.setupEventListeners();
        
        // Load and display data
        this.loadData();
        // One-time import of existing titles/composers into autocomplete lists
        this.ensureAutocompleteImported();
        
        // Automatically reschedule any overdue sections to today
        this.rescheduleOverdueSections();
        
        // Build calendar data
        this.buildScheduledSessions();
        
        this.updateUI();
        
        // Setup auto-refresh when page gets focus (e.g., returning from practice session)
        window.addEventListener('focus', () => {
            console.log('🔄 Dashboard regained focus, refreshing data...');
            if (window.MPLog) MPLog.info('Dashboard regained focus');
            this.loadCurrentProfile(); // Reload data from localStorage
            this.loadData();
            this.rescheduleOverdueSections(); // Check for overdue sections
            this.updateUI();
        });

        // Listen for practice session events from popup windows
        window.addEventListener('message', (event) => {
            const isSameOrigin = event.origin === window.location.origin;
            const isFileProtocol = window.location.protocol === 'file:';
            const isTrustedFileOrigin = isFileProtocol && (event.origin === 'null' || event.origin === 'file://' || event.origin === '');
            if (!isSameOrigin && !isTrustedFileOrigin) {
                console.warn('Ignoring message from unexpected origin:', event.origin);
                return;
            }

            if (event.data && event.data.type === 'MP_PRACTICE_SESSION_EVENT') {
                console.log('📨 Received practice session event:', event.data.event);
                if (window.MPLog) MPLog.info('Practice session event received', { event: event.data.event });

                this.loadCurrentProfile();
                this.loadData();
                this.rescheduleOverdueSections();
                this.buildScheduledSessions();
                this.updateUI();
                this.renderCalendar();

                console.log('✅ Dashboard refreshed after practice session event');
            }
        });

        // Setup cleanup for any timers to prevent memory leaks
        window.addEventListener('beforeunload', () => this.cleanup());
        window.addEventListener('pagehide', () => this.cleanup());
        
        console.log('✅ Dashboard initialized');
        if (window.MPLog) MPLog.info('Dashboard initialized', { profileId: this.currentProfile?.id });
        
        // Track page view
        if (window.ga4Tracker) {
            window.ga4Tracker.trackPageView('Dashboard');
        }
    }

    // Cleanup function for dashboard timers
    cleanup() {
        // Placeholder for future cleanup needs
        console.log('🧹 Dashboard cleanup completed');
    }

    loadCurrentProfile() {
        const lastProfileId = localStorage.getItem(this.storagePrefix + 'lastProfile');
        if (!lastProfileId) {
            // No profile selected, redirect to profile selection
            window.location.href = 'moduspractica-app.html';
            return;
        }

        // Load profile info
        const profilesJson = localStorage.getItem(this.storagePrefix + 'profiles');
        if (profilesJson) {
            const profiles = JSON.parse(profilesJson);
            this.currentProfile = profiles.find(p => p.id === lastProfileId);
        }

        if (!this.currentProfile) {
            // Profile not found, redirect
            window.location.href = 'moduspractica-app.html';
            return;
        }

        // Load profile data
        const dataJson = localStorage.getItem(this.storagePrefix + lastProfileId + '_data');
        if (dataJson) {
            this.profileData = JSON.parse(dataJson);
            this.musicPieces = this.profileData.musicPieces || [];
        } else {
            // Initialize empty data structure
            this.profileData = {
                musicPieces: [],
                practiceHistory: [], // Practice sessions history
                settings: {
                    retentionTarget: 0.80,
                    enableDiagnostics: false
                },
                statistics: {
                    totalSessions: 0,
                    totalPracticeTime: 0
                }
            };
            this.saveProfileData();
        }
        
        // Update currentPiece reference if it exists
        if (this.currentPiece) {
            this.currentPiece = this.musicPieces.find(p => p.id === this.currentPiece.id);
        }
        
        // Ensure practiceHistory array exists for existing profiles
        if (!this.profileData.practiceHistory) {
            this.profileData.practiceHistory = [];
        }
    }

    setupEventListeners() {
        // Logout button
        const logoutBtn = document.getElementById('logout-btn');
        if (logoutBtn) {
            logoutBtn.addEventListener('click', () => {
                window.location.href = 'moduspractica-app.html';
            });
        }

        // Add piece button
        const addPieceBtn = document.getElementById('add-piece-btn');
        if (addPieceBtn) {
            addPieceBtn.addEventListener('click', () => {
                this.showAddPieceDialog();
            });
        }

        // Search input
        const searchInput = document.getElementById('search-input');
        if (searchInput) {
            searchInput.addEventListener('input', (e) => {
                this.filterPieces(e.target.value);
            });
        }

        // Title filter
        const titleFilter = document.getElementById('title-filter');
        if (titleFilter) {
            titleFilter.addEventListener('change', () => {
                this.selectedTitleFilter = titleFilter.value || null;
                this.applyPieceFilters();
            });
        }

        // Color filter
        const colorFilter = document.getElementById('color-filter');
        if (colorFilter) {
            // Populate options from availableColors
            this.availableColors.forEach(c => {
                const opt = document.createElement('option');
                opt.value = c.value;
                opt.textContent = c.name;
                colorFilter.appendChild(opt);
            });

            colorFilter.addEventListener('change', () => {
                this.selectedColorFilter = colorFilter.value || null;
                this.applyPieceFilters();
            });
        }

        // Sort mode
        const sortMode = document.getElementById('sort-mode');
        if (sortMode) {
            sortMode.addEventListener('change', () => {
                this.selectedSortMode = sortMode.value || 'title';
                this.applyPieceFilters();
            });
        }

        // Calendar navigation buttons
        const btnPrevMonth = document.getElementById('btnPrevMonth');
        if (btnPrevMonth) {
            btnPrevMonth.addEventListener('click', () => this.previousMonth());
        }

        const btnNextMonth = document.getElementById('btnNextMonth');
        if (btnNextMonth) {
            btnNextMonth.addEventListener('click', () => this.nextMonth());
        }

        const btnToday = document.getElementById('btnToday');
        if (btnToday) {
            btnToday.addEventListener('click', () => this.goToToday());
        }

        const toggleCurrentWeekBtn = document.getElementById('toggleCurrentWeekBtn');
        if (toggleCurrentWeekBtn) {
            toggleCurrentWeekBtn.addEventListener('click', () => {
                this.isCalendarExpanded = !this.isCalendarExpanded;
                toggleCurrentWeekBtn.textContent = this.isCalendarExpanded
                    ? 'Collapse all weeks'
                    : 'Expand all weeks';
                this.renderCalendar();
            });
        }

        // Calendar title filter
        const calendarTitleFilter = document.getElementById('calendar-title-filter');
        if (calendarTitleFilter) {
            calendarTitleFilter.addEventListener('change', () => {
                this.selectedCalendarTitleFilter = calendarTitleFilter.value || null;
                this.renderCalendar();
            });
        }

        // Modal: Close button
        const closeAddPiece = document.getElementById('close-add-piece');
        if (closeAddPiece) {
            closeAddPiece.addEventListener('click', () => {
                this.hideAddPieceDialog();
            });
        }

        // Modal: Cancel button
        const cancelAddPiece = document.getElementById('cancel-add-piece');
        if (cancelAddPiece) {
            cancelAddPiece.addEventListener('click', () => {
                this.hideAddPieceDialog();
            });
        }

        // Modal: Save button
        const saveNewPiece = document.getElementById('save-new-piece');
        if (saveNewPiece) {
            saveNewPiece.addEventListener('click', () => {
                this.saveNewPiece();
            });
        }

        const pieceLifecycleSelect = document.getElementById('piece-lifecycle-select');
        if (pieceLifecycleSelect) {
            pieceLifecycleSelect.addEventListener('change', (e) => {
                if (!this.currentPiece) {
                    e.target.value = '0';
                    return;
                }
                this.changePieceLifecycleState(this.currentPiece.id, e.target.value);
            });
        }

        // Modal: Close on backdrop click
        const addPieceModal = document.getElementById('add-piece-modal');
        if (addPieceModal) {
            addPieceModal.addEventListener('click', (e) => {
                if (e.target.id === 'add-piece-modal') {
                    this.hideAddPieceDialog();
                }
            });
        }
    }

    // ========================================================================
    // DATA MANAGEMENT
    // ========================================================================

    loadData() {
        // Sort pieces alphabetically by title
        if (this.musicPieces) {
            this.ensurePieceLifecycleDefaults();
            this.musicPieces.sort((a, b) => {
                const titleA = (a.title || '').toLowerCase();
                const titleB = (b.title || '').toLowerCase();
                return titleA.localeCompare(titleB);
            });
        }
        this.filteredPieces = [...(this.musicPieces || [])];
    }

    saveProfileData() {
        if (this.currentProfile && this.profileData) {
            const key = this.storagePrefix + this.currentProfile.id + '_data';
            
            try {
                // Use safe storage with quota check
                storageQuotaManager.safeSetItem(key, JSON.stringify(this.profileData));
                if (window.MPLog) {
                    MPLog.debug('Dashboard saveProfileData persisted profileData', {
                        musicPieces: this.profileData.musicPieces?.length ?? 0
                    });
                } else {
                    console.log('📁 saveProfileData stored profileData', {
                        pieces: this.profileData.musicPieces?.length ?? 0
                    });
                }
            } catch (error) {
                if (error.name === 'QuotaExceededError') {
                    // Probeer automatische cleanup
                    console.warn('Storage quota exceeded, attempting cleanup...');
                    const cleaned = storageQuotaManager.emergencyCleanup();
                    
                    if (cleaned > 0) {
                        // Retry save after cleanup
                        try {
                            storageQuotaManager.safeSetItem(key, JSON.stringify(this.profileData));
                        } catch (retryError) {
                            this.showQuotaExceededError(error.storageInfo);
                        }
                    } else {
                        this.showQuotaExceededError(error.storageInfo);
                    }
                } else {
                    console.error('Error saving profile data:', error);
                }
            }
        }
    }

    // ========================================================================
    // UI UPDATES
    // ========================================================================

    updateUI() {
        // Update profile name
        const profileNameEl = document.getElementById('profile-name');
        if (profileNameEl) {
            profileNameEl.textContent = this.currentProfile.name;
        }

        // Update statistics
        this.updateStatistics();

        // Populate title filter dropdown
        this.populateTitleFilter();

        // Render pieces list
        this.renderPiecesList();

        // Render today's sessions
        this.renderTodaySessions();
    }

    updateStatistics() {
        // Total pieces
        const statPieces = document.getElementById('stat-pieces');
        if (statPieces) statPieces.textContent = this.musicPieces.length;

        // Due today
        const dueToday = this.calculateDueToday();
        const statToday = document.getElementById('stat-today');
        if (statToday) statToday.textContent = dueToday;

        // Total sessions
        const totalSessions = this.profileData.statistics.totalSessions || 0;
        const statSessions = document.getElementById('stat-sessions');
        if (statSessions) statSessions.textContent = totalSessions;
        
        // Practice time today
        const todayPracticeTime = this.calculateTodayPracticeTime();
        const statTodayTime = document.getElementById('stat-today-time');
        if (statTodayTime) statTodayTime.textContent = this.formatPracticeTime(todayPracticeTime);

        // New statistics for Today sidebar
        const overdue = this.calculateOverdue();
        const statOverdue = document.getElementById('stat-overdue');
        if (statOverdue) statOverdue.textContent = overdue;

        const completedToday = this.calculateCompletedToday();
        const statCompletedToday = document.getElementById('stat-completed-today');
        if (statCompletedToday) statCompletedToday.textContent = completedToday;

        const streak = this.calculatePracticeStreak();
        const statStreak = document.getElementById('stat-streak');
        if (statStreak) statStreak.textContent = streak === 1 ? '1 day' : `${streak} days`;
    }
    
    calculateTodayPracticeTime() {
        // Get today's date at midnight (local time)
        const today = new Date();
        const todayNormalized = new Date(today.getFullYear(), today.getMonth(), today.getDate());
        
        // Sum duration of all practice sessions from today
        let totalMinutes = 0;
        
        if (this.profileData.practiceHistory) {
            this.profileData.practiceHistory.forEach(session => {
                // Parse ISO string date and normalize to local date (without time)
                const sessionDateObj = new Date(session.date);
                const sessionNormalized = new Date(
                    sessionDateObj.getFullYear(), 
                    sessionDateObj.getMonth(), 
                    sessionDateObj.getDate()
                );
                
                if (sessionNormalized.getTime() === todayNormalized.getTime()) {
                    totalMinutes += session.durationMinutes || 0;
                }
            });
        }
        
        return totalMinutes;
    }
    
    formatPracticeTime(minutes) {
        // Format like C# version: HH:MM:SS
        const totalSeconds = Math.round(minutes * 60);
        const hours = Math.floor(totalSeconds / 3600);
        const mins = Math.floor((totalSeconds % 3600) / 60);
        const secs = totalSeconds % 60;
        
        return `${hours.toString().padStart(2, '0')}:${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
    }

    calculateDueToday() {
        const now = new Date();
        const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        
        let count = 0;
        this.musicPieces.forEach(piece => {
            if (this.isPieceArchived(piece)) return;
            if (piece.barSections) {
                piece.barSections.forEach(section => {
                    // Skip inactive sections
                    if (section.lifecycleState === 2) return;
                    
                    if (this.isSectionDueToday(section, today)) {
                        count++;
                    }
                });
            }
        });
        
        return count;
    }

    calculateOverdue() {
        const now = new Date();
        const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        
        let count = 0;
        this.musicPieces.forEach(piece => {
            if (this.isPieceArchived(piece)) return;
            if (piece.barSections) {
                piece.barSections.forEach(section => {
                    // Skip only paused or inactive sections
                    if (section.isPaused || section.lifecycleState === 2) {
                        return;
                    }
                    
                    if (section.nextPracticeDate) {
                        const nextDateObj = new Date(section.nextPracticeDate);
                        const nextDate = new Date(nextDateObj.getFullYear(), nextDateObj.getMonth(), nextDateObj.getDate());
                        
                        // Overdue if scheduled date is before today
                        if (nextDate < today) {
                            count++;
                        }
                    }
                });
            }
        });
        
        return count;
    }

    calculateCompletedToday() {
        const now = new Date();
        const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        
        let count = 0;
        
        if (this.profileData.practiceHistory) {
            this.profileData.practiceHistory.forEach(session => {
                const sessionDateObj = new Date(session.date);
                const sessionDate = new Date(sessionDateObj.getFullYear(), sessionDateObj.getMonth(), sessionDateObj.getDate());
                
                if (sessionDate.getTime() === today.getTime()) {
                    count++;
                }
            });
        }
        
        return count;
    }

    rescheduleOverdueSections() {
        console.log('🔄 Checking for overdue sections to reschedule...');
        
        const now = new Date();
        const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        let rescheduledCount = 0;
        
        this.musicPieces.forEach(piece => {
            if (piece.barSections) {
                piece.barSections.forEach(section => {
                    // Skip only paused or inactive sections
                    // NOTE: practiceScheduleStage is NOT a mastery indicator - removed that check
                    if (section.isPaused || section.lifecycleState === 2) {
                        return;
                    }
                    
                    // Check if section has a nextReviewDate
                    if (section.nextReviewDate) {
                        const nextDateObj = new Date(section.nextReviewDate);
                        const nextDate = new Date(nextDateObj.getFullYear(), nextDateObj.getMonth(), nextDateObj.getDate());
                        
                        // If scheduled date is before today, move to today
                        if (nextDate < today) {
                            const daysLate = Math.floor((today - nextDate) / (1000 * 60 * 60 * 24));
                            console.log(`📌 Rescheduling overdue section: ${piece.title} - ${section.barRange || section.description} (${daysLate} day${daysLate !== 1 ? 's' : ''} late)`);
                            
                            // Move to today
                            section.nextReviewDate = today.toISOString();
                            rescheduledCount++;
                        }
                    }
                });
            }
        });
        
        if (rescheduledCount > 0) {
            console.log(`✅ Rescheduled ${rescheduledCount} overdue section${rescheduledCount !== 1 ? 's' : ''} to today`);
            this.saveProfileData();
        } else {
            console.log('✅ No overdue sections found');
        }
    }

    calculatePracticeStreak() {
        if (!this.profileData.practiceHistory || this.profileData.practiceHistory.length === 0) {
            return 0;
        }

        // Sort sessions by date (most recent first)
        const sortedSessions = [...this.profileData.practiceHistory]
            .sort((a, b) => new Date(b.date) - new Date(a.date));

        // Get unique practice dates
        const uniqueDates = new Set();
        sortedSessions.forEach(session => {
            const sessionDateObj = new Date(session.date);
            const dateStr = `${sessionDateObj.getFullYear()}-${sessionDateObj.getMonth()}-${sessionDateObj.getDate()}`;
            uniqueDates.add(dateStr);
        });

        const sortedDates = Array.from(uniqueDates).sort().reverse();
        
        if (sortedDates.length === 0) return 0;

        const now = new Date();
        const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        
        // Check if there's practice today or yesterday (streak can continue from yesterday)
        const mostRecentDate = sortedDates[0].split('-').map(Number);
        // Timezone-safe: parse most recent date
        const mostRecent = parseDateYMD(`${mostRecentDate[0]}-${String(mostRecentDate[1]+1).padStart(2,'0')}-${String(mostRecentDate[2]).padStart(2,'0')}`);
        const todayDate = getTodayLocal();
        
        const daysDiff = daysBetween(mostRecent, todayDate);
        
        // If last practice was more than 1 day ago, streak is broken
        if (daysDiff > 1) return 0;

        // Count consecutive days using timezone-safe comparison
        let streak = 0;
        let expectedDate = todayDate;
        
        // If no practice today, start from yesterday
        if (daysDiff === 1) {
            expectedDate = addDays(todayDate, -1);
        }

        for (let i = 0; i < sortedDates.length; i++) {
            const dateParts = sortedDates[i].split('-').map(Number);
            const dateStr = `${dateParts[0]}-${String(dateParts[1]+1).padStart(2,'0')}-${String(dateParts[2]).padStart(2,'0')}`;
            const practiceDate = parseDateYMD(dateStr);
            
            if (isSameDay(practiceDate, expectedDate)) {
                streak++;
                expectedDate = addDays(expectedDate, -1);
            } else {
                break;
            }
        }

        return streak;
    }

    isSectionDueToday(section, today) {
        // Skip paused or completed sections
        if (section.isPaused || section.practiceScheduleStage >= 6) {
            return false;
        }
        
        // Check if section has a scheduled date
        if (!section.nextPracticeDate) {
            // New section - consider it due
            return true;
        }
        
        // Timezone-safe: compare date-only
        const nextPracticeDate = toDateOnly(section.nextPracticeDate);
        const todayDate = toDateOnly(today);
        
        // Due if scheduled date is today or earlier
        return nextPracticeDate <= todayDate;
    }

    renderPiecesList() {
        const listEl = document.getElementById('pieces-list');
        if (!listEl) return;
        
        if (this.filteredPieces.length === 0) {
            listEl.innerHTML = `
                <div class="empty-state">
                    <div class="empty-icon">🎼</div>
                    <div class="empty-title">No Music Pieces Yet</div>
                    <div class="empty-text">Start by adding your first music piece to practice</div>
                    <button class="btn btn-primary" onclick="app.showAddPieceDialog()">
                        + Add Your First Piece
                    </button>
                </div>
            `;
            return;
        }

        listEl.innerHTML = this.filteredPieces.map(piece => {
            const sectionCount = piece.barSections ? piece.barSections.length : 0;
            // Count active sections (not mastered, not archived)
            const activeCount = piece.barSections ? piece.barSections.filter(s => s.lifecycleState !== 2 && s.practiceScheduleStage < 6).length : 0;
            
            const lifecycleState = this.getPieceLifecycleState(piece);
            const isArchived = lifecycleState === 2;
            const statusLabel = this.getPieceLifecycleLabel(lifecycleState);

            const colorValue = piece.colorValue || '#F08080';
            const bgColor = this.hexToRgba(colorValue, 0.18);
            const listItemClass = ['list-item'];
            if (isArchived) listItemClass.push('archived');

            // Calculate how long the piece has been in the app
            let ageLabel = '';
            if (piece.creationDate) {
                const created = new Date(piece.creationDate);
                const now = new Date();
                const diffMs = now.getTime() - created.getTime();
                const diffDays = Math.max(0, Math.floor(diffMs / (1000 * 60 * 60 * 24)));

                if (diffDays < 1) {
                    ageLabel = 'Added today';
                } else if (diffDays === 1) {
                    ageLabel = 'Added 1 day ago';
                } else if (diffDays < 30) {
                    ageLabel = `Added ${diffDays} days ago`;
                } else {
                    const diffMonths = Math.floor(diffDays / 30);
                    if (diffMonths === 1) {
                        ageLabel = 'Added 1 month ago';
                    } else if (diffMonths < 12) {
                        ageLabel = `Added ${diffMonths} months ago`;
                    } else {
                        const diffYears = Math.floor(diffMonths / 12);
                        ageLabel = diffYears === 1
                            ? 'Added 1 year ago'
                            : `Added ${diffYears} years ago`;
                    }
                }
            }
            
            const progress = this.calculatePieceProgress(piece);

                const ytSvg = `<svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" focusable="false" style="display:inline-block; vertical-align:middle;">
                    <rect x="2" y="6" width="20" height="12" rx="3" ry="3" fill="#FF0000"></rect>
                    <polygon points="10,9 16,12 10,15" fill="#ffffff"></polygon>
                </svg>`;
                const youTubeSnippet = (piece.youTubeLink && piece.youTubeLink.trim())
                    ? `<div style="margin-top:6px; display:flex; align-items:center; gap:6px;">
                            <a href="${this.escapeHtml(piece.youTubeLink)}" target="_blank" rel="noopener noreferrer" class="btn btn-secondary" style="padding:2px 8px; font-size:12px; line-height:1; display:inline-flex; align-items:center; justify-content:center; text-decoration:none;" title="Open YouTube link" aria-label="Open YouTube link">${ytSvg}<span style="margin-left:6px; vertical-align:middle;">YouTube</span></a>
                       </div>`
                    : `<div style="margin-top:6px; font-size:11px;">
                            <a href="#" onclick="event.preventDefault(); event.stopPropagation(); app.currentPiece = app.musicPieces.find(p=>p.id==='${piece.id}'); app.showEditPieceModal(); setTimeout(()=>{ const i=document.getElementById('edit-piece-youtube'); if(i) i.focus(); }, 50);">+ Add YouTube link</a>
                       </div>`;

            return `
                <div class="${listItemClass.join(' ')}" style="background-color: ${bgColor}; border-left: 4px solid ${colorValue}; height: 250px; display: flex; flex-direction: column; align-self: flex-start;">
                        <div class="item-main" onclick="app.openPiece('${piece.id}')">
                        <div class="item-icon" style="background-color: ${this.hexToRgba(colorValue, 0.35)}; color: ${colorValue};">
                            🎼
                        </div>
                        <div class="item-details">
                            <div class="item-title" title="${this.escapeHtml(piece.title)}">${this.escapeHtml(piece.title)}</div>
                            <div class="item-subtitle" title="${this.escapeHtml(piece.composer || 'Unknown')}">${this.escapeHtml(piece.composer || 'Unknown Composer')}</div>
                            ${ageLabel ? `<div class="item-subtitle" style="font-size: 11px; opacity: 0.8;">${ageLabel}</div>` : ''}
                        </div>
                    </div>
                    <div class="piece-progress-wrapper" aria-label="Overall progress ${progress}%" title="Overall progress based on chunk success rate">
                        <div class="piece-progress-track">
                            <div class="piece-progress-fill" style="width: ${progress}%;"></div>
                        </div>
                        <div style="margin-top: 2px; font-size: 11px; color: rgba(0,0,0,0.6);">
                            ${progress}% overall
                        </div>
                        ${youTubeSnippet}
                    </div>
                    <div class="item-meta" style="display:flex; flex-direction:column; gap:6px;">
                        <div style="display:flex; align-items:center; gap:8px;">
                            <span>${activeCount} active / ${sectionCount} total</span>
                        </div>
                        <div style="display:flex; align-items:center; gap:8px;">
                            <select class="piece-state-select" onchange="event.stopPropagation(); app.changePieceLifecycleState('${piece.id}', this.value)" style="color: ${lifecycleState === 0 ? 'var(--success-color)' : 'var(--danger-color)'};">
                                <option value="0" ${lifecycleState === 0 ? 'selected' : ''} style="color: var(--success-color);">Active</option>
                                <option value="2" ${lifecycleState === 2 ? 'selected' : ''} style="color: var(--danger-color);">Archive</option>
                            </select>
                            <span style="opacity:.5;">|</span>
                            <button class="btn btn-secondary" style="padding: 2px 6px; font-size: 11px;" onclick="event.stopPropagation(); app.confirmDeletePiece('${piece.id}')">Delete</button>
                        </div>
                    </div>
                </div>
            `;
        }).join('');
    }

    changePieceLifecycleState(pieceId, newStateValue) {
        if (!pieceId) return;

        const piece = this.musicPieces.find(p => p.id === pieceId);
        if (!piece) return;

        const parsedState = parseInt(newStateValue, 10);
        if (Number.isNaN(parsedState)) return;

        const previousState = this.getPieceLifecycleState(piece);
        if (previousState === parsedState) return;

        piece.lifecycleState = parsedState;
        piece.isPaused = this.isPieceArchived(piece);

        this.saveProfileData();

        if (this.currentPiece && this.currentPiece.id === pieceId) {
            this.currentPiece = piece;
        }

        this.applyPieceFilters();
        this.populateTitleFilter();
        this.populateCalendarTitleFilter();

        this.updateStatistics();
        this.renderTodaySessions();
        this.buildScheduledSessions();
        if (this.currentView === 'calendar') {
            this.renderCalendar();
        }

        if (this.currentPiece && this.currentPiece.id === pieceId) {
            this.renderPieceDetail(piece);
        }
    }

    calculatePieceProgress(piece) {
        if (!piece || !Array.isArray(piece.barSections) || piece.barSections.length === 0) {
            return 0;
        }

        const history = Array.isArray(this.profileData?.practiceHistory)
            ? this.profileData.practiceHistory
            : [];

        const MIN_SESSIONS_PER_PIECE = 3;
        const MIN_SESSIONS_PER_SECTION = 2;

        const pieceSessionCount = history.filter(h => h.musicPieceId === piece.id && !h.isDeleted).length;
        if (pieceSessionCount < MIN_SESSIONS_PER_PIECE) {
            return 0;
        }

        const relevantSections = piece.barSections.filter(section => {
            if (!section) return false;
            if (section.lifecycleState === 2) return false;
            if (section.practiceScheduleStage >= 6) return false;
            const sectionHistory = history.filter(h => h.barSectionId === section.id && !h.isDeleted);
            return sectionHistory.length >= MIN_SESSIONS_PER_SECTION;
        });

        if (relevantSections.length === 0) {
            return 0;
        }

        let totalRatio = 0;
        let sectionWithDataCount = 0;

        relevantSections.forEach(section => {
            const sectionHistory = history.filter(h => h.barSectionId === section.id && !h.isDeleted);
            if (sectionHistory.length === 0) return;

            let reps = 0;
            let fails = 0;

            sectionHistory.forEach(entry => {
                const repetitions = typeof entry.repetitions === 'number' ? entry.repetitions : (entry.completedRepetitions || 0);
                const memoryFailures = typeof entry.memoryFailures === 'number' ? entry.memoryFailures : (entry.totalFailures || 0);
                const executionFailures = typeof entry.executionFailures === 'number' ? entry.executionFailures : (entry.attemptsTillSuccess || 0);
                reps += Math.max(0, repetitions);
                fails += Math.max(0, memoryFailures) + Math.max(0, executionFailures);
            });

            const total = reps + fails;
            if (total <= 0) return;

            const ratio = reps / total;
            totalRatio += ratio;
            sectionWithDataCount++;
        });

        if (sectionWithDataCount === 0) {
            return 0;
        }

        const avgRatio = totalRatio / sectionWithDataCount;
        const clamped = Math.min(1, Math.max(0, avgRatio));
        const percent = Math.round(clamped * 100);

        if (piece) {
            piece.progress = percent;
        }

        return percent;
    }

    renderTodaySessions() {
        const now = new Date();
        const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());

        const buckets = this.getAgendaBucketsForDate(today);
        const overdueSections = buckets.overdueSections;
        const dueTodaySections = buckets.dueSections;
        const completedTodaySections = buckets.completedSections;

        const totalCount = dueTodaySections.length + overdueSections.length;
        const completedCount = completedTodaySections.length;
        const todayCountEl = document.getElementById('today-count');
        if (todayCountEl) {
            const parts = [`${totalCount} due`];
            if (completedCount > 0) {
                parts.push(`${completedCount} done`);
            }
            todayCountEl.textContent = parts.join(' • ');
            todayCountEl.className = totalCount > 0 ? 'status-badge status-active' : 'status-badge';
        }

        const sessionsEl = document.getElementById('today-sessions');
        if (!sessionsEl) return;
        
        if (totalCount === 0 && completedTodaySections.length === 0) {
            sessionsEl.innerHTML = `
                <div class="empty-state" style="padding: 20px;">
                    <div class="empty-icon" style="font-size: 24px;">✅</div>
                    <div class="empty-text">All caught up!</div>
                </div>
            `;
            return;
        }

        let html = '';
        
        // Show overdue sections first (single-line layout)
        overdueSections.forEach(item => {
            const daysLate = Math.floor((today - item.scheduledDate) / (1000 * 60 * 60 * 24));
            // Status-based kleur: rood voor nog te oefenen (overdue)
            const statusColor = 'var(--danger-color)';
            const bgColor = '#fff0f0';
            const titleText = this.escapeHtml(item.piece.title);
            const rangeText = this.escapeHtml(item.section.barRange || `${item.section.startBar}-${item.section.endBar}`);
            const descText = item.section.description ? ' - ' + this.escapeHtml(item.section.description) : '';
            const leftText = `${titleText} — ${rangeText}${descText}`;
            const leftMarkup = `<span class="task-title">${titleText}</span> — ${rangeText}${descText}`;
            if (window.MPLog) MPLog.info(`Overdue item: ${item.piece.title}, statusColor: ${statusColor}, bgColor: ${bgColor}`);
            html += `
                <div class="task-item task-overdue" onclick="app.openPracticeSession('${item.section.id}')" style="border-left-color: ${statusColor}; background-color: ${bgColor};">
                    <div class="task-checkbox">⚠️</div>
                    <div class="task-content">
                        <div class="task-line-left" title="${leftText}">${leftMarkup}</div>
                        <div class="task-tag" style="color: var(--danger-color); background: #fff0f0;">
                            ${daysLate} day${daysLate !== 1 ? 's' : ''} late
                        </div>
                    </div>
                </div>
            `;
        });
        
        // Show today's sections (single-line layout)
        dueTodaySections.forEach(item => {
            // Status-based kleur: oranje voor vandaag (due today)
            const statusColor = 'var(--warning-color)';
            const bgColor = '#fff7ed';
            const titleText = this.escapeHtml(item.piece.title);
            const rangeText = this.escapeHtml(item.section.barRange || `${item.section.startBar}-${item.section.endBar}`);
            const descText = item.section.description ? ' - ' + this.escapeHtml(item.section.description) : '';
            const leftText = `${titleText} — ${rangeText}${descText}`;
            const leftMarkup = `<span class=\"task-title\">${titleText}</span> — ${rangeText}${descText}`;
            if (window.MPLog) MPLog.info(`Due today item: ${item.piece.title}, statusColor: ${statusColor}, bgColor: ${bgColor}`);
            html += `
                <div class="task-item" onclick="app.openPracticeSession('${item.section.id}')" style="border-left-color: ${statusColor}; background-color: ${bgColor};">
                    <div class="task-checkbox">⬜</div>
                    <div class="task-content">
                        <div class="task-line-left" title="${leftText}">${leftMarkup}</div>
                    </div>
                </div>
            `;
        });

        // Show completed sections (single-line layout)
        completedTodaySections.forEach(item => {
            // Status-based kleur: groen voor gedane chunks
            const statusColor = 'var(--success-color)';
            const bgColor = '#f0fff4';
            const titleText = this.escapeHtml(item.piece.title);
            const rangeText = this.escapeHtml(item.section.barRange || `${item.section.startBar}-${item.section.endBar}`);
            const descText = item.section.description ? ' - ' + this.escapeHtml(item.section.description) : '';
            const leftText = `${titleText} — ${rangeText}${descText}`;
            const leftMarkup = `<span class=\"task-title\">${titleText}</span> — ${rangeText}${descText}`;
            let followUpTag = '';

            const nextReview = item.section.nextReviewDate || item.section.nextPracticeDate || null;
            if (nextReview) {
                const nextReviewDate = new Date(nextReview);
                if (!Number.isNaN(nextReviewDate.getTime())) {
                    followUpTag = `<div class=\"task-tag task-tag-next\">Next: ${this.formatShortDateDisplay(nextReviewDate)}</div>`;
                }
            }
            if (window.MPLog) MPLog.info(`Completed item: ${item.piece.title}, statusColor: ${statusColor}, bgColor: ${bgColor}`);
            html += `
                <div class="task-item task-completed" onclick="app.openPracticeSession('${item.section.id}')" style="border-left-color: ${statusColor}; background-color: ${bgColor};">
                    <div class="task-checkbox">✅</div>
                    <div class="task-content">
                        <div class="task-line-left" title="${leftText}">${leftMarkup}</div>
                        <div class="task-tag-group">
                            <div class="task-tag" style="color: #2f855a; background: #f0fff4;">Completed</div>
                            ${followUpTag}
                        </div>
                    </div>
                </div>
            `;
        });

        sessionsEl.innerHTML = html;
    }

    /**
     * Returns sections for a given date, using the same rules as Today's Agenda.
     * Buckets: overdue (scheduled < date), due (scheduled === date), completed (lastPracticeDate === date).
     */
    getAgendaBucketsForDate(targetDate) {
        const day = toDateOnly(targetDate);
        if (!day || Number.isNaN(day.getTime())) {
            console.warn('⚠️ Invalid target date supplied to getAgendaBucketsForDate', targetDate);
            return { overdueSections: [], dueSections: [], completedSections: [] };
        }

        const dayTime = day.getTime();
        const overdueSections = [];
        const dueSections = [];
        const completedSections = [];

        this.musicPieces.forEach(piece => {
            if (this.isPieceArchived(piece)) return;
            if (!piece.barSections) return;

            piece.barSections.forEach(section => {
                if (section.isPaused || section.lifecycleState === 2) {
                    return;
                }

                let isCompletedOnDay = false;
                if (section.lastPracticeDate) {
                    const lastPractice = toDateOnly(section.lastPracticeDate);
                    if (lastPractice && !Number.isNaN(lastPractice.getTime()) && lastPractice.getTime() === dayTime) {
                        completedSections.push({
                            piece,
                            section,
                            scheduledDate: lastPractice
                        });
                        isCompletedOnDay = true;
                    }
                }

                if (isCompletedOnDay) return;

                const nextDateStr = section.nextPracticeDate || section.nextReviewDate;
                if (!nextDateStr) return;

                const nextDate = toDateOnly(nextDateStr);
                if (!nextDate || Number.isNaN(nextDate.getTime())) return;

                const scheduledDate = nextDate.getTime();
                const item = { piece, section, scheduledDate: nextDate };

                if (scheduledDate === dayTime) {
                    dueSections.push(item);
                } else if (scheduledDate < dayTime) {
                    overdueSections.push(item);
                }
            });
        });

        return { overdueSections, dueSections, completedSections };
    }

    filterPieces(query) {
        this.searchQuery = query || '';
        this.applyPieceFilters();
    }

    populateTitleFilter() {
        const titleFilter = document.getElementById('title-filter');
        if (!titleFilter) return;

        // Store current selection
        const currentSelection = this.selectedTitleFilter;

        // Clear existing options except the first one ("All pieces")
        titleFilter.innerHTML = '<option value="">All pieces</option>';

        // Sort pieces by title
        const sortedPieces = [...this.musicPieces].sort((a, b) => 
            a.title.localeCompare(b.title)
        );

        // Add option for each piece
        sortedPieces.forEach(piece => {
            const opt = document.createElement('option');
            opt.value = piece.id;
            const archivedSuffix = this.isPieceArchived(piece) ? ' (Archived)' : '';
            opt.textContent = piece.title + archivedSuffix;
            if (piece.id === currentSelection) {
                opt.selected = true;
            }
            titleFilter.appendChild(opt);
        });
    }

    applyPieceFilters() {
        const lowerQuery = (this.searchQuery || '').toLowerCase().trim();
        const titleFilter = this.selectedTitleFilter || null;
        const colorFilter = this.selectedColorFilter || null;
        const sortMode = this.selectedSortMode || 'title';

        // Text filter
        let result = this.musicPieces.filter(piece => {
            // Title filter (specific piece)
            if (titleFilter) {
                if (piece.id !== titleFilter) return false;
            }

            // Text search filter
            if (lowerQuery) {
                const titleMatch = piece.title.toLowerCase().includes(lowerQuery);
                const composerMatch = (piece.composer || '').toLowerCase().includes(lowerQuery);
                if (!titleMatch && !composerMatch) return false;
            }

            // Color filter
            if (colorFilter) {
                if (!piece.colorValue || piece.colorValue.toLowerCase() !== colorFilter.toLowerCase()) {
                    return false;
                }
            }

            return true;
        });

        // If the title filter points to a piece that no longer exists (e.g. after import), reset it
        if (result.length === 0 && titleFilter) {
            const stillExists = this.musicPieces.some(piece => piece.id === titleFilter);
            if (!stillExists) {
                console.warn('🎯 Clearing stale title filter that referenced a removed piece.');
                this.selectedTitleFilter = null;
                return this.applyPieceFilters();
            }
        }

        // Sorting
        result.sort((a, b) => {
            if (sortMode === 'composer') {
                const ac = (a.composer || '').toLowerCase();
                const bc = (b.composer || '').toLowerCase();
                if (ac === bc) return a.title.localeCompare(b.title);
                return ac.localeCompare(bc);
            }

            if (sortMode === 'color') {
                const ac = (a.colorValue || '').toLowerCase();
                const bc = (b.colorValue || '').toLowerCase();
                if (ac === bc) return a.title.localeCompare(b.title);
                return ac.localeCompare(bc);
            }

            // Default: sort by title
            return a.title.localeCompare(b.title);
        });

        this.filteredPieces = result;
        this.renderPiecesList();
    }

    // ========================================================================
    // ACTIONS
    // ========================================================================

    confirmDeletePiece(pieceId) {
        const piece = this.musicPieces.find(p => p.id === pieceId);
        if (!piece) return;

        const message =
            `Are you sure you want to delete the piece "${piece.title}"?\n\n` +
            `This will remove the piece and all its sections from this profile. ` +
            `Practice history entries will remain for analytics but will no longer be scheduled.`;

        if (!confirm(message)) return;

        this.deletePiece(pieceId);
    }

    deletePiece(pieceId) {
        // Remove piece from collection
        this.musicPieces = this.musicPieces.filter(p => p.id !== pieceId);

        // Persist updated profile data
        this.saveProfileData();

        // Rebuild derived views
        this.filteredPieces = [...this.musicPieces];
        this.renderPiecesList();
        this.buildScheduledSessions();
        this.renderCalendar();
        this.renderTodaySessions();

        if (window.MPLog) MPLog.info(`Piece deleted`, { pieceId });
    }

    showAddPieceDialog() {
        // Reset form
        document.getElementById('piece-title').value = '';
        document.getElementById('piece-composer').value = '';
        document.getElementById('piece-creation-date').value = new Date().toISOString().split('T')[0];
        this.selectedColor = this.availableColors[0]; // Default to first color

        // Render color picker
        this.renderColorPicker();

        // Setup autocomplete UI and data binding
        this.setupAutocomplete();

        // Show modal
        document.getElementById('add-piece-modal').classList.add('active');
        
        // Focus on title field
        setTimeout(() => {
            document.getElementById('piece-title').focus();
        }, 100);
    }

    hideAddPieceDialog() {
        document.getElementById('add-piece-modal').classList.remove('active');
    }

    renderColorPicker() {
        const colorPicker = document.getElementById('color-picker');
        if (!colorPicker) return;
        
        colorPicker.innerHTML = '';

        this.availableColors.forEach(color => {
            const colorOption = document.createElement('div');
            colorOption.className = 'color-option';
            colorOption.style.backgroundColor = color.value;
            colorOption.title = color.name;
            
            if (this.selectedColor && this.selectedColor.name === color.name) {
                colorOption.classList.add('selected');
            }

            colorOption.addEventListener('click', () => {
                // Remove previous selection
                colorPicker.querySelectorAll('.color-option').forEach(opt => {
                    opt.classList.remove('selected');
                });
                
                // Add selection to clicked option
                colorOption.classList.add('selected');
                this.selectedColor = color;
            });

            colorPicker.appendChild(colorOption);
        });
    }

    saveNewPiece() {
        // Validate input
        const title = document.getElementById('piece-title').value.trim();
        const composer = document.getElementById('piece-composer').value.trim();
        const creationDate = document.getElementById('piece-creation-date').value;
        const youTubeLinkRaw = (document.getElementById('piece-youtube-link')?.value || '').trim();

        if (!title) {
            alert('Please enter a title for the music piece.');
            document.getElementById('piece-title').focus();
            return;
        }

        if (!composer) {
            alert('Please enter a composer for the music piece.');
            document.getElementById('piece-composer').focus();
            return;
        }

        // Normalize YouTube link (optional)
        let youTubeLink = youTubeLinkRaw;
        if (youTubeLink && !/^https?:\/\//i.test(youTubeLink)) {
            youTubeLink = 'https://' + youTubeLink;
        }
        if (youTubeLink) {
            try {
                const u = new URL(youTubeLink);
                const host = (u.hostname || '').toLowerCase();
                const isYouTube = host.includes('youtube.com') || host.includes('youtu.be');
                if (!isYouTube) {
                    const proceed = confirm('The link does not look like a YouTube URL. Save anyway?');
                    if (!proceed) return;
                }
            } catch (e) {
                alert('Invalid URL format for YouTube link. Please check it.');
                return;
            }
        }

        // Create new piece object
        const newPiece = {
            id: this.generateGuid(),
            title: title,
            composer: composer,
            creationDate: creationDate || new Date().toISOString(),
            colorResourceName: this.selectedColor ? this.selectedColor.name : 'LightCoral',
            colorValue: this.selectedColor ? this.selectedColor.value : '#F08080',
            progress: 0,
            barSections: [],
            practiceSessions: [],
            notes: '',
            lifecycleState: 0,
            isPaused: false,
            pauseUntilDate: null,
            youTubeLink: youTubeLink || ''
        };

        // Add to music pieces array
        this.musicPieces.push(newPiece);
        this.profileData.musicPieces = this.musicPieces;

        // Save to localStorage
        this.saveProfileData();

        // Persist autocomplete values (recent list without duplicates)
        // Track both composers and titles so users can re-use them
        this.addAutocompleteValue('composer', composer);
        this.addAutocompleteValue('title', title);

        // Update UI
        this.loadData();
        this.updateUI();

        // Hide modal
        this.hideAddPieceDialog();

        console.log('✅ New piece added:', newPiece);
        
        // Track piece addition
        if (window.ga4Tracker) {
            window.ga4Tracker.trackPieceAdded(
                newPiece.title,
                newPiece.composer,
                newPiece.barSections?.length || 0
            );
        }
    }

    openPiece(pieceId) {
        console.log('Opening piece:', pieceId);
        const piece = this.musicPieces.find(p => p.id === pieceId);
        if (!piece) return;

        this.currentPiece = piece; // Store current piece

        // Switch views
        document.getElementById('pieces-view').style.display = 'none';
        const detailView = document.getElementById('piece-detail-view');
        detailView.style.display = 'flex';

        // Render detail view
        this.renderPieceDetail(piece);
    }

    closePieceDetail() {
        this.currentPiece = null;
        document.getElementById('piece-detail-view').style.display = 'none';
        document.getElementById('pieces-view').style.display = 'flex';
        
        // Refresh list in case of changes
        this.loadData();
        this.renderPiecesList();
        this.updateStatistics();
    }

    renderPieceDetail(piece) {
        // Update header
        document.getElementById('detail-title').textContent = piece.title;
        
        // Render metadata
        const metadataEl = document.getElementById('detail-metadata');
        const sectionCount = piece.barSections ? piece.barSections.length : 0;
        const activeCount = piece.barSections ? piece.barSections.filter(s => s.lifecycleState !== 2).length : 0;
        const progress = piece.progress || 0;
        const lifecycleState = this.getPieceLifecycleState(piece);
        const statusLabel = this.getPieceLifecycleLabel(lifecycleState);
        const statusBadgeClass = lifecycleState === 0 ? 'status-active' : 'status-archived';
        const ytLink = piece.youTubeLink || '';
        const ytLinkHtml = ytLink ? `<a href="${this.escapeHtml(ytLink)}" target="_blank" rel="noopener noreferrer">Open on YouTube</a>` : '—';
        const ytSvg = `<svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true" focusable="false">
                <rect x="2" y="6" width="20" height="12" rx="3" ry="3" fill="#FF0000"></rect>
                <polygon points="10,9 16,12 10,15" fill="#ffffff"></polygon>
            </svg>`;
        
        // Helper to extract YouTube video ID (supports youtu.be, watch?v=, shorts/, embed/)
        const parseYouTubeId = (url) => {
            if (!url) return null;
            try {
                const u = new URL(url.startsWith('http') ? url : 'https://' + url);
                const host = (u.hostname || '').toLowerCase();
                if (!(host.includes('youtube.com') || host.includes('youtu.be'))) return null;
                if (host.includes('youtu.be')) {
                    const path = (u.pathname || '/').replace(/^\//, '');
                    return path ? path.split('/')[0] : null;
                }
                if (u.searchParams && u.searchParams.get('v')) return u.searchParams.get('v');
                const path = (u.pathname || '/');
                const mShorts = path.match(/\/shorts\/([A-Za-z0-9_-]{6,})/);
                if (mShorts) return mShorts[1];
                const mEmbed = path.match(/\/embed\/([A-Za-z0-9_-]{6,})/);
                if (mEmbed) return mEmbed[1];
                return null;
            } catch (e) {
                return null;
            }
        };

        metadataEl.innerHTML = `
            <div><strong>Composer:</strong> ${this.escapeHtml(piece.composer)}</div>
            <div><strong>Created:</strong> ${new Date(piece.creationDate).toLocaleDateString()}</div>
            <div><strong>Chunks:</strong> ${activeCount} active / ${sectionCount} total</div>
            <div><strong>Progress:</strong> ${progress}%</div>
            <div><strong>Status:</strong> <span class="status-badge ${statusBadgeClass}">${statusLabel}</span></div>
            <div><strong>YouTube:</strong> ${ytLinkHtml}</div>
            <div style="grid-column: span 2; display: flex; align-items: center; gap: 6px;">
                <strong>Color:</strong> 
                <span style="width: 12px; height: 12px; background-color: ${piece.colorValue}; border-radius: 2px; display: inline-block;"></span>
                ${piece.colorResourceName}
            </div>
        `;

        const lifecycleSelect = document.getElementById('piece-lifecycle-select');
        if (lifecycleSelect) {
            lifecycleSelect.value = lifecycleState.toString();
            lifecycleSelect.style.color = (lifecycleState === 0) ? 'var(--success-color)' : 'var(--danger-color)';
        }

        const statusBanner = document.getElementById('piece-status-banner');
        if (statusBanner) {
            statusBanner.style.display = this.isPieceArchived(piece) ? 'block' : 'none';
        }

        // Configure YouTube button visibility/action
        const ytBtn = document.getElementById('btn-open-youtube');
        if (ytBtn) {
            if (ytLink) {
                ytBtn.style.display = '';
                ytBtn.innerHTML = ytSvg + '<span style="margin-left:6px;">YouTube</span>';
                ytBtn.setAttribute('aria-label', 'Open YouTube link');
                ytBtn.setAttribute('title', 'Open YouTube link');
                ytBtn.style.display = 'inline-flex';
                ytBtn.style.alignItems = 'center';
                ytBtn.style.gap = '6px';
                ytBtn.onclick = () => {
                    try { window.open(ytLink, '_blank'); } catch (e) { /* ignore */ }
                };
            } else {
                ytBtn.style.display = 'none';
                ytBtn.onclick = null;
            }
        }

        // Render YouTube thumbnail preview if possible
        const previewEl = document.getElementById('youtube-preview');
        if (previewEl) {
            const vid = parseYouTubeId(ytLink);
            if (vid) {
                const thumb = `https://img.youtube.com/vi/${vid}/hqdefault.jpg`;
                previewEl.innerHTML = `
                    <a href="${this.escapeHtml(ytLink)}" target="_blank" rel="noopener noreferrer" style="display:inline-flex; align-items:center; gap:8px; text-decoration:none;">
                        <img src="${thumb}" alt="YouTube thumbnail" style="height:72px; width:auto; border-radius:4px; border:1px solid var(--border-color); background:#fff;"/>
                        <span style="font-size:12px; color: var(--text-secondary);">Open YouTube preview</span>
                    </a>
                `;
                previewEl.style.display = 'block';
            } else {
                previewEl.style.display = 'none';
                previewEl.innerHTML = '';
            }
        }

        // Render sections
        this.renderSectionsList(piece);
    }

    generateThumbnailChart(sectionId) {
        if (!this.profileData.practiceHistory) return '';

        // Filter history for this section
        const history = this.profileData.practiceHistory
            .filter(h => h.barSectionId === sectionId && !h.isDeleted)
            .sort((a, b) => new Date(a.date) - new Date(b.date));

        if (history.length < 2) {
            return '<div style="height: 80px; display: flex; align-items: center; justify-content: center; color: #ccc; font-size: 10px; font-style: italic; background: #f9f9f9; border-radius: 4px; margin: 8px 0;">Not enough data</div>';
        }

        // Take last 10 sessions
        const recentHistory = history.slice(-10);
        
        const width = 150; // Internal coordinate system width
        const height = 100; // Internal coordinate system height
        
        // Helper to convert ratio to Y coordinate (0 at top)
        // Matches SuccessRatioTrendChart.js non-linear scaling
        const ratioToY = (ratio) => {
            let scaledH; // 0.0 to 1.0 (from bottom)
            if (ratio <= 0.60) {
                scaledH = (ratio / 0.60) * 0.20; // 0-60% -> 0-20% height
            } else {
                scaledH = 0.20 + ((ratio - 0.60) / 0.40) * 0.80; // 60-100% -> 20-100% height
            }
            return height - (scaledH * height);
        };

        // Zones (matching SuccessRatioTrendChart.js colors)
        const zones = [
            { min: 0.95, max: 1.00, color: '#D0D0D0' }, // Overlearning
            { min: 0.90, max: 0.95, color: '#C8E6C9' }, // Mastery
            { min: 0.80, max: 0.90, color: '#FFE0B2' }, // Consolidation
            { min: 0.60, max: 0.80, color: '#FFF59D' }, // Exploration
            { min: 0.00, max: 0.60, color: '#FFCDD2' }  // Too Hard
        ];

        let svgContent = '';

        // Draw zones
        zones.forEach(zone => {
            const yTop = ratioToY(zone.max);
            const yBottom = ratioToY(zone.min);
            const h = Math.max(0, yBottom - yTop);
            svgContent += `<rect x="0" y="${yTop}" width="${width}" height="${h}" fill="${zone.color}" fill-opacity="0.6" />`;
        });

        // Calculate points
        const points = recentHistory.map((session, index) => {
            const reps = session.repetitions || 0;
            let fails = 0;
            if (session.memoryFailures !== undefined) fails += session.memoryFailures;
            if (session.executionFailures !== undefined) fails += session.executionFailures;
            if (session.failures !== undefined && session.memoryFailures === undefined) fails += session.failures;
            
            const total = reps + fails;
            const ratio = total > 0 ? reps / total : 0;
            
            const x = (index / (recentHistory.length - 1)) * width;
            const y = ratioToY(ratio);
            return `${x},${y}`;
        });

        // Draw line
        const pathData = `M ${points.join(' L ')}`;
        svgContent += `<path d="${pathData}" fill="none" stroke="#4A86E8" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />`;

        // Draw dots
        points.forEach((p, i) => {
            const [x, y] = p.split(',');
            const isLast = i === points.length - 1;
            const r = isLast ? 2.5 : 1.5;
            const fill = isLast ? '#4A86E8' : '#fff';
            const stroke = isLast ? '#fff' : '#4A86E8';
            svgContent += `<circle cx="${x}" cy="${y}" r="${r}" fill="${fill}" stroke="${stroke}" stroke-width="1" />`;
        });

        return `
            <div style="margin: 8px 0; border: 1px solid #ddd; border-radius: 4px; overflow: hidden; background: white;">
                <svg viewBox="0 0 ${width} ${height}" style="width: 100%; height: 80px; display: block;" preserveAspectRatio="none">
                    ${svgContent}
                </svg>
            </div>
        `;
    }

    renderSectionsList(piece) {
        const listEl = document.getElementById('sections-list');
        if (!listEl) return;

        const sections = piece.barSections || [];
        const pieceArchived = this.isPieceArchived(piece);
        
        if (sections.length === 0) {
            listEl.innerHTML = `
                <div class="empty-state">
                    <div class="empty-icon">📝</div>
                    <div class="empty-text">No chunks added yet</div>
                    <button class="btn btn-primary" onclick="app.showAddSectionModal()">Add Your First Chunk</button>
                </div>
            `;
            return;
        }

        // Sort sections by barRange
        sections.sort((a, b) => (a.barRange || '').localeCompare(b.barRange || ''));

        listEl.innerHTML = sections.map(section => {
            const nextReview = section.nextReviewDate ? new Date(section.nextReviewDate).toLocaleDateString() : 'Not scheduled';
            const target = section.targetRepetitions || 6;
            const progress = Math.round(((section.completedRepetitions || 0) / target) * 100);
            const lifecycleStateValue = section.lifecycleState !== undefined ? section.lifecycleState : 0; // 0=Active
            const isInactive = pieceArchived || lifecycleStateValue === 2; // 2 = Inactive/Archive
            const disableMessage = pieceArchived
                ? 'Reactivate this piece to practice this chunk'
                : 'Reactivate this chunk to practice';
            const practiceButtonAttrs = isInactive ? `disabled title="${disableMessage}"` : '';
            
            // Generate chart thumbnail
            const chartThumbnail = this.generateThumbnailChart(section.id);
            
            return `
                <div class="list-item" style="cursor: default; opacity: ${isInactive ? 0.6 : 1}; height: 260px; display: flex; flex-direction: column; align-self: flex-start;">
                    <div class="item-main">
                        <div class="item-details">
                            <div class="item-title" style="display: flex; justify-content: space-between; align-items: center;">
                                <span>${this.escapeHtml(section.barRange)}</span>
                                <div style="display: flex; gap: 4px;">
                                    <button class="btn btn-secondary" style="padding: 2px 6px; font-size: 10px;" onclick="app.openPracticeSession('${section.id}')" ${practiceButtonAttrs}>Practice</button>
                                    <button class="btn btn-secondary" style="padding: 2px 6px; font-size: 10px;" onclick="app.showEditSectionModal('${section.id}')">Edit</button>
                                    <button class="btn btn-secondary" style="padding: 2px 6px; font-size: 10px; color: var(--danger-color); border-color: var(--danger-color);" onclick="app.deleteSection('${section.id}')">×</button>
                                </div>
                            </div>
                            <div class="item-subtitle">${this.escapeHtml(section.description || '')}</div>
                        </div>
                        ${chartThumbnail}
                    </div>
                    <div class="item-meta" style="display:flex; flex-direction:column; gap:6px; align-items:flex-start;">
                        <div>Reps: ${section.completedRepetitions || 0}/${target} (${progress}%)</div>
                        <div>Next: ${nextReview}</div>
                        <div>
                            <select style="font-size: 10px; padding: 1px 4px; border: 1px solid var(--border-color); border-radius: 3px; background: #fff; color: ${lifecycleStateValue === 0 ? 'var(--success-color)' : (lifecycleStateValue === 1 ? 'var(--accent-color)' : 'var(--danger-color)')};" onchange="app.changeLifecycleState('${section.id}', this.value)">
                                <option value="0" ${lifecycleStateValue === 0 ? 'selected' : ''} style="color: var(--success-color);">Active</option>
                                <option value="1" ${lifecycleStateValue === 1 ? 'selected' : ''} style="color: var(--accent-color);">Maintenance</option>
                                <option value="2" ${lifecycleStateValue === 2 ? 'selected' : ''} style="color: var(--danger-color);">Archive</option>
                            </select>
                        </div>
                    </div>
                </div>
            `;
        }).join('');
    }

    changeLifecycleState(sectionId, newStateValue) {
        if (!this.currentPiece) return;
        
        const section = this.currentPiece.barSections.find(s => s.id === sectionId);
        if (!section) return;

        const oldStateValue = section.lifecycleState !== undefined ? section.lifecycleState : 0;
        const newState = parseInt(newStateValue);
        
        if (oldStateValue === newState) return;

        // Apply lifecycle state business rules
        switch (newState) {
            case 0: // Active
                section.lifecycleState = 0;
                if (oldStateValue === 2) { // From Archive
                    const today = new Date();
                    today.setHours(0, 0, 0, 0);
                    section.nextReviewDate = today.toISOString();
                }
                break;

            case 1: // Maintenance
                section.lifecycleState = 1;
                const maintenanceMinDays = 7;
                if (section.interval < maintenanceMinDays) {
                    section.interval = maintenanceMinDays;
                }
                const dueDate = new Date();
                dueDate.setHours(0, 0, 0, 0);
                dueDate.setDate(dueDate.getDate() + section.interval);
                section.nextReviewDate = dueDate.toISOString();
                break;

            case 2: // Inactive/Archive
                section.lifecycleState = 2;
                section.nextReviewDate = null;
                break;
        }

        // Synchronize profileData before saving
        this.profileData.musicPieces = this.musicPieces;

        this.saveProfileData();
        this.renderPieceDetail(this.currentPiece);
        // Keep calendar data in sync when a section changes lifecycle state.
        this.buildScheduledSessions();
        if (this.currentView === 'calendar') {
            this.renderCalendar();
        }
        
        // Update sidebar
        this.renderTodaySessions();
        this.updateStatistics();
    }

    openPracticeSession(sectionId) {
        const lookup = this.findSectionById(sectionId);
        if (!lookup) {
            console.warn('Section not found for practice session:', sectionId);
            return;
        }

        const { section, piece } = lookup;

        if (this.isPieceArchived(piece) || (section.lifecycleState !== undefined && section.lifecycleState === 2)) {
            alert('This piece or chunk is archived. Reactivate it to resume practice.');
            return;
        }

        console.log('Opening practice session for section:', sectionId);
        
        // Track session start
        if (window.ga4Tracker) {
            window.ga4Tracker.trackPracticeSessionStarted(
                piece.title,
                section.barRange
            );
        }

        if (typeof openPracticeSessionWindow === 'function') {
            openPracticeSessionWindow(sectionId);
        } else {
            window.location.href = `moduspractica-practice-session.html?section=${sectionId}`;
        }
    }

    // Helper to find section for tracking
    findSectionById(sectionId) {
        for (const piece of this.musicPieces) {
            if (piece.barSections) {
                const section = piece.barSections.find(s => s.id === sectionId);
                if (section) {
                    return { section, piece };
                }
            }
        }
        return null;
    }

    deletePiece(pieceId, pieceTitle) {
        // Confirm deletion
        const confirmed = confirm(`Are you sure you want to delete "${pieceTitle}"?\n\nThis will remove the piece and all its sections, but practice history will be preserved for statistics.`);
        
        if (!confirmed) return;
        
        // Find the piece
        const piece = this.musicPieces.find(p => p.id === pieceId);
        if (!piece) {
            alert('Piece not found');
            return;
        }
        
        // Log deletion
        console.log(`🗑️ Deleting piece: ${pieceTitle} (${pieceId})`);
        if (piece.barSections) {
            console.log(`   Removing ${piece.barSections.length} sections`);
        }
        console.log(`   Practice history will be preserved for statistics`);
        
        // Track piece deletion
        if (window.ga4Tracker) {
            window.ga4Tracker.trackPieceDeleted(pieceTitle);
        }
        
        // Remove the piece from the array
        this.musicPieces = this.musicPieces.filter(p => p.id !== pieceId);
        this.profileData.musicPieces = this.musicPieces;
        
        // Note: practiceHistory is NOT modified - it remains for statistics
        // The history entries will still show the piece title and section info
        
        // Save to localStorage
        this.saveProfileData();
        
        // Update UI
        this.loadData();
        this.updateUI();
        
        console.log('✅ Piece deleted successfully');
        alert(`"${pieceTitle}" has been deleted.\n\nPractice history has been preserved for statistics.`);
    }

    // ========================================================================
    // PIECE & SECTION MANAGEMENT (MODALS)
    // ========================================================================

    showEditPieceModal() {
        if (!this.currentPiece) return;
        
        document.getElementById('edit-piece-title').value = this.currentPiece.title;
        document.getElementById('edit-piece-composer').value = this.currentPiece.composer;
        document.getElementById('edit-piece-creation-date').value = this.currentPiece.creationDate.split('T')[0];
        const ytInput = document.getElementById('edit-piece-youtube');
        if (ytInput) ytInput.value = this.currentPiece.youTubeLink || '';

        // Render color picker for edit
        const colorPicker = document.getElementById('edit-color-picker');
        colorPicker.innerHTML = '';
        this.availableColors.forEach(color => {
            const colorOption = document.createElement('div');
            colorOption.className = 'color-option';
            colorOption.style.backgroundColor = color.value;
            colorOption.title = color.name;
            
            if (this.currentPiece.colorResourceName === color.name) {
                colorOption.classList.add('selected');
            }

            colorOption.addEventListener('click', () => {
                colorPicker.querySelectorAll('.color-option').forEach(opt => opt.classList.remove('selected'));
                colorOption.classList.add('selected');
            });
            colorPicker.appendChild(colorOption);
        });

        document.getElementById('edit-piece-modal').classList.add('active');
    }

    hideEditPieceModal() {
        document.getElementById('edit-piece-modal').classList.remove('active');
    }

    saveEditedPiece() {
        if (!this.currentPiece) return;

        const title = document.getElementById('edit-piece-title').value.trim();
        const composer = document.getElementById('edit-piece-composer').value.trim();
        const creationDate = document.getElementById('edit-piece-creation-date').value;
        const youTubeLinkRaw = (document.getElementById('edit-piece-youtube')?.value || '').trim();
        
        if (!title || !composer) {
            alert('Please fill in all required fields');
            return;
        }

        const selectedColorEl = document.querySelector('#edit-color-picker .color-option.selected');
        if (!selectedColorEl) {
            alert('Please select a color theme');
            return;
        }
        
        // Find color object
        const colorValue = selectedColorEl.style.backgroundColor; // This might be rgb(), need to match with availableColors
        // Better way:
        const colorName = selectedColorEl.title;
        const colorObj = this.availableColors.find(c => c.name === colorName);

        // Normalize YouTube link (optional)
        let youTubeLink = youTubeLinkRaw;
        if (youTubeLink && !/^https?:\/\//i.test(youTubeLink)) {
            youTubeLink = 'https://' + youTubeLink;
        }
        if (youTubeLink) {
            try {
                const u = new URL(youTubeLink);
                const host = (u.hostname || '').toLowerCase();
                const isYouTube = host.includes('youtube.com') || host.includes('youtu.be');
                if (!isYouTube) {
                    const proceed = confirm('The link does not look like a YouTube URL. Save anyway?');
                    if (!proceed) return;
                }
            } catch (e) {
                alert('Invalid URL format for YouTube link. Please check it.');
                return;
            }
        }

        this.currentPiece.title = title;
        this.currentPiece.composer = composer;
        this.currentPiece.creationDate = new Date(creationDate).toISOString();
        this.currentPiece.colorResourceName = colorObj.name;
        this.currentPiece.colorValue = colorObj.value;
        this.currentPiece.youTubeLink = youTubeLink || '';

        // Synchronize profileData before saving
        this.profileData.musicPieces = this.musicPieces;

        this.saveProfileData();
        this.renderPieceDetail(this.currentPiece);
        this.hideEditPieceModal();
        
        // Update autocomplete
        this.addAutocompleteValue('title', title);
        this.addAutocompleteValue('composer', composer);
    }

    showAddSectionModal() {
        document.getElementById('chunk-range').value = '';
        document.getElementById('chunk-description').value = '';
        document.getElementById('target-repetitions').value = '6';
        document.getElementById('add-section-modal').classList.add('active');
        setTimeout(() => document.getElementById('chunk-range').focus(), 100);
    }

    hideAddSectionModal() {
        document.getElementById('add-section-modal').classList.remove('active');
    }

    addSection() {
        if (!this.currentPiece) return;

        const chunkRangeInput = document.getElementById('chunk-range').value.trim();
        const description = document.getElementById('chunk-description').value.trim();
        let targetRepetitions = parseInt(document.getElementById('target-repetitions').value, 10);

        if (!chunkRangeInput) {
            alert('Please enter a chunk range');
            return;
        }

        const validation = this.validateBarRangeFormat(chunkRangeInput);
        if (!validation.valid) {
            alert(validation.error);
            return;
        }

        const chunkRange = this.formatBarRange(chunkRangeInput);

        // Check duplicate
        if (this.currentPiece.barSections && this.currentPiece.barSections.some(s => s.barRange === chunkRange)) {
            if (!confirm(`The chunk range '${chunkRange}' already exists. Add it anyway?`)) return;
        }

        const today = new Date();
        today.setHours(0, 0, 0, 0);

        const newSection = {
            id: this.generateGuid(),
            barRange: chunkRange,
            description: description,
            targetRepetitions: targetRepetitions || 6,
            completedRepetitions: 0,
            stage: 0,
            nextReviewDate: today.toISOString(),
            lastPracticeDate: null,
            startDate: new Date().toISOString(),
            status: 'New',
            difficulty: 'Difficult',
            attemptsTillSuccess: 0,
            interval: 1,
            practiceScheduleStage: 0,
            adaptiveTauMultiplier: 1.0,
            lifecycleState: 0 // Active
        };

        if (!this.currentPiece.barSections) this.currentPiece.barSections = [];
        const beforeCount = this.currentPiece.barSections.length;
        this.currentPiece.barSections.push(newSection);
        console.log('➕ Added new section:', {
            barRange: newSection.barRange,
            beforeCount,
            afterCount: this.currentPiece.barSections.length
        });

        // Synchronize profileData before saving
        this.profileData.musicPieces = this.musicPieces;

        console.log('💾 Saving new section to localStorage...');
        this.saveProfileData();
        const savedData = JSON.parse(localStorage.getItem(this.storagePrefix + this.currentProfile.id + '_data'));
        const savedPiece = savedData.musicPieces.find(p => p.id === this.currentPiece.id);
        console.log('✅ Verified saved sections count after add:', savedPiece.barSections.length);
        this.renderPieceDetail(this.currentPiece);
        this.hideAddSectionModal();
        
        // Also update the Today's Agenda sidebar
        this.renderTodaySessions();
        this.updateStatistics();
    }

    showEditSectionModal(sectionId) {
        const section = this.currentPiece.barSections.find(s => s.id === sectionId);
        if (!section) return;

        document.getElementById('edit-section-id').value = sectionId;
        document.getElementById('edit-chunk-range').value = section.barRange;
        document.getElementById('edit-chunk-description').value = section.description || '';
        document.getElementById('edit-target-repetitions').value = (section.targetRepetitions || 6).toString();

        document.getElementById('edit-section-modal').classList.add('active');
    }

    hideEditSectionModal() {
        document.getElementById('edit-section-modal').classList.remove('active');
    }

    saveEditedSection() {
        const sectionId = document.getElementById('edit-section-id').value;
        const description = document.getElementById('edit-chunk-description').value.trim();
        const targetRepetitions = parseInt(document.getElementById('edit-target-repetitions').value, 10);

        const section = this.currentPiece.barSections.find(s => s.id === sectionId);
        if (!section) return;

        section.description = description;
        section.targetRepetitions = targetRepetitions;

        // Synchronize profileData before saving
        this.profileData.musicPieces = this.musicPieces;

        this.saveProfileData();
        this.renderPieceDetail(this.currentPiece);
        this.hideEditSectionModal();
    }

    deleteSection(sectionId) {
        if (window.MPLog) MPLog.info('deleteSection called', { sectionId, currentPieceId: this.currentPiece?.id });
        
        // Find the piece in the musicPieces array (not currentPiece reference)
        const piece = this.musicPieces.find(p => p.id === this.currentPiece.id);
        if (!piece) {
            if (window.MPLog) MPLog.warn('deleteSection: Piece not found in musicPieces', { pieceId: this.currentPiece.id });
            return;
        }
        
        const section = piece.barSections.find(s => s.id === sectionId);
        if (!section) {
            if (window.MPLog) MPLog.warn('deleteSection: Section not found', { sectionId, pieceId: piece.id });
            return;
        }

        if (!confirm(`Are you sure you want to delete the chunk "${section.barRange}"?`)) return;

        if (window.MPLog) MPLog.info('🗑️ Deleting section', { sectionId, barRange: section.barRange, pieceId: piece.id });
        if (window.MPLog) MPLog.info('📊 Before delete - sections count', { count: piece.barSections.length, sections: piece.barSections.map(s => ({ id: s.id, barRange: s.barRange })) });
        
        piece.barSections = piece.barSections.filter(s => s.id !== sectionId);
        
        if (window.MPLog) MPLog.info('📊 After delete - sections count', { count: piece.barSections.length, sections: piece.barSections.map(s => ({ id: s.id, barRange: s.barRange })) });
        
        // Update currentPiece reference
        this.currentPiece = piece;
        
        // Synchronize profileData before saving
        this.profileData.musicPieces = this.musicPieces;
        
        if (window.MPLog) MPLog.info('💾 Saving to localStorage...', { profileId: this.currentProfile.id, storageKey: this.storagePrefix + this.currentProfile.id + '_data' });
        this.saveProfileData();
        
        // Verify save
        const savedData = JSON.parse(localStorage.getItem(this.storagePrefix + this.currentProfile.id + '_data'));
        const savedPiece = savedData.musicPieces.find(p => p.id === piece.id);
        if (window.MPLog) MPLog.info('✅ Verified saved sections count', { savedCount: savedPiece.barSections.length, savedSections: savedPiece.barSections.map(s => ({ id: s.id, barRange: s.barRange })) });
        
        this.renderPieceDetail(this.currentPiece);
        
        // Update sidebar
        this.renderTodaySessions();
        this.updateStatistics();
    }

    validateBarRangeFormat(barRange) {
        if (!barRange) return { valid: false, error: 'Please enter a chunk range.' };
        const parts = barRange.split('-');
        if (parts.length !== 2) return { valid: false, error: 'Use format: XX-YY (e.g., 01-02).' };
        if (!parseInt(parts[0].trim())) return { valid: false, error: 'Invalid start number.' };
        if (!parseInt(parts[1].trim().split(' ')[0])) return { valid: false, error: 'Invalid end number.' };
        return { valid: true };
    }

    formatBarRange(barRange) {
        if (!barRange) return barRange;
        const parts = barRange.split('-');
        if (parts.length !== 2) return barRange;
        
        const start = parseInt(parts[0].trim());
        const secondPart = parts[1].trim();
        const secondPartTokens = secondPart.split(' ');
        const end = parseInt(secondPartTokens[0].trim());
        
        let formatted = `${String(start).padStart(2, '0')}-${String(end).padStart(2, '0')}`;
        if (secondPartTokens.length > 1) {
            formatted += ' ' + secondPartTokens.slice(1).join(' ');
        }
        return formatted;
    }

    showQuotaExceededError(storageInfo) {
        const usedMB = (storageInfo.used / 1024 / 1024).toFixed(2);
        const limitMB = (storageInfo.estimatedLimit / 1024 / 1024).toFixed(2);
        const percentage = (storageInfo.usedPercentage * 100).toFixed(1);

        const message = 
            `⚠️ LocalStorage Vol (${percentage}%)\n\n` +
            `Gebruikt: ${usedMB} MB / ${limitMB} MB\n\n` +
            `Acties:\n` +
            `1. Exporteer je data (backup)\n` +
            `2. Verwijder oude profielen\n` +
            `3. Ruim oude sessies op in profiel instellingen`;

        alert(message);

        // Toon storage status in console
        storageQuotaManager.showQuotaStatus();
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    formatShortDateDisplay(date) {
        if (!(date instanceof Date) || Number.isNaN(date.getTime())) {
            return 'Unknown';
        }

        const today = new Date();
        today.setHours(0, 0, 0, 0);

        const target = new Date(date.getFullYear(), date.getMonth(), date.getDate());
        const diffDays = Math.round((target - today) / (1000 * 60 * 60 * 24));

        if (diffDays === 0) return 'Today';
        if (diffDays === 1) return 'Tomorrow';
        if (diffDays === -1) return 'Yesterday';

        const options = { weekday: 'short', day: 'numeric', month: 'short' };
        return target.toLocaleDateString(undefined, options);
    }

    formatDate(dateString) {
        if (!dateString) return 'Unknown';
        const date = new Date(dateString);
        const now = new Date();
        const diffDays = Math.floor((now - date) / (1000 * 60 * 60 * 24));
        
        if (diffDays === 0) return 'Today';
        if (diffDays === 1) return 'Yesterday';
        if (diffDays < 7) return `${diffDays} days ago`;
        if (diffDays < 30) return `${Math.floor(diffDays / 7)} weeks ago`;
        if (diffDays < 365) return `${Math.floor(diffDays / 30)} months ago`;
        return `${Math.floor(diffDays / 365)} years ago`;
    }

    // GUID generation now in moduspractica-utils.js
    generateGuid() {
        return generateGUID(); // Delegate to global utility function
    }
}

// ============================================================================
// AUTOCOMPLETE (Title/Composer) WITH REMOVAL SUPPORT
// ============================================================================
ModusPracticaDashboard.prototype.ensureAutocompleteImported = function() {
    try {
        if (!this.currentProfile) return;
        const flagKey = `${this.storagePrefix}${this.currentProfile.id}_auto_imported_v1`;
        if (localStorage.getItem(flagKey) === 'true') return;

        // Import from existing pieces (titles + composers)
        if (Array.isArray(this.musicPieces)) {
            this.musicPieces.forEach(p => {
                if (p?.composer) this.addAutocompleteValue('composer', String(p.composer).trim());
                if (p?.title) this.addAutocompleteValue('title', String(p.title).trim());
            });
        }

        localStorage.setItem(flagKey, 'true');
        console.log('✅ Autocomplete lists initialized from existing pieces');
    } catch (e) {
        console.warn('Autocomplete import skipped:', e);
    }
};

ModusPracticaDashboard.prototype.getAutocompleteKey = function(type) {
    return `${this.storagePrefix}${this.currentProfile?.id || 'default'}_auto_${type}`;
};

ModusPracticaDashboard.prototype.loadAutocompleteList = function(type) {
    try {
        const raw = localStorage.getItem(this.getAutocompleteKey(type));
        if (!raw) return [];
        const arr = JSON.parse(raw);
        return Array.isArray(arr) ? arr : [];
    } catch { return []; }
};

ModusPracticaDashboard.prototype.saveAutocompleteList = function(type, list) {
    try { localStorage.setItem(this.getAutocompleteKey(type), JSON.stringify(list)); } catch {}
};

ModusPracticaDashboard.prototype.addAutocompleteValue = function(type, value) {
    if (!value) return;
    const list = this.loadAutocompleteList(type);
    const lower = value.toLowerCase();
    const filtered = list.filter(v => v.toLowerCase() !== lower);
    filtered.unshift(value);
    // limit to 50 recent
    this.saveAutocompleteList(type, filtered.slice(0, 50));
};

ModusPracticaDashboard.prototype.removeAutocompleteValue = function(type, value) {
    const list = this.loadAutocompleteList(type).filter(v => v !== value);
    this.saveAutocompleteList(type, list);
};

ModusPracticaDashboard.prototype.wordStartsMatch = function(fullText, query) {
    const q = (query || '').trim().toLowerCase();
    if (!q) return true;
    return fullText.split(/\s+/).some(w => w.toLowerCase().startsWith(q));
};

ModusPracticaDashboard.prototype.renderSuggestions = function(type) {
    const input = document.getElementById(type === 'title' ? 'piece-title' : 'piece-composer');
    const box = document.getElementById(type === 'title' ? 'title-suggestions' : 'composer-suggestions');
    if (!input || !box) return;

    const list = this.loadAutocompleteList(type);
    const query = input.value || '';
    const matches = list.filter(v => this.wordStartsMatch(v, query));

    box.innerHTML = '';
    if (matches.length === 0) {
        box.style.display = 'none';
        return;
    }

    matches.forEach(val => {
        const item = document.createElement('div');
        item.className = 'autocomplete-item';

        const text = document.createElement('div');
        text.className = 'autocomplete-text';
        text.textContent = val;
        item.appendChild(text);

        // For both 'composer' and 'title' show a remove/x button so users can prune suggestions
        if (type === 'composer' || type === 'title') {
            const remove = document.createElement('button');
            remove.className = 'autocomplete-remove';
            remove.type = 'button';
            remove.textContent = '×';
            remove.title = `Remove \"${val}\" from suggestions`;
            remove.setAttribute('aria-label', `Remove ${val} from suggestions`);
            remove.addEventListener('mousedown', (e) => {
                e.preventDefault();
                e.stopPropagation();
                this.removeAutocompleteValue(type, val);
                this.renderSuggestions(type);
            });
            item.appendChild(remove);
        }

        item.addEventListener('mousedown', (e) => {
            // Use mousedown instead of click to fire before blur event
            // Check if the click was on the remove button
            if (e.target.classList.contains('autocomplete-remove')) {
                return; // Let the remove button handler take care of it
            }
            e.preventDefault(); // Prevent blur from firing
            input.value = val;
            box.style.display = 'none';
            input.focus(); // Return focus to input
            input.dispatchEvent(new Event('input', { bubbles: true }));
        });

        box.appendChild(item);
    });

    box.style.display = 'block';
};

ModusPracticaDashboard.prototype.setupAutocomplete = function() {
    const bind = (type) => {
        const input = document.getElementById(type === 'title' ? 'piece-title' : 'piece-composer');
        const box = document.getElementById(type === 'title' ? 'title-suggestions' : 'composer-suggestions');
        if (!input || !box) return;

        const show = () => this.renderSuggestions(type);
        const hide = () => setTimeout(() => { box.style.display = 'none'; }, 200);

        input.addEventListener('focus', show);
        input.addEventListener('input', show);
        input.addEventListener('blur', hide);
    };

    // Enable suggestions for both composer and title
    bind('composer');
    bind('title');
};

// ============================================================================
// SETTINGS MODAL FUNCTIONS
// ============================================================================

/**
 * Opens the settings modal and loads current Intensity Module settings
 */
function openSettingsModal() {
    const modal = document.getElementById('settingsModal');
    if (!modal) {
        console.error('❌ Settings modal element not found');
        return;
    }

    let currentSettings = {};
    // Load current settings
    const currentProfile = localStorage.getItem('mp_lastProfile') || localStorage.getItem('mp_currentProfile');
    if (currentProfile) {
        const settingsKey = `mp_${currentProfile}_intensitySettings`;
        currentSettings = JSON.parse(localStorage.getItem(settingsKey) || '{}');
        const intensityToggle = document.getElementById('intensityModuleToggle');
        const microToggle = document.getElementById('microBreakToggle');

        const intensityEnabled = currentSettings.enabled !== false; // Default: enabled
        const microEnabled = currentSettings.enableMicroBreaks !== false; // Default: enabled

        if (intensityToggle) {
            intensityToggle.checked = intensityEnabled;
        }

        if (microToggle) {
            microToggle.checked = microEnabled;
        }
    }

    updateSettingsSummaries(currentSettings);

    modal.style.display = 'flex';
    console.log('⚙️ Settings modal opened');
}

/**
 * Closes the settings modal
 */
function closeSettingsModal() {
    const modal = document.getElementById('settingsModal');
    if (modal) {
        modal.style.display = 'none';
        console.log('⚙️ Settings modal closed');
    }
}

/**
 * Opens the logging page from the settings modal
 */
function openLoggingPage() {
    window.location.href = 'moduspractica-logging.html';
}

/**
 * Toggles the Intensity Module on/off and saves the setting
 */
function toggleIntensityModule() {
    const toggle = document.getElementById('intensityModuleToggle');
    const currentProfile = localStorage.getItem('mp_lastProfile') || localStorage.getItem('mp_currentProfile');
    
    if (!toggle || !currentProfile) {
        console.error('❌ Cannot toggle: missing toggle element or profile');
        return;
    }

    const enabled = toggle.checked;
    const settingsKey = `mp_${currentProfile}_intensitySettings`;
    
    // Load existing settings or create new
    const settings = JSON.parse(localStorage.getItem(settingsKey) || '{}');
    settings.enabled = enabled;
    settings.lastModified = new Date().toISOString();
    
    // Save to localStorage
    localStorage.setItem(settingsKey, JSON.stringify(settings));
    
    console.log(`✅ Intensity Module ${enabled ? 'ingeschakeld' : 'uitgeschakeld'} voor profiel ${currentProfile}`);

    updateSettingsSummaries(settings);
}

/**
 * Toggles the Micro-Break reminders on/off and saves the setting
 */
function toggleMicroBreaks() {
    const toggle = document.getElementById('microBreakToggle');
    const currentProfile = localStorage.getItem('mp_lastProfile') || localStorage.getItem('mp_currentProfile');

    if (!toggle || !currentProfile) {
        console.error('❌ Cannot toggle micro-breaks: missing toggle element or profile');
        return;
    }

    const enabled = toggle.checked;
    const settingsKey = `mp_${currentProfile}_intensitySettings`;
    const settings = JSON.parse(localStorage.getItem(settingsKey) || '{}');

    settings.enableMicroBreaks = enabled;
    settings.lastModified = new Date().toISOString();

    localStorage.setItem(settingsKey, JSON.stringify(settings));

    console.log(`✅ Micro-break reminders ${enabled ? 'ingeschakeld' : 'uitgeschakeld'} voor profiel ${currentProfile}`);

    updateSettingsSummaries(settings);
}

/**
 * Updates the status summaries shown in the settings modal
 */
function updateSettingsSummaries(settings = {}) {
    const intensityEnabled = settings.enabled !== false;
    const microEnabled = settings.enableMicroBreaks !== false;

    const intensityStatus = document.getElementById('intensityStatus');
    if (intensityStatus) {
        intensityStatus.innerHTML = `<strong>Status:</strong> ${intensityEnabled ? 'Enabled' : 'Disabled'}`;
    }

    const intensityInfo = document.getElementById('intensityModuleInfo');
    if (intensityInfo) {
        intensityInfo.style.display = 'block';
    }

    const microStatus = document.getElementById('microBreakStatus');
    if (microStatus) {
        microStatus.innerHTML = `<strong>Status:</strong> ${microEnabled ? 'Enabled' : 'Disabled'}`;
    }

    const microInfo = document.getElementById('microBreakInfo');
    if (microInfo) {
        microInfo.style.display = 'block';
    }
}

/**
 * Close modal when clicking outside of it
 */
window.addEventListener('click', (event) => {
    const modal = document.getElementById('settingsModal');
    if (event.target === modal) {
        closeSettingsModal();
    }
});

// ============================================================================
// CALENDAR FUNCTIONS (Dashboard Integration)
// ============================================================================

ModusPracticaDashboard.prototype.switchView = function(viewName) {
    console.log('📍 Switching view to:', viewName);
    
    // Update tab states
    document.querySelectorAll('.view-tab').forEach(tab => {
        tab.classList.remove('active');
    });
    
    if (viewName === 'pieces') {
        document.getElementById('tab-pieces').classList.add('active');
        document.getElementById('pieces-view').style.display = 'flex';
        document.getElementById('calendar-view').style.display = 'none';
        document.getElementById('piece-detail-view').style.display = 'none';
        this.currentView = 'pieces';
    } else if (viewName === 'calendar') {
        document.getElementById('tab-calendar').classList.add('active');
        document.getElementById('pieces-view').style.display = 'none';
        document.getElementById('calendar-view').style.display = 'flex';
        document.getElementById('piece-detail-view').style.display = 'none';
        this.currentView = 'calendar';
        
        // Populate calendar title filter
        this.populateCalendarTitleFilter();
        
        // Render calendar when switching to it
        this.renderCalendar();
    }
};

ModusPracticaDashboard.prototype.populateCalendarTitleFilter = function() {
    const titleFilter = document.getElementById('calendar-title-filter');
    if (!titleFilter) return;

    // Store current selection
    const currentSelection = this.selectedCalendarTitleFilter;

    // Clear existing options except the first one ("All pieces")
    titleFilter.innerHTML = '<option value="">All pieces</option>';

    // Sort pieces by title
    const sortedPieces = [...this.musicPieces].sort((a, b) => 
        a.title.localeCompare(b.title)
    );

    // Add option for each piece
    sortedPieces.forEach(piece => {
        const opt = document.createElement('option');
        opt.value = piece.id;
        const archivedSuffix = this.isPieceArchived(piece) ? ' (Archived)' : '';
        opt.textContent = piece.title + archivedSuffix;
        if (piece.id === currentSelection) {
            opt.selected = true;
        }
        titleFilter.appendChild(opt);
    });
};

ModusPracticaDashboard.prototype.buildScheduledSessions = function() {
    this.scheduledSessions = [];
    
    // Add future/planned sessions from current sections
    for (const piece of this.musicPieces) {
        if (this.isPieceArchived(piece)) continue;
        if (!piece.barSections || piece.barSections.length === 0) continue;
        
        for (const section of piece.barSections) {
            if (!section.nextReviewDate) continue;
            
            // Skip inactive sections
            if (section.lifecycleState === 2) continue;
            
            const reviewDate = new Date(section.nextReviewDate);
            const now = new Date();
            const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
            const reviewDay = new Date(reviewDate.getFullYear(), reviewDate.getMonth(), reviewDate.getDate());
            
            let status = 'Planned';
            if (reviewDay < today) status = 'Overdue';
            else if (reviewDay.getTime() === today.getTime()) status = 'Due Today';

            this.scheduledSessions.push({
                id: section.id,
                pieceId: piece.id,
                pieceTitle: piece.title,
                pieceColor: piece.colorValue || '#ADD8E6',
                sectionRange: section.barRange,
                scheduledDate: reviewDate,
                difficulty: section.difficulty || 'Average',
                status: status,
                completedRepetitions: section.completedRepetitions || 0
            });
        }
    }

    // Add completed sessions from history
    if (this.profileData.practiceHistory) {
        for (const historyItem of this.profileData.practiceHistory) {
            const piece = this.musicPieces.find(p => p.id === historyItem.musicPieceId);
            const color = piece ? (piece.colorValue || '#ADD8E6') : '#cccccc';
            
            const historyDate = new Date(historyItem.date);
            if (isNaN(historyDate.getTime())) continue;

            this.scheduledSessions.push({
                id: historyItem.barSectionId,
                pieceId: historyItem.musicPieceId,
                pieceTitle: historyItem.musicPieceTitle || 'Unknown Piece',
                pieceColor: color,
                sectionRange: historyItem.barSectionRange || 'Unknown Section',
                scheduledDate: historyDate,
                difficulty: historyItem.difficulty || 'Average',
                status: 'Completed',
                completedRepetitions: historyItem.repetitions || 0
            });
        }
    }
    
    console.log(`📊 Built ${this.scheduledSessions.length} scheduled sessions`);
};

ModusPracticaDashboard.prototype.renderCalendar = function() {
    this.updateMonthTitle();
    this.renderCalendarDays();
};

ModusPracticaDashboard.prototype.updateMonthTitle = function() {
    const monthNames = ['January', 'February', 'March', 'April', 'May', 'June',
                       'July', 'August', 'September', 'October', 'November', 'December'];
    const monthName = monthNames[this.currentMonth.getMonth()];
    const year = this.currentMonth.getFullYear();
    const el = document.getElementById('currentMonth');
    if (el) el.textContent = `${monthName} ${year}`;
};

ModusPracticaDashboard.prototype.renderCalendarDays = function() {
    const container = document.getElementById('calendarDays');
    if (!container) return;
    
    container.innerHTML = '';
    
    const year = this.currentMonth.getFullYear();
    const month = this.currentMonth.getMonth();
    
    const firstDay = new Date(year, month, 1);
    let firstDayOfWeek = firstDay.getDay();
    firstDayOfWeek = firstDayOfWeek === 0 ? 7 : firstDayOfWeek;
    
    const lastDay = new Date(year, month + 1, 0);
    const daysInMonth = lastDay.getDate();
    
    const prevMonthLastDay = new Date(year, month, 0);
    const daysInPrevMonth = prevMonthLastDay.getDate();
    
    const now = new Date();
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    
    // Calculate current week start (Monday) and end (Sunday)
    const currentWeekStart = new Date(today);
    const dayOfWeek = today.getDay();
    const daysToMonday = dayOfWeek === 0 ? 6 : dayOfWeek - 1;
    currentWeekStart.setDate(today.getDate() - daysToMonday);
    currentWeekStart.setHours(0, 0, 0, 0);
    
    const currentWeekEnd = new Date(currentWeekStart);
    currentWeekEnd.setDate(currentWeekStart.getDate() + 6);
    currentWeekEnd.setHours(23, 59, 59, 999);
    
    // Previous month trailing days
    for (let i = firstDayOfWeek - 1; i > 0; i--) {
        const day = daysInPrevMonth - i + 1;
        const date = new Date(year, month - 1, day);
        const isCurrentWeek = date >= currentWeekStart && date <= currentWeekEnd;
        const cell = this.createDayCell(date, false, true, isCurrentWeek);
        container.appendChild(cell);
    }
    
    // Current month days
    for (let day = 1; day <= daysInMonth; day++) {
        const date = new Date(year, month, day);
        const isToday = date.getTime() === today.getTime();
        const isCurrentWeek = date >= currentWeekStart && date <= currentWeekEnd;
        const cell = this.createDayCell(date, isToday, false, isCurrentWeek);
        container.appendChild(cell);
    }
    
    // Next month leading days
    const totalCells = container.children.length;
    const remainingCells = 42 - totalCells;
    for (let day = 1; day <= remainingCells; day++) {
        const date = new Date(year, month + 1, day);
        const isCurrentWeek = date >= currentWeekStart && date <= currentWeekEnd;
        const cell = this.createDayCell(date, false, true, isCurrentWeek);
        container.appendChild(cell);
    }
};

ModusPracticaDashboard.prototype.createDayCell = function(date, isToday, isOtherMonth, isCurrentWeek = false) {
    const cell = document.createElement('div');
    cell.className = 'day-cell';
    
    if (isToday) cell.classList.add('today');
    if (isOtherMonth) cell.classList.add('other-month');
    if (isCurrentWeek) cell.classList.add('current-week');
    
    const dayNumber = document.createElement('div');
    dayNumber.className = 'day-number';
    dayNumber.textContent = date.getDate();
    cell.appendChild(dayNumber);
    
    const sessionsContainer = document.createElement('div');
    sessionsContainer.className = 'day-sessions';
    
    const daySessions = this.scheduledSessions.filter(session => {
        const sessionDateStr = session.scheduledDate.toDateString();
        if (sessionDateStr !== date.toDateString()) return false;
        
        // Apply calendar title filter
        if (this.selectedCalendarTitleFilter && session.pieceId !== this.selectedCalendarTitleFilter) {
            return false;
        }
        
        return true;
    });
    
    if (daySessions.length > 0) {
        const MAX_VISIBLE_WHEN_COLLAPSED = 4;
        const isExpanded = this.isCalendarExpanded;
        const limit = isExpanded ? daySessions.length : MAX_VISIBLE_WHEN_COLLAPSED;

        for (let i = 0; i < Math.min(limit, daySessions.length); i++) {
            const session = daySessions[i];
            const sessionEl = document.createElement('div');
            sessionEl.className = 'session-item';

            if (session.status === 'Completed') {
                sessionEl.classList.add('completed');
            } else if (session.status === 'Overdue') {
                sessionEl.classList.add('overdue');
            }

            const pieceColor = session.pieceColor || '#ADD8E6';
            sessionEl.style.borderLeftColor = pieceColor;
            // Use shared color density as piece tiles / agenda items
            sessionEl.style.backgroundColor = this.hexToRgba(pieceColor, 0.20);
            sessionEl.textContent = `${session.pieceTitle} - ${session.sectionRange}`;
            sessionEl.title = `${session.pieceTitle} - ${session.sectionRange}`;

            sessionsContainer.appendChild(sessionEl);
        }

        if (!isExpanded && daySessions.length > MAX_VISIBLE_WHEN_COLLAPSED) {
            const more = document.createElement('div');
            more.className = 'day-more-indicator';
            const hiddenCount = daySessions.length - MAX_VISIBLE_WHEN_COLLAPSED;
            more.textContent = `+${hiddenCount} more`;
            sessionsContainer.appendChild(more);
        }
    }
    
    cell.appendChild(sessionsContainer);
    
    // No click handler: sessions are started from Today's Agenda on the right
    return cell;
};
 
ModusPracticaDashboard.prototype.previousMonth = function() {
    this.currentMonth.setMonth(this.currentMonth.getMonth() - 1);
    this.renderCalendar();
};

ModusPracticaDashboard.prototype.nextMonth = function() {
    this.currentMonth.setMonth(this.currentMonth.getMonth() + 1);
    this.renderCalendar();
};

ModusPracticaDashboard.prototype.goToToday = function() {
    this.currentMonth = new Date();
    this.currentMonth.setDate(1);
    this.renderCalendar();
};

ModusPracticaDashboard.prototype.openCalendarWindow = function() {
    // Open standalone calendar in a new window
    const width = 1200;
    const height = 800;
    const left = Math.max(0, (screen.width - width) / 2);
    const top = Math.max(0, (screen.height - height) / 2);
    
    window.open(
        'moduspractica-calendar.html',
        'ModusPracticaCalendar',
        `width=${width},height=${height},left=${left},top=${top},resizable=yes,scrollbars=yes`
    );
    
    console.log('📅 Opened standalone calendar window');
};

// ============================================================================
// INTERLEAVED PRACTICE MODE
// ============================================================================

ModusPracticaDashboard.prototype.startDailyInterleavedReview = function() {
    console.log('🔀 Starting Daily Interleaved Review...');
    
    // Timezone-safe reference date
    const today = getTodayLocal();
    
    // Filter practice history for sessions completed today (timezone-safe)
    const todaysSessions = (this.profileData.practiceHistory || []).filter(session => {
        if (session.isDeleted) return false;
        return isSameDay(session.date, today);
    });

    // Extract unique barSectionIds
    const uniqueSectionIds = [...new Set(todaysSessions.map(s => s.barSectionId))].filter(id => id);
    
    console.log(`📊 Found ${todaysSessions.length} sessions today with ${uniqueSectionIds.length} unique chunks`);
    
    // Constraint: Need at least 2 unique sections
    if (uniqueSectionIds.length < 2) {
        alert(
            '🔀 Interleaved Practice vereist minimaal 2 verschillende chunks vandaag.\n\n' +
            `Je hebt vandaag ${uniqueSectionIds.length} unieke chunk${uniqueSectionIds.length === 1 ? '' : 's'} geoefend.\n\n` +
            'Tip: Oefen eerst een paar verschillende chunks via de normale agenda, ' +
            'en kom dan terug voor een Interleaved Circuit!'
        );
        return;
    }
    
    // Save the queue to sessionStorage
    sessionStorage.setItem('mp_interleaved_queue', JSON.stringify(uniqueSectionIds));
    
    // Log for debugging
    console.log('✅ Interleaved queue saved to sessionStorage:', uniqueSectionIds);
    if (window.MPLog) {
        MPLog.info('Interleaved Practice started', { 
            uniqueChunks: uniqueSectionIds.length,
            sectionIds: uniqueSectionIds 
        });
    }
    
    // Track event
    if (window.ga4Tracker) {
        window.ga4Tracker.trackEvent('interleaved_practice_started', {
            chunk_count: uniqueSectionIds.length
        });
    }
    
    // Redirect to practice session with mode=interleaved
    window.location.href = 'moduspractica-practice-session.html?mode=interleaved';
};

ModusPracticaDashboard.prototype.hexToRgba = function(hex, alpha = 1) {
    if (!hex) {
        return `rgba(0, 0, 0, ${alpha})`;
    }

    // Normalize and strip '#'
    let cleaned = String(hex).trim();
    if (cleaned.startsWith('#')) cleaned = cleaned.slice(1);

    // Support shorthand #RGB
    if (cleaned.length === 3) {
        cleaned = cleaned.split('').map(ch => ch + ch).join('');
    }

    if (cleaned.length !== 6) {
        return `rgba(0, 0, 0, ${alpha})`;
    }

    const r = parseInt(cleaned.substring(0, 2), 16) || 0;
    const g = parseInt(cleaned.substring(2, 4), 16) || 0;
    const b = parseInt(cleaned.substring(4, 6), 16) || 0;

    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
};

// ============================================================================
// EXPOSE FUNCTIONS TO GLOBAL SCOPE
// ============================================================================

// Make settings functions globally accessible for onclick handlers
window.openSettingsModal = openSettingsModal;
window.closeSettingsModal = closeSettingsModal;
window.openLoggingPage = openLoggingPage;
window.toggleIntensityModule = toggleIntensityModule;
window.toggleMicroBreaks = toggleMicroBreaks;

// ============================================================================
// INITIALIZE DASHBOARD
// ============================================================================

let app;

document.addEventListener('DOMContentLoaded', () => {
    app = new ModusPracticaDashboard();
});

// Debug helper for console inspection: type debugDashboard() to inspect state
window.debugDashboard = function() {
    if (!app) {
        console.warn('Dashboard app not initialized yet.');
        return;
    }
    console.log('=== DASHBOARD DEBUG STATE ===');
    console.log('Current profile:', app.currentProfile);
    console.log('Music pieces loaded:', app.musicPieces ? app.musicPieces.length : 'n/a');
    if (Array.isArray(app.musicPieces)) {
        app.musicPieces.slice(0, 5).forEach((piece, index) => {
            console.log(`  Piece[${index}]:`, {
                id: piece.id,
                title: piece.title,
                sections: Array.isArray(piece.barSections) ? piece.barSections.length : 0,
                lifecycleState: piece.lifecycleState,
                isPaused: piece.isPaused
            });
        });
    }
    console.log('Filtered pieces:', app.filteredPieces ? app.filteredPieces.length : 'n/a');
    console.log('Selected filters:', {
        searchQuery: app.searchQuery,
        selectedTitleFilter: app.selectedTitleFilter,
        selectedColorFilter: app.selectedColorFilter,
        selectedSortMode: app.selectedSortMode
    });
    console.log('First filtered piece:', app.filteredPieces && app.filteredPieces[0]);
    console.log('profileData statistics:', app.profileData ? app.profileData.statistics : 'n/a');
    console.log('Practice history entries:', app.profileData && Array.isArray(app.profileData.practiceHistory) ? app.profileData.practiceHistory.length : 'n/a');
};
