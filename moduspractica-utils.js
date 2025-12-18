/**
 * ModusPractica - Shared Utility Functions
 * Copyright © 2025 Frank De Baere - Partura Music™
 * Licensed under AGPL-3.0-or-later (see LICENSE-AGPL)
 * Commercial license available (see LICENSE-COMMERCIAL)
 */

/**
 * Lifecycle state enum for bar sections (chunks)
 * Determines scheduling behavior and visibility
 */
const LifecycleState = {
    Active: 0,       // Normal active practice with standard scheduling
    Maintenance: 1,  // Long-term maintenance with minimum 7-day intervals
    Inactive: 2      // Archive sections are never scheduled
};

/**
 * Get lifecycle state name from value
 * @param {number} value - Lifecycle state value (0, 1, 2)
 * @returns {string} State name (Active, Maintenance, Archive)
 */
function getLifecycleStateName(value) {
    switch (value) {
        case LifecycleState.Active: return 'Active';
        case LifecycleState.Maintenance: return 'Maintenance';
        case LifecycleState.Inactive: return 'Archive';
        default: return 'Active';
    }
}

/**
 * Get lifecycle state value from name
 * @param {string} name - State name (Active, Maintenance, Archive)
 * @returns {number} State value (0, 1, 2)
 */
function getLifecycleStateValue(name) {
    switch (name) {
        case 'Active': return LifecycleState.Active;
        case 'Maintenance': return LifecycleState.Maintenance;
        case 'Archive': return LifecycleState.Inactive;
        default: return LifecycleState.Active;
    }
}

/**
 * Generate a UUID v4 compliant GUID
 * @returns {string} UUID in format: xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx
 */
function generateGUID() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
        const r = Math.random() * 16 | 0;
        const v = c === 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
}

/**
 * Generate a unique profile ID with timestamp
 * @returns {string} Profile ID in format: profile_<timestamp>_<random>
 */
function generateProfileId() {
    return 'profile_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
}

/**
 * Read memory failures (streak resets) from a session entry.
 * Backward-compatible: if new field `memoryFailures` exists use it else fall back to `totalFailures`.
 * @param {Object} session - Session object from practiceHistory
 * @returns {number} Number of memory failures
 */
function getMemoryFailures(session) {
    if (!session) return 0;
    if (typeof session.memoryFailures === 'number') return session.memoryFailures || 0;
    return session.totalFailures || 0;
}

/**
 * Read execution failures (attempts before success) from a session entry.
 * Backward-compatible: if new field `executionFailures` exists use it else fall back to `attemptsTillSuccess`.
 * @param {Object} session - Session object from practiceHistory
 * @returns {number} Number of execution failures
 */
function getExecutionFailures(session) {
    if (!session) return 0;
    if (typeof session.executionFailures === 'number') return session.executionFailures || 0;
    return session.attemptsTillSuccess || 0;
}

/**
 * Combined failures (memory + execution) for success rate calculations and UI.
 * @param {Object} session - Session object from practiceHistory
 * @returns {number}
 */
function getCombinedFailures(session) {
    return getMemoryFailures(session) + getExecutionFailures(session);
}

/**
 * Ensure practiceHistory entries use normalized failure fields.
 * Adds `memoryFailures` and `executionFailures` fields if missing
 * and keeps legacy fields (`totalFailures`, `attemptsTillSuccess`) for compatibility.
 * @param {Object} profileData - The profile data object saved in localStorage.
 */
function normalizeProfilePracticeHistory(profileData) {
    if (!profileData || !Array.isArray(profileData.practiceHistory)) return profileData;

    profileData.practiceHistory.forEach(session => {
        // If only legacy names exist, set new fields
        if (session.memoryFailures === undefined) {
            session.memoryFailures = session.totalFailures || 0;
        }
        if (session.executionFailures === undefined) {
            session.executionFailures = session.executionFailures || session.attemptsTillSuccess || 0;
        }

        // Maintain legacy fields for backwards compatibility unless explicitly overridden
        if (session.totalFailures === undefined) {
            session.totalFailures = session.memoryFailures || 0;
        }
        if (session.attemptsTillSuccess === undefined) {
            session.attemptsTillSuccess = session.executionFailures || 0;
        }
    });

    return profileData;
}

// ============================================================================
// TIMEZONE-SAFE DATE UTILITIES
// Ensures consistent date handling across all timezones and devices
// ============================================================================

/**
 * Get today's date in the user's local timezone (date-only, no time)
 * Always returns midnight in local timezone for consistent comparisons
 * @returns {Date} Today at 00:00:00 local time
 */
function getTodayLocal() {
    const now = new Date();
    return new Date(now.getFullYear(), now.getMonth(), now.getDate());
}

/**
 * Convert any date to date-only (midnight) in local timezone
 * Strips time component for consistent date comparisons
 * @param {Date|string} date - Date to convert
 * @returns {Date} Date at 00:00:00 local time
 */
function toDateOnly(date) {
    const d = date instanceof Date ? date : new Date(date);
    return new Date(d.getFullYear(), d.getMonth(), d.getDate());
}

/**
 * Check if two dates are the same calendar day (timezone-aware)
 * Compares only the date portion, ignoring time and timezone differences
 * @param {Date|string} date1 - First date
 * @param {Date|string} date2 - Second date
 * @returns {boolean} True if same calendar day
 */
function isSameDay(date1, date2) {
    const d1 = toDateOnly(date1);
    const d2 = toDateOnly(date2);
    return d1.getFullYear() === d2.getFullYear() &&
           d1.getMonth() === d2.getMonth() &&
           d1.getDate() === d2.getDate();
}

