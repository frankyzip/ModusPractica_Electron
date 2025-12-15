# Intensity Module - Integration Summary

## 🎯 Overview

De **Intensity Module** is volledig geïntegreerd in ModusPractica. Deze module werkt parallel aan het bestaande Ebbinghaus SRS-systeem:

- **SRS Core**: Bepaalt **WANNEER** te oefenen (spaced repetition scheduling)
- **Intensity Module**: Bepaalt **HOE LANG** en **HOE INTENSIEF** te oefenen (practice duration & difficulty guidance)

## ✅ Geïmplementeerde Features

### 1. **Settings Toggle** ⚙️

**Locatie**: `moduspractica-dashboard.html` + `moduspractica-dashboard.js`

**Functionaliteit**:

- Settings knop in dashboard header
- Modal met toggle switch voor enable/disable Intensity Module
- Persistente opslag per profiel in localStorage (`mp_[profileId]_intensitySettings`)
- Uitgebreide info over wat de module doet (wanneer enabled/disabled)

**JavaScript Functies**:

- `openSettingsModal()` - Opent modal en laadt huidige settings
- `closeSettingsModal()` - Sluit modal
- `toggleIntensityModule()` - Toggle module on/off + refresh analytics

**CSS Styling**:

- `.modal` - Modal backdrop met flex centering
- `.modal-content` - Modal container met shadow
- `.settings-toggle` - Custom checkbox slider (groen wanneer enabled)
- `.settings-toggle-slider` - Animated toggle switch
- `.settings-info` - Info box met enabled/disabled uitleg

---

### 2. **Session UI** 📊

**Locatie**: `moduspractica-practice-session.html` + `moduspractica-practice-session.js`

**Functionaliteit**:

- Real-time TDS bar met percentage (0-100%)
- Learning phase badge met emoji en fase naam
- OLQ progress bar met target waarde
- Automatisch verbergen wanneer module disabled is

**HTML Structuur**:

```html
<div id="intensityDisplay" class="intensity-module-display">
  <!-- TDS Bar -->
  <div class="intensity-metric">
    <div class="intensity-label">Technical Difficulty Score (TDS)</div>
    <div class="intensity-bar-container">
      <div class="intensity-bar" id="intensityBar" style="width: 0%;"></div>
    </div>
    <div class="intensity-value" id="intensityValue">0%</div>
  </div>

  <!-- Phase Badge -->
  <div class="intensity-metric">
    <div class="intensity-label">Learning Phase</div>
    <div class="phase-badge" id="phaseBadge">🎯 Not Started</div>
  </div>

  <!-- OLQ Progress -->
  <div class="intensity-metric">
    <div class="intensity-label">Overlearning Quotum (OLQ)</div>
    <div class="olq-progress">
      <span id="olqCurrent">0</span> /
      <span id="olqTarget" class="olq-target">0</span>
    </div>
  </div>
</div>
```

**JavaScript Functies**:

- `loadIntensityModuleSettings()` - Laadt module settings bij sessie start
- `updateIntensityDisplay()` - Update TDS, phase, OLQ display real-time
- `updateIntensityDisplayVisibility()` - Show/hide display op basis van settings
- `saveAdaptiveTimeCalibration()` - Leert T̄_CR (avg time per CR) per sectie

**CSS Styling**:

- `.intensity-module-display` - Container met gradient background
- `.intensity-bar` - Animated gradient bar (rood → geel → groen)
- `.phase-badge` - Badge met emoji, fase naam, en gradient background
- `.olq-progress` - Progress display met highlighted target value

**Data Flow**:

1. Bij sessie start: `loadIntensityModuleSettings()` → check of module enabled
2. Bij elke correcte response: `updateTrackingMetrics()` → `updateIntensityDisplay()`
3. Bij sessie einde: `saveAdaptiveTimeCalibration()` → sla T̄_CR op in localStorage

---

### 3. **Dashboard Analytics** 📈

**Locatie**: `moduspractica-dashboard.html` + `moduspractica-dashboard.js`

**Functionaliteit**:

