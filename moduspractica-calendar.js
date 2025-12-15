// ============================================================================
// ModusPractica Calendar - Practice Schedule Overview
// Copyright © 2025 Frank De Baere - All Rights Reserved
// ============================================================================

class ModusPracticaCalendar {
    constructor() {
        this.storagePrefix = 'mp_';
        this.currentProfile = null;
        this.profileData = null;
        this.currentMonth = new Date();
        this.currentMonth.setDate(1); // Set to first day of month
        this.hasOpenedInitialDay = false;
        
        this.musicPieces = [];
        this.selectedPieceId = null;
        this.scheduledSessions = [];
        
        // Ebbinghaus/Adaptive managers (aligned with Dashboard)
        this.pmcManager = new PersonalizedMemoryCalibration(this.storagePrefix);
        this.stabilityManager = new MemoryStabilityManager(this.storagePrefix);
        this.adaptiveTauManager = new AdaptiveTauManager();

        const params = new URLSearchParams(window.location.search);
        this.initialDayParam = params.get('day');
        this.initialDayDate = null;
        if (this.initialDayParam) {
            const parsedDate = this.parseDayParam(this.initialDayParam);
            if (parsedDate) {
                this.initialDayDate = parsedDate;
                this.currentMonth = new Date(parsedDate.getFullYear(), parsedDate.getMonth(), 1);
            } else {
                this.initialDayParam = null;
            }
        }
        
        this.init();
    }

    // ========================================================================
    // INITIALIZATION
    // ========================================================================

    init() {
        if (window.MPLog) MPLog.info('📅 Initializing ModusPractica Calendar...');
        console.log('📅 Initializing ModusPractica Calendar...');
        
        // Load current profile
        this.loadCurrentProfile();
        
        if (!this.currentProfile) {
            if (window.MPLog) MPLog.error('No profile loaded, redirecting to app');
            window.location.href = 'moduspractica-app.html';
            return;
        }
        
        if (window.MPLog) MPLog.info(`Calendar initialized for profile: ${this.currentProfile.name}`);
        
        // Initialize adaptive systems for this profile (same as Dashboard)
        try {
            this.pmcManager.initializeCalibrationSystem(this.currentProfile.id);
            this.stabilityManager.initializeForUser(this.currentProfile.id);
            if (window.MPLog) MPLog.info('✅ Ebbinghaus systems initialized (Calendar)');
            console.log('✅ Ebbinghaus systems initialized (Calendar)');
        } catch (e) {
            if (window.MPLog) MPLog.warn('Ebbinghaus systems init warning (Calendar)', e);
            console.warn('Ebbinghaus systems init warning (Calendar):', e);
        }
        
        // Setup event listeners
        this.setupEventListeners();
        
        // Load data
        this.loadData();
        
        // Render calendar
        this.renderCalendar();
        
        if (window.MPLog) MPLog.info('✅ Calendar initialized successfully');
        console.log('✅ Calendar initialized');

        this.openInitialDayFocus();
    }

    loadCurrentProfile() {
        const lastProfileId = localStorage.getItem(this.storagePrefix + 'lastProfile');
        if (!lastProfileId) {
            return;
        }

        const profilesJson = localStorage.getItem(this.storagePrefix + 'profiles');
        if (profilesJson) {
            const profiles = JSON.parse(profilesJson);
            this.currentProfile = profiles.find(p => p.id === lastProfileId);
        }
    }

