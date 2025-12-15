# Interleaved Practice Feature - Implementation Summary

## Overzicht

De **Interleaved Practice** mode is succesvol geïmplementeerd voor ModusPractica. Deze functie stelt gebruikers in staat om alle chunks die ze vandaag al hebben geoefend te reviewen in een gerandomiseerde circuit voor verbeterde retentie.

## Pedagogische Basis

**Interleaved Practice** (afgewisselde herhaling) is een wetenschappelijk bewezen leermethode waarbij materiaal in een gerandomiseerde volgorde wordt herhaald in plaats van in één blok. Dit bevordert:

- **Betere langetermijnretentie** - Het brein moet actief schakelen tussen verschillende concepten
- **Verbeterde discriminatie** - Je leert subtiele verschillen tussen stukken beter te herkennen
- **Transfer van vaardigheden** - Betere toepassing in nieuwe contexten

## Geïmplementeerde Bestanden

### Nieuwe Bestanden

1. **`moduspractica/InterleavedSessionManager.js`** (nieuwe class)
   - Beheert de circuit queue met shuffle algoritme
   - Houdt circuit resultaten bij per section
   - Handelt data persistentie naar localStorage
   - Integreert met Memory Stability Manager voor retrieval practice tracking

### Aangepaste Bestanden

1. **`moduspractica/moduspractica-dashboard.html`**

   - Toegevoegd: Sidebar navigatie-item "🔀 Interleaved Practice"

2. **`moduspractica/moduspractica-dashboard.js`**

   - Nieuwe functie: `startDailyInterleavedReview()`
   - Valideert minimaal 2 unieke chunks vandaag geoefend
   - Extraheert unieke section IDs uit practiceHistory
   - Slaat queue op in sessionStorage

3. **`moduspractica/moduspractica-practice-session.html`**

   - Script tag toegevoegd voor InterleavedSessionManager.js

4. **`moduspractica/moduspractica-practice-session.js`**
   - Detectie van `mode=interleaved` URL parameter
   - Nieuwe functie: `loadInterleavedSession()`
   - Nieuwe functie: `renderInterleavedSessionInfo()`
   - Nieuwe functie: `adaptUIForInterleavedMode()`
   - Nieuwe functie: `moveToNextInterleavedChunk()`
   - Nieuwe functie: `finishInterleavedCircuit()`
   - Timer aangepast: countdown mode voor interleaved practice (3 min per chunk)
   - Cancel button aangepast: volledige circuit annuleren met confirmatie

## Functionaliteit

### Gebruikersflow

1. **Start**: Gebruiker klikt op "🔀 Interleaved Practice" in dashboard sidebar
2. **Validatie**: Systeem controleert of minimaal 2 unieke chunks vandaag zijn geoefend
3. **Queue maken**: Unieke section IDs worden gerandomiseerd en opgeslagen in sessionStorage
4. **Circuit start**: Redirect naar practice session met `mode=interleaved` parameter
5. **Per chunk**:
   - Countdown timer (3 minuten standaard, aanpasbaar)
   - Vereenvoudigde UI (geen Intensity Module, geen overlearning targets)
   - "Next Chunk ⏭️" button om door te gaan
   - Resultaten worden automatisch opgeslagen per chunk
6. **Afsluiten**:
   - "Finish Circuit 🏁" button slaat alle resultaten op
   - Elk chunk krijgt een PracticeHistory entry met `sessionType: 'interleaved'`
   - Memory Stability wordt bijgewerkt voor elke chunk (telt als retrieval practice)

### UI Aanpassingen in Interleaved Mode

- **Header**: Toont "Interleaved Circuit - Chunk X of Y"
- **Timer**: Countdown mode (3 minuten per chunk)
- **Verborgen elementen**:
  - Intensity Module display
  - Target Repetitions controls
  - Overlearning Intensity toggle
- **Aangepaste buttons**:
  - "Complete Session" → "Next Chunk ⏭️"
  - Nieuwe button: "Finish Circuit 🏁"

### Data Structuur

#### SessionStorage

```javascript
// Key: 'mp_interleaved_queue'
["section-id-1", "section-id-2", "section-id-3"];
```

#### PracticeHistory Entry

```javascript
{
  id: "guid",
  date: "2025-12-03T...",
  musicPieceId: "piece-id",
  barSectionId: "section-id",
  repetitions: 5,
  memoryFailures: 0,          // Interleaved mode doesn't track streak resets
  executionFailures: 3,
  durationMinutes: 2.5,
  notes: "...",
  sessionType: "interleaved",  // Tag voor analytics
  isDeleted: false
}
```

