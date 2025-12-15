# Incomplete Session Handling

## Probleem

Gebruikers kunnen soms lang worstelen met een moeilijke passage (10+ minuten) zonder ook maar één succesvolle herhaling te behalen. In de oude implementatie was het onmogelijk om zo'n sessie op te slaan als `correctRepetitions` op 0 bleef staan. Dit was frustrerend omdat:

1. De tijd en moeite wel degelijk waardevol is (exposure/spacing effect)
2. De gebruiker wil de sessie kunnen documenteren
3. Het is beter om te stoppen en later opnieuw te proberen met een frisse geest
4. De sectie was misschien de vorige dag wel gelukt, maar vandaag niet

## Oplossing (Vanaf November 2025)

### Validatie Aanpassingen

**Oude logica** (geblokkeerd):

```javascript
if (totalAttempts === 0) {
  alert("No practice activity detected!");
  return; // BLOCKED - user couldn't save
}
```

**Nieuwe logica** (toegestaan):

```javascript
const hasMinimalActivity = totalAttempts > 0 || totalElapsedSeconds >= 120; // 2+ minutes

if (!hasMinimalActivity) {
  alert("You must either record attempts OR practice for at least 2 minutes");
  return;
}
```

### Session Outcome Tracking

De `sessionOutcome` property wordt nu toegevoegd aan elke practice session:

```javascript
let sessionOutcome;
if (correctRepetitions === 0 && totalElapsedSeconds >= 120) {
  sessionOutcome = "Incomplete"; // Struggled but didn't succeed
} else if (correctRepetitions >= targetRepetitions) {
  sessionOutcome = "TargetReached";
} else {
  sessionOutcome = "PartialProgress";
}
```

**Mogelijke waarden:**

