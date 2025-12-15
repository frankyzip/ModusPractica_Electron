// ============================================================================
// Intensity Module - Optional Practice Duration & Intensity Calculator
// Parallel module to Ebbinghaus SRS Core
// Copyright © 2025 Frank De Baere - All Rights Reserved
// ============================================================================

/**
 * IntensityModule - Optionele module voor oefenintensiteit
 * 
 * ARCHITECTUUR:
 * - Werkt PARALLEL aan de Ebbinghaus-kern (niet geïntegreerd)
 * - De Ebbinghaus-kern bepaalt WANNEER (nextReviewDate, τ, Streak Reset)
 * - Deze module bepaalt HOELANG en HOE INTENSIEF (duration, OLQ)
 * 
 * METRICS (Optioneel):
 * - Failed Attempts (FA): Technische moeilijkheid meten
 * - Technical Difficulty Score (TDS): Leertrajectfase bepalen via Success Ratio
 * - Overlearning Quotum (OLQ): Target aantal herhalingen instellen (Dr. Gebrian)
 * - Gemiddelde Tijd (T̄_CR): Sessieduur voorspellen
 * 
 * GEBRUIK:
 * - Module AAN (nieuwe gebruikers/studenten): Voorspelt duur + geeft OLQ instructie
 * - Module UIT (ervaren muzikanten): Vaste duur (bijv. 15min), geen OLQ instructie
 * 
 * DATA MANAGEMENT:
 * - Archiveert chunks bij CR=0 (onwerkbare data)
 * - Nieuwe chunks beginnen schoon (CR=0, τ=standaard)
 */
class IntensityModule {
    /**
     * TDS thresholds voor leertrajectfasen (gebaseerd op Success Ratio)
     * Pure Success Ratio: CR / (CR + FA)
     * 
     * Fasen:
     * 1. Initial Acquisition (0-40%): Veel fouten, steil leren
     * 2. Refinement (40-70%): Afnemende fouten, techniek stabiliseert
     * 3. Consolidation (70-85%): Weinig fouten, automatisering begint
     * 4. Mastery (85-95%): Zeer weinig fouten, hoge consistentie
     * 5. Overlearning (95-100%): Foutloos, ready voor performance
     */
    static TDS_THRESHOLDS = {
        INITIAL_ACQUISITION: 0.40,   // 0-40%: Veel fouten
        REFINEMENT: 0.70,            // 40-70%: Afnemende fouten
        CONSOLIDATION: 0.85,         // 70-85%: Weinig fouten
        MASTERY: 0.95,               // 85-95%: Zeer weinig fouten
        OVERLEARNING: 1.00           // 95-100%: Foutloos
    };

    /**
     * OLQ mapping volgens Dr. Gebrian (leertrajectfase → target herhalingen)
     * Minimum baseline = 6 correcte herhalingen, met oplopende ranges naargelang fase.
     * 
     * Fasen:
     * 1. Initial (0-40%): 6-8 CR's (begrip bevestigen met minimum 6 correcte)
     * 2. Refinement (40-70%): 7-10 CR's (techniek stabiliseren)
     * 3. Consolidation (70-85%): 8-12 CR's (automatisering opbouwen)
     * 4. Mastery (85-95%): 9-14 CR's (diep consolideren)
     * 5. Overlearning (95-100%): 10-18 CR's (performance-ready maken)
     */
    static OLQ_MAPPING = {
        INITIAL_ACQUISITION: { min: 6, max: 8 },
        REFINEMENT: { min: 7, max: 10 },
        CONSOLIDATION: { min: 8, max: 12 },
        MASTERY: { min: 9, max: 14 },
        OVERLEARNING: { min: 10, max: 18 }
    };

    /**
     * Geschatte tijd per correcte herhaling (seconden) per fase
     * Gebaseerd op empirische data - kan worden aangepast per gebruiker
     */
    static TIME_PER_CR_ESTIMATES = {
        INITIAL_ACQUISITION: 120,    // 2 min per CR (veel proberen/leren)
        REFINEMENT: 90,              // 1.5 min per CR (techniek verfijnen)
        CONSOLIDATION: 60,           // 1 min per CR (automatisering)
        MASTERY: 45,                 // 45s per CR (geautomatiseerd)
        OVERLEARNING: 30             // 30s per CR (foutloos, snel)
    };