## Technische Details

### InterleavedSessionManager Class

**Constructor Parameters**:

- `profileData` - Volledige profiel data (musicPieces, practiceHistory)
- `currentProfile` - Actief profiel object
- `storagePrefix` - localStorage prefix (standaard `'mp_'`)

**Key Methods**:

- `shuffleQueue()` - Fisher-Yates shuffle voor randomisatie
- `getCurrentSection()` - Haalt huidige piece + section op
- `saveCurrentResults(data)` - Accumuleert resultaten per chunk
- `nextChunk()` - Beweegt naar volgende chunk in circuit
- `finishCircuit()` - Slaat alle resultaten op naar practiceHistory
- `saveProfileData()` - Persists data met QuotaManager

**State Tracking**:

```javascript
circuitResults = {
  "section-id-1": {
    repetitions: 8,
    failures: 2,
    durationSeconds: 150,
    notes: "Tempo verbeterd",
    attempts: 1,
  },
  // ... meer sections
};
```

### Timer Logica

**Normal Mode** (count-up):

```javascript
totalElapsedSeconds = Math.floor(elapsed / 1000);
```

**Interleaved Mode** (countdown):

```javascript
totalElapsedSeconds = Math.max(
  0,
  defaultSlotDuration - Math.floor(elapsed / 1000)
);
```

Bij 0 seconden: Timer pauzeert automatisch met alert.

## Testing Checklist

### Pre-Condities

- [x] Server draait op localhost:8080
- [ ] Profiel heeft minimaal 2 pieces met meerdere chunks
- [ ] Vandaag al minimaal 2 verschillende chunks geoefend (normale sessies)

### Test Scenario's

#### 1. Start Interleaved Practice (Happy Path)

- [ ] Klik op "🔀 Interleaved Practice" in sidebar
- [ ] Validatie: minimaal 2 unieke chunks vereist (alert als niet voldaan)
- [ ] Redirect naar practice-session.html?mode=interleaved
- [ ] UI toont "Interleaved Circuit - Chunk 1 of X"
- [ ] Timer toont countdown (03:00:00)
- [ ] Intensity Module is verborgen
- [ ] Target Reps controls zijn verborgen
- [ ] "Next Chunk ⏭️" button aanwezig
- [ ] "Finish Circuit 🏁" button aanwezig

#### 2. Circuit Navigatie

- [ ] Start timer
- [ ] Countdown loopt correct af
- [ ] Bij 00:00:00: Timer pauzeert + alert verschijnt
- [ ] Voeg enkele failed attempts / correct reps toe
- [ ] Klik "Next Chunk ⏭️"
- [ ] Chunk data wordt opgeslagen (console log controle)
- [ ] Volgende chunk laadt
- [ ] Metrics zijn gereset (0 failures, 0 reps)
- [ ] Timer reset naar 03:00:00

#### 3. Circuit Voltooien

- [ ] Oefen meerdere chunks
- [ ] Klik "Finish Circuit 🏁"
- [ ] Confirmatie alert verschijnt met aantal chunks
- [ ] Console toont "✅ Saved X interleaved practice entries"
- [ ] Redirect naar dashboard
- [ ] Dashboard toont bijgewerkte statistics

#### 4. Data Persistentie

- [ ] Open localStorage in DevTools
- [ ] Check `mp_[profileId]_data`
- [ ] `practiceHistory` bevat nieuwe entries met `sessionType: 'interleaved'`
- [ ] Elke chunk heeft correcte `repetitions`, `failures`, `durationMinutes`
- [ ] `statistics.totalSessions` is verhoogd

#### 5. Memory Stability Update

- [ ] Open console
- [ ] Run `showMemoryStats('section-id')` voor geoefende chunk
- [ ] Stability (S) en Difficulty (D) zijn bijgewerkt
- [ ] Review count is verhoogd

#### 6. Edge Cases

- [ ] Start interleaved practice zonder voldoende chunks vandaag → Alert
- [ ] Annuleer circuit halverwege → Confirmatie + data wordt niet opgeslagen
- [ ] Browser refresh tijdens circuit → beforeunload warning (normale flow)
- [ ] Voltooi circuit met 0 reps op alle chunks → Data wordt toch opgeslagen

#### 7. Analytics Tracking

- [ ] GA4 event: `interleaved_practice_started` (bij start)
- [ ] GA4 event: `interleaved_circuit_completed` (bij finish)
- [ ] GA4 event: `interleaved_circuit_cancelled` (bij cancel)

