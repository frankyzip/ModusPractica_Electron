// ============================================================================
// ModusPractica Statistics - Practice Overview & Analytics
// Copyright © 2025 Frank De Baere - All Rights Reserved
// ============================================================================

class PracticeStatistics {
    constructor() {
        this.storagePrefix = 'mp_';
        this.currentProfile = null;
        this.profileData = null;
        this.practiceHistory = [];
        this.currentPeriod = 'currentWeek';
        this.activeTab = 0;
        
        // Chart data
        this.practiceTimePerDay = [];
        this.timePerCategory = [];
        this.topMusicPiecesWeek = [];
        this.topMusicPiecesMonth = [];
        
        // Pastel colors for charts (matching C# app)
        this.pastelColors = [
            '#FFB3BA', '#FFDFBA', '#FFFFBA', '#BAFFC9', '#BAE1FF',
            '#BABAFF', '#FFBAFF', '#E5CCFF', '#CCFFE5', '#FFCCCC'
        ];
    }

    // ========================================================================
    // INITIALIZATION
    // ========================================================================

    init() {
        console.log('📊 Initializing Practice Statistics...');
        
        // Load current profile
        this.loadCurrentProfile();
        
        // Setup event listeners
        this.setupEventListeners();
        
        // Load and display statistics
        this.refreshStatistics();
        
        console.log('✅ Statistics initialized');
    }

    loadCurrentProfile() {
        const lastProfileId = localStorage.getItem(this.storagePrefix + 'lastProfile');
        if (!lastProfileId) {
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
            window.location.href = 'moduspractica-app.html';
            return;
        }

        // Update profile name in sidebar
        const profileNameEl = document.getElementById('profile-name');
        if (profileNameEl) {
            profileNameEl.textContent = this.currentProfile.name;
        }

        // Load profile data
        const dataJson = localStorage.getItem(this.storagePrefix + lastProfileId + '_data');
        if (dataJson) {
            this.profileData = JSON.parse(dataJson);
            this.practiceHistory = this.profileData.practiceHistory || [];
        } else {
            this.practiceHistory = [];
        }
    }

    setupEventListeners() {
        // Period filter
        const periodFilter = document.getElementById('periodFilter');
        if (periodFilter) {
            periodFilter.value = this.currentPeriod;
            periodFilter.addEventListener('change', (e) => {
                this.currentPeriod = e.target.value;
                this.refreshStatistics();
            });
        }

        // Logout button (returns to profile selection)
        const logoutBtn = document.getElementById('logout-btn');
        if (logoutBtn) {
            logoutBtn.addEventListener('click', () => {
                window.location.href = 'moduspractica-app.html';
            });
        }
    }

    // ========================================================================
    // STATISTICS CALCULATION
    // ========================================================================

    refreshStatistics() {
        console.log('🔄 Refreshing statistics...');
        
        // Update summary cards
        this.updateSummaryCards();
        
        // Collect data for current period
        this.collectPracticeTimes();
        
        // Collect music piece data
        this.collectPracticeTimesPerMusicPiece();
        
        // Draw active chart
        this.drawActiveChart();
    }

    updateSummaryCards() {
        const today = this.getLocalToday();
        const weekStart = this.getWeekStart(today);
        const monthStart = new Date(today.getFullYear(), today.getMonth(), 1);
        const yearStart = new Date(today.getFullYear(), 0, 1);

        // Calculate totals
        let todayMin = 0, weekMin = 0, monthMin = 0, yearMin = 0, totalMin = 0;

        this.practiceHistory.forEach(session => {
            // Parse date correctly (handles ISO strings)
            const sessionDate = this.toLocalDate(session.date);
            const durationMin = session.durationMinutes || 0;

            totalMin += durationMin;

            // Use isSameDay for today comparison (no time component)
            if (this.isSameDay(sessionDate, today)) {
                todayMin += durationMin;
            }

            // Week comparison: ensure dates are normalized
            if (sessionDate >= weekStart && sessionDate <= today) {
                weekMin += durationMin;
            }

            // Month comparison
            if (sessionDate.getFullYear() === today.getFullYear() && 
                sessionDate.getMonth() === today.getMonth()) {
                monthMin += durationMin;
            }

            // Year comparison
            if (sessionDate.getFullYear() === today.getFullYear()) {
                yearMin += durationMin;
            }
        });

        // Update UI
        document.getElementById('todayTime').textContent = this.formatTime(todayMin);
        document.getElementById('weekTime').textContent = this.formatTime(weekMin);
        document.getElementById('monthTime').textContent = this.formatTime(monthMin);
        document.getElementById('yearTime').textContent = this.formatTime(yearMin);
        document.getElementById('totalTime').textContent = this.formatTime(totalMin);
    }