/**
 * Calculate days between two dates (timezone-safe)
 * Always uses date-only comparison, ignoring time components
 * @param {Date|string} date1 - Start date
 * @param {Date|string} date2 - End date
 * @returns {number} Number of days difference (can be negative)
 */
function daysBetween(date1, date2) {
    const d1 = toDateOnly(date1);
    const d2 = toDateOnly(date2);
    const diffMs = d2.getTime() - d1.getTime();
    return Math.floor(diffMs / (1000 * 60 * 60 * 24));
}

/**
 * Add days to a date (timezone-safe)
 * @param {Date|string} date - Starting date
 * @param {number} days - Number of days to add (can be negative)
 * @returns {Date} New date with days added
 */
function addDays(date, days) {
    const d = toDateOnly(date);
    const result = new Date(d);
    result.setDate(result.getDate() + days);
    return result;
}

/**
 * Format date as YYYY-MM-DD (timezone-safe)
 * Uses local timezone, not UTC
 * @param {Date|string} date - Date to format
 * @returns {string} Date in YYYY-MM-DD format
 */
function formatDateYMD(date) {
    const d = date instanceof Date ? date : new Date(date);
    const year = d.getFullYear();
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
}

/**
 * Parse YYYY-MM-DD string to Date (timezone-safe)
 * Returns midnight in local timezone
 * @param {string} dateString - Date string in YYYY-MM-DD format
 * @returns {Date|null} Parsed date or null if invalid
 */
function parseDateYMD(dateString) {
    if (!dateString || typeof dateString !== 'string') return null;
    const parts = dateString.split('-');
    if (parts.length !== 3) return null;
    
    const year = parseInt(parts[0], 10);
    const month = parseInt(parts[1], 10) - 1; // Month is 0-indexed
    const day = parseInt(parts[2], 10);
    
    if (isNaN(year) || isNaN(month) || isNaN(day)) return null;
    if (month < 0 || month > 11 || day < 1 || day > 31) return null;
    
    return new Date(year, month, day);
}

/**
 * Check if a date is in the past (timezone-aware)
 * @param {Date|string} date - Date to check
 * @returns {boolean} True if date is before today
 */
function isPastDate(date) {
    return daysBetween(date, getTodayLocal()) > 0;
}

/**
 * Check if a date is in the future (timezone-aware)
 * @param {Date|string} date - Date to check
 * @returns {boolean} True if date is after today
 */
function isFutureDate(date) {
    return daysBetween(date, getTodayLocal()) < 0;
}

/**
 * Check if a date is today (timezone-aware)
 * @param {Date|string} date - Date to check
 * @returns {boolean} True if date is today
 */
function isToday(date) {
    return isSameDay(date, getTodayLocal());
}

/**
 * Convert legacy date storage to timezone-safe format
 * Ensures all stored dates use consistent format
 * @param {Date|string|number} date - Date in any format
 * @returns {string} ISO string of date at midnight local time
 */
function normalizeDateForStorage(date) {
    if (!date) return getTodayLocal().toISOString();
    
    let d;
    if (typeof date === 'number') {
        d = new Date(date);
    } else if (typeof date === 'string') {
        d = new Date(date);
    } else {
        d = date;
    }
    
    // Convert to date-only in local timezone, then to ISO
    const dateOnly = toDateOnly(d);
    return dateOnly.toISOString();
}

/**
 * Open the practice session page inside a centered popup window.
 * Falls back to a normal navigation when the browser blocks popups.
 * @param {string} sectionId - Target bar section identifier
 * @param {Object} [options]
 * @param {string} [options.returnDate] - Optional YYYY-MM-DD hint for calendar focus
 * @param {number} [options.width] - Desired popup width in pixels
 * @param {number} [options.height] - Desired popup height in pixels
 * @param {string} [options.windowName] - Explicit name for reuse
 * @returns {Window|null} Reference to the opened window when available
 */
function openPracticeSessionWindow(sectionId, options = {}) {
    if (!sectionId) {
        console.warn('openPracticeSessionWindow requires a sectionId');
        return null;
    }

    if (typeof window === 'undefined') {
        console.warn('openPracticeSessionWindow unavailable (no window context)');
        return null;
    }

    const params = new URLSearchParams({ section: sectionId });
    if (options.returnDate) {
        params.set('returnDate', options.returnDate);
    }

    const url = `moduspractica-practice-session.html?${params.toString()}`;
    const width = options.width || 750;
    const height = options.height || 840;
    const left = Math.max(0, Math.round((window.screen.width - width) / 2));
    const top = Math.max(0, Math.round((window.screen.height - height) / 2));
    const features = [
        `width=${width}`,
        `height=${height}`,
        `left=${left}`,
        `top=${top}`,
        'resizable=yes',
        'scrollbars=yes'
    ].join(',');
    const windowName = options.windowName || `PracticeSession_${sectionId}`;

    const popup = window.open(url, windowName, features);
    if (!popup) {
        console.warn('Popup blocked, falling back to inline navigation:', url);
        window.location.href = url;
        return null;
    }

    popup.focus();
    return popup;
}

// Export for use in other modules
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { 
        generateGUID, 
        generateProfileId, 
        LifecycleState, 
        getLifecycleStateName, 
        getLifecycleStateValue,
        getMemoryFailures, 
        getExecutionFailures, 
        getCombinedFailures, 
        normalizeProfilePracticeHistory,
        openPracticeSessionWindow,
        // Timezone-safe date utilities
        getTodayLocal,
        toDateOnly,
        isSameDay,
        daysBetween,
        addDays,
        formatDateYMD,
        parseDateYMD,
        isPastDate,
        isFutureDate,
        isToday,
        normalizeDateForStorage
    };
}