## Mogelijke Uitbreidingen (Toekomst)

1. **Configureerbare Timer**

   - Gebruiker kan countdown tijd per chunk aanpassen (1-10 min)
   - Opslaan in profiel settings

2. **Circuit Types**

   - "Quick Review" (1 min per chunk)
   - "Deep Review" (5 min per chunk)
   - "Custom Circuit" (gebruiker selecteert specifieke chunks)

3. **Progress Tracking**

   - Visuele progress bar voor circuit voltooiing
   - Kleur-gecodeerde chunks (groen = voltooid, geel = in uitvoering)

4. **Spaced Repetition Integration**

   - Interleaved circuits gebaseerd op Ebbinghaus intervals
   - "Suggested Circuit" op basis van due dates

5. **Performance Analytics**
   - Grafiek: Interleaved vs. Normal practice retentie
   - Success rate vergelijking per chunk type

## Bestandslocaties

```
moduspractica/
├── InterleavedSessionManager.js          (NIEUW)
├── moduspractica-dashboard.html          (AANGEPAST)
├── moduspractica-dashboard.js            (AANGEPAST)
├── moduspractica-practice-session.html   (AANGEPAST)
└── moduspractica-practice-session.js     (AANGEPAST)
```

## Console Debug Commands

```javascript
// Bekijk huidige interleaved queue
JSON.parse(sessionStorage.getItem("mp_interleaved_queue"));

// Bekijk circuit resultaten (tijdens sessie)
interleavedManager.circuitResults;

// Bekijk progress
interleavedManager.getProgress();

// Forceer shuffle
interleavedManager.shuffleQueue();
```

## Commit Message Suggestie

```
feat: Implement Interleaved Practice mode for enhanced retention

- Add InterleavedSessionManager class for circuit management
- Integrate Fisher-Yates shuffle for randomized review
- Implement countdown timer mode (3 min per chunk)
- Add circuit navigation (Next Chunk, Finish Circuit)
- Tag practice history entries with sessionType: 'interleaved'
- Update Memory Stability tracking for retrieval practice
- Hide Intensity Module in interleaved mode (not relevant for quick reviews)
- Add validation: require min 2 unique chunks practiced today
- Track GA4 events: circuit_started, circuit_completed, circuit_cancelled

Based on pedagogical principle: Interleaved practice improves
long-term retention compared to blocked practice.
```

## Verantwoording Design Beslissingen

1. **Waarom geen Intensity Module in Interleaved Mode?**

   - Interleaved practice is bedoeld voor **retrieval practice** (ophalen van kennis)
   - Intensity Module is bedoeld voor **deep learning** (nieuwe vaardigheden aanleren)
   - Mengen van beide zou verwarrend zijn en de focus verliezen

2. **Waarom 3 minuten countdown per chunk?**

   - Gebaseerd op research: effectieve retrieval practice is kort maar frequent
   - 3 minuten is genoeg om een chunk 2-3x te spelen
   - Voorkomt "analysis paralysis" (te lang blijven hangen op één chunk)

3. **Waarom alleen chunks van vandaag?**

   - Pedagogisch: interleaving werkt best met recentelijk geleerd materiaal
   - Praktisch: voorkomt overload (te veel chunks in circuit)
   - Gebruiker heeft verse context van eerdere oefensessies

4. **Waarom geen stage/interval updates in Interleaved Mode?**
   - Interleaved practice telt als **retrieval practice**, niet als scheduled review
   - Scheduling blijft gebaseerd op de normale Ebbinghaus agenda
   - Memory Stability wordt WEL bijgewerkt (tracking retentie, niet planning)

## Support & Troubleshooting

**Probleem**: "You need to practice at least 2 different chunks today" alert

- **Oplossing**: Oefen eerst 2 verschillende chunks via normale agenda

**Probleem**: Interleaved queue is leeg na refresh

- **Oorzaak**: sessionStorage wordt gewist bij browser sluiten
- **Oplossing**: Dit is bedoeld gedrag - circuits zijn per sessie

**Probleem**: Timer telt niet af in interleaved mode

- **Check**: Console errors voor InterleavedSessionManager instantie
- **Check**: `interleavedMode` variable is `true`

**Probleem**: Memory Stability niet bijgewerkt na circuit

- **Check**: Console logs voor "updateMemoryStability" calls
- **Check**: MemoryStabilityManager is geïnitialiseerd

---

**Implementatie voltooid**: 3 december 2025
**Versie**: ModusPractica v2.0.0
**Author**: Frank De Baere (via GitHub Copilot)