    collectPracticeTimes() {
        this.practiceTimePerDay = [];
        this.timePerCategory = [];

        const today = this.getLocalToday();
        
        if (this.practiceHistory.length === 0) return;

        switch (this.currentPeriod) {
            case 'currentWeek':
            case 'previousWeek':
                this.collectWeekData(today, this.currentPeriod === 'previousWeek');
                break;
            case 'currentMonth':
            case 'previousMonth':
                this.collectMonthData(today, this.currentPeriod === 'previousMonth');
                break;
            case 'currentYear':
                this.collectYearData(today);
                break;
            case 'allYears':
                this.collectAllYearsData();
                break;
            case 'averagePerDay':
                this.collectAveragePerDayData();
                break;
        }
    }

    collectWeekData(today, isPrevious) {
        let weekStart = this.getWeekStart(today);
        if (isPrevious) {
            weekStart = new Date(weekStart.getTime() - 7 * 24 * 60 * 60 * 1000);
        }
        const weekEnd = new Date(weekStart.getTime() + 6 * 24 * 60 * 60 * 1000);

        // Group by day
        const weekData = {};
        this.practiceHistory.forEach(session => {
            const sessionDate = this.toLocalDate(session.date);
            if (sessionDate >= weekStart && sessionDate <= weekEnd) {
                const dateKey = sessionDate.toDateString();
                weekData[dateKey] = (weekData[dateKey] || 0) + (session.durationMinutes || 0);
            }
        });

        // Create 7-day array
        this.practiceTimePerDay = [];
        this.timePerCategory = [];
        for (let i = 0; i < 7; i++) {
            const date = new Date(weekStart.getTime() + i * 24 * 60 * 60 * 1000);
            const dateKey = date.toDateString();
            const minutes = weekData[dateKey] || 0;
            
            this.practiceTimePerDay.push({ date, minutes });
            this.timePerCategory.push({
                label: date.toLocaleDateString(undefined, { weekday: 'short', day: 'numeric' }),
                minutes
            });
        }
    }

    collectMonthData(today, isPrevious) {
        let year = today.getFullYear();
        let month = today.getMonth();
        
        if (isPrevious) {
            month--;
            if (month < 0) {
                month = 11;
                year--;
            }
        }

        const monthStart = new Date(year, month, 1);
        const daysInMonth = new Date(year, month + 1, 0).getDate();

        // Group by day
        const monthData = {};
        this.practiceHistory.forEach(session => {
            const sessionDate = this.toLocalDate(session.date);
            if (sessionDate.getFullYear() === year && sessionDate.getMonth() === month) {
                const day = sessionDate.getDate();
                monthData[day] = (monthData[day] || 0) + (session.durationMinutes || 0);
            }
        });

        // Create day array
        this.practiceTimePerDay = [];
        this.timePerCategory = [];
        for (let day = 1; day <= daysInMonth; day++) {
            const date = new Date(year, month, day);
            const minutes = monthData[day] || 0;
            
            this.practiceTimePerDay.push({ date, minutes });
            this.timePerCategory.push({ label: day.toString(), minutes });
        }
    }

    collectYearData(today) {
        const year = today.getFullYear();

        // Group by month
        const monthData = {};
        this.practiceHistory.forEach(session => {
            const sessionDate = this.toLocalDate(session.date);
            if (sessionDate.getFullYear() === year) {
                const month = sessionDate.getMonth();
                monthData[month] = (monthData[month] || 0) + (session.durationMinutes || 0);
            }
        });

        // Create month array
        this.practiceTimePerDay = [];
        this.timePerCategory = [];
        const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 
                           'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        
        for (let month = 0; month < 12; month++) {
            const minutes = monthData[month] || 0;
            this.timePerCategory.push({ label: monthNames[month], minutes });
        }
    }

