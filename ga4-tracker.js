// ============================================================================
// ModusPractica - Google Analytics 4 Event Tracking Utilities
// Copyright © 2025 Frank De Baere - Partura Music™
// Licensed under AGPL-3.0-or-later (see LICENSE-AGPL)
// Commercial license available (see LICENSE-COMMERCIAL)
// ============================================================================

/**
 * GA4 Event Tracker voor ModusPractica
 * Centrale helper functies voor het tracken van user events
 */

class GA4Tracker {
    constructor() {
        // Check of we in developer mode zijn (localhost)
        this.isDeveloper = window.location.hostname === 'localhost' || 
                          window.location.hostname === '127.0.0.1' ||
                          window.location.hostname === '';
        
        // Check of gtag beschikbaar is
        this.isEnabled = typeof gtag !== 'undefined' && !this.isDeveloper;
        
        if (this.isDeveloper) {
            console.log('[GA4] Tracking uitgeschakeld - Developer mode gedetecteerd');
        } else if (!this.isEnabled) {
            console.warn('[GA4] gtag niet geladen - events worden niet getracked');
        }
    }

    /**
     * Track een custom event
     * @param {string} eventName - Naam van het event
     * @param {object} parameters - Extra parameters (optioneel)
     */
    trackEvent(eventName, parameters = {}) {
        if (!this.isEnabled) {
            console.log(`[GA4] Event zou getracked worden: ${eventName}`, parameters);
            return;
        }

        try {
            gtag('event', eventName, {
                app_name: 'ModusPractica',
                ...parameters
            });
            console.log(`[GA4] Event tracked: ${eventName}`, parameters);
        } catch (error) {
            console.error('[GA4] Error tracking event:', error);
        }
    }

    // ========================================================================
    // PROFILE EVENTS
    // ========================================================================

    trackProfileCreated(profileName, ageGroup) {
        this.trackEvent('profile_created', {
            profile_name: profileName,
            age_group: ageGroup
        });
    }

    trackProfileSelected(profileName) {
        this.trackEvent('profile_selected', {
            profile_name: profileName
        });
    }

    trackProfileSaved(profileName, method) {
        this.trackEvent('profile_saved', {
            profile_name: profileName,
            save_method: method // 'manual' of 'auto'
        });
    }

    // ========================================================================
    // PIECE MANAGEMENT EVENTS
    // ========================================================================

    trackPieceAdded(pieceTitle, composer, sectionsCount) {
        this.trackEvent('piece_added', {
            piece_title: pieceTitle,
            composer: composer,
            sections_count: sectionsCount
        });
    }

    trackPieceDeleted(pieceTitle) {
        this.trackEvent('piece_deleted', {
            piece_title: pieceTitle
        });
    }

    trackSectionAdded(pieceTitle, barRange) {
        this.trackEvent('section_added', {
            piece_title: pieceTitle,
            bar_range: barRange
        });
    }

    // ========================================================================
    // PRACTICE SESSION EVENTS
    // ========================================================================

    trackPracticeSessionStarted(pieceTitle, sectionRange) {
        this.trackEvent('practice_session_started', {
            piece_title: pieceTitle,
            section_range: sectionRange
        });
    }

    trackPracticeSessionCompleted(pieceTitle, sectionRange, durationSeconds, performance, successRate) {
        this.trackEvent('practice_session_completed', {
            piece_title: pieceTitle,
            section_range: sectionRange,
            duration_seconds: durationSeconds,
            performance_rating: performance,
            success_rate: successRate
        });
    }

    trackPracticeSessionCancelled(pieceTitle, sectionRange, elapsedSeconds) {
        this.trackEvent('practice_session_cancelled', {
            piece_title: pieceTitle,
            section_range: sectionRange,
            elapsed_seconds: elapsedSeconds
        });
    }

    // ========================================================================
    // EBBINGHAUS ALGORITHM EVENTS
    // ========================================================================

    trackNextReviewCalculated(sectionId, intervalDays, retentionTarget) {
        this.trackEvent('next_review_calculated', {
            section_id: sectionId,
            interval_days: Math.round(intervalDays),
            retention_target: retentionTarget
        });
    }

    trackMemoryStabilityUpdated(sectionId, stabilityScore, stage) {
        this.trackEvent('memory_stability_updated', {
            section_id: sectionId,
            stability_score: Math.round(stabilityScore * 100) / 100,
            stage: stage
        });
    }

    // ========================================================================
    // NAVIGATION EVENTS
    // ========================================================================

    trackPageView(pageName) {
        this.trackEvent('page_view', {
            page_name: pageName,
            page_location: window.location.href,
            page_path: window.location.pathname
        });
    }

    trackNavigationAction(fromPage, toPage, action) {
        this.trackEvent('navigation', {
            from_page: fromPage,
            to_page: toPage,
            action: action
        });
    }

    // ========================================================================
    // DATA MANAGEMENT EVENTS
    // ========================================================================

    trackDataExported(exportType, profileCount) {
        this.trackEvent('data_exported', {
            export_type: exportType, // 'single-profile' of 'full-backup'
            profile_count: profileCount
        });
    }

    trackDataImported(importType, profileCount) {
        this.trackEvent('data_imported', {
            import_type: importType,
            profile_count: profileCount
        });
    }

    // ========================================================================
    // ERROR TRACKING
    // ========================================================================

    trackError(errorType, errorMessage, context) {
        this.trackEvent('error_occurred', {
            error_type: errorType,
            error_message: errorMessage,
            context: context
        });
    }
}

// Maak een globale instance beschikbaar
window.ga4Tracker = new GA4Tracker();

// Log initialisatie
console.log('[GA4] Tracker geïnitialiseerd');
