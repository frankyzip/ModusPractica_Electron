# Intensiteitsmodule - Werkende Voorbeelden

## Voorbeeld 1: Beginnend Student (Initial Acquisition Fase)

### Scenario

- **Piece:** Bach - Invention No. 1 in C Major
- **Section:** Measures 1-4 (eerste regel)
- **Practice Data:**
  - Correct Repetitions (CR): 2
  - Failed Attempts (FA): 8
  - Initial Failed Attempts (vóór eerste succes): 8
  - Total Attempts: 10

### Berekeningen

#### 1. TDS Calculation

```javascript
TDS = CR / (CR + FA)
TDS = 2 / (2 + 8)
TDS = 2 / 10
TDS = 0.20 (20%)
```

#### 2. Learning Phase

```javascript
TDS = 20% → INITIAL_ACQUISITION fase
```

#### 3. OLQ Recommendation

```javascript
FixedGoal (Initial Acquisition) = 6
Overlearning Term = ceil(8 × 0.5) = 4
OLQ Target = 6 + 4 = 10 correcte herhalingen
```

**Rationale:** Acht mislukte pogingen vóór de eerste correcte uitvoering signaleren zware motorische uitdaging. Dr. Gebrian's 50%-regel voegt daarom vier extra herhalingen toe bovenop het basisdoel van zes. Verdere fouten na het eerste succes verhogen dit doel niet langer.

#### 4. Duration Prediction

```javascript
Time per CR (Initial Acquisition) = 120 seconden
Duration = OLQ × Time per CR = 10 × 120s = 1 200s ≈ 20 minuten
```

#### 5. Practice Instruction (Module Enabled)

```
Initial Learning: Focus on understanding and basic execution

Target: 10 correct repetitions
FixedGoal: 6, Overlearning bonus: +4
Estimated duration: ~20 minutes
```

### Interpretatie

De student zit in de **steile leerfase**. Veel startfouten zijn normaal en worden bewust vertaald naar extra overlearning-repetities.

- **Doelgericht OLQ (10 reps):** Baseline 6 + 50% van de startfouten
- **Sessieduur (~20 min):** Genoeg tijd om nieuwe motorische patronen vast te leggen
- **Focus:** Analyseer de fouten vóór het eerste succes en corrigeer ze meteen

## Voorbeeld 2: Vooruitgaande Student (Refinement Fase)

### Scenario

- **Piece:** Chopin - Waltz Op. 64 No. 2
- **Section:** Measures 9-16 (A-section)
- **Practice Data:**
  - Correct Repetitions (CR): 7
  - Failed Attempts (FA): 5
  - Initial Failed Attempts (vóór eerste succes): 5
  - Total Attempts: 12

### Berekeningen

#### 1. TDS Calculation

```javascript
TDS = CR / (CR + FA)
TDS = 7 / (7 + 5)
TDS = 7 / 12
TDS = 0.583 (58%)
```

#### 2. Learning Phase

```javascript
TDS = 58% → REFINEMENT fase
```

#### 3. OLQ Recommendation

```javascript
FixedGoal (Refinement) = 7
Overlearning Term = ceil(5 × 0.5) = 3
OLQ Target = 7 + 3 = 10 correcte herhalingen
```

**Rationale:** Een success ratio van 58% wijst op een sectie die al deels onder controle is, maar vijf startfouten vragen om bijkomende overlearning. De 50%-regel verhoogt het doel naar tien correcte herhalingen zodat de recente problemen effectief worden opgelost.

#### 4. Duration Prediction

```javascript
Time per CR (Refinement) = 90 seconden
Duration = OLQ × Time per CR = 10 × 90s = 900s ≈ 15 minuten
```

#### 5. Practice Instruction (Module Enabled)

```
Refinement: Reducing errors and stabilizing technique

Target: 10 correct repetitions
FixedGoal: 7, Overlearning bonus: +3
Estimated duration: ~15 minutes
```

### Interpretatie

De student maakt duidelijke vooruitgang. De module adviseert:

- **OLQ (10 reps):** Baseline 7 plus 50% van de startfouten
- **Sessieduur (~15 min):** Ruimte om techniek gecontroleerd te herhalen
- **Focus op stabiliteit:** Herhaal trage probleemzones tot ze moeiteloos slagen

---

## Voorbeeld 3: Gevorderde Student (Consolidation Fase)

### Scenario

- **Piece:** Beethoven - Moonlight Sonata, 3rd Movement
- **Section:** Measures 21-28 (arpeggios)
- **Practice Data:**
  - Correct Repetitions (CR): 9
  - Failed Attempts (FA): 2
  - Initial Failed Attempts (vóór eerste succes): 2
  - Total Attempts: 11

### Berekeningen

#### 1. TDS Calculation

```javascript
TDS = CR / (CR + FA)
TDS = 9 / (9 + 2)
TDS = 9 / 11
TDS = 0.818 (82%)
```

#### 2. Learning Phase

```javascript
TDS = 82% → CONSOLIDATION fase
```

#### 3. OLQ Recommendation

```javascript
FixedGoal (Consolidation) = 8
Overlearning Term = ceil(2 × 0.5) = 1
OLQ Target = 8 + 1 = 9 correcte herhalingen
```

**Rationale:** Met slechts twee startfouten en een TDS van 82% is het stuk bijna geautomatiseerd. Eén extra overlearning-repetitie bovenop de baseline volstaat om de fragiele passages te verstevigen.

#### 4. Duration Prediction

```javascript
Time per CR (Consolidation) = 60 seconden
Duration = OLQ × Time per CR = 9 × 60s = 540s = 9 minuten
```

#### 5. Practice Instruction (Module Enabled)

```
Consolidation: Building automaticity and consistency

Target: 9 correct repetitions
FixedGoal: 8, Overlearning bonus: +1
Estimated duration: ~9 minutes
```

### Interpretatie

De student bereikt automatisering. De module adviseert:

- **OLQ (9 reps):** Baseline 8 + één extra correct repetition voor foutloze integratie
- **Sessieduur (~9 min):** Gericht oefenen zonder overbelasting
- **Focus op consistentie:** Blijf moeilijke overslagen controleren tot ze probleemloos lukken

---

## Voorbeeld 4: Expert (Mastery Fase)

### Scenario

- **Piece:** Liszt - Hungarian Rhapsody No. 2
- **Section:** Measures 100-108 (cadenza)
- **Practice Data:**
  - Correct Repetitions (CR): 11
  - Failed Attempts (FA): 1
  - Initial Failed Attempts (vóór eerste succes): 1
  - Total Attempts: 12

### Berekeningen

#### 1. TDS Calculation

```javascript
TDS = CR / (CR + FA)
TDS = 11 / (11 + 1)
TDS = 11 / 12
TDS = 0.917 (92%)
```

#### 2. Learning Phase

```javascript
TDS = 92% → MASTERY fase
```

#### 3. OLQ Recommendation

```javascript
FixedGoal (Mastery) = 9
Overlearning Term = ceil(1 × 0.5) = 1
OLQ Target = 9 + 1 = 10 correcte herhalingen
```

**Rationale:** Eén startfout duidt op een detail dat nog verfijnd moet worden. De overlearning-term dwingt één extra herhaling bovenop de baseline van negen om performancebetrouwbaarheid te borgen.

#### 4. Duration Prediction

```javascript
Time per CR (Mastery) = 45 seconden
Duration = OLQ × Time per CR = 10 × 45s = 450s ≈ 7,5 minuten
```

#### 5. Practice Instruction (Module Enabled)

```
Mastery: Achieving high consistency and reliability

Target: 10 correct repetitions
FixedGoal: 9, Overlearning bonus: +1
Estimated duration: ~7.5 minutes
```

### Interpretatie

De student heeft mastery bereikt. De module adviseert:

- **OLQ (10 reps):** Baseline 9 + één extra herhaling voor zekerheid onder druk
- **Sessieduur (~7,5 min):** Intensieve maar beknopte onderhoudssessie
- **Focus op betrouwbaarheid:** Speel kritieke passages in verschillende dynamische varianten

---

## Voorbeeld 5: Performance-Ready (Overlearning Fase)

### Scenario

