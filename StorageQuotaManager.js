// ============================================================================
// LocalStorage Quota Manager - Prevent Data Loss
// Copyright © 2025 Frank De Baere - All Rights Reserved
// ============================================================================

class StorageQuotaManager {
    constructor() {
        this.warningThreshold = 0.85; // Waarschuw bij 85% vol
        this.criticalThreshold = 0.95; // Kritiek bij 95% vol
    }

    /**
     * Check hoeveel localStorage ruimte beschikbaar is
     */
    getStorageInfo() {
        let totalSize = 0;
        let itemCount = 0;

        for (let key in localStorage) {
            if (localStorage.hasOwnProperty(key)) {
                totalSize += localStorage[key].length + key.length;
                itemCount++;
            }
        }

        // Meeste browsers hebben 5-10MB limiet
        // We nemen 5MB (5242880 bytes) als veilige schatting
        const estimatedLimit = 5242880; // 5MB in bytes
        const usedPercentage = totalSize / estimatedLimit;

        return {
            used: totalSize,
            estimatedLimit: estimatedLimit,
            usedPercentage: usedPercentage,
            itemCount: itemCount,
            available: estimatedLimit - totalSize,
            isWarning: usedPercentage >= this.warningThreshold,
            isCritical: usedPercentage >= this.criticalThreshold
        };
    }

    /**
     * Probeer data op te slaan met quota check
     */
    safeSetItem(key, value) {
        const info = this.getStorageInfo();
        const dataSize = (key.length + value.length) * 2; // UTF-16 = 2 bytes per char

        // Check of nieuwe data erbij past
        if (info.available < dataSize) {
            throw new QuotaExceededError(
                'LocalStorage quota exceeded. Kan niet opslaan.',
                info
            );
        }

        // Waarschuwing bij 85% vol
        if (info.isWarning && !info.isCritical) {
            console.warn(
                `⚠️ LocalStorage bijna vol (${(info.usedPercentage * 100).toFixed(1)}%). ` +
                `Overweeg oude data op te ruimen.`
            );
        }

        // Kritiek bij 95% vol - blokkeer save
        if (info.isCritical) {
            throw new QuotaExceededError(
                'LocalStorage kritiek vol! Data kan niet opgeslagen worden. ' +
                'Exporteer je data en ruim oude profielen op.',
                info
            );
        }

        try {
            localStorage.setItem(key, value);
            return true;
        } catch (e) {
            if (e.name === 'QuotaExceededError' || e.code === 22) {
                throw new QuotaExceededError(
                    'Browser storage quota exceeded tijdens opslaan',
                    info,
                    e
                );
            }
            throw e;
        }
    }

    /**
     * Ruim oude draft sessies op (ouder dan 7 dagen)
     */
    cleanupOldDrafts() {
        const sevenDaysAgo = Date.now() - (7 * 24 * 60 * 60 * 1000);
        let cleaned = 0;

        for (let key in localStorage) {
            if (key.startsWith('mp_') && key.includes('_draft_')) {
                try {
                    const draft = JSON.parse(localStorage.getItem(key));
                    const draftDate = new Date(draft.startTime || 0).getTime();
                    
                    if (draftDate < sevenDaysAgo) {
                        localStorage.removeItem(key);
                        cleaned++;
                    }
                } catch (e) {
                    // Ongeldige draft - verwijder
                    localStorage.removeItem(key);
                    cleaned++;
                }
            }
        }

        return cleaned;
    }

    /**
     * Ruim oude offline queue items op
     */
    cleanupOfflineQueue() {
        const queueKey = 'mp_offline_queue';
        const maxAge = 30 * 24 * 60 * 60 * 1000; // 30 dagen
        
        try {
            const queue = JSON.parse(localStorage.getItem(queueKey) || '[]');
            const now = Date.now();
            
            const filtered = queue.filter(item => {
                const itemAge = now - item.timestamp;
                return itemAge < maxAge;
            });
            
            if (filtered.length < queue.length) {
                localStorage.setItem(queueKey, JSON.stringify(filtered));
                return queue.length - filtered.length;
            }
        } catch (e) {
            console.error('Error cleaning offline queue:', e);
        }
        
        return 0;
    }

    /**
     * Toon gebruiker quota status
     */
    showQuotaStatus() {
        const info = this.getStorageInfo();
        const usedMB = (info.used / 1024 / 1024).toFixed(2);
        const limitMB = (info.estimatedLimit / 1024 / 1024).toFixed(2);
        const percentage = (info.usedPercentage * 100).toFixed(1);

        console.log(`📊 LocalStorage Status:`);
        console.log(`   Gebruikt: ${usedMB} MB / ${limitMB} MB (${percentage}%)`);
        console.log(`   Items: ${info.itemCount}`);
        console.log(`   Beschikbaar: ${(info.available / 1024 / 1024).toFixed(2)} MB`);

        if (info.isCritical) {
            console.error('🔴 KRITIEK: LocalStorage bijna vol!');
        } else if (info.isWarning) {
            console.warn('🟡 WAARSCHUWING: LocalStorage voor 85%+ gevuld');
        } else {
            console.log('✅ LocalStorage heeft voldoende ruimte');
        }

        return info;
    }

