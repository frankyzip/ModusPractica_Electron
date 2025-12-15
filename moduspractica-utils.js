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
        getLifecycleStateValue 
        , getMemoryFailures, getExecutionFailures, getCombinedFailures, normalizeProfilePracticeHistory
        , openPracticeSessionWindow
    };
}