- **Piece:** Mozart - Piano Sonata K. 545, 1st Movement
- **Section:** Full movement (performance preparation)
- **Practice Data:**
  - Correct Repetitions (CR): 15
  - Failed Attempts (FA): 0
  - Initial Failed Attempts (vóór eerste succes): 0
  - Total Attempts: 15

### Berekeningen

#### 1. TDS Calculation

```javascript
TDS = CR / (CR + FA)
TDS = 15 / (15 + 0)
TDS = 15 / 15
TDS = 1.00 (100%)
```

#### 2. Learning Phase

```javascript
TDS = 100% → OVERLEARNING fase
```

#### 3. OLQ Recommendation

```javascript
FixedGoal (Overlearning) = 10
Overlearning Term = ceil(0 × 0.5) = 0
OLQ Target = 10 correcte herhalingen
```

**Rationale:** Geen startfouten betekent dat de baseline volstaat. De speler hoeft enkel het vaste doel van tien foutloze herhalingen te halen om performanceklaar te blijven.

#### 4. Duration Prediction

```javascript
Time per CR (Overlearning) = 30 seconden
Duration = OLQ × Time per CR = 10 × 30s = 300s = 5 minuten
```

#### 5. Practice Instruction (Module Enabled)

```
Overlearning: Performance-ready, minimal errors

Target: 10 correct repetitions
FixedGoal: 10, Overlearning bonus: +0
Estimated duration: ~5 minutes
```

### Interpretatie

De student is performance-ready. De module adviseert:

- **OLQ (10 reps):** Pure baseline voor onderhoud
- **Sessieduur (~5 min):** Korte, intensieve bevestiging van automatisering
- **Focus op stress-bestendigheid:** Varieer tempo en dynamiek om concertdruk te simuleren

---

## Voorbeeld 6: Ervaren Muzikant (Module UIT)

### Scenario

- **Piece:** Rachmaninoff - Prelude Op. 23 No. 5
- **Section:** Measures 45-60 (development)
- **Module Status:** DISABLED (expert wil zelf bepalen)

### Practice Instruction (Module Disabled)

```
Practice this section at your own pace.
Duration: 15 minutes
```

### Interpretatie

De ervaren muzikant:

- **Geen OLQ target:** Eigen judgement gebruiken
- **Vaste duur (15 min):** Standaard sessie
- **Volledige controle:** Intensiteit zelf bepalen

De module respecteert expertise en geeft geen onnodige guidance.

---

## Voorbeeld 7: Historische Data Analyse

### Scenario

- **Piece:** Debussy - Clair de Lune
- **Section:** Measures 15-27 (middle section)
- **Historical Sessions:**

| Session | CR  | FA  | Duration (sec) | TDS | Time/CR |
| ------- | --- | --- | -------------- | --- | ------- |
| 1       | 2   | 8   | 240            | 20% | 120s    |
| 2       | 4   | 6   | 360            | 40% | 90s     |
| 3       | 6   | 4   | 360            | 60% | 60s     |
| 4       | 8   | 2   | 360            | 80% | 45s     |

### Analyse

#### 1. TDS Progression

```
Session 1: 20% → INITIAL_ACQUISITION
Session 2: 40% → REFINEMENT
Session 3: 60% → REFINEMENT
Session 4: 80% → CONSOLIDATION
```

**Learning Curve:** Student progresses steadily through phases.

#### 2. Average Time per CR

```javascript
Total Time: 240 + 360 + 360 + 360 = 1320 seconds
Total CRs: 2 + 4 + 6 + 8 = 20
Average Time per CR: 1320 / 20 = 66 seconds
```

#### 3. Next Session Prediction

```javascript
Current TDS: 80% (Consolidation)
OLQ Recommended: 8-12 repetitions
Predicted Duration: 10 × 66s = 660 seconds ≈ 11 minutes
```

**Personalized Estimate:** Uses actual user data (66s/CR) instead of generic estimate (60s/CR).

---

## Voorbeeld 8: Archivering Regel (CR=0)

### Scenario A: Werkbare Chunk

- **CR:** 5
- **FA:** 10
- **Archive:** NO (CR > 0)