    /**
     * Emergency cleanup - verwijder oude data
     * 
     * BELANGRIJK: Statistics (totalSessions, totalPracticeTime) worden NOOIT aangeraakt!
     * Deze zijn cumulatieve totalen van ALLE sessies ooit, niet alleen recente.
     */
    emergencyCleanup() {
        console.warn('🧹 Starting emergency cleanup...');
        let totalCleaned = 0;

        // 1. Ruim drafts op
        const draftsRemoved = this.cleanupOldDrafts();
        totalCleaned += draftsRemoved;

        // 2. Ruim offline queue op
        const queueRemoved = this.cleanupOfflineQueue();
        totalCleaned += queueRemoved;

        // 3. Ruim oude session history op (>90 dagen) - STATISTICS BLIJVEN BEHOUDEN!
        const ninetyDaysAgo = Date.now() - (90 * 24 * 60 * 60 * 1000);
        let sessionsRemoved = 0;
        
        for (let key in localStorage) {
            if (key.startsWith('mp_') && key.includes('_data')) {
                try {
                    const data = JSON.parse(localStorage.getItem(key));
                    if (!data) continue;
                    
                    // **KRITIEK: Statistics NOOIT aanpassen - dit zijn cumulatieve totalen**
                    // data.statistics.totalSessions bevat ALLE sessies ooit (bijv. 200)
                    // data.statistics.totalPracticeTime bevat ALLE uren ooit (bijv. 300)
                    // Deze waardes MOETEN behouden blijven, ook als oude history wordt verwijderd!
                    
                    let profileSessionsRemoved = 0;
                    
                    // Verwijder oude sessions uit musicPieces practiceHistory
                    if (data.musicPieces && Array.isArray(data.musicPieces)) {
                        data.musicPieces.forEach(piece => {
                            if (piece.barSections && Array.isArray(piece.barSections)) {
                                piece.barSections.forEach(section => {
                                    if (section.practiceHistory && Array.isArray(section.practiceHistory)) {
                                        const originalLength = section.practiceHistory.length;
                                        section.practiceHistory = section.practiceHistory.filter(session => {
                                            const sessionDate = new Date(session.date).getTime();
                                            return sessionDate > ninetyDaysAgo;
                                        });
                                        profileSessionsRemoved += (originalLength - section.practiceHistory.length);
                                    }
                                });
                            }
                        });
                    }
                    
                    // Verwijder oude practiceHistory (top-level free practice sessions)
                    if (data.practiceHistory && Array.isArray(data.practiceHistory)) {
                        const originalLength = data.practiceHistory.length;
                        data.practiceHistory = data.practiceHistory.filter(session => {
                            const sessionDate = new Date(session.date).getTime();
                            return sessionDate > ninetyDaysAgo;
                        });
                        profileSessionsRemoved += (originalLength - data.practiceHistory.length);
                    }
                    
                    if (profileSessionsRemoved > 0) {
                        localStorage.setItem(key, JSON.stringify(data));
                        sessionsRemoved += profileSessionsRemoved;
                        console.log(`  ✓ Verwijderd ${profileSessionsRemoved} oude sessions uit ${key}`);
                        console.log(`    → Statistics behouden: ${data.statistics?.totalSessions || 0} totale sessies, ${data.statistics?.totalPracticeTime || 0} totale uren`);
                    }
                    
                } catch (e) {
                    console.error(`  ✗ Fout bij cleanup ${key}:`, e);
                }
            }
        }
        
        totalCleaned += sessionsRemoved;

        console.log(`✅ Emergency cleanup voltooid: ${totalCleaned} items verwijderd`);
        console.log(`   • ${draftsRemoved} drafts`);
        console.log(`   • ${queueRemoved} offline queue items`);
        console.log(`   • ${sessionsRemoved} oude sessions (>90 dagen)`);
        console.log(`   ⚠️  BELANGRIJK: Alle statistics (totale uren/sessies) blijven behouden!`);
        
        return totalCleaned;
    }
}

/**
 * Custom error voor quota issues
 */
class QuotaExceededError extends Error {
    constructor(message, storageInfo, originalError = null) {
        super(message);
        this.name = 'QuotaExceededError';
        this.storageInfo = storageInfo;
        this.originalError = originalError;
    }
}

// Export singleton instance
const storageQuotaManager = new StorageQuotaManager();

// Export voor gebruik in andere modules
if (typeof module !== 'undefined' && module.exports) {
    module.exports = { StorageQuotaManager, storageQuotaManager, QuotaExceededError };
}
