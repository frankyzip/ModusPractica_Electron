// UI for the logging page `moduspractica-logging.html`
(function(){
    'use strict';

    function renderLogs() {
        const logEl = document.getElementById('log');
        const logs = (window.MPLog && typeof window.MPLog.getAll === 'function') ? window.MPLog.getAll() : [];
        if (!logs.length) {
            logEl.textContent = 'No logs yet';
            return;
        }
        const autoScroll = document.getElementById('auto-scroll').checked;
        const txt = logs.map(l => `${l.t} [${l.level}] ${l.message} ${l.meta ? JSON.stringify(l.meta) : ''}`).join('\n');
        logEl.textContent = txt;
        if (autoScroll) {
            logEl.scrollTop = logEl.scrollHeight;
        }
    }

    function init() {
        if (!window.MPLog) {
            console.warn('MPLog not found — are you loading logging.js?');
        }

        const autoScrollCheckbox = document.getElementById('auto-scroll');
        const logEl = document.getElementById('log');

        document.getElementById('btn-refresh').addEventListener('click', renderLogs);
        document.getElementById('btn-clear').addEventListener('click', () => {
            if (confirm('Clear all logs?')) {
                window.MPLog && window.MPLog.clear();
                renderLogs();
            }
        });
        document.getElementById('btn-download').addEventListener('click', () => {
            if (window.MPLog) window.MPLog.download();
        });

        // Disable auto-scroll when the user intentionally scrolls away from the bottom.
        logEl.addEventListener('scroll', () => {
            // Small tolerance avoids accidental disables when new lines arrive.
            const nearBottom = logEl.scrollHeight - logEl.scrollTop - logEl.clientHeight <= 16;
            if (!nearBottom && autoScrollCheckbox.checked) {
                autoScrollCheckbox.checked = false;
            }
        });

        // Autorefresh every 2 seconds
        setInterval(renderLogs, 2000);

        // Initial render
        renderLogs();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();