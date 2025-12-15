# Intensiteitsmodule - Documentatie

## Overzicht

De **Intensiteitsmodule** is een optionele, onafhankelijke component die parallel werkt aan de Ebbinghaus SRS-kern in Modus Practica. Deze module bepaalt **HOELANG** en **HOE INTENSIEF** geoefend moet worden, terwijl de SRS-kern bepaalt **WANNEER** geoefend moet worden.

## Architectuur

### 1. De Bestaande Planner (SRS-kern)

Dit gedeelte blijft **ongewijzigd**. Het is de motor van de agenda die bepaalt WANNEER geoefend moet worden.

| Metric            | Doel                          | Formule                | Status              |
| ----------------- | ----------------------------- | ---------------------- | ------------------- |
| Streak Reset (SR) | Geheugenverlies meten         | Beïnvloedt de τ-waarde | ✅ Verplicht/Actief |
| τ-waarde (Tau)    | Planningsfrequentie berekenen | Ebbinghaus formule     | ✅ Verplicht/Actief |

### 2. De Nieuwe Optionele Module (Intensiteit)

Dit is de module die **in- en uitgeschakeld** kan worden. Het bepaalt HOELANG en HOE INTENSIEF geoefend moet worden.

| Metric                           | Doel                                | Formule                                       | Status       |
| -------------------------------- | ----------------------------------- | --------------------------------------------- | ------------ |
| Failed Attempts (FA)             | Technische moeilijkheid meten       | Berekent de TDS                               | ⚙️ Optioneel |
| Technical Difficulty Score (TDS) | Leertrajectfase bepalen             | CR / (CR + FA)                                | ⚙️ Optioneel |
| Overlearning Quotum (OLQ)        | Target aantal herhalingen instellen | FixedGoal + ceil(initialFailedAttempts × 0.5) | ⚙️ Optioneel |
| Gemiddelde Tijd (T̄_CR)           | Sessieduur voorspellen              | Σ(Tijd) / Σ(CR's)                             | ⚙️ Optioneel |

## In-/Uitschakel Logica

### Module AAN (Nieuwe gebruikers/Studenten)

De agenda plant de sessie met:

- **Voorspelde duur** (bijv. "12 minuten")
- **OLQ instructie** (bijv. "Target: 5 correcte herhalingen (Consolidation fase)")

**Voorbeeld:**

```
Practice for 12 minutes
Target: 5 correct repetitions
Phase: Consolidation (Building automaticity and consistency)
```

### Module UIT (Ervaren muzikanten)

De agenda plant de sessie met:

- **Standaard vaste duur** (bijv. 15 minuten)
- **Geen OLQ instructie** - De gebruiker bepaalt zelf de intensiteit en duur

**Voorbeeld:**

```
Practice this section at your own pace.
Duration: 15 minutes
```

## TDS (Technical Difficulty Score) Fasen

De TDS wordt berekend als een **pure success ratio**:

```
TDS = CR / (CR + FA)
```

| TDS Range | Leertrajectfase     | Kenmerken                                 | FixedGoal (Baseline) | Overlearning Component            |
| --------- | ------------------- | ----------------------------------------- | -------------------- | --------------------------------- |
| 0-40%     | Initial Acquisition | Veel fouten, steil leren, focus op begrip | 6 CR's               | ceil(initialFailedAttempts × 0.5) |
| 40-70%    | Refinement          | Afnemende fouten, techniek stabiliseert   | 7 CR's               | ceil(initialFailedAttempts × 0.5) |
| 70-85%    | Consolidation       | Weinig fouten, automatisering begint      | 8 CR's               | ceil(initialFailedAttempts × 0.5) |
| 85-95%    | Mastery             | Zeer weinig fouten, hoge consistentie     | 9 CR's               | ceil(initialFailedAttempts × 0.5) |
| 95-100%   | Overlearning        | Foutloos, ready voor performance          | 10 CR's              | ceil(initialFailedAttempts × 0.5) |

## OLQ (Overlearning Quotum) - Dr. Gebrian Framework

Het OLQ is gebaseerd op onderzoek van Dr. Molly Gebrian, dat aantoont dat gerichte overlearning (oefenen voorbij de initiële beheersing) essentieel is voor betrouwbaarheid onder stress.

### Rationale per fase:

- **Early phases (TDS < 40%):** FixedGoal = 6 correcte herhalingen. Elke mislukte poging vóór de eerste correcte uitvoering levert meteen extra overlearning-reps op.
- **Middle phases (TDS 40-85%):** FixedGoal = 7-8 correcte herhalingen (fase-afhankelijk). De extra reps volgen nog steeds de `ceil(initialFailedAttempts × 0.5)`-regel zodat het werkvolume aanleunt bij de werkelijke moeilijkheid.
- **Advanced phases (TDS > 85%):** FixedGoal = 9-10 correcte herhalingen. De overlearning-term verzekert betrouwbaarheid onder stress, precies conform Dr. Gebrian's overlearningprincipe.

De volledige formule is:

```
OLQ_target = FixedGoal_phase + ceil(initialFailedAttempts × 0.5)
```

`initialFailedAttempts` telt uitsluitend de fouten die gemaakt worden vóór de eerste correcte herhaling in de sessie. De eerste geslaagde poging telt onmiddellijk als repetitie #1; fouten nadien worden wel gelogd voor analyse, maar verhogen het OLQ niet meer.

## Duur Voorspelling

Sessieduur wordt voorspeld met:

```
Duur = OLQ × T̄_CR
```

Voor nieuwe secties zonder geschiedenis gebruikt het systeem fase-gebaseerde schattingen:

| Fase                | Tijd per CR   | Rationale                              |
| ------------------- | ------------- | -------------------------------------- |
| Initial Acquisition | 120s (2 min)  | Fouten vereisen probleemoplossingstijd |
| Refinement          | 90s (1.5 min) | Techniek verfijnen                     |
| Consolidation       | 60s (1 min)   | Automatisering ontwikkelt              |
| Mastery             | 45s           | Geautomatiseerde uitvoering            |
| Overlearning        | 30s           | Foutloos, snel                         |

## Data Management Regel (Archivering)

**Verplichte regel** (onafhankelijk van module status):

```
IF CR = 0 THEN Archive Chunk
```

**Rationale:** Chunks met nul correcte herhalingen leveren geen bruikbare data voor scheduling berekeningen. Ze vertegenwoordigen:

- Secties die te moeilijk zijn voor het huidige vaardigheidsniveau van de gebruiker
- Oefensessies die werden verlaten of onderbroken
- Data-invoerfouten

Archivering voorkomt dat deze chunks scheduling berekeningen corrumperen, terwijl ze bewaard blijven in de database voor toekomstige reactivering indien gewenst.

## Gebruik in Code

### Beschikbare Debug Commands

```javascript
// Genereer intensiteitsrapport voor huidige sectie
window.showIntensityReport();

// Resulteert in console output:
// ============================================================
// 📊 INTENSITY MODULE REPORT
// ============================================================
// METRICS:
//   CR (Correct): 8
//   FA (Failed): 2
//   Total Attempts: 10
//   TDS (Success Ratio): 80%
//
// LEARNING PHASE:
//   Phase: CONSOLIDATION
//   Description: Consolidation: Building automaticity and consistency
//
// OLQ RECOMMENDATION:
//   Recommended: 9 repetitions
//   Range: 8-12 repetitions
//   Overlearning Reps: 1
//   Phase: CONSOLIDATION
//
// DURATION PREDICTION:
//   Estimated: 9 minutes
//   Time per CR: 60s
//
// HISTORICAL DATA:
//   Sessions: 3
//   Avg Time/CR: 1.2 min
//
// ARCHIVING:
//   Should Archive: NO
// ============================================================
```

### API Usage

```javascript
// Bereken TDS
const tds = IntensityModule.calculateTDS(correctRepetitions, failedAttempts);

// Bepaal leertrajectfase
const phase = IntensityModule.getLearningPhase(tds);

// Bereken OLQ (met automatische foutcorrectie)
const olq = IntensityModule.calculateOLQ(tds, {
  correctRepetitions,
  failedAttempts,
});
// Returns: {
//   min: 8,
//   max: 12,
//   recommended: 9,
//   phase: 'CONSOLIDATION',
//   phaseDescription: '...',
//   adjustmentMultiplier: 9 / 8,
//   overlearningReps: 1
// }

// Voorspel sessieduur
const duration = IntensityModule.predictSessionDuration(olq.recommended, tds);
// Returns: { durationSeconds: 540, durationMinutes: 9, phase: 'CONSOLIDATION', timePerCR: 60 }

// Genereer praktijkinstructie
const instruction = IntensityModule.generatePracticeInstruction(
  moduleEnabled, // boolean
  olq, // OLQ data
  duration // Duration data
);
// Returns: { instruction: 'string', duration: number, olq: number|null, moduleStatus: 'enabled'|'disabled' }

// Check of chunk gearchiveerd moet worden
const shouldArchive = IntensityModule.shouldArchiveChunk(correctRepetitions);
// Returns: true als CR = 0, anders false

// Bereken gemiddelde tijd per CR uit historische data
const avgTime = IntensityModule.calculateAverageTimePerCR(sectionHistory);
// Returns: gemiddelde tijd in seconden, 0 als geen data

// Genereer volledig rapport
const report = IntensityModule.generateIntensityReport(
  correctRepetitions,
  failedAttempts,
  sectionHistory
);

// Log rapport naar console
IntensityModule.logIntensityReport(report);
```

## Wetenschappelijke Basis

De Intensiteitsmodule is gedocumenteerd in de wetenschappelijke paper:

- **Bestand:** `moduspractica-science.html`
- **Sectie:** "9. The Intensity Module: Optional Practice Duration & Quality Control"

### Key References:

- **Fitts, P. M., & Posner, M. I. (1967).** Human performance. _Brooks/Cole_. (Motor learning fasen)
- **Gebrian, M. (2013).** Shaping sound musicians: An innovative approach to teaching comprehensive musicianship through performance. _GIA Publications_. (Overlearning framework)

## Implementatiestatus

| Component                      | Status      | Bestand                               |
| ------------------------------ | ----------- | ------------------------------------- |
| Core Module                    | ✅ Complete | `IntensityModule.js`                  |
| Debug Helpers                  | ✅ Complete | `moduspractica-practice-session.js`   |
| HTML Integration               | ✅ Complete | `moduspractica-practice-session.html` |
| Wetenschappelijke Documentatie | ✅ Complete | `moduspractica-science.html`          |
| UI Toggle                      | 🔄 To Do    | Settings interface                    |
| Auto-Duration Prediction       | 🔄 To Do    | Session planning                      |
| OLQ Display                    | 🔄 To Do    | Session UI                            |

## Toekomstige Uitbreidingen

1. **Settings Toggle:** Voeg een gebruikersinstelling toe om de module in/uit te schakelen
2. **Auto-Duration:** Voorspel automatisch sessieduur op basis van TDS en OLQ
3. **OLQ Display:** Toon OLQ target en fase-informatie in de session UI
4. **Historical Analysis:** Toon trend van TDS over tijd (Progress tracking)
5. **Adaptive Time Estimates:** Verfijn T̄_CR schattingen op basis van individuele gebruikersdata

## Contact

Voor vragen over de Intensiteitsmodule:

- **Auteur:** Frank De Baere
- **Project:** Partura Music™ - Modus Practica™
- **Copyright:** © 2025 All Rights Reserved