    /**
     * Bereken Technical Difficulty Score (TDS) op basis van Success Ratio
     * 
     * @param {number} correctRepetitions - CR (aantal succesvolle herhalingen)
     * @param {number} failedAttempts - FA (aantal fouten/herhalingen)
     * @returns {number} TDS als percentage (0.0 - 1.0)
     */
    static calculateTDS(correctRepetitions, failedAttempts) {
        // Pure Success Ratio: CR / (CR + FA)
        const totalAttempts = correctRepetitions + failedAttempts;
        
        if (totalAttempts === 0) {
            return 0.0; // Geen data = TDS 0%
        }
        
        const tds = correctRepetitions / totalAttempts;
        
        // Clamp to [0.0, 1.0]
        return Math.max(0.0, Math.min(1.0, tds));
    }

    /**
     * Bepaal leertrajectfase op basis van TDS
     * 
     * @param {number} tds - Technical Difficulty Score (0.0 - 1.0)
     * @returns {string} Fase naam
     */
    static getLearningPhase(tds) {
        if (tds < this.TDS_THRESHOLDS.INITIAL_ACQUISITION) {
            return 'INITIAL_ACQUISITION';
        } else if (tds < this.TDS_THRESHOLDS.REFINEMENT) {
            return 'REFINEMENT';
        } else if (tds < this.TDS_THRESHOLDS.CONSOLIDATION) {
            return 'CONSOLIDATION';
        } else if (tds < this.TDS_THRESHOLDS.MASTERY) {
            return 'MASTERY';
        } else {
            return 'OVERLEARNING';
        }
    }

    /**
     * Bereken Overlearning Quotum (OLQ) op basis van TDS
     * Dr. Gebrian's principe: aantal herhalingen schaalt met leertrajectfase
     * 
     * @param {number} tds - Technical Difficulty Score (0.0 - 1.0)
     * @returns {Object} { min, max, recommended, phase }
     */
    static calculateOLQ(tds, metrics = {}) {
        const {
            failedAttempts = 0,
            correctRepetitions = 0
        } = metrics;
        
        const phase = this.getLearningPhase(tds);
        const range = this.OLQ_MAPPING[phase];
        const baselineGoal = range.min;
        const totalAttempts = failedAttempts + correctRepetitions;
        const failuresBeforeSuccess = metrics.initialFailedAttempts ?? metrics.failedAttemptsBeforeSuccess ?? failedAttempts;

        let failureRatio = 0;
        let overlearningReps = 0;

        if (failuresBeforeSuccess > 0) {
            failureRatio = totalAttempts > 0 ? failedAttempts / totalAttempts : 1;
            overlearningReps = Math.ceil(failuresBeforeSuccess * 0.5);
        }

        let recommended = baselineGoal + overlearningReps;

        if (totalAttempts === 0) {
            recommended = baselineGoal;
        }

        const adjustedMin = baselineGoal;
        const adjustedMax = Math.max(range.max, recommended);
        const adjustmentMultiplier = baselineGoal > 0 ? recommended / baselineGoal : 1;
        
        return {
            min: adjustedMin,
            max: adjustedMax,
            recommended: recommended,
            phase: phase,
            phaseDescription: this.getPhaseDescription(phase),
            adjustmentMultiplier,
            failureRatio,
            overlearningReps
        };
    }

    /**
     * Voorspel sessieduur op basis van OLQ en leertrajectfase
     * 
     * @param {number} olq - Overlearning Quotum (target aantal herhalingen)
     * @param {number} tds - Technical Difficulty Score (0.0 - 1.0)
     * @returns {Object} { durationSeconds, durationMinutes, phase }
     */
    static predictSessionDuration(olq, tds) {
        const phase = this.getLearningPhase(tds);
        const timePerCR = this.TIME_PER_CR_ESTIMATES[phase];
        
        // Total duration = OLQ × time per CR
        const durationSeconds = olq * timePerCR;
        const durationMinutes = Math.round(durationSeconds / 60);
        
        return {
            durationSeconds: durationSeconds,
            durationMinutes: durationMinutes,
            phase: phase,
            timePerCR: timePerCR
        };
    }