    collectAllYearsData() {
        // Group by year
        const yearData = {};
        this.practiceHistory.forEach(session => {
            const sessionDate = this.toLocalDate(session.date);
            const year = sessionDate.getFullYear();
            yearData[year] = (yearData[year] || 0) + (session.durationMinutes || 0);
        });

        // Create year array (sorted)
        this.timePerCategory = [];
        const years = Object.keys(yearData).sort();
        years.forEach(year => {
            this.timePerCategory.push({ label: year, minutes: yearData[year] });
        });
    }

    collectAveragePerDayData() {
        // Calculate average per day for: week, month, year, all-time
        // Only count days from first registration day in each period
        const today = this.getLocalToday();
        this.timePerCategory = [];

        // Current week average
        const weekStart = this.getWeekStart(today);
        const weekEnd = new Date(weekStart.getTime() + 6 * 24 * 60 * 60 * 1000);
        let weekTotal = 0;
        let firstWeekDate = null;
        this.practiceHistory.forEach(session => {
            const sessionDate = this.toLocalDate(session.date);
            if (sessionDate >= weekStart && sessionDate <= weekEnd) {
                weekTotal += session.durationMinutes || 0;
                if (!firstWeekDate || sessionDate < firstWeekDate) {
                    firstWeekDate = sessionDate;
                }
            }
        });
        const weekDays = firstWeekDate ? 
            Math.floor((today - firstWeekDate) / (24 * 60 * 60 * 1000)) + 1 : 0;
        const weekAvg = weekDays > 0 ? weekTotal / weekDays : 0;

        // Current month average
        let monthTotal = 0;
        let firstMonthDate = null;
        this.practiceHistory.forEach(session => {
            const sessionDate = this.toLocalDate(session.date);
            if (sessionDate.getFullYear() === today.getFullYear() && 
                sessionDate.getMonth() === today.getMonth()) {
                monthTotal += session.durationMinutes || 0;
                if (!firstMonthDate || sessionDate < firstMonthDate) {
                    firstMonthDate = sessionDate;
                }
            }
        });
        const monthDays = firstMonthDate ? 
            Math.floor((today - firstMonthDate) / (24 * 60 * 60 * 1000)) + 1 : 0;
        const monthAvg = monthDays > 0 ? monthTotal / monthDays : 0;

        // Current year average (from first registration this year)
        let yearTotal = 0;
        let firstYearDate = null;
        this.practiceHistory.forEach(session => {
            const sessionDate = this.toLocalDate(session.date);
            if (sessionDate.getFullYear() === today.getFullYear()) {
                yearTotal += session.durationMinutes || 0;
                if (!firstYearDate || sessionDate < firstYearDate) {
                    firstYearDate = sessionDate;
                }
            }
        });
        const yearDays = firstYearDate ? 
            Math.floor((today - firstYearDate) / (24 * 60 * 60 * 1000)) + 1 : 0;
        const yearAvg = yearDays > 0 ? yearTotal / yearDays : 0;

        // All-time average (from first to last registration)
        let allTimeTotal = 0;
        let firstDate = null;
        let lastDate = null;
        this.practiceHistory.forEach(session => {
            const sessionDate = this.toLocalDate(session.date);
            allTimeTotal += session.durationMinutes || 0;
            if (!firstDate || sessionDate < firstDate) firstDate = sessionDate;
            if (!lastDate || sessionDate > lastDate) lastDate = sessionDate;
        });
        const totalDays = firstDate && lastDate ? 
            Math.floor((today - firstDate) / (24 * 60 * 60 * 1000)) + 1 : 0;
        const allTimeAvg = totalDays > 0 ? allTimeTotal / totalDays : 0;

        // Build result
        this.timePerCategory = [
            { label: 'This Week', minutes: weekAvg },
            { label: 'This Month', minutes: monthAvg },
            { label: 'This Year', minutes: yearAvg },
            { label: 'All-Time', minutes: allTimeAvg }
        ];
    }

