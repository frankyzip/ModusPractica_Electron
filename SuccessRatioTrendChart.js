// ModusPractica Web App - Success Ratio Trend Chart Component
// Copyright (c) 2025 Frank De Baere - Partura Music™
// All rights reserved.
// Reusable component for visualizing practice session success ratio trends

class SuccessRatioTrendChart {
    constructor(canvasId, options = {}) {
        this.canvas = document.getElementById(canvasId);
        if (!this.canvas) {
            console.error(`Canvas with id '${canvasId}' not found`);
            return;
        }
        
        this.ctx = this.canvas.getContext('2d');
        this.options = {
            maxSessions: options.maxSessions || 7,
            highlightLatest: options.highlightLatest !== false,
            showLegend: options.showLegend !== false,
            showTempoOverlay: options.showTempoOverlay || false,
            showStartFrictionLine: options.showStartFrictionLine !== false,
            frictionMaxAttempts: options.frictionMaxAttempts || 5,
            height: options.height || 300,
            width: options.width || 500,
            ...options
        };
        
        // Initial canvas size (will be updated in draw())
        this.updateCanvasSize();
    }
    
    updateCanvasSize() {
        // Get container size dynamically
        const container = this.canvas.parentElement;
        if (container) {
            // Fit within smaller popups: allow compact minimums
            this.canvas.width = Math.max(container.clientWidth - 20, 320);
            this.canvas.height = Math.max(container.clientHeight - 20, 240);
        } else {
            this.canvas.width = this.options.width;
            this.canvas.height = this.options.height;
        }
        
        // Update chart dimensions based on canvas size
        this.chartLeft = 40;
        this.chartTop = 15;
        this.chartWidth = this.canvas.width - 50;
        this.chartHeight = this.canvas.height - 70;
        
        // Zone definitions (matching C# desktop version)
        this.zones = [
            { name: 'Too Hard', min: 0.00, max: 0.60, color: '#FFCDD2', textColor: '#C62828' },
            { name: 'Exploration', min: 0.60, max: 0.80, color: '#FFF59D', textColor: '#F57F17' },
            { name: 'Consolidation', min: 0.80, max: 0.90, color: '#FFE0B2', textColor: '#E65100' },
            { name: 'Mastery', min: 0.90, max: 0.95, color: '#C8E6C9', textColor: '#2E7D32' },
            { name: 'Overlearning', min: 0.95, max: 1.00, color: '#D0D0D0', textColor: '#616161' }
        ];
    }
    
    /**
     * Draw the chart with practice history data
     * @param {Array} practiceHistory - Array of practice session objects with properties:
     *   - repetitions: number of successful repetitions
     *   - totalFailures: number of failures
     *   - date: ISO date string
     */
    draw(practiceHistory) {
        if (!this.ctx) return;
        
        // Update canvas size based on container
        this.updateCanvasSize();
        
        // Clear canvas
        this.ctx.clearRect(0, 0, this.canvas.width, this.canvas.height);
        
        // Filter and prepare data
        const data = this.prepareData(practiceHistory);
        
        if (data.length < 2) {
            this.drawEmptyState();
            return;
        }
        
        // Draw chart layers
        this.drawZones();
        this.drawBoundaries();
        this.drawAxes();
        this.drawYAxisLabels();
        this.drawSeries(data);
        if (this.options.showStartFrictionLine) {
            this.drawFrictionSeries(data);
        }
        
        if (this.options.showTempoOverlay) {
            this.drawTempoOverlay(practiceHistory);
        }
        
        if (this.options.showLegend) {
            this.drawLegend();
        }
    }
    