    loadData() {
        // Load full profile data (align with Dashboard)
        const dataKey = this.storagePrefix + this.currentProfile.id + '_data';
        const dataJson = localStorage.getItem(dataKey);
        if (dataJson) {
            this.profileData = JSON.parse(dataJson);
            this.musicPieces = this.profileData.musicPieces || [];
            if (!this.profileData.practiceHistory) this.profileData.practiceHistory = [];
            
            const dataInfo = {
                pieces: this.musicPieces.length,
                totalSections: this.musicPieces.reduce((sum, p) => sum + (p.barSections?.length || 0), 0),
                historyEntries: this.profileData.practiceHistory.length
            };
            
            if (window.MPLog) MPLog.info('📊 Loaded profile data', dataInfo);
            console.log(`📊 Loaded profile data:`, dataInfo);
        } else {
            // Initialize minimal structure to proceed safely
            this.profileData = { musicPieces: [], practiceHistory: [], settings: {}, statistics: { totalSessions: 0, totalPracticeTime: 0 } };
            this.musicPieces = [];
            if (window.MPLog) MPLog.warn('⚠️ No profile data found, using empty structure');
            console.log('⚠️ No profile data found, using empty structure');
            try {
                if (typeof storageQuotaManager !== 'undefined') {
                    storageQuotaManager.safeSetItem(dataKey, JSON.stringify(this.profileData));
                } else {
                    localStorage.setItem(dataKey, JSON.stringify(this.profileData));
                }
            } catch (_) {}
        }
        
        // Initialize sections without nextReviewDate
        this.initializeMissingSessions();
        
        // Build scheduled sessions from sections
        this.buildScheduledSessions();
        
        if (window.MPLog) MPLog.info(`✅ Built ${this.scheduledSessions.length} scheduled sessions for calendar display`);
        console.log(`✅ Built ${this.scheduledSessions.length} scheduled sessions for calendar display`);
        
        // Populate piece filter
        this.populatePieceFilter();
    }
    initializeMissingSessions() {
        // Initialize nextReviewDate for sections that don't have one
        // This ensures all active sections appear in the calendar
        let needsSave = false;
        const now = new Date();
        const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        
        for (const piece of this.musicPieces) {
            if (!piece.barSections) continue;
            
            for (const section of piece.barSections) {
                // Skip inactive sections (check both string and numeric values for backward compatibility)
                if (section.lifecycleState === 'Inactive' || section.lifecycleState === 2) continue;
                
                // If no nextReviewDate, set it to today
                if (!section.nextReviewDate) {
                    section.nextReviewDate = today.toISOString();
                    needsSave = true;
                    console.log(`✨ Initialized nextReviewDate for section: ${piece.title} - ${section.barRange}`);
                }
            }
        }
        
        // Save back to localStorage if any sections were initialized
        if (needsSave) {
            const dataKey = this.storagePrefix + this.currentProfile.id + '_data';
            const data = {
                musicPieces: this.musicPieces,
                settings: JSON.parse(localStorage.getItem(dataKey))?.settings || {},
                statistics: JSON.parse(localStorage.getItem(dataKey))?.statistics || {}
            };
            
            try {
                if (typeof storageQuotaManager !== 'undefined') {
                    storageQuotaManager.safeSetItem(dataKey, JSON.stringify(data));
                } else {
                    localStorage.setItem(dataKey, JSON.stringify(data));
                }
                console.log(`💾 Saved ${this.musicPieces.length} pieces with initialized review dates`);
            } catch (error) {
                if (error.name === 'QuotaExceededError') {
                    console.warn('Storage quota exceeded, attempting cleanup...');
                    storageQuotaManager.emergencyCleanup();
                    try {
                        if (typeof storageQuotaManager !== 'undefined') {
                            storageQuotaManager.safeSetItem(dataKey, JSON.stringify(data));
                        } else {
                            localStorage.setItem(dataKey, JSON.stringify(data));
                        }
                    } catch (retryError) {
                        alert('⚠️ Opslag vol! Exporteer je data en ruim oude profielen op.');
                    }
                }
            }
        }
    }
    