    collectPracticeTimesPerMusicPiece() {
        const today = this.getLocalToday();
        
        // Week data
        const weekStart = this.getWeekStart(today);
        const weekEnd = new Date(weekStart.getTime() + 6 * 24 * 60 * 60 * 1000);
        
        const weekPieceData = {};
        this.practiceHistory.forEach(session => {
            const sessionDate = this.toLocalDate(session.date);
            if (sessionDate >= weekStart && sessionDate <= weekEnd) {
                const pieceId = session.musicPieceId;
                const title = session.musicPieceTitle || 'Unknown';
                const minutes = session.durationMinutes || 0;
                
                if (!weekPieceData[pieceId]) {
                    weekPieceData[pieceId] = { title, minutes: 0 };
                }
                weekPieceData[pieceId].minutes += minutes;
            }
        });

        this.topMusicPiecesWeek = Object.values(weekPieceData)
            .filter(p => p.minutes > 0)
            .sort((a, b) => b.minutes - a.minutes)
            .slice(0, 10);

        // Month data
        const monthStart = new Date(today.getFullYear(), today.getMonth(), 1);
        const monthEnd = new Date(today.getFullYear(), today.getMonth() + 1, 0);
        
        const monthPieceData = {};
        this.practiceHistory.forEach(session => {
            const sessionDate = this.toLocalDate(session.date);
            if (sessionDate >= monthStart && sessionDate <= monthEnd) {
                const pieceId = session.musicPieceId;
                const title = session.musicPieceTitle || 'Unknown';
                const minutes = session.durationMinutes || 0;
                
                if (!monthPieceData[pieceId]) {
                    monthPieceData[pieceId] = { title, minutes: 0 };
                }
                monthPieceData[pieceId].minutes += minutes;
            }
        });

        this.topMusicPiecesMonth = Object.values(monthPieceData)
            .filter(p => p.minutes > 0)
            .sort((a, b) => b.minutes - a.minutes)
            .slice(0, 10);
    }

    // ========================================================================
    // CHART DRAWING
    // ========================================================================

    drawActiveChart() {
        switch (this.activeTab) {
            case 0:
                this.drawPeriodChart();
                break;
            case 1:
                this.drawWeekPiecesChart();
                break;
            case 2:
                this.drawMonthPiecesChart();
                break;
        }
    }

    drawPeriodChart() {
        const canvas = document.getElementById('periodChart');
        if (!canvas || !this.timePerCategory || this.timePerCategory.length === 0) {
            this.showNoData(canvas);
            return;
        }

        const ctx = canvas.getContext('2d');
        const container = canvas.parentElement;
        
        // Set canvas size dynamically based on container
        canvas.width = container.clientWidth - 40;
        canvas.height = Math.max(container.clientHeight - 40, 400);

        const data = this.timePerCategory;
        const maxMinutes = Math.max(...data.map(d => d.minutes), 1);

        const marginLeft = 80;
        const marginRight = 20;
        const marginTop = 20;
        const marginBottom = 60;

        const chartWidth = canvas.width - marginLeft - marginRight;
        const chartHeight = canvas.height - marginTop - marginBottom;

        // Clear canvas
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        // Draw axes
        this.drawAxes(ctx, marginLeft, marginTop, chartWidth, chartHeight, maxMinutes);

        // Draw bars
        const barWidth = (chartWidth / data.length) * 0.7;
        const barSpacing = (chartWidth / data.length) * 0.3;

        data.forEach((item, i) => {
            const barHeight = (item.minutes / maxMinutes) * chartHeight;
            const x = marginLeft + (i * (barWidth + barSpacing)) + (barSpacing / 2);
            const y = canvas.height - marginBottom - barHeight;

            // Bar
            ctx.fillStyle = '#4A7EBB';
            ctx.fillRect(x, y, barWidth, barHeight);

            // Value label
            ctx.fillStyle = '#4a5568';
            ctx.font = 'bold 11px Inter';
            ctx.textAlign = 'center';
            const timeLabel = this.formatTime(item.minutes);
            ctx.fillText(timeLabel, x + barWidth / 2, y - 5);

            // Category label
            ctx.fillStyle = '#4a5568';
            ctx.font = '11px Inter';
            ctx.textAlign = 'center';
            ctx.fillText(item.label, x + barWidth / 2, canvas.height - marginBottom + 20);
        });
    }