    /**
     * Bereken gemiddelde tijd per correcte herhaling uit historische data
     * 
     * @param {Array} sectionHistory - Praktijkhistorie voor deze sectie
     * @returns {number} Gemiddelde tijd per CR in seconden (0 als geen data)
     */
    static calculateAverageTimePerCR(sectionHistory) {
        if (!sectionHistory || sectionHistory.length === 0) {
            return 0;
        }
        
        // Filter sessies met geldige data
        const validSessions = sectionHistory.filter(h => 
            h.repetitions > 0 && h.duration > 0 && !h.isDeleted
        );
        
        if (validSessions.length === 0) {
            return 0;
        }
        
        // Tel totale tijd en totale CR's
        let totalTime = 0;
        let totalCRs = 0;
        
        validSessions.forEach(session => {
            totalTime += session.duration / 1000; // Convert ms to seconds
            totalCRs += session.repetitions;
        });
        
        if (totalCRs === 0) {
            return 0;
        }
        
        return totalTime / totalCRs;
    }

    /**
     * Bereken robuuste gemiddelde tijd per correcte herhaling (sec/CR)
     * - Per sessie tijd-per-CR berekenen
     * - Onrealistische waarden filteren (<5s of >600s per CR)
     * - Trimmed mean (10% onder/boven) bij >=5 datapunten
     *
     * @param {Array} sectionHistory - Praktijkhistorie voor deze sectie
     * @returns {number} Gemiddelde tijd per CR in seconden (0 als geen data)
     */
    static calculateAverageTimePerCRRobust(sectionHistory) {
        if (!sectionHistory || sectionHistory.length === 0) return 0;

        const values = [];
        for (const h of sectionHistory) {
            if (!h || h.isDeleted) continue;
            const reps = Number(h.repetitions || 0);
            const durMs = Number(h.duration || 0);
            if (reps > 0 && durMs > 0) {
                const secPerCr = (durMs / 1000) / reps;
                // Filter extreme outliers
                if (secPerCr >= 5 && secPerCr <= 600) {
                    values.push(secPerCr);
                }
            }
        }

        if (values.length === 0) return 0;

        values.sort((a, b) => a - b);
        if (values.length >= 5) {
            const trim = Math.floor(values.length * 0.10); // 10% trimming
            const trimmed = values.slice(trim, values.length - trim);
            const sum = trimmed.reduce((s, v) => s + v, 0);
            return trimmed.length > 0 ? (sum / trimmed.length) : 0;
        } else {
            const sum = values.reduce((s, v) => s + v, 0);
            return sum / values.length;
        }
    }

    /**
     * Genereer praktijkinstructie op basis van intensiteitsmodule status
     * 
     * @param {boolean} moduleEnabled - Is de module ingeschakeld?
     * @param {Object} olqData - OLQ berekening { min, max, recommended, phase }
     * @param {Object} durationData - Duur voorspelling { durationMinutes, phase }
     * @returns {Object} { instruction, duration, olq }
     */
    static generatePracticeInstruction(moduleEnabled, olqData, durationData) {
        if (!moduleEnabled) {
            // Module UIT: Vaste duur, geen OLQ instructie
            return {
                instruction: 'Practice this section at your own pace.',
                duration: 15, // Standaard 15 minuten
                olq: null,
                moduleStatus: 'disabled'
            };
        }
        
        // Module AAN: Dynamische instructie
        const phaseDesc = olqData.phaseDescription;
        const instruction = 
            `${phaseDesc}\n\n` +
            `Target: ${olqData.recommended} correct repetitions\n` +
            `Range: ${olqData.min}-${olqData.max} repetitions\n` +
            `Estimated duration: ${durationData.durationMinutes} minutes`;
        
        return {
            instruction: instruction,
            duration: durationData.durationMinutes,
            olq: olqData.recommended,
            phase: olqData.phase,
            moduleStatus: 'enabled'
        };
    }

    /**
     * Bepaal of chunk gearchiveerd moet worden (CR=0 regel)
     * 
     * @param {number} correctRepetitions - CR (aantal succesvolle herhalingen)
     * @returns {boolean} True als chunk onwerkbaar is en gearchiveerd moet worden
     */
    static shouldArchiveChunk(correctRepetitions) {
        // Archiveer als CR = 0 (planner kan niet werken met onwerkbare chunks)
        return correctRepetitions === 0;
    }