- **TDS Trends Chart**: Toont TDS progressie per sectie (laatste 10 sessies)
- **Phase Distribution Chart**: Bar chart met aantal secties per leerfase
- **Average Time per CR Chart**: Adaptieve calibratie data (T̄_CR per sectie)
- Automatisch verbergen wanneer module disabled

**HTML Structuur**:

```html
<div id="intensityAnalyticsSection" class="intensity-analytics-section">
  <div class="section-header">
    <h2>📊 Intensity Module Analytics</h2>
    <button onclick="refreshIntensityAnalytics()">🔄 Refresh</button>
  </div>

  <div class="intensity-analytics-grid">
    <!-- TDS Trends Card -->
    <div class="analytics-card">
      <div id="tdsTrendsChart"></div>
    </div>

    <!-- Phase Distribution Card -->
    <div class="analytics-card">
      <div id="phaseDistributionChart"></div>
    </div>

    <!-- Avg Time per CR Card -->
    <div class="analytics-card">
      <div id="avgTimePerCRChart"></div>
    </div>
  </div>
</div>
```

**JavaScript Functies**:

- `refreshIntensityAnalytics()` - Main coordinator: laadt data + render charts
- `renderTDSTrendsChart(sectionsWithData)` - Render TDS trend lines per sectie
- `renderTrendPoints(history, intensityModule)` - Render trend dots (laatste 10 sessies)
- `renderPhaseDistributionChart(sectionsWithData)` - Render phase bar chart
- `renderAvgTimePerCRChart(profileId)` - Render adaptive calibration data
- `getPhaseColor(phaseName)` - Helper: map fase → kleur
- `formatDuration(seconds)` - Helper: format seconden → MM:SS

**Chart Details**:

**TDS Trends Chart**:

- Per sectie: titel, huidige TDS, learning phase, trend dots
- Trend dots: 10 meest recente sessies
- Current dot: groter + groen glow
- Historical dots: kleur gebaseerd op TDS (high/medium/low)
- Border-left kleur matched learning phase

**Phase Distribution Chart**:

- Bar chart met 5 fases (Initial → Overlearning)
- Percentage + count per fase
- Gradient fill (brown → gold)
- Responsive layout

**Average Time per CR Chart**:

- Global average (alle secties samen)
- Top 5 meest geoefende secties
- Time formatted als MM:SS
- Session count per sectie

**CSS Styling**:

- `.intensity-analytics-section` - Main container met gradient background
- `.intensity-analytics-grid` - Responsive grid (3 cols → 1 col op mobile)
- `.analytics-card` - Witte kaart met shadow
- `.tds-section-row` - Sectie rij met border-left fase kleur
- `.tds-trend-point` - Trend dot (8px → 12px voor current)
- `.phase-bar-fill` - Gradient bar met percentage label

**Data Sources**:

- Practice history: `mp_[profileId]_data` → `pieces[].sections[].practiceHistory[]`
- Adaptive calibration: `mp_[profileId]_adaptiveTimeCalibration` → `{sectionId: {avgTimePerCR, sessionCount}}`

**Auto-Refresh**:

- Bij dashboard init: `refreshIntensityAnalytics()`
- Bij window focus: `refreshIntensityAnalytics()` (return from practice session)
- Bij toggle module: `refreshIntensityAnalytics()` (immediate show/hide)

---

### 4. **Adaptive Calibration** 🧠

**Locatie**: `moduspractica-practice-session.js`

**Functionaliteit**:

- Leert **T̄_CR** (average time per correct response) per sectie
- Gebruikt exponential moving average (α = 0.3)
- Persistente opslag in localStorage
- Gebruikt voor duration prediction in `IntensityModule.predictSessionDuration()`

**JavaScript Functie**:

```javascript
saveAdaptiveTimeCalibration(sectionId, sectionName, sessionDuration, correctResponses) {
    if (!this.currentProfile || correctResponses === 0) return;

    const key = `mp_${this.currentProfile.id}_adaptiveTimeCalibration`;
    const calibrationData = JSON.parse(localStorage.getItem(key) || '{}');

    const avgTimePerCR = sessionDuration / correctResponses;

    if (!calibrationData[sectionId]) {
        // First session: initialize
        calibrationData[sectionId] = {
            avgTimePerCR: avgTimePerCR,
            sessionCount: 1,
            lastUpdated: new Date().toISOString(),
            sectionName: sectionName
        };
    } else {
        // Subsequent sessions: exponential moving average (α = 0.3)
        const prevAvg = calibrationData[sectionId].avgTimePerCR;
        const newAvg = 0.3 * avgTimePerCR + 0.7 * prevAvg;

        calibrationData[sectionId].avgTimePerCR = newAvg;
        calibrationData[sectionId].sessionCount += 1;
        calibrationData[sectionId].lastUpdated = new Date().toISOString();
    }

    localStorage.setItem(key, JSON.stringify(calibrationData));
    console.log(`✅ Adaptive time calibration updated: ${avgTimePerCR.toFixed(2)}s per CR`);
}
```

**Data Structure** (localStorage):

```json
{
  "mp_[profileId]_adaptiveTimeCalibration": {
    "section-uuid-1": {
      "avgTimePerCR": 12.5,
      "sessionCount": 8,
      "lastUpdated": "2025-01-15T14:30:00.000Z",
      "sectionName": "Chopin - Nocturne Op. 9 No. 2 - Measures 1-8"
    },
    "section-uuid-2": {
      "avgTimePerCR": 18.3,
      "sessionCount": 3,
      "lastUpdated": "2025-01-15T15:00:00.000Z",
      "sectionName": "Bach - Invention No. 1 - Full piece"
    }
  }
}
```

**Usage in IntensityModule**:

```javascript
const calibrationData = JSON.parse(
  localStorage.getItem(calibrationKey) || "{}"
);
const sectionData = calibrationData[sectionId];

if (sectionData && sectionData.avgTimePerCR) {
  // Use learned T̄_CR for this specific section
  avgTimePerCR = sectionData.avgTimePerCR;
} else {
  // Fallback to global default (15 seconds)
  avgTimePerCR = 15;
}

const predictedDuration = olq * avgTimePerCR;
```

---

## 📁 Modified Files

### Created Files:

1. `IntensityModule.js` - Core calculation engine
2. `IntensityModule.test.js` - Unit tests
3. `INTENSITY_MODULE_README.md` - API documentation
4. `INTENSITY_MODULE_IMPLEMENTATION.md` - Implementation status
5. `INTENSITY_MODULE_EXAMPLES.md` - Working examples
6. `INTENSITY_MODULE_ROADMAP.md` - Project roadmap
7. `INTENSITY_MODULE_INTEGRATION_SUMMARY.md` - This file

### Modified Files:

1. **moduspractica-practice-session.html**:

   - Added intensity display HTML section
   - Added comprehensive CSS styling for intensity UI

2. **moduspractica-practice-session.js**:

   - Added state variables: `intensityModuleEnabled`, `currentTDS`, `currentPhase`, `currentOLQ`, `adaptiveTimePerCR`
   - Added functions: `loadIntensityModuleSettings()`, `updateIntensityDisplay()`, `updateIntensityDisplayVisibility()`, `saveAdaptiveTimeCalibration()`
   - Modified `updateTrackingMetrics()` to call `updateIntensityDisplay()`
   - Added initialization call in session start logic

3. **moduspractica-dashboard.html**:

   - Added "⚙️ Settings" button in header
   - Added settings modal HTML structure with toggle switch
   - Added intensity analytics section HTML
   - Added comprehensive CSS styling for modal, analytics, and charts
   - Added `IntensityModule.js` script include

4. **moduspractica-dashboard.js**:
   - Added settings modal functions: `openSettingsModal()`, `closeSettingsModal()`, `toggleIntensityModule()`
   - Added analytics functions: `refreshIntensityAnalytics()`, `renderTDSTrendsChart()`, `renderPhaseDistributionChart()`, `renderAvgTimePerCRChart()`
   - Added helper functions: `renderTrendPoints()`, `getPhaseColor()`, `formatDuration()`
   - Modified `init()` to auto-load analytics on dashboard open
   - Added window focus listener to refresh analytics after practice session

---

## 🔧 Technical Architecture

### Data Flow:

```
┌─────────────────────────────────────────────────────────────────┐
│                      ModusPractica App                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌──────────────────┐         ┌──────────────────┐             │
│  │ Dashboard Page   │         │ Practice Session │             │
│  │                  │         │      Page        │             │
│  │ - Settings Modal │◄────────┤                  │             │
│  │ - Analytics      │  Enable │ - TDS Display    │             │
│  │   Charts         │  Module │ - Phase Badge    │             │
│  └──────────────────┘         │ - OLQ Progress   │             │
│           │                    └──────────────────┘             │
│           │                            │                         │
│           │                            │                         │
│           ▼                            ▼                         │
│  ┌────────────────────────────────────────────────┐            │
│  │          localStorage (Per Profile)            │            │
│  ├────────────────────────────────────────────────┤            │
│  │ mp_[profileId]_intensitySettings:              │            │
│  │   { enabled: true/false, lastModified: ... }   │            │
│  │                                                 │            │
│  │ mp_[profileId]_adaptiveTimeCalibration:        │            │
│  │   { sectionId: {avgTimePerCR, sessionCount} }  │            │
│  │                                                 │            │
│  │ mp_[profileId]_data:                           │            │
│  │   { pieces: [{sections: [{practiceHistory}]}] }│            │
│  └────────────────────────────────────────────────┘            │
│                            │                                     │
│                            ▼                                     │
│                  ┌──────────────────┐                           │
│                  │ IntensityModule  │                           │
│                  │  (Core Engine)   │                           │
│                  │                  │                           │
│                  │ - calculateTDS() │                           │
│                  │ - getLearning    │                           │
│                  │   Phase()        │                           │
│                  │ - calculateOLQ() │                           │
│                  │ - predict        │                           │
│                  │   Duration()     │                           │
│                  └──────────────────┘                           │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
```

### Module State:

**Enabled**:

- Settings: `enabled: true` in localStorage
- Practice Session: TDS display visible, OLQ targets shown
- Dashboard: Analytics section visible
- Duration: Predicted via `OLQ × T̄_CR`

**Disabled**:

- Settings: `enabled: false` in localStorage
- Practice Session: TDS display hidden
- Dashboard: Analytics section hidden
- Duration: Fixed 15 minutes

---

## 🧪 Testing Guide

### Manual Testing Checklist:

#### 1. Settings Modal:

- [ ] Open dashboard → Click "⚙️ Settings"
- [ ] Modal appears with toggle switch
- [ ] Toggle ON → check localStorage: `mp_[profileId]_intensitySettings.enabled = true`
- [ ] Toggle OFF → check localStorage: `mp_[profileId]_intensitySettings.enabled = false`
- [ ] Close modal → Click "×" or "Close" button
- [ ] Click outside modal → Modal closes

#### 2. Practice Session UI:

- [ ] Enable module in settings
- [ ] Start practice session
- [ ] Verify intensity display visible
- [ ] Play notes → TDS bar updates
- [ ] Check phase badge changes (0% → 40% → 70% → 85% → 95%)
- [ ] Check OLQ progress updates
- [ ] Disable module in settings
- [ ] Reload practice session → intensity display hidden

#### 3. Dashboard Analytics:

- [ ] Complete practice sessions for multiple sections
- [ ] Open dashboard
- [ ] Verify "📊 Intensity Module Analytics" section visible
- [ ] **TDS Trends Chart**: Check trend dots, current section highlighted
- [ ] **Phase Distribution Chart**: Check bar percentages sum to 100%
- [ ] **Avg Time per CR Chart**: Check global average + top 5 sections
- [ ] Disable module → analytics section hidden
- [ ] Enable module → analytics section visible

#### 4. Adaptive Calibration:

- [ ] Complete practice session with 10 CRs in 120 seconds
- [ ] Check localStorage: `mp_[profileId]_adaptiveTimeCalibration[sectionId].avgTimePerCR = 12.0`
- [ ] Complete another session with 20 CRs in 300 seconds
- [ ] Verify exponential moving average: `newAvg = 0.3 × 15.0 + 0.7 × 12.0 = 12.9`
- [ ] Check dashboard analytics shows updated T̄_CR

#### 5. Browser Console Commands:

```javascript
// Check if module enabled
const profileId = localStorage.getItem("mp_currentProfile");
const settings = JSON.parse(
  localStorage.getItem(`mp_${profileId}_intensitySettings`) || "{}"
);
console.log("Module enabled:", settings.enabled);

// Check adaptive calibration data
const calibration = JSON.parse(
  localStorage.getItem(`mp_${profileId}_adaptiveTimeCalibration`) || "{}"
);
console.table(calibration);

// Manually refresh analytics
refreshIntensityAnalytics();

// Test IntensityModule directly
const im = new IntensityModule();
console.log("TDS:", im.calculateTDS(8, 2)); // 0.8
console.log("Phase:", im.getLearningPhase(0.8)); // Consolidation
console.log(
  "OLQ:",
  im.calculateOLQ(0.8, { correctRepetitions: 10, failedAttempts: 2 })
);
```

---

## 📊 Data Persistence Schema

### localStorage Keys:

```javascript
// Per profile:
`mp_${profileId}_intensitySettings` → {
    enabled: boolean,           // Default: true
    lastModified: ISO8601       // Timestamp of last change
}

`mp_${profileId}_adaptiveTimeCalibration` → {
    [sectionId]: {
        avgTimePerCR: number,   // Exponential moving avg (seconds)
        sessionCount: number,   // Total sessions for this section
        lastUpdated: ISO8601,   // Timestamp of last update
        sectionName: string     // Human-readable section name
    }
}

// Global (shared):
`mp_currentProfile` → string   // Current profile ID
```

---

## 🚀 Future Enhancements

### Phase 2 (Planned):

1. **Export/Import Analytics**: CSV export van TDS trends + calibration data
2. **Advanced Charts**: Line charts met Chart.js (TDS over time)
3. **Difficulty-based T̄_CR**: Apart T̄_CR voor verschillende difficulty levels
4. **Session Recommendations**: AI-based suggesties voor practice duration
5. **Gamification**: Badges voor fase milestones (eerste Overlearning, etc.)

### Phase 3 (Research):

1. **Machine Learning**: Predict optimal OLQ per gebruiker (personalized learning)
2. **Biometric Integration**: Heart rate/fatigue detection → dynamic OLQ adjustment
3. **Multi-user Analytics**: Compare TDS trends met peers (anoniem)

---

## 📝 Developer Notes

### Design Decisions:

1. **Parallel Architecture**: Intensity Module werkt naast SRS, niet als replacement

   - Rationale: Ervaren muzikanten willen misschien zelf practice duration bepalen

2. **Client-side Only**: Alle berekeningen + opslag in browser

   - Rationale: Privacy, offline functionality, geen server costs

3. **Exponential Moving Average voor T̄_CR**: α = 0.3 (30% nieuw, 70% oud)

   - Rationale: Reageert snel genoeg op veranderingen, maar niet te gevoelig voor outliers

4. **5 Learning Phases** (niet 3 of 7):

   - Rationale: Balans tussen granulariteit en overzichtelijkheid
   - Bronnen: Dr. Molly Gebrian's Overlearning research, motor learning literature

5. **TDS Formula**: `CR / (CR + FA)` (niet `CR / Total Attempts`)
   - Rationale: Voorkomt 100% TDS bij 1 CR + 0 FA (edge case)

### Performance Considerations:

- **localStorage Limits**: Max ~5-10MB per origin
  - Mitigation: Store only aggregated adaptive data, not raw session logs
- **Chart Rendering**: Vanilla JS (no Chart.js dependency)
  - Rationale: Keep bundle size small, custom styling control
- **Mobile Responsiveness**: Grid layouts met `auto-fit` + `minmax()`
  - Works on: iPhone SE (320px), iPad (768px), Desktop (1920px)

---

## 📚 Related Documentation

- **Core API**: `INTENSITY_MODULE_README.md`
- **Implementation Status**: `INTENSITY_MODULE_IMPLEMENTATION.md`
- **Code Examples**: `INTENSITY_MODULE_EXAMPLES.md`
- **Roadmap**: `INTENSITY_MODULE_ROADMAP.md`
- **Main Project**: `.github/copilot-instructions.md`

---

**Last Updated**: January 2025  
**Version**: 1.0.0  
**Status**: ✅ Fully Integrated & Operational