    drawWeekPiecesChart() {
        this.drawMusicPiecesChart('weekPiecesChart', this.topMusicPiecesWeek, 
                                  'No practice sessions for music pieces this week');
    }

    drawMonthPiecesChart() {
        this.drawMusicPiecesChart('monthPiecesChart', this.topMusicPiecesMonth,
                                  'No practice sessions for music pieces this month');
    }

    drawMusicPiecesChart(canvasId, data, noDataMessage) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;

        if (!data || data.length === 0) {
            this.showNoData(canvas, noDataMessage);
            return;
        }

        const ctx = canvas.getContext('2d');
        const container = canvas.parentElement;
        
        // Set canvas size dynamically based on container
        canvas.width = container.clientWidth - 40;
        canvas.height = Math.max(container.clientHeight - 40, 400);

        const maxMinutes = Math.max(...data.map(d => d.minutes), 1);

        const marginLeft = 80;
        const marginRight = 20;
        const marginTop = 20;
        const marginBottom = 100;

        const chartWidth = canvas.width - marginLeft - marginRight;
        const chartHeight = canvas.height - marginTop - marginBottom;

        // Clear canvas
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        // Draw axes
        this.drawAxes(ctx, marginLeft, marginTop, chartWidth, chartHeight, maxMinutes);

        // Draw bars
        const barWidth = (chartWidth / data.length) * 0.7;
        const barSpacing = (chartWidth / data.length) * 0.3;