    /**
     * Prepare cumulative rolling average data (matching C# algorithm)
     */
    prepareData(practiceHistory) {
        if (!practiceHistory || practiceHistory.length < 2) {
            return [];
        }
        
        // Sort by date (oldest first)
        const sorted = [...practiceHistory].sort((a, b) => 
            new Date(a.date) - new Date(b.date)
        );
        
        // Take last N sessions
        const take = Math.min(this.options.maxSessions, sorted.length);
        const startIndex = sorted.length - take;
        
        const data = [];
        
        // Calculate cumulative ratio for each session (matching C# logic)
        for (let i = startIndex; i < sorted.length; i++) {
            let totalReps = 0;
            let totalFails = 0;
            
            // Sum from startIndex to current position
                for (let j = startIndex; j <= i; j++) {
                    totalReps += sorted[j].repetitions || 0;
                    // Use helper to read failures (supports both new `memoryFailures`/`executionFailures` and legacy fields)
                    totalFails += (getCombinedFailures(sorted[j]) || 0);
                }
            
            let ratio = 0.0;
            if (totalReps + totalFails > 0) {
                ratio = totalReps / (totalReps + totalFails);
            }

            const rawAttempts = Number(getExecutionFailures(sorted[i]));
            const attempts = Number.isFinite(rawAttempts)
                ? Math.max(0, rawAttempts)
                : 0;
            const normalizedAttempts = this.options.frictionMaxAttempts > 0
                ? Math.min(1, attempts / this.options.frictionMaxAttempts)
                : 0;
            
            data.push({
                date: new Date(sorted[i].date),
                ratio: ratio,
                isLatest: i === sorted.length - 1,
                attempts: attempts,
                frictionValue: normalizedAttempts
            });
        }
        
        return data;
    }
    
    /**
     * Convert ratio to scaled Y coordinate (non-linear scaling)
     * 0-60% takes 20% of chart, 60-100% takes 80% of chart
     */
    ratioToScaledY(ratio) {
        if (ratio <= 0.60) {
            return (ratio / 0.60) * 0.20; // 0-60% → 0-20%
        }
        return 0.20 + ((ratio - 0.60) / 0.40) * 0.80; // 60-100% → 20-100%
    }
    
    /**
     * Convert scaled Y to canvas Y coordinate
     */
    scaledYToCanvasY(scaledY) {
        return this.chartTop + this.chartHeight - (scaledY * this.chartHeight);
    }
    
    /**
     * Draw colored background zones
     */
    drawZones() {
        this.zones.forEach(zone => {
            const scaledYMax = this.ratioToScaledY(zone.max);
            const scaledYMin = this.ratioToScaledY(zone.min);
            const yTop = this.scaledYToCanvasY(scaledYMax);
            const yBottom = this.scaledYToCanvasY(scaledYMin);
            const zoneHeight = yBottom - yTop;
            
            // Draw zone background
            this.ctx.fillStyle = zone.color;
            this.ctx.globalAlpha = 0.4;
            this.ctx.fillRect(this.chartLeft, yTop, this.chartWidth, zoneHeight);
            this.ctx.globalAlpha = 1.0;
            
            // Draw zone name (centered in zone)
            this.ctx.fillStyle = zone.textColor;
            this.ctx.font = 'bold 11px Arial';
            this.ctx.textAlign = 'center';
            this.ctx.fillText(zone.name, this.chartLeft + this.chartWidth / 2, yTop + zoneHeight / 2 + 4);
        });
    }
    
    /**
     * Draw zone boundary lines
     */
    drawBoundaries() {
        const boundaries = [0.60, 0.80, 0.90, 0.95];
        
        boundaries.forEach(ratio => {
            const scaledY = this.ratioToScaledY(ratio);
            const y = this.scaledYToCanvasY(scaledY);
            
            this.ctx.strokeStyle = '#999999';
            this.ctx.lineWidth = 1;
            this.ctx.setLineDash([3, 3]);
            this.ctx.beginPath();
            this.ctx.moveTo(this.chartLeft, y);
            this.ctx.lineTo(this.chartLeft + this.chartWidth, y);
            this.ctx.stroke();
            this.ctx.setLineDash([]);
        });
    }
    
    /**
     * Draw X and Y axes
     */
    drawAxes() {
        this.ctx.strokeStyle = '#666666';
        this.ctx.lineWidth = 1.5;
        
        // X-axis
        this.ctx.beginPath();
        this.ctx.moveTo(this.chartLeft, this.chartTop + this.chartHeight);
        this.ctx.lineTo(this.chartLeft + this.chartWidth, this.chartTop + this.chartHeight);
        this.ctx.stroke();
    }
    