    /**
     * Krijg fase beschrijving voor UI
     * 
     * @param {string} phase - Fase naam
     * @returns {string} Menselijk leesbare beschrijving
     */
    static getPhaseDescription(phase) {
        const descriptions = {
            'INITIAL_ACQUISITION': 'Initial Learning: Focus on understanding and basic execution',
            'REFINEMENT': 'Refinement: Reducing errors and stabilizing technique',
            'CONSOLIDATION': 'Consolidation: Building automaticity and consistency',
            'MASTERY': 'Mastery: Achieving high consistency and reliability',
            'OVERLEARNING': 'Overlearning: Performance-ready, minimal errors'
        };
        
        return descriptions[phase] || 'Unknown phase';
    }

    /**
     * Genereer intensiteitsrapport voor debugging/logging
     * 
     * @param {number} correctRepetitions - CR
     * @param {number} failedAttempts - FA
     * @param {Array} sectionHistory - Historische data
     * @returns {Object} Volledig rapport
     */
    static generateIntensityReport(correctRepetitions, failedAttempts, sectionHistory = []) {
        const tds = this.calculateTDS(correctRepetitions, failedAttempts);
        const phase = this.getLearningPhase(tds);
        const olq = this.calculateOLQ(tds, { failedAttempts, correctRepetitions });
        const duration = this.predictSessionDuration(olq.recommended, tds);
        const avgTimePerCR = this.calculateAverageTimePerCR(sectionHistory);
        
        return {
            // Metrics
            metrics: {
                correctRepetitions: correctRepetitions,
                failedAttempts: failedAttempts,
                totalAttempts: correctRepetitions + failedAttempts,
                tds: tds,
                tdsPercentage: Math.round(tds * 100)
            },
            
            // Leertrajectfase
            phase: {
                name: phase,
                description: this.getPhaseDescription(phase)
            },
            
            // OLQ aanbeveling
            olq: olq,
            
            // Duur voorspelling
            duration: duration,
            
            // Historische data
            history: {
                sessionCount: sectionHistory.length,
                avgTimePerCR: avgTimePerCR,
                avgTimePerCRMinutes: avgTimePerCR > 0 ? (avgTimePerCR / 60).toFixed(1) : 0
            },
            
            // Archivering status
            shouldArchive: this.shouldArchiveChunk(correctRepetitions)
        };
    }

    /**
     * Log intensiteitsrapport naar console (voor debugging)
     * 
     * @param {Object} report - Rapport van generateIntensityReport()
     */
    static logIntensityReport(report) {
        console.log(
            `\n${'='.repeat(60)}\n` +
            `📊 INTENSITY MODULE REPORT\n` +
            `${'='.repeat(60)}\n` +
            `METRICS:\n` +
            `  CR (Correct): ${report.metrics.correctRepetitions}\n` +
            `  FA (Failed): ${report.metrics.failedAttempts}\n` +
            `  Total Attempts: ${report.metrics.totalAttempts}\n` +
            `  TDS (Success Ratio): ${report.metrics.tdsPercentage}%\n` +
            `\n` +
            `LEARNING PHASE:\n` +
            `  Phase: ${report.phase.name}\n` +
            `  Description: ${report.phase.description}\n` +
            `\n` +
            `OLQ RECOMMENDATION:\n` +
            `  Recommended: ${report.olq.recommended} repetitions\n` +
            `  Range: ${report.olq.min}-${report.olq.max} repetitions\n` +
            `  Phase: ${report.olq.phase}\n` +
            `\n` +
            `DURATION PREDICTION:\n` +
            `  Estimated: ${report.duration.durationMinutes} minutes\n` +
            `  Time per CR: ${report.duration.timePerCR}s\n` +
            `\n` +
            `HISTORICAL DATA:\n` +
            `  Sessions: ${report.history.sessionCount}\n` +
            `  Avg Time/CR: ${report.history.avgTimePerCRMinutes} min\n` +
            `\n` +
            `ARCHIVING:\n` +
            `  Should Archive: ${report.shouldArchive ? 'YES (CR=0)' : 'NO'}\n` +
            `${'='.repeat(60)}\n`
        );
    }
}

// Export voor gebruik in andere modules
if (typeof module !== 'undefined' && module.exports) {
    module.exports = IntensityModule;
}