        data.forEach((item, i) => {
            const barHeight = (item.minutes / maxMinutes) * chartHeight;
            const x = marginLeft + (i * (barWidth + barSpacing)) + (barSpacing / 2);
            const y = canvas.height - marginBottom - barHeight;

            // Bar (with pastel color)
            ctx.fillStyle = this.pastelColors[i % this.pastelColors.length];
            ctx.fillRect(x, y, barWidth, barHeight);

            // Value label
            ctx.fillStyle = '#4a5568';
            ctx.font = 'bold 11px Inter';
            ctx.textAlign = 'center';
            const timeLabel = this.formatTime(item.minutes);
            ctx.fillText(timeLabel, x + barWidth / 2, y - 5);

            // Music piece name (rotated)
            ctx.save();
            ctx.translate(x + 5, canvas.height - marginBottom + 10);
            ctx.rotate(-Math.PI / 4);
            ctx.fillStyle = '#4a5568';
            ctx.font = '11px Inter';
            ctx.textAlign = 'left';
            const truncatedTitle = item.title.length > 20 ? item.title.substring(0, 20) + '...' : item.title;
            ctx.fillText(truncatedTitle, 0, 0);
            ctx.restore();
        });
    }

    drawAxes(ctx, left, top, width, height, maxValue) {
        const bottom = top + height;

        // Vertical axis
        ctx.strokeStyle = '#cbd5e0';
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo(left, top);
        ctx.lineTo(left, bottom);
        ctx.stroke();

        // Horizontal axis
        ctx.beginPath();
        ctx.moveTo(left, bottom);
        ctx.lineTo(left + width, bottom);
        ctx.stroke();

        // Y-axis ticks and labels
        const tickCount = 5;
        for (let i = 0; i <= tickCount; i++) {
            const y = bottom - (i * height / tickCount);
            const value = (i * maxValue / tickCount);

            // Tick mark
            ctx.beginPath();
            ctx.moveTo(left - 5, y);
            ctx.lineTo(left, y);
            ctx.stroke();

            // Label
            ctx.fillStyle = '#718096';
            ctx.font = '10px Inter';
            ctx.textAlign = 'right';
            ctx.fillText(this.formatTime(value), left - 10, y + 3);
        }
    }

    showNoData(canvas, message = 'No practice time available for the selected period') {
        if (!canvas) return;
        
        const ctx = canvas.getContext('2d');
        const container = canvas.parentElement;
        
        canvas.width = container.clientWidth - 40;
        canvas.height = Math.max(container.clientHeight - 40, 300);
        
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        ctx.fillStyle = '#718096';
        ctx.font = '14px Inter';
        ctx.textAlign = 'center';
        ctx.fillText(message, canvas.width / 2, canvas.height / 2);
    }

    // ========================================================================
    // UTILITY FUNCTIONS
    // ========================================================================

    formatTime(minutes) {
        const totalSeconds = Math.round(minutes * 60);
        const hours = Math.floor(totalSeconds / 3600);
        const mins = Math.floor((totalSeconds % 3600) / 60);
        const secs = totalSeconds % 60;
        return `${hours.toString().padStart(2, '0')}:${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
    }

    getLocalToday() {
        const now = new Date();
        return new Date(now.getFullYear(), now.getMonth(), now.getDate());
    }

    toLocalDate(date) {
        // Handle ISO string dates that might be in UTC - convert to local date parts
        if (typeof date === 'string') {
            date = new Date(date);
        }
        // Create date using local timezone components only (ignore time)
        return new Date(date.getFullYear(), date.getMonth(), date.getDate());
    }

    isSameDay(date1, date2) {
        return date1.getFullYear() === date2.getFullYear() &&
               date1.getMonth() === date2.getMonth() &&
               date1.getDate() === date2.getDate();
    }

    getWeekStart(date) {
        const day = date.getDay();
        const diff = day === 0 ? -6 : 1 - day; // Monday as first day
        const weekStart = new Date(date);
        weekStart.setDate(date.getDate() + diff);
        return this.toLocalDate(weekStart);
    }
}

// ============================================================================
// TAB SWITCHING
// ============================================================================

let stats = null;

function switchTab(index) {
    // Update buttons
    const buttons = document.querySelectorAll('.tab-button');
    buttons.forEach((btn, i) => {
        btn.classList.toggle('active', i === index);
    });

    // Update panels
    const panels = document.querySelectorAll('.tab-panel');
    panels.forEach((panel, i) => {
        panel.classList.toggle('active', i === index);
    });

    // Draw chart
    if (stats) {
        stats.activeTab = index;
        stats.drawActiveChart();
    }
}

// ============================================================================
// EXPORT FUNCTIONALITY
// ============================================================================

function exportStatistics() {
    if (!stats || !stats.practiceHistory) return;

    try {
        let csv = 'Date,Music Piece,Duration (minutes)\n';
        
        stats.practiceHistory.forEach(session => {
            const date = new Date(session.date).toISOString().split('T')[0];
            const title = (session.musicPieceTitle || 'Unknown').replace(/,/g, ';');
            const minutes = (session.durationMinutes || 0).toFixed(2);
            csv += `${date},${title},${minutes}\n`;
        });

        // Add summary
        csv += '\nPeriod,Practice Time (minutes)\n';
        if (stats.timePerCategory) {
            stats.timePerCategory.forEach(item => {
                csv += `${item.label},${item.minutes.toFixed(2)}\n`;
            });
        }

        // Download
        const blob = new Blob([csv], { type: 'text/csv' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        // Build timestamp: YYYY-MM-DD_HH-MM-SS (Windows-safe)
        const now = new Date();
        const pad = n => String(n).padStart(2, '0');
        const stamp = `${now.getFullYear()}-${pad(now.getMonth()+1)}-${pad(now.getDate())}_${pad(now.getHours())}-${pad(now.getMinutes())}-${pad(now.getSeconds())}`;
        const tzMinutes = now.getTimezoneOffset();
        const tzSign = tzMinutes <= 0 ? '+' : '-';
        const tzAbs = Math.abs(tzMinutes);
        const tz = `UTC${tzSign}${pad(Math.floor(tzAbs/60))}-${pad(tzAbs%60)}`;
        a.download = `ModusPractica_Statistics_${stamp}_${tz}.csv`;
        a.click();
        URL.revokeObjectURL(url);

        console.log('✅ Statistics exported');
    } catch (error) {
        console.error('❌ Export failed:', error);
        alert('Error exporting statistics: ' + error.message);
    }
}

// ============================================================================
// INITIALIZATION
// ============================================================================

document.addEventListener('DOMContentLoaded', () => {
    stats = new PracticeStatistics();
    stats.init();
});
