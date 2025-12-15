# Intensiteitsmodule - Complete Roadmap

## 🎯 Project Overzicht

De Intensiteitsmodule is een **optionele, parallelle module** die naast de Ebbinghaus SRS-kern werkt om oefenintensiteit te optimaliseren.

```
┌─────────────────────────────────────────────────────────────────┐
│                    MODUS PRACTICA ARCHITECTURE                  │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────┐  ┌─────────────────────────────┐
│      SRS-KERN (VERPLICHT)   │  │  INTENSITY MODULE (OPTIONEEL)│
│    "WANNEER oefenen?"       │  │   "HOELANG & HOE INTENSIEF?" │
├─────────────────────────────┤  ├─────────────────────────────┤
│ • Streak Reset (SR)         │  │ • Failed Attempts (FA)       │
│ • Tau (τ) waarde            │  │ • Technical Difficulty (TDS) │
│ • Next Review Date          │  │ • Overlearning Quotum (OLQ)  │
│ • Ebbinghaus formule        │  │ • Duration Prediction        │
└─────────────────────────────┘  └─────────────────────────────┘
         │                                    │
         │                                    │
         └────────────────┬───────────────────┘
                          │
                          ▼
              ┌─────────────────────┐
              │  SESSION PLANNING   │
              │   (COMBINED VIEW)   │
              └─────────────────────┘
```

## ✅ Huidige Status (November 2025)

### Fase 1: Core Implementation ✅ VOLTOOID

| Component                | Status      | Bestand                             |
| ------------------------ | ----------- | ----------------------------------- |
| JavaScript Class         | ✅ Complete | IntensityModule.js                  |
| Unit Tests               | ✅ Complete | IntensityModule.test.js             |
| Debug Helpers            | ✅ Complete | moduspractica-practice-session.js   |
| HTML Integration         | ✅ Complete | moduspractica-practice-session.html |
| Scientific Documentation | ✅ Complete | moduspractica-science.html          |
| README                   | ✅ Complete | INTENSITY_MODULE_README.md          |
| Implementation Guide     | ✅ Complete | INTENSITY_MODULE_IMPLEMENTATION.md  |
| Working Examples         | ✅ Complete | INTENSITY_MODULE_EXAMPLES.md        |

### Fase 2: UI Integration 🔄 IN PLANNING

| Feature                  | Status     | Priority |
| ------------------------ | ---------- | -------- |
| Settings Toggle          | 🔄 Planned | HIGH     |
| Session Duration Display | 🔄 Planned | HIGH     |
| OLQ Target Display       | 🔄 Planned | HIGH     |
| TDS Progress Indicator   | 🔄 Planned | MEDIUM   |
| Phase Badge UI           | 🔄 Planned | MEDIUM   |
| Historical TDS Chart     | 🔄 Planned | LOW      |

### Fase 3: Advanced Features 📋 BACKLOG

| Feature                    | Status     | Priority |
| -------------------------- | ---------- | -------- |
| Adaptive Time Calibration  | 📋 Backlog | MEDIUM   |
| OLQ Auto-adjustment        | 📋 Backlog | LOW      |
| Cross-section TDS Analysis | 📋 Backlog | LOW      |
| Export TDS Reports         | 📋 Backlog | LOW      |

## 📊 Data Flow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        USER PRACTICE SESSION                    │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      METRIC COLLECTION                          │
├─────────────────────────────────────────────────────────────────┤
│ • Correct Repetitions (CR)          [User increments]           │
│ • Failed Attempts (FA)              [User increments]           │
│ • Streak Resets (SR)                [Auto on CR reset]          │
│ • Duration (seconds)                [Timer tracks]              │
└─────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┴─────────────────────┐
        │                                           │
        ▼                                           ▼
┌──────────────────┐                    ┌──────────────────────┐
│   SRS-KERN       │                    │ INTENSITY MODULE     │
│   PROCESSING     │                    │ PROCESSING           │
├──────────────────┤                    ├──────────────────────┤
│ • Use SR for τ   │                    │ • Calculate TDS      │
│ • Ignore FA      │                    │ • Determine Phase    │
│ • Calculate      │                    │ • Calculate OLQ      │
│   next review    │                    │ • Predict Duration   │
└──────────────────┘                    └──────────────────────┘
        │                                           │
        └─────────────────────┬─────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                       SAVE TO LOCALSTORAGE                      │
├─────────────────────────────────────────────────────────────────┤
│ • Section data (nextReviewDate, stage, etc.)                    │
│ • Practice history (CR, FA, SR, duration, TDS, OLQ)             │
│ • Adaptive system state (PMC, Stability, etc.)                  │
└─────────────────────────────────────────────────────────────────┘
```

## 🎨 TDS Fasen Visualisatie

```
0%    20%   40%    60%   70%    85%   95%   100%
│─────┼─────┼──────┼─────┼──────┼─────┼─────│
│  🔴  │  🟠  │   🟡   │  🟢   │   🔵   │  💜  │
│     │     │      │     │      │     │     │
│ INITIAL │ REFINEMENT │ CONSOLIDATION │ MASTERY │ OVERLEARNING
│         │           │               │         │
│ OLQ: 6-8│  OLQ: 7-10│   OLQ: 8-12   │ OLQ: 9-14│ OLQ: 10-18
│ 120s/CR │   90s/CR  │    60s/CR     │  45s/CR │   30s/CR
```

### Fase Beschrijvingen

#### 🔴 Initial Acquisition (0-40% TDS)

- **Kenmerken:** Veel fouten, steil leren
- **Focus:** Begrip en probleem-oplossing
- **OLQ:** Minimum 6 (6-8) - eerste consolidatie verzekeren
- **Tijd/CR:** 120s - veel trial-and-error

#### 🟠 Refinement (40-70% TDS)

- **Kenmerken:** Afnemende fouten, techniek stabiliseert
- **Focus:** Consistentie opbouwen
- **OLQ:** Oplopend (7-10) - deliberate practice
- **Tijd/CR:** 90s - verfijning

#### 🟡 Consolidation (70-85% TDS)

- **Kenmerken:** Weinig fouten, automatisering begint
- **Focus:** Betrouwbaarheid
- **OLQ:** Hoog (8-12) - diep consolideren
- **Tijd/CR:** 60s - efficiënter

#### 🟢 Mastery (85-95% TDS)

- **Kenmerken:** Zeer weinig fouten, hoge consistentie
- **Focus:** Performance reliability
- **OLQ:** Zeer hoog (9-14) - stress-bestendigheid
- **Tijd/CR:** 45s - automatisch

#### 💜 Overlearning (95-100% TDS)

- **Kenmerken:** Foutloos, performance-ready
- **Focus:** Maximum consolidatie
- **OLQ:** Extreem (10-18) - concert-klaar
- **Tijd/CR:** 30s - volledig automatisch

## 🔧 Implementation Checklist

### Fase 1: Core ✅ COMPLETE

- [x] Create IntensityModule.js class
- [x] Implement TDS calculation
- [x] Implement phase determination
- [x] Implement OLQ calculation
- [x] Implement duration prediction
- [x] Implement archiving rule
- [x] Create unit tests
- [x] Add debug helpers
- [x] Integrate into HTML
- [x] Document in science.html
- [x] Write comprehensive README
- [x] Create working examples

### Fase 2: UI Integration 🔄 NEXT

- [ ] Add settings toggle (enable/disable module)

  - [ ] Profile settings UI
  - [ ] localStorage persistence
  - [ ] Default: enabled for new, disabled for existing

- [ ] Session Planning Integration

  - [ ] Display predicted duration on calendar
  - [ ] Show OLQ target in session UI
  - [ ] Display current TDS and phase
  - [ ] Add phase badge/indicator

- [ ] Practice Session UI

  - [ ] Show OLQ progress bar
  - [ ] Display phase transition notifications
  - [ ] Add TDS trend mini-chart
  - [ ] Visual feedback for phase completion

- [ ] Dashboard Enhancements
  - [ ] TDS overview per piece
  - [ ] Phase distribution chart
  - [ ] OLQ completion statistics

### Fase 3: Advanced Features 📋 FUTURE

- [ ] Adaptive Time Calibration

  - [ ] Per-user learning of T̄_CR
  - [ ] Confidence intervals for predictions
  - [ ] Auto-adjust based on actual vs predicted

- [ ] OLQ Auto-adjustment

  - [ ] Increase OLQ when TDS consistently high
  - [ ] Decrease OLQ when student struggling
  - [ ] Personalized OLQ ranges

- [ ] Analytics & Reporting
  - [ ] Historical TDS progression charts
  - [ ] Cross-section phase comparison
  - [ ] Export detailed TDS reports
  - [ ] Performance prediction models

## 📱 UI Mockups (Conceptual)

### Settings Panel

```
┌─────────────────────────────────────┐
│ INTENSITY MODULE SETTINGS           │
├─────────────────────────────────────┤
│                                     │
│ ☑ Enable Intensity Module          │
│                                     │
│ When enabled:                       │
│ • Practice duration predicted       │
│ • OLQ targets provided              │
│ • Phase guidance shown              │
│                                     │
│ When disabled:                      │
│ • Fixed 15-minute sessions          │
│ • No OLQ targets                    │
│ • Self-directed practice            │
│                                     │
│ Recommended for:                    │
│ ✓ Students & new musicians          │
│ ✓ Structured practice schedules     │
│                                     │
│ [ Save Settings ]                   │
└─────────────────────────────────────┘
```

### Practice Session UI (Module Enabled)

```
┌─────────────────────────────────────┐
│ PRACTICE SESSION                    │
├─────────────────────────────────────┤
│ Piece: Bach - Invention No. 1      │
│ Section: Measures 1-4               │
│                                     │
│ ┌─────────────────────────────────┐ │
│ │ 🟡 CONSOLIDATION PHASE          │ │
│ │ TDS: 75% (9/12 attempts)        │ │
│ └─────────────────────────────────┘ │
│                                     │
│ OLQ Progress:                       │
│ [████████░░] 4/5 correct reps      │
│                                     │
│ Estimated Duration: 5 minutes       │
│ Elapsed: 00:03:45                   │
│                                     │
│ Target: 5 correct repetitions       │
│ (Building automaticity)             │
└─────────────────────────────────────┘
```

### Practice Session UI (Module Disabled)

```
┌─────────────────────────────────────┐
│ PRACTICE SESSION                    │
├─────────────────────────────────────┤
│ Piece: Rachmaninoff - Prelude      │
│ Section: Measures 45-60             │
│                                     │
│ Duration: 15 minutes (fixed)        │
│ Elapsed: 00:08:30                   │
│                                     │
│ Practice at your own pace           │
│                                     │
│ [No OLQ guidance]                   │
└─────────────────────────────────────┘
```

## 🧪 Testing Strategy

### Unit Tests ✅ COMPLETE

```javascript
// Run in console:
runIntensityModuleTests();

