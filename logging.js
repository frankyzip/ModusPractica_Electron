// ============================================================================
// ModusPractica - Logging Utility
// Copyright © 2025 Frank De Baere - Partura Music™
// Licensed under AGPL-3.0-or-later (see LICENSE-AGPL)
// Commercial license available (see LICENSE-COMMERCIAL)
// ============================================================================

/* ModusPractica Logging Utility
   Exposes a global `MPLog` object for logging to localStorage and console.
   Keeps bounded history to avoid storing too much data.
*/
(function(global){
    'use strict';

    const STORAGE_KEY = 'mp_logs';
    const MAX_ENTRIES = 5000; // Keep it bounded

    function readLogs(){
        try{
            const raw = localStorage.getItem(STORAGE_KEY) || '[]';
            return JSON.parse(raw) || [];
        }catch(e){
            console.warn('MPLog: failed to read logs', e);
            return [];
        }
    }

    function writeLogs(arr){
        try{
            localStorage.setItem(STORAGE_KEY, JSON.stringify(arr));
        }catch(e){
            // Avoid failing silently in production
            console.warn('MPLog: failed to write logs', e);
        }
    }

    function formatMessage(level, message, meta){
        // Use local time instead of UTC for user-friendly timestamps
        const now = new Date();
        const year = now.getFullYear();
        const month = String(now.getMonth() + 1).padStart(2, '0');
        const day = String(now.getDate()).padStart(2, '0');
        const hours = String(now.getHours()).padStart(2, '0');
        const minutes = String(now.getMinutes()).padStart(2, '0');
        const seconds = String(now.getSeconds()).padStart(2, '0');
        const ms = String(now.getMilliseconds()).padStart(3, '0');
        const tzOffset = -now.getTimezoneOffset();
        const tzHours = String(Math.floor(Math.abs(tzOffset) / 60)).padStart(2, '0');
        const tzMinutes = String(Math.abs(tzOffset) % 60).padStart(2, '0');
        const tzSign = tzOffset >= 0 ? '+' : '-';
        const localTimestamp = `${year}-${month}-${day}T${hours}:${minutes}:${seconds}.${ms}${tzSign}${tzHours}:${tzMinutes}`;
        
        return {
            t: localTimestamp,
            level: level.toUpperCase(),
            message: String(message),
            meta: meta || null
        };
    }

    function trim(arr){
        if(arr.length <= MAX_ENTRIES) return arr;
        // keep newest MAX_ENTRIES
        return arr.slice(arr.length - MAX_ENTRIES);
    }

    const MPLog = {
        debug: function(message, meta){ this.log('DEBUG', message, meta); },
        info: function(message, meta){ this.log('INFO', message, meta); },
        warn: function(message, meta){ this.log('WARN', message, meta); },
        error: function(message, meta){ this.log('ERROR', message, meta); },
        log: function(level, message, meta){
            const logs = readLogs();
            logs.push(formatMessage(level, message, meta));
            const trimmed = trim(logs);
            writeLogs(trimmed);
            // Also keep browser console useful
            if(level === 'ERROR'){
                console.error('[MPLog]', message, meta || '');
            } else if(level === 'WARN'){
                console.warn('[MPLog]', message, meta || '');
            } else {
                console.log('[MPLog]', message, meta || '');
            }
        },
        getAll: function(){ return readLogs(); },
        clear: function(){ writeLogs([]); },
        download: function(filename = `mp-logs-${new Date().toISOString().slice(0,10)}.txt`){
            const logs = readLogs();
            const text = logs.map(l => `${l.t} [${l.level}] ${l.message} ${l.meta ? JSON.stringify(l.meta): ''}`).join('\n');
            const blob = new Blob([text], { type: 'text/plain' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = filename;
            document.body.appendChild(a);
            a.click();
            a.remove();
            URL.revokeObjectURL(url);
        }
    };

    // Attach to global
    global.MPLog = MPLog;

})(window);