    buildScheduledSessions() {
        this.scheduledSessions = [];
        
        if (window.MPLog) MPLog.info(`🔨 Building scheduled sessions from ${this.musicPieces.length} pieces...`);
        console.log(`🔨 Building scheduled sessions from ${this.musicPieces.length} pieces...`);
        
        // 1. Add future/planned sessions from current sections
        let plannedCount = 0;
        for (const piece of this.musicPieces) {
            if (!piece.barSections || piece.barSections.length === 0) continue;
            
            for (const section of piece.barSections) {
                if (!section.nextReviewDate) {
                    if (window.MPLog) MPLog.warn(`⚠️ Section ${piece.title} - ${section.barRange} has no nextReviewDate`);
                    console.log(`⚠️ Section ${piece.title} - ${section.barRange} has no nextReviewDate`);
                    continue;
                }
                
                // Skip inactive sections (check both string and numeric values for backward compatibility)
                if (section.lifecycleState === 'Inactive' || section.lifecycleState === 2) continue;
                
                // Get lifecycle state name for display
                let lifecycleStateName = 'Active';
                if (typeof getLifecycleStateName === 'function' && typeof section.lifecycleState === 'number') {
                    lifecycleStateName = getLifecycleStateName(section.lifecycleState);
                } else if (section.lifecycleState) {
                    lifecycleStateName = section.lifecycleState;
                }
                
                // Determine status based on date only
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
                    completedRepetitions: section.completedRepetitions || 0,
                    lifecycleState: lifecycleStateName
                });
                plannedCount++;
            }
        }
        
        if (window.MPLog) MPLog.info(`✅ Added ${plannedCount} planned sessions from sections`);
        console.log(`✅ Added ${plannedCount} planned sessions from sections`);

        // 2. Add completed sessions from history
        if (this.profileData.practiceHistory) {
            if (window.MPLog) MPLog.info(`📜 Processing ${this.profileData.practiceHistory.length} history items...`);
            console.log(`📜 Processing ${this.profileData.practiceHistory.length} history items...`);
            for (const historyItem of this.profileData.practiceHistory) {
                // Find piece info (it might be deleted, so use history data if available or fallback)
                const piece = this.musicPieces.find(p => p.id === historyItem.musicPieceId);
                const color = piece ? (piece.colorValue || '#ADD8E6') : '#cccccc';
                
                // Ensure date is valid
                const historyDate = new Date(historyItem.date);
                if (isNaN(historyDate.getTime())) {
                    console.warn('Invalid date in history item:', historyItem);
                    continue;
                }

                this.scheduledSessions.push({
                    id: historyItem.barSectionId,
                    pieceId: historyItem.musicPieceId,
                    pieceTitle: historyItem.musicPieceTitle || 'Unknown Piece',
                    pieceColor: color,
                    sectionRange: historyItem.barSectionRange || 'Unknown Section',
                    scheduledDate: historyDate,
                    difficulty: historyItem.difficulty || 'Average',
                    status: 'Completed',
                    completedRepetitions: historyItem.repetitions || 0,
                    lifecycleState: 'History'
                });
            }
            if (window.MPLog) MPLog.info(`✅ Added ${this.profileData.practiceHistory.length} history entries`);
            console.log(`✅ Added ${this.profileData.practiceHistory.length} history entries`);
        } else {
            if (window.MPLog) MPLog.info('📜 No practice history found');
            console.log('📜 No practice history found');
        }
        
        const totalMsg = `📊 TOTAL: ${this.scheduledSessions.length} scheduled sessions (${plannedCount} planned + ${this.profileData.practiceHistory?.length || 0} history)`;
        if (window.MPLog) MPLog.info(totalMsg);
        console.log(totalMsg);
    }

    openInitialDayFocus() {
        if (this.hasOpenedInitialDay || !this.initialDayDate) return;
        const sessions = this.getSessionsForDate(this.initialDayDate);
        this.showDayDetails(this.initialDayDate, sessions, { force: true });
        this.hasOpenedInitialDay = true;
        this.initialDayParam = null;
    }

    populatePieceFilter() {
        const select = document.getElementById('pieceFilter');
        select.innerHTML = '<option value="">All Pieces</option>';
        
        for (const piece of this.musicPieces) {
            const option = document.createElement('option');
            option.value = piece.id;
            option.textContent = `${piece.title}${piece.composer ? ' - ' + piece.composer : ''}`;
            select.appendChild(option);
        }
    }

    setupEventListeners() {
        if (window.MPLog) MPLog.info('Setting up event listeners...');
        
        const btnPrev = document.getElementById('btnPrevMonth');
        const btnNext = document.getElementById('btnNextMonth');
        const btnToday = document.getElementById('btnToday');
        const btnFreePractice = document.getElementById('btnFreePractice');
        const btnRecalc = document.getElementById('btnRecalculate');
        const pieceFilter = document.getElementById('pieceFilter');
        const modalClose = document.getElementById('modalClose');
        const dayModal = document.getElementById('dayModal');
        
        if (btnPrev) btnPrev.addEventListener('click', () => this.previousMonth());
        if (btnNext) btnNext.addEventListener('click', () => this.nextMonth());
        if (btnToday) btnToday.addEventListener('click', () => this.goToToday());
        if (btnFreePractice) btnFreePractice.addEventListener('click', () => this.openFreePractice());
        if (btnRecalc) btnRecalc.addEventListener('click', () => this.recalculateSchedule());
        
        if (pieceFilter) {
            pieceFilter.addEventListener('change', (e) => {
                this.selectedPieceId = e.target.value || null;
                this.renderCalendar();
            });
        }
        
        // Modal close handlers
        if (modalClose) modalClose.addEventListener('click', () => this.closeModal());
        if (dayModal) {
            dayModal.addEventListener('click', (e) => {
                if (e.target.id === 'dayModal') {
                    this.closeModal();
                }
            });
        }
        
        // Close modal on ESC key
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') {
                this.closeModal();
            }
        });
        
        if (window.MPLog) MPLog.info('✅ Event listeners set up successfully');
    }

    /**
     * Recalculate schedule using the same logic as Dashboard
     * Preserves future dates, clamps overdue to today, saves to localStorage
     */
    recalculateSchedule() {
        const message =
            "This action will recalculate your entire practice schedule based on your current progress and the latest settings.\n\n" +
            "What happens?\n" +
            "• All sections will be rescheduled according to their practice history using spaced repetition.\n" +
            "• Sections you've practiced more will be scheduled further in the future.\n" +
            "• New or recently practiced sections will be scheduled sooner.\n" +
            "• Overdue sections will be rescheduled to today.\n" +
            "• Future scheduled dates will be preserved.\n\n" +
            "Do you want to proceed and regenerate the schedule?";

        if (!confirm(message)) return;

        console.log('🔄 [Calendar] Starting schedule recalculation...');

        const now = new Date();
        const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        let totalUpdated = 0;
        let overdueClamped = 0;
        let futurePreserved = 0;

        try {
            this.musicPieces.forEach(piece => {
                if (!piece.barSections) return;

                piece.barSections.forEach(section => {
                    try {
                        // Skip paused/inactive/mastered
                        if (section.isPaused || section.lifecycleState === 'Inactive' || section.lifecycleState === 2 || section.practiceScheduleStage >= 6) return;

                        const difficulty = section.difficulty || 'Average';
                        const completedReps = section.completedRepetitions || 0;

                        // Calculate tau via adaptive system
                        const tau = this.adaptiveTauManager.calculateIntegratedTau(
                            difficulty,
                            completedReps,
                            {
                                barSectionId: section.id,
                                sectionHistory: this.getSectionPracticeHistory(section.id),
                                userAge: this.currentProfile.age || 30,
                                userExperience: this.currentProfile.musicalExperience || 'intermediate',
                                pmcManager: this.pmcManager,
                                stabilityManager: this.stabilityManager,
                                useAdaptiveSystems: true
                            }
                        );

                        const targetRetention = this.getRetentionTargetForDifficulty(difficulty);
                        const logInput = (targetRetention - 0.15) / 0.85;
                        let newInterval = 1;
                        if (logInput > 0 && isFinite(logInput)) {
                            const rawInterval = -tau * Math.log(logInput);
                            newInterval = isFinite(rawInterval) ? Math.max(0, Math.min(365, Math.round(rawInterval))) : 1;
                        }

                        const computedNextDue = new Date(today);
                        computedNextDue.setDate(computedNextDue.getDate() + newInterval);

                        let finalNextDue = computedNextDue;
                        if (section.nextPracticeDate || section.nextReviewDate) {
                            const existingDateStr = section.nextPracticeDate || section.nextReviewDate;
                            const existingDateObj = new Date(existingDateStr);
                            const existingDate = new Date(existingDateObj.getFullYear(), existingDateObj.getMonth(), existingDateObj.getDate());

                            if (existingDate > today) {
                                finalNextDue = existingDate; // preserve future
                                futurePreserved++;
                            } else if (existingDate < today) {
                                finalNextDue = new Date(today); // clamp overdue
                                overdueClamped++;
                            }
                        }

                        if (finalNextDue < today) finalNextDue = new Date(today);

                        section.nextPracticeDate = finalNextDue.toISOString();
                        section.nextReviewDate = finalNextDue.toISOString();
                        section.interval = Math.floor((finalNextDue - today) / (1000 * 60 * 60 * 24));

                        totalUpdated++;
                    } catch (err) {
                        console.error('[Calendar] Error recalculating section', section?.barRange, err);
                    }
                });
            });

            // Save and refresh
            this.profileData.musicPieces = this.musicPieces;
            const key = this.storagePrefix + this.currentProfile.id + '_data';
            try {
                if (typeof storageQuotaManager !== 'undefined') {
                    storageQuotaManager.safeSetItem(key, JSON.stringify(this.profileData));
                } else {
                    localStorage.setItem(key, JSON.stringify(this.profileData));
                }
            } catch (e) {
                console.warn('Storage save warning:', e);
            }

            const summary =
                `Schedule recalculation complete!\n\n` +
                `✅ Updated ${totalUpdated} active sections\n` +
                `📅 Preserved ${futurePreserved} future dates\n` +
                `⚠️ Clamped ${overdueClamped} overdue sections to today`;
            alert(summary);

            console.log('✅ [Calendar] Recalculation done:', { totalUpdated, futurePreserved, overdueClamped });

            // Refresh calendar view
            this.loadData();
            this.renderCalendar();
        } catch (error) {
            console.error('[Calendar] Error during schedule recalculation:', error);
            alert('An error occurred during schedule recalculation. Please check the console for details.');
        }
    }

    getSectionPracticeHistory(barSectionId) {
        if (!this.profileData || !this.profileData.practiceHistory) return [];
        return this.profileData.practiceHistory.filter(s => s.barSectionId === barSectionId);
    }

    getRetentionTargetForDifficulty(difficulty) {
        switch (difficulty) {
            case 'Easy': return 0.70;
            case 'Average': return 0.80;
            case 'Difficult': return 0.85;
            default: return 0.80;
        }
    }
    
    closeModal() {
        document.getElementById('dayModal').style.display = 'none';
    }

    // ========================================================================
    // NAVIGATION
    // ========================================================================

    previousMonth() {
        this.currentMonth.setMonth(this.currentMonth.getMonth() - 1);
        this.renderCalendar();
    }

    nextMonth() {
        this.currentMonth.setMonth(this.currentMonth.getMonth() + 1);
        this.renderCalendar();
    }

    goToToday() {
        this.currentMonth = new Date();
        this.currentMonth.setDate(1);
        this.renderCalendar();
    }

    openFreePractice() {
        // Open Free Practice timer in a new window
        const width = 700;
        const height = 600;
        const left = (screen.width - width) / 2;
        const top = (screen.height - height) / 2;
        
        window.open(
            'moduspractica-free-practice.html',
            'FreePractice',
            `width=${width},height=${height},left=${left},top=${top},resizable=yes,scrollbars=no`
        );
        
        console.log('🎵 Opened Free Practice window');
    }

    // ========================================================================
    // RENDERING
    // ========================================================================

    renderCalendar() {
        this.updateMonthTitle();
        this.renderCalendarDays();
        this.updateStats();
    }

    updateMonthTitle() {
        const monthNames = ['January', 'February', 'March', 'April', 'May', 'June',
                           'July', 'August', 'September', 'October', 'November', 'December'];
        const monthName = monthNames[this.currentMonth.getMonth()];
        const year = this.currentMonth.getFullYear();
        document.getElementById('currentMonth').textContent = `${monthName} ${year}`;
    }

    renderCalendarDays() {
        const container = document.getElementById('calendarDays');
        container.innerHTML = '';
        
        const year = this.currentMonth.getFullYear();
        const month = this.currentMonth.getMonth();
        
        // Get first day of month (0 = Sunday, 1 = Monday, etc.)
        const firstDay = new Date(year, month, 1);
        let firstDayOfWeek = firstDay.getDay();
        // Convert Sunday (0) to 7 for Monday-first week
        firstDayOfWeek = firstDayOfWeek === 0 ? 7 : firstDayOfWeek;
        
        // Get last day of month
        const lastDay = new Date(year, month + 1, 0);
        const daysInMonth = lastDay.getDate();
        
        // Get days from previous month
        const prevMonthLastDay = new Date(year, month, 0);
        const daysInPrevMonth = prevMonthLastDay.getDate();
        
        const now = new Date();
        const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        
        // Calculate current week start (Monday) and end (Sunday)
        const currentWeekStart = new Date(today);
        const dayOfWeek = today.getDay();
        const daysToMonday = dayOfWeek === 0 ? 6 : dayOfWeek - 1; // Sunday = 0, Monday = 1
        currentWeekStart.setDate(today.getDate() - daysToMonday);
        currentWeekStart.setHours(0, 0, 0, 0);
        
        const currentWeekEnd = new Date(currentWeekStart);
        currentWeekEnd.setDate(currentWeekStart.getDate() + 6);
        currentWeekEnd.setHours(23, 59, 59, 999);
        
        // Render previous month's trailing days
        for (let i = firstDayOfWeek - 1; i > 0; i--) {
            const day = daysInPrevMonth - i + 1;
            const date = new Date(year, month - 1, day);
            const isCurrentWeek = date >= currentWeekStart && date <= currentWeekEnd;
            const cell = this.createDayCell(date, false, true, isCurrentWeek);
            container.appendChild(cell);
        }
        
        // Render current month's days
        for (let day = 1; day <= daysInMonth; day++) {
            const date = new Date(year, month, day);
            const isToday = date.getTime() === today.getTime();
            const isCurrentWeek = date >= currentWeekStart && date <= currentWeekEnd;
            const cell = this.createDayCell(date, isToday, false, isCurrentWeek);
            container.appendChild(cell);
        }
        
        // Render next month's leading days to complete the grid
        const totalCells = container.children.length;
        const remainingCells = 42 - totalCells; // 6 weeks * 7 days
        for (let day = 1; day <= remainingCells; day++) {
            const date = new Date(year, month + 1, day);
            const isCurrentWeek = date >= currentWeekStart && date <= currentWeekEnd;
            const cell = this.createDayCell(date, false, true, isCurrentWeek);
            container.appendChild(cell);
        }
    }

    createDayCell(date, isToday, isOtherMonth, isCurrentWeek = false) {
        const cell = document.createElement('div');
        cell.className = 'day-cell';
        
        if (isToday) cell.classList.add('today');
        if (isOtherMonth) cell.classList.add('other-month');
        if (isCurrentWeek) cell.classList.add('current-week');
        
        const dayOfWeek = date.getDay();
        if (dayOfWeek === 0 || dayOfWeek === 6) {
            cell.classList.add('weekend');
        }
        
        // Day number
        const dayNumber = document.createElement('div');
        dayNumber.className = 'day-number';
        dayNumber.textContent = date.getDate();
        cell.appendChild(dayNumber);
        
        // Sessions container
        const sessionsContainer = document.createElement('div');
        sessionsContainer.className = 'day-sessions';
        
        // Get sessions for this day
        const daySessions = this.getSessionsForDate(date);
        
        if (daySessions.length > 0) {
            // For current week: show ALL sessions
            // For other weeks: show max 4 sessions
            if (isCurrentWeek) {
                // Show all sessions for current week
                for (const session of daySessions) {
                    const sessionEl = this.createSessionElement(session);
                    sessionsContainer.appendChild(sessionEl);
                }
            } else {
                // Show maximum 4 sessions for other weeks
                const maxVisible = 4;
                const sessionsToShow = daySessions.slice(0, maxVisible);
                
                for (const session of sessionsToShow) {
                    const sessionEl = this.createSessionElement(session);
                    sessionsContainer.appendChild(sessionEl);
                }
                
                // Add indicator if there are more sessions
                if (daySessions.length > maxVisible) {
                    const indicator = document.createElement('div');
                    indicator.className = 'day-more-indicator';
                    indicator.textContent = `+${daySessions.length - maxVisible} meer...`;
                    sessionsContainer.appendChild(indicator);
                }
            }
        }
        
        cell.appendChild(sessionsContainer);
        
        // Click handler to show day details
        cell.addEventListener('click', () => this.showDayDetails(date, daySessions));
        
        return cell;
    }

    createSessionElement(session) {
        const el = document.createElement('div');
        el.className = 'session-item';
        
        if (session.status === 'Completed') {
            el.classList.add('completed');
        } else if (session.status === 'Overdue') {
            el.classList.add('overdue');
        }
        
        // Set border color to piece color
        el.style.borderLeftColor = session.pieceColor || '#ADD8E6';
        
        // Show piece title and bar range in one line
        el.textContent = `${session.pieceTitle} - ${session.sectionRange}`;
        el.title = `${session.pieceTitle} - ${session.sectionRange} (${session.status})`;
        
        return el;
    }

    getSessionsForDate(date) {
        const dateStr = date.toDateString();
        
        return this.scheduledSessions.filter(session => {
            // Filter by selected piece if applicable
            if (this.selectedPieceId && session.pieceId !== this.selectedPieceId) {
                return false;
            }
            
            const sessionDateStr = session.scheduledDate.toDateString();
            return sessionDateStr === dateStr;
        }).sort((a, b) => {
            // Sort: completed last, then by piece title
            if (a.status === 'Completed' && b.status !== 'Completed') return 1;
            if (a.status !== 'Completed' && b.status === 'Completed') return -1;
            return a.pieceTitle.localeCompare(b.pieceTitle);
        });
    }

    showDayDetails(date, sessions, options = {}) {
        const forceDisplay = options.force === true;
        if (!forceDisplay && sessions.length === 0) return;
        
        const dateStr = date.toLocaleDateString(undefined, { 
            weekday: 'long', 
            year: 'numeric', 
            month: 'long', 
            day: 'numeric' 
        });
        
        // Set modal title
        document.getElementById('modalDate').textContent = `📅 ${dateStr}`;
        
        // Build modal body content
        const modalBody = document.getElementById('modalBody');
        modalBody.innerHTML = '';
        
        const count = document.createElement('p');
        count.style.marginBottom = '20px';
        count.style.fontSize = '1.1em';
        count.style.fontWeight = '600';
        count.textContent = `${sessions.length} session${sessions.length === 1 ? '' : 's'} scheduled`;
        modalBody.appendChild(count);
        
        if (sessions.length === 0) {
            const emptyMessage = document.createElement('p');
            emptyMessage.style.marginBottom = '10px';
            emptyMessage.style.fontSize = '1em';
            emptyMessage.style.color = '#7a2d17';
            emptyMessage.textContent = 'No scheduled sessions on this day.';
            modalBody.appendChild(emptyMessage);
        }

        const returnDate = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;

        for (const session of sessions) {
            const sessionDiv = document.createElement('div');
            sessionDiv.className = 'modal-session-item';
            sessionDiv.style.borderLeftColor = session.pieceColor;
            sessionDiv.style.backgroundColor = this.hexToRgba(session.pieceColor, 0.1);
            
            const header = document.createElement('div');
            header.className = 'modal-session-header';
            
            const title = document.createElement('div');
            title.className = 'modal-session-title';
            title.textContent = session.pieceTitle;
            
            const statusBadge = document.createElement('div');
            statusBadge.className = 'modal-session-status';
            
            if (session.status === 'Completed') {
                statusBadge.classList.add('status-completed');
                statusBadge.textContent = '✅ Completed';
            } else if (session.status === 'Overdue') {
                statusBadge.classList.add('status-overdue');
                statusBadge.textContent = '⚠️ Overdue';
            } else {
                statusBadge.classList.add('status-planned');
                statusBadge.textContent = '📝 ' + session.status;
            }
            
            header.appendChild(title);
            header.appendChild(statusBadge);
            
            const info = document.createElement('div');
            info.className = 'modal-session-info';
            info.innerHTML = `
                <div><strong>Bars:</strong> ${session.sectionRange}</div>
                <div><strong>Difficulty:</strong> ${session.difficulty}</div>
                <div><strong>Completed Reps:</strong> ${session.completedRepetitions}</div>
                <div><strong>Lifecycle State:</strong> ${session.lifecycleState}</div>
            `;
            
            sessionDiv.appendChild(header);
            sessionDiv.appendChild(info);
            
            // Add Practice/Review button
            const actionBtn = document.createElement('button');
            actionBtn.className = 'session-action-btn';
            
            if (session.status === 'Completed') {
                actionBtn.classList.add('btn-review');
                actionBtn.textContent = 'Review';
                actionBtn.title = 'Start an extra practice session. Due date stays unchanged.';
            } else {
                actionBtn.classList.add('btn-practice');
                actionBtn.textContent = 'Practice';
                actionBtn.title = 'Start practice session for this section';
            }
            
            actionBtn.addEventListener('click', () => {
                if (typeof openPracticeSessionWindow === 'function') {
                    openPracticeSessionWindow(session.id, { returnDate });
                } else {
                    const url = `moduspractica-practice-session.html?section=${session.id}&returnDate=${encodeURIComponent(returnDate)}`;
                    window.location.href = url;
                }
            });
            
            sessionDiv.appendChild(actionBtn);
            modalBody.appendChild(sessionDiv);
        }
        
        // Show modal
        document.getElementById('dayModal').style.display = 'flex';
    }

    updateStats() {
        const year = this.currentMonth.getFullYear();
        const month = this.currentMonth.getMonth();
        
        // Filter sessions for current month
        const monthSessions = this.scheduledSessions.filter(session => {
            // Apply piece filter if set
            if (this.selectedPieceId && session.pieceId !== this.selectedPieceId) {
                return false;
            }
            
            return session.scheduledDate.getFullYear() === year &&
                   session.scheduledDate.getMonth() === month;
        });
        
        const completed = monthSessions.filter(s => s.status === 'Completed').length;
        const planned = monthSessions.filter(s => s.status === 'Planned' || s.status === 'Due Today').length;
        const overdue = monthSessions.filter(s => s.status === 'Overdue').length;
        
        document.getElementById('statThisMonth').textContent = monthSessions.length;
        document.getElementById('statCompleted').textContent = completed;
        document.getElementById('statPlanned').textContent = planned;
        document.getElementById('statOverdue').textContent = overdue;
    }

    // ========================================================================
    // UTILITIES
    // ========================================================================

    hexToRgba(hex, alpha) {
        // Remove # if present
        hex = hex.replace('#', '');
        
        // Parse hex values
        const r = parseInt(hex.substring(0, 2), 16);
        const g = parseInt(hex.substring(2, 4), 16);
        const b = parseInt(hex.substring(4, 6), 16);
        
        return `rgba(${r}, ${g}, ${b}, ${alpha})`;
    }

    parseDayParam(dayString) {
        if (!dayString || typeof dayString !== 'string') return null;
        const parts = dayString.split('-');
        if (parts.length !== 3) return null;

        const [yearStr, monthStr, dayStr] = parts;
        const year = Number(yearStr);
        const month = Number(monthStr);
        const day = Number(dayStr);

        if (!Number.isInteger(year) || !Number.isInteger(month) || !Number.isInteger(day)) {
            return null;
        }

        if (month < 1 || month > 12 || day < 1 || day > 31) {
            return null;
        }

        const parsedDate = new Date(year, month - 1, day);
        if (parsedDate.getFullYear() !== year || parsedDate.getMonth() !== month - 1 || parsedDate.getDate() !== day) {
            return null;
        }

        return parsedDate;
    }
}

// Initialize calendar when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    window.calendarApp = new ModusPracticaCalendar();
});