De chunk heeft bruikbare data en blijft actief in de planner.

### Scenario B: Onwerkbare Chunk

- **CR:** 0
- **FA:** 15
- **Archive:** YES (CR = 0)

De chunk heeft geen bruikbare data. Mogelijke redenen:

1. **Te moeilijk:** Boven niveau van student
2. **Verlaten sessie:** Student stopte vroeg
3. **Data fout:** Verkeerde invoer

**Actie:** Chunk wordt gearchiveerd. Scheduler kan niet werken met CR=0 data.

---

## Console Debug Voorbeelden

### Example Output: showIntensityReport()

```
============================================================
📊 INTENSITY MODULE REPORT
============================================================
METRICS:
  CR (Correct): 8
  FA (Failed): 2
  Initial Failures (before first success): 4
  Total Attempts: 10
  TDS (Success Ratio): 80%

LEARNING PHASE:
  Phase: CONSOLIDATION
  Description: Consolidation: Building automaticity and consistency

OLQ RECOMMENDATION:
  Recommended: 10 correct repetitions
  FixedGoal Baseline: 8 repetitions
  Overlearning Bonus: +2 (from 4 initial failures)
  Phase: CONSOLIDATION

DURATION PREDICTION:
  Estimated: 10 minutes
  Time per CR: 60s

HISTORICAL DATA:
  Sessions: 4
  Avg Time/CR: 1.1 min

ARCHIVING:
  Should Archive: NO
============================================================
```

---

## Vergelijking: Module AAN vs UIT

### Student A (Module ENABLED)

```
📊 Session Plan:
  TDS: 75% (Consolidation)
  OLQ Target: 10 correct repetitions (FixedGoal 8 + bonus 2)
  Duration: 10 minutes

📝 Instruction:
  "Practice this section focusing on building automaticity.
   Target: 10 correct repetitions.
   FixedGoal baseline: 8, overlearning bonus: +2.
   Estimated duration: 10 minutes."
```

### Student B (Module DISABLED)

```
📊 Session Plan:
  Duration: 15 minutes (fixed)

📝 Instruction:
  "Practice this section at your own pace."
```

**Key Difference:** Module provides structure and guidance vs complete freedom.

---

## Wetenschappelijke Validatie

### Dr. Gebrian's Framework Validation

| TDS Phase | FixedGoal Baseline     | Overlearning Term            | Research Basis                                     |
| --------- | ---------------------- | ---------------------------- | -------------------------------------------------- |
| 0-40%     | 6 correct repetitions  | ceil(0.5 × initial failures) | Avoid cognitive overload while enforcing minimum 6 |
| 40-70%    | 7 correct repetitions  | ceil(0.5 × initial failures) | Deliberate practice phase (Ericsson, 1993)         |
| 70-85%    | 8 correct repetitions  | ceil(0.5 × initial failures) | Automaticity development (Fitts & Posner, 1967)    |
| 85-95%    | 9 correct repetitions  | ceil(0.5 × initial failures) | Consolidation phase (Schmidt & Lee, 2011)          |
| 95-100%   | 10 correct repetitions | ceil(0.5 × initial failures) | Overlearning for stress resilience (Gebrian, 2013) |

**Conclusie:** De FixedGoal baselines zijn empirisch gegrond, terwijl de overlearning-term (50% van de startfouten, afgerond omhoog) het wetenschappelijke kader van Dr. Gebrian operationaliseert.

---

## Best Practices

### Voor Nieuwe Gebruikers

1. ✅ **Enable Module:** Krijg structured guidance
2. ✅ **Follow OLQ targets:** Bouw systematic progress
3. ✅ **Track TDS progression:** Monitor learning phases

### Voor Ervaren Muzikanten

1. ✅ **Disable Module:** Gebruik eigen judgement
2. ✅ **Fixed duration:** Standaard sessies (bijv. 15 min)
3. ✅ **Self-directed intensity:** Eigen intensiteit bepalen

### Voor Teachers

1. ✅ **Enable for students:** Provide structure
2. ✅ **Monitor TDS reports:** Track student progress
3. ✅ **Adjust OLQ targets:** Personalize when needed