    /**
     * Draw Y-axis labels (0%, 60%, 80%, 90%, 95%, 100%)
     */
    drawYAxisLabels() {
        const labels = [
            { ratio: 0.0, text: '0%' },
            { ratio: 0.60, text: '60%' },
            { ratio: 0.80, text: '80%' },
            { ratio: 0.90, text: '90%' },
            { ratio: 0.95, text: '95%' },
            { ratio: 1.0, text: '100%' }
        ];
        
        this.ctx.fillStyle = '#666666';
        this.ctx.font = '10px Arial';
        this.ctx.textAlign = 'right';
        
        labels.forEach(label => {
            const scaledY = this.ratioToScaledY(label.ratio);
            const y = this.scaledYToCanvasY(scaledY);
            
            // Draw tick mark
            this.ctx.strokeStyle = '#666666';
            this.ctx.lineWidth = 1;
            this.ctx.beginPath();
            this.ctx.moveTo(this.chartLeft - 5, y);
            this.ctx.lineTo(this.chartLeft, y);
            this.ctx.stroke();
            
            // Draw label
            this.ctx.fillText(label.text, this.chartLeft - 8, y + 3);
        });
    }
    
    /**
     * Draw the data series (line and points)
     */
    drawSeries(data) {
        if (data.length === 0) return;
        
        const xStep = this.chartWidth / (data.length - 1);
        const points = [];
        
        // Calculate all points
        data.forEach((d, i) => {
            const x = this.chartLeft + (i * xStep);
            const scaledY = this.ratioToScaledY(d.ratio);
            const y = this.scaledYToCanvasY(scaledY);
            points.push({ x, y, isLatest: d.isLatest });
        });
        
        // Draw connecting lines
        this.ctx.strokeStyle = '#4A86E8';
        this.ctx.lineWidth = 2.5;
        this.ctx.beginPath();
        points.forEach((point, i) => {
            if (i === 0) {
                this.ctx.moveTo(point.x, point.y);
            } else {
                this.ctx.lineTo(point.x, point.y);
            }
        });
        this.ctx.stroke();
        
        // Draw data points
        points.forEach(point => {
            const isLatest = point.isLatest && this.options.highlightLatest;
            const radius = isLatest ? 5 : 3;
            const strokeWidth = isLatest ? 3 : 2;
            
            // Outer circle (white stroke)
            this.ctx.fillStyle = '#4A86E8';
            this.ctx.strokeStyle = '#FFFFFF';
            this.ctx.lineWidth = strokeWidth;
            this.ctx.beginPath();
            this.ctx.arc(point.x, point.y, radius, 0, Math.PI * 2);
            this.ctx.fill();
            this.ctx.stroke();
        });
    }

    /**
     * Draw start friction trend line (normalized attempts before first success)
     */
    drawFrictionSeries(data) {
        if (!data || data.length < 2) return;
        
        const xStep = this.chartWidth / (data.length - 1);
        const points = [];
        
        data.forEach((d, i) => {
            const x = this.chartLeft + (i * xStep);
            const frictionRatio = Math.max(0, Math.min(1, d.frictionValue || 0));
            const scaledY = this.ratioToScaledY(frictionRatio);
            const y = this.scaledYToCanvasY(scaledY);
            points.push({ x, y });
        });
        
        // Draw dashed friction trend line
        this.ctx.strokeStyle = '#9C27B0';
        this.ctx.lineWidth = 2;
        this.ctx.setLineDash([6, 4]);
        this.ctx.beginPath();
        points.forEach((point, i) => {
            if (i === 0) {
                this.ctx.moveTo(point.x, point.y);
            } else {
                this.ctx.lineTo(point.x, point.y);
            }
        });
        this.ctx.stroke();
        this.ctx.setLineDash([]);
        
        // Draw points for friction trend
        points.forEach(point => {
            this.ctx.fillStyle = '#9C27B0';
            this.ctx.strokeStyle = '#FFFFFF';
            this.ctx.lineWidth = 1.5;
            this.ctx.beginPath();
            this.ctx.arc(point.x, point.y, 3, 0, Math.PI * 2);
            this.ctx.fill();
            this.ctx.stroke();
        });
    }
    