// Coverage:
• TDS calculation (all ranges)
• Phase determination (boundaries)
• OLQ calculation (all phases)
• Duration prediction (all phases)
• Archiving rule (CR=0)
• Edge cases (0%, 100%, large numbers)
```

### Integration Tests 🔄 TO DO

```javascript
// Test scenarios:
1. Complete practice session with module enabled
2. Complete practice session with module disabled
3. Toggle module on/off mid-profile
4. Historical data migration
5. Cross-profile consistency
```

### User Acceptance Tests 🔄 TO DO

```
1. New user experience (module enabled by default)
2. Experienced user experience (module disabled)
3. Settings toggle functionality
4. Duration prediction accuracy
5. OLQ guidance usefulness
6. Phase transition clarity
```

## 📈 Performance Considerations

### Memory Usage

- **Minimal:** Module is stateless, calculates on-demand
- **Storage:** No additional localStorage keys (uses existing history)
- **Impact:** Negligible (<1KB code)

### Computation Cost

- **TDS calculation:** O(1) - simple division
- **Phase determination:** O(1) - threshold checks
- **OLQ calculation:** O(1) - lookup + interpolation
- **Duration prediction:** O(n) - n = history length (typically <100)
- **Overall:** Negligible impact on UI responsiveness

## 🔒 Data Privacy & Security

### Data Storage

- **Location:** localStorage (client-side only)
- **Transmission:** No server communication
- **Privacy:** All data stays on user's device
- **Security:** No sensitive data collected

### Data Structure

```javascript
// Practice History Entry (existing structure + TDS)
{
  id: "guid",
  barSectionId: "section-guid",
  date: "2025-11-23T10:00:00Z",
  duration: 300000,  // milliseconds
  repetitions: 8,    // CR
  attemptsTillSuccess: 2,  // FA
  totalFailures: 1,  // SR (for SRS-kern)
  // No new fields needed - TDS calculated on-demand
}
```

## 📚 Documentation Index

| Document                           | Purpose              | Audience               |
| ---------------------------------- | -------------------- | ---------------------- |
| IntensityModule.js                 | Source code          | Developers             |
| IntensityModule.test.js            | Unit tests           | Developers             |
| INTENSITY_MODULE_README.md         | API documentation    | Developers             |
| INTENSITY_MODULE_IMPLEMENTATION.md | Implementation guide | Developers             |
| INTENSITY_MODULE_EXAMPLES.md       | Working examples     | All users              |
| INTENSITY_MODULE_ROADMAP.md        | Project overview     | Project managers       |
| moduspractica-science.html         | Scientific basis     | Researchers, educators |

## 🎓 Educational Resources

### For Students

- **What is TDS?** Success ratio showing your learning progress
- **What is OLQ?** Target number of correct repetitions for optimal learning
- **Why phases?** Your brain learns motor skills in predictable stages
- **Module enabled?** Get structured guidance tailored to your level

### For Teachers

- **Monitoring Progress:** Use TDS to track student improvement
- **Setting Expectations:** Share phase descriptions with students
- **Adjusting Practice:** Modify OLQ based on individual needs
- **Module recommendation:** Enable for structured learners

### For Researchers

- **Scientific Basis:** Fitts & Posner (1967), Gebrian (2013)
- **Data Collection:** TDS and OLQ data available for analysis
- **Open Source:** Algorithm is fully documented and transparent
- **Validation:** Unit tests demonstrate correctness

## 🌟 Success Metrics

### User Engagement

- [ ] % of users with module enabled
- [ ] Average TDS progression rate
- [ ] OLQ completion rate
- [ ] Session completion rate

### Learning Outcomes

- [ ] Time to reach Consolidation phase
- [ ] Time to reach Mastery phase
- [ ] Retention rate (SRS-kern metrics)
- [ ] User satisfaction scores

### System Performance

- [ ] UI responsiveness (< 100ms)
- [ ] Calculation accuracy (TDS, OLQ)
- [ ] Prediction accuracy (duration)
- [ ] Bug/error rate (target: < 0.1%)

## 🚀 Deployment Plan

### Phase 1: Core ✅ DEPLOYED

- [x] Module loaded in practice session
- [x] Debug helpers available in console
- [x] Unit tests passing
- [x] Documentation complete

### Phase 2: UI Integration 🔄 Q1 2026

- [ ] Settings toggle implementation
- [ ] Session UI enhancements
- [ ] Dashboard integration
- [ ] User testing & feedback

### Phase 3: Optimization 📋 Q2 2026

- [ ] Adaptive time calibration
- [ ] OLQ auto-adjustment
- [ ] Advanced analytics
- [ ] Performance optimization

## 📞 Support & Feedback

### For Developers

- **Questions:** Check INTENSITY_MODULE_README.md
- **Examples:** See INTENSITY_MODULE_EXAMPLES.md
- **Bugs:** Document in project issue tracker
- **Contributions:** Follow existing code style

### For Users

- **Help:** Module is optional - disable if not useful
- **Feedback:** Contact via Modus Practica feedback form
- **Suggestions:** Feature requests welcome
- **Issues:** Report via support channel

---

**Copyright © 2025 Frank De Baere - Partura Music™**  
**All Rights Reserved**