- `'TargetReached'` - Target aantal herhalingen behaald
- `'PartialProgress'` - Enkele successen, maar target niet bereikt
- `'Incomplete'` - 0 correcte herhalingen na 2+ minuten oefenen
- `'FreePractice'` - Vrije oefensessie (geen specifieke sectie)
- `'TargetNotReached'` - (C# desktop only, legacy)

### Interval Berekening voor Incomplete Sessions

**Speciale behandeling:**

```javascript
if (session.sessionOutcome === "Incomplete") {
  // Fixed 1-day interval regardless of stage
  intervalDays = 1.0;
  console.log("Fixed 1-day interval (passage too difficult, retry tomorrow)");
}
```

**Rationale:**

- **Kort interval (1 dag)**: Gebruiker moet snel opnieuw kunnen proberen
- **Geen Ebbinghaus formule**: Normale retention curves gelden niet voor onsuccesvolle sessies
- **Rust periode**: 1 dag geeft herstel en mentale reset
- **Geen stage penalty**: Stage incrementeert NIET bij incomplete sessions

### Stage Progression Logica

```javascript
// Only increment stage if NOT incomplete AND target reached
if (
  session.sessionOutcome !== "Incomplete" &&
  currentSection.completedRepetitions >= currentSection.targetRepetitions
) {
  currentSection.practiceScheduleStage += 1;
  currentSection.completedRepetitions = 0;
}
```

**Belangrijke regel:** Incomplete sessies blokkeren stage advancement. Gebruiker moet eerst slagen.

### UI Feedback

**Evaluation Modal Waarschuwing:**

```javascript
if (correctRepetitions === 0 && totalElapsedSeconds >= 120) {
  // Show orange warning banner:
  // "⚠️ Passage te moeilijk?"
  // "Je hebt X minuten geoefend zonder succesvolle herhalingen..."
  // "...morgen (1 dag) opnieuw de kans om te proberen."
  // "Geen stage penalty"
}
```

**Tooltip bij Correct Repetitions:**

> "TIP: It's OK if this stays at 0 when a passage is too difficult - you can still save the session after 2+ minutes, and you'll get another chance tomorrow without stage penalty."

## Data Structuur

### Practice Session Object

```javascript
{
    id: "uuid",
    sectionId: "uuid",
    date: "2025-11-27T10:00:00.000Z",
    durationSeconds: 600, // 10 minutes
    correctRepetitions: 0, // ← Can now be 0!
    failedAttempts: 2,
    streakResets: 3,
    performance: "Poor",
    sessionOutcome: "Incomplete", // ← NEW
    targetRepetitions: 10,
    notes: "Too difficult today, will try again tomorrow"
}
```

### Bar Section Update

```javascript
// completedRepetitions += 0 (no change)
// practiceScheduleStage = 6 (no increment)
// nextReviewDate = tomorrow (1 day later)
```

## Adaptive Learning Impact

### Memory Stability Manager

Incomplete sessions **worden wel** doorgegeven aan `MemoryStabilityManager.updateAfterPractice()`:

```javascript
memoryStabilityManager.updateAfterPractice(
  currentSection.id,
  performanceScore, // 2.5 for "Poor"
  totalElapsedSeconds
);
```

**Effect op Stability (S):**

- Exposure time wordt meegewogen (spacing effect)
- Lage performance score → hogere difficulty (D)
- S kan toenemen ondanks 0 successen (SM-17+ principe)

**Zie logging voorbeeld:**

```
stability: 0.673 → 1.368 (+103%)
difficulty: 0.25 → 0.35 (+40%)
reviewCount: 1 → 2
```

### Personalized Memory Calibration (PMC)

Incomplete sessions tellen mee voor calibratie:

- Verhogen session count
- Beïnvloeden difficulty-specific tau adjustments
- Helpen bij learning van individuele forgetting curves

**Waarschuwing:** Te veel incomplete sessions kunnen PMC "difficult" factor verhogen:

```javascript
difficultyAdjustments: {
    difficult: {
        factor: 1.14, // τ multiplier verhoogd
        confidence: 0.30,
        sessions: 6
    }
}
```

## Gebruik Scenario's

### Scenario 1: Passage Lukt Niet (Eerste Keer)

```
Dag 1: Stage 2, target 10 reps
  - Oefent 13 minuten
  - 0 succesvolle herhalingen
  - Selecteert "Poor"
  → Outcome: Incomplete
  → Interval: 1 dag
  → Stage blijft 2

Dag 2: Stage 2, target 10 reps (opnieuw proberen)
  - Oefent 7 minuten
  - 10 succesvolle herhalingen
  - Selecteert "Good"
  → Outcome: TargetReached
  → Interval: Ebbinghaus (bijv. 2 dagen)
  → Stage → 3
```

### Scenario 2: Passage Lukte Gisteren, Vandaag Niet

```
Dag 1: Stage 5, target 6 reps
  - 6 reps, "Excellent"
  → Stage → 6, interval 7 dagen

Dag 8: Stage 6, target 9 reps
  - Oefent 10 minuten
  - 0 succesvolle herhalingen (off day)
  - Selecteert "Poor"
  → Outcome: Incomplete
  → Interval: 1 dag
  → Stage blijft 6 (geen penalty)

Dag 9: Stage 6, target 9 reps
  - 9 reps, "Good"
  → Stage → 7, interval 5 dagen
```

### Scenario 3: Consistent Te Moeilijk

```
Dag 1-5: Meerdere incomplete sessions
  → PMC leert: deze difficulty level is te hoog
  → τ multiplier verhoogt automatisch
  → Toekomstige intervals worden korter
  → Stability (S) stijgt geleidelijk door exposure
```

**Aanbeveling voor gebruiker:** Verlaag target repetitions handmatig of splits sectie in kleinere delen.

## Logging Voorbeeld

```
2025-11-27T11:03:23.729Z [INFO] Section updated after practice {
    sectionId: "76c44cd0-...",
    prevStage: 2,
    newCompleted: 0,
    targetReps: 10,
    stageAfter: 2, // NO INCREMENT
    intervalDays: 1,
    nextReview: "2025-11-28T11:03:23.728Z",
    performance: "Poor",
    sessionOutcome: "Incomplete"
}

2025-11-27T11:03:23.733Z [DEBUG] AdaptiveLearning Details {
    memoryStats: {
        stability: 1.368,
        difficulty: 0.35, // Increased
        currentRetrievability: 0.9999,
        reviewCount: 2
    }
}
```

## Technische Implementatie

### Web App (JavaScript)

**Bestanden gewijzigd:**

- `moduspractica-practice-session.js`
  - `saveEvaluation()` - Validatie + outcome detection
  - `updateSectionAfterPractice()` - Incomplete interval + stage logic
- `moduspractica-practice-session.html`
  - Tooltip update bij "Correct Repetitions"

### Desktop App (C# WPF)

**Status:** C# code ondersteunt al `SessionOutcome` property.

**Te updaten:**

- `PracticeSessionWindow.xaml.cs`
  - `SaveSessionAndClose()` methode
  - Voeg "Incomplete" outcome detection toe
- `EbbinghausExtensions.cs`
  - `CalculateNextIntervalAfterSession()` methode
  - Speciale case voor `SessionOutcome == "Incomplete"`

**Implementatie voorbeeld:**

```csharp
// In PracticeSessionWindow.xaml.cs:
string outcome = "TargetReached";
if (_completedRepetitions == 0 && _duration.TotalMinutes >= 2) {
    outcome = "Incomplete";
} else if (_completedRepetitions >= _targetRepetitions) {
    outcome = "TargetReached";
} else {
    outcome = "PartialProgress";
}

// In EbbinghausExtensions.cs:
if (session.SessionOutcome == "Incomplete") {
    return 1.0; // Fixed 1-day interval
}
```

## Best Practices

### Voor Gebruikers

1. **Stop op tijd**: Als je 10+ minuten oefent zonder vooruitgang → stop en probeer morgen
2. **Gebruik Failed Attempts**: Klik op de counters om struggles te documenteren
3. **Eerlijke evaluatie**: Selecteer "Poor" bij incomplete sessions voor accurate adaptieve learning
4. **Overweeg target verlagen**: Als consistent incomplete → verlaag target reps handmatig

### Voor Developers

1. **Altijd check sessionOutcome**: Gebruik in alle logica die stage/interval berekent
2. **Log incomplete sessions apart**: Voor analyse en debugging
3. **Test edge cases**: 0 reps + 0 tijd (blocked), 0 reps + 2 min (allowed)
4. **Monitor PMC impact**: Veel incomplete sessions → difficulty adjustment monitoring

## Toekomstige Verbeteringen

### Automatische Target Adjustment

Als gebruiker 3+ incomplete sessions heeft voor dezelfde sectie:

```javascript
if (incompleteCount >= 3) {
    currentSection.targetRepetitions = Math.max(3, currentSection.targetRepetitions - 2);
    console.log(`Auto-lowered target from ${old} to ${new} after ${incompleteCount} incomplete sessions`);
}
```

### Rescue Mechanism voor Hoge Stages

Als section in stage 6+ zit met meerdere incomplete sessions:

```javascript
if (stage >= 6 && incompleteCount >= 2) {
  // Optie 1: Stage terugzetten naar 4
  // Optie 2: Target verlagen naar 5
  // Optie 3: Passage splitsen suggestie
}
```

### UI Indicatie in Dashboard

Laat sections met recente incomplete sessions zien met speciaal icoon:

```javascript
if (lastSession.sessionOutcome === "Incomplete") {
  icon = "⚠️"; // Warning indicator
  tooltip = "Last attempt was incomplete - try again today";
}
```

## Referenties

- **Issue identificatie**: Logging analyse 27 november 2025
- **Geïmplementeerd**: 27 november 2025
- **Getest met**: Section "Sad at Heart - 01-01 BH" (Stage 6, 0/9 reps, 13+ minuten)
- **Relates to**: `DEPLOYMENT.md`, `copilot-instructions.md`