    /**
     * Draw tempo overlay (percentage of target tempo achieved)
     */
    drawTempoOverlay(practiceHistory) {
        if (!practiceHistory || practiceHistory.length < 2) return;
        
        // Sort by date (oldest first)
        const sorted = [...practiceHistory].sort((a, b) => 
            new Date(a.date) - new Date(b.date)
        );
        
        // Take last N sessions
        const take = Math.min(this.options.maxSessions, sorted.length);
        const startIndex = sorted.length - take;
        
        const tempoData = [];
        
        // Extract tempo ratios
        for (let i = startIndex; i < sorted.length; i++) {
            const session = sorted[i];
            if (session.targetTempo > 0 && session.achievedTempo > 0) {
                const ratio = Math.min(1.0, session.achievedTempo / session.targetTempo);
                tempoData.push({ ratio, index: i - startIndex });
            }
        }
        
        if (tempoData.length < 2) return;
        
        const xStep = this.chartWidth / (take - 1);
        const points = [];
        
        // Calculate tempo points
        tempoData.forEach(d => {
            const x = this.chartLeft + (d.index * xStep);
            const scaledY = this.ratioToScaledY(d.ratio);
            const y = this.scaledYToCanvasY(scaledY);
            points.push({ x, y });
        });
        
        // Draw tempo line (dashed orange line)
        this.ctx.strokeStyle = '#FF9800';
        this.ctx.lineWidth = 2;
        this.ctx.setLineDash([5, 5]);
        this.ctx.beginPath();
        points.forEach((point, i) => {
            if (i === 0) {
                this.ctx.moveTo(point.x, point.y);
            } else {
                this.ctx.lineTo(point.x, point.y);
            }
        });
        this.ctx.stroke();
        this.ctx.setLineDash([]); // Reset to solid line
        
        // Draw tempo points (orange circles)
        points.forEach(point => {
            this.ctx.fillStyle = '#FF9800';
            this.ctx.strokeStyle = '#FFFFFF';
            this.ctx.lineWidth = 2;
            this.ctx.beginPath();
            this.ctx.arc(point.x, point.y, 3, 0, Math.PI * 2);
            this.ctx.fill();
            this.ctx.stroke();
        });
    }
    
    /**
     * Draw legend at bottom
     */
    drawLegend() {
        const legendY = this.chartTop + this.chartHeight + 15;
        const itemWidth = this.chartWidth / 5;
        let x = this.chartLeft;
        
        this.ctx.font = '8px Arial';
        this.ctx.textAlign = 'left';
        
        this.zones.forEach(zone => {
            // Draw color box
            this.ctx.fillStyle = zone.color;
            this.ctx.strokeStyle = '#999999';
            this.ctx.lineWidth = 0.5;
            this.ctx.fillRect(x, legendY, 10, 10);
            this.ctx.strokeRect(x, legendY, 10, 10);
            
            // Draw label
            this.ctx.fillStyle = '#666666';
            const percentage = `${Math.round(zone.min * 100)}-${Math.round(zone.max * 100)}%`;
            this.ctx.fillText(zone.name, x + 13, legendY + 6);
            this.ctx.fillText(`(${percentage})`, x + 13, legendY + 15);
            
            x += itemWidth;
        });
        
        // Start friction legend entry
        // Add tempo overlay legend if enabled
        if (this.options.showTempoOverlay) {
            const tempoLegendX = this.chartLeft;
            const tempoLegendY = legendY + 25;
            
            // Draw orange dashed line
            this.ctx.strokeStyle = '#FF9800';
            this.ctx.lineWidth = 2;
            this.ctx.setLineDash([5, 5]);
            this.ctx.beginPath();
            this.ctx.moveTo(tempoLegendX, tempoLegendY + 5);
            this.ctx.lineTo(tempoLegendX + 16, tempoLegendY + 5);
            this.ctx.stroke();
            this.ctx.setLineDash([]);
            
            // Draw label
            this.ctx.fillStyle = '#666666';
            this.ctx.font = '9px Arial';
            this.ctx.fillText('Tempo (% of target)', tempoLegendX + 20, tempoLegendY + 9);
        }
    }
    
    /**
     * Draw empty state message
     */
    drawEmptyState() {
        this.ctx.fillStyle = '#999999';
        this.ctx.font = 'italic 14px Arial';
        this.ctx.textAlign = 'center';
        this.ctx.fillText(
            'Not enough data to show a trend yet',
            this.canvas.width / 2,
            this.canvas.height / 2
        );
    }
}

// Export for use in other modules
if (typeof module !== 'undefined' && module.exports) {
    module.exports = SuccessRatioTrendChart;
}
