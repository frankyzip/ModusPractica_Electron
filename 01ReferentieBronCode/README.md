# ModusPractica

> **BELANGRIJKE WIJZIGING – 2025-11-05**
>
> **Nieuwste updates:** Verbeterde autocomplete voor titels en componisten in NewMusicPieceWindow.
>
> **Eerdere updates (2025-11-04):** Nullable reference warnings opgelost voor betere null-safety en crash preventie.
>
> **Eerdere updates (2025-11-04):** Unit testing toegevoegd voor kernfunctionaliteit validatie.
>
> **Nieuwe features toegevoegd (2025-11-03):** Stage-aware MASTERED interval groei en volledig profielbeheer systeem.
>
> **ML.NET-onderdelen zijn volledig verwijderd** uit deze branch (feature/ebb-unify-clamps) om de complexiteit te verlagen en focus te leggen op de wetenschappelijk onderbouwde Ebbinghaus-gebaseerde adaptieve logica.
>
> **WAT IS VERWIJDERD:**
>
> - MLManager.cs en alle ML.NET integratie code
> - ML.NET NuGet packages en dependencies
> - ML model training en prediction pipeline
>
> **WAT BLIJFT ACTIEF:**
>
> - Ebbinghaus vergeetcurve berekeningen
> - Adaptieve τ management (AdaptiveTauManager)
> - Personalisatie via demographics (leeftijd, ervaring)
> - Sectie-specifieke memory stability tracking
> - Performance-gebaseerde interval aanpassingen
> - Alle retention diagnostics en feature flags
>
> **NIEUWE FEATURES:**
>
> - **Autocomplete Verbetering (2025-11-05):** Slimme filtering voor titel en componist velden
>   - Intelligente word-based matching (matcht op begin van elk woord)
>   - "Frederic Chopin" wordt gevonden met "F", "Fr", "C", "Ch", of "Chopin"
>   - Geen automatische tekst selectie meer (cursor blijft op juiste positie)
>   - Direct filteren vanaf eerste karakter (geen 3-karakter minimum meer)
>   - Incrementele lijst updates zonder gebruikersinvoer te wissen
>   - Dropdown blijft open tijdens typen voor betere gebruikerservaring
> - **Null-Safety (2025-11-04):** Alle kritieke nullable warnings opgelost
>   - 4 nullable reference warnings gefixed in CalendarWindow en DaySessionsWindow
>   - Explicit nullable types (`?`) voor betere compile-time safety
>   - Pattern matching voor veiligere type casts
>   - Van 899 naar ~15 warnings (alleen WPF style suggestions)
> - **Unit Testing (2025-11-04):** ModusPractica.Tests project met xUnit 2.5.3
>   - 5 unit tests voor CalculateAdjustedTau validatie
>   - Test coverage: Average difficulty, Mastered stages (3, 4, 5+), edge cases
>   - Alle tests slagen - valideren correcte tau-waarden
> - Stage-aware MASTERED interval groei (wetenschappelijk onderbouwd)
> - Volledig geïsoleerd multi-user profielbeheer
> - Custom data locatie ondersteuning (USB, network drives)
> - Rollback functionaliteit voor sessie annulering
> - Interleaved practice auto-generation
> - Lifecycle state management voor bar sections

> **UI UPDATES – 2025-11-03**
>
> - New button next to Overall Timer: "YouTube" quick link with context menu
>   - Left-click: opens the stored URL
>   - Right-click: "Set YouTube Link…" to store/replace the URL, or "Clear Link"
>   - Pastel yellow tooltip on hover with short instructions
>   - If no link is stored, clicking shows an info message with instructions (button remains enabled)
> - Profile Selector: clearer first-run flow
>   - Inline pastel yellow hint when no data location is selected
>   - New profile creation requires a data location (guard with message)
>   - All messages are in English
> - Wording in UI: Tab "Bar Sections" is now shown as "Chunk Sections"; first column header is "Chunk Sect." (bindings remain the same)
>
> **DOCUMENTATIE STATUS:**
> Alle secties hieronder over ML.NET integratie zijn **historisch archief** en niet langer van toepassing op de huidige code. Ze worden bewaard voor referentie en mogelijke toekomstige re-implementatie.

## Overzicht

ModusPractica is een WPF-applicatie gericht op gestructureerde oefensessies voor muziek (secties / maatnummers), met een adaptief geheugenmodel gebaseerd op de (uitgebreide) Ebbinghaus vergeetcurve. Het systeem combineert demografische personalisatie, herhalingsdynamiek, sectie-specifieke stabiliteit en prestatie-trends tot een geïntegreerde voorspelling van optimale herhaalintervallen.

## Kerncomponenten

- `EbbinghausConstants` – Normatieve parameters, clamp-beleid (τ & interval), retentiedoelen. **NIEUW:** Stage-aware MASTERED modifiers voor graduele interval groei.
- `AdaptiveTauManager` – Integreert meerdere bronnen (demografie, PersonalizedMemoryCalibration, MemoryStabilityManager, recente performance) tot één τ.
- `EbbinghausExtensions` – Koppeling + post-sessie updates (adaptieve multiplier, kalibratie, stabiliteit).
- `PracticeHistoryManager` – Bewaart oefensessies per sectie.
- `PersonalizedMemoryCalibration` – Leert individuele afgeleide τ-correcties over tijd.
- `MemoryStabilityManager` – Houdt stabiliteit (S) en moeilijkheid bij per sectie.
- `MLDebugWindow` – Visualisatie & debugging van retentiecurves.
- `DataPathProvider` – **NIEUW:** Centrale filesystem pad-beheer voor profiel-isolatie.
- `ProfileConfiguration` – **NIEUW:** Persistent profiel en data locatie configuratie.
- `SectionLifecycleService` – **NIEUW:** Business logic voor lifecycle state transitions (Active/Maintenance/Inactive).

## Nieuw: Profielbeheer Systeem (Multi-User Ondersteuning)

Volledig geïsoleerd multi-user systeem met custom data locaties voor privacy en flexibiliteit.

### Architectuur

**Data Isolation:**

```
CustomDataRoot/
├── Profiles/
│   ├── ProfileA/
│   │   ├── MusicPieces/     # JSON bestanden
│   │   ├── History/         # practice_history.json
│   │   ├── Logs/            # application_log.txt
│   │   ├── Scheduled/       # scheduled_sessions.json
│   │   ├── Calibration/     # personal_memory_calibration.json
│   │   └── ...
│   └── ProfileB/
│       └── ... (zelfde structuur)
```

### Functionaliteit

- **Profiel Selectie:** ProfileSelectorWindow bij opstart voor keuze profiel en data locatie
- **Custom Data Locaties:** Ondersteuning voor USB drives, network shares, etc.
- **Data Migratie:** Automatische migratie van bestaande data naar nieuwe locatie
- **Isolatie:** Geen cross-contamination tussen profielen
- **Persistentie:** Configuratie opgeslagen in `%LOCALAPPDATA%\ModusPractica\profile_config.json`

### Gebruik

1. Bij eerste start: ProfileSelectorWindow opent automatisch
2. Kies of maak profiel aan
3. Selecteer data locatie (default of custom)
4. Start applicatie met gekozen profiel

#### First-run UX Improvements (2025-11-03)

- Inline hint (pastel yellow) indicates when no data location is selected
- "New Profile" requires a chosen data location; otherwise a clear English warning is shown
- "Start" requires both a profile and a data location
- All dialogs/messages in this flow are in English

### Bestanden

- `ProfileSelectorWindow.xaml/.xaml.cs` – Hoofdingang voor profiel selectie
- `DataPathProvider.cs` – Centrale pad-beheer
- `ProfileConfiguration.cs` – Configuratie persistence
- `DataMigrationHelper.cs` – Migratie ondersteuning

## Unit Testing (2025-11-04)

Een testproject is toegevoegd om kernfunctionaliteit te valideren en regressies te voorkomen.

### Test Project Structuur

```
ModusPractica.Tests/
├── ModusPractica.Tests.csproj    # xUnit 2.5.3, .NET 8.0-windows
└── UnitTest1.cs                   # 5 unit tests voor CalculateAdjustedTau
```

### Test Coverage

**Stage-aware MASTERED tau berekening:**

- ✅ Average difficulty baseline (verwacht: 9.0 dagen)
- ✅ Mastered stage 3 conservative modifier (verwacht: 18.0 dagen)
- ✅ Mastered stage 5+ full modifier (verwacht: 31.5 dagen)
- ✅ Negatieve repetition count correctie
- ✅ Extreme repetition count clamping

### Tests Uitvoeren

```bash
cd ModusPractica.Tests
dotnet test
```

**Expected output:**

```
Test summary: total: 5; failed: 0; succeeded: 5; skipped: 0
```

### Gevalideerde Waarden

De tests valideren dat `CalculateAdjustedTau` correcte tau-waarden produceert:

| Difficulty | Stage | Repetitions | Formula         | Expected τ |
| ---------- | ----- | ----------- | --------------- | ---------- |
| Average    | 1     | 0           | 3.0 × 3.0 × 1.0 | 9.0 dagen  |
| Mastered   | 3     | 0           | 3.0 × 3.0 × 2.0 | 18.0 dagen |
| Mastered   | 5+    | 0           | 3.0 × 3.0 × 3.5 | 31.5 dagen |

**Waarom belangrijk:** Deze waarden worden door de hele planner gebruikt. Tests garanderen dat de stage-aware graduele groei correct functioneert en voorkomt onbedoelde wijzigingen.

### Project Configuratie

Het test project is uitgesloten van de main build via `.csproj` exclusion rules om build conflicts te voorkomen:

```xml
<ItemGroup>
  <Compile Remove="ModusPractica.Tests\**"/>
  <EmbeddedResource Remove="ModusPractica.Tests\**"/>
  <None Remove="ModusPractica.Tests\**"/>
  <Page Remove="ModusPractica.Tests\**"/>
</ItemGroup>
```

## Nieuw: Retentie Instrumentatie & Feature Flags (Soft Simplification)

Om complexiteit beheersbaar te houden én transparantie te maximaliseren, is een instrumentatielaag toegevoegd:

### Bestanden

- `RetentionFeatureFlags.cs` – Centrale feature toggles & loglimiter.
- `RetentionDiagnostics.cs` – Gestructureerde diagnostische logregels met prefix `[RETENTION_DIAG]`.

### Belangrijkste Flags

| Flag                        | Default | Beschrijving                                                           |
| --------------------------- | ------- | ---------------------------------------------------------------------- |
| `UseDemographics`           | true    | Activeer leeftijd/ervaring-baseline (v3.0: gender verwijderd)          |
| `UseRepetitionBonus`        | true    | Laat herhalingsdynamiek in τ toe (indirect via bestaande berekeningen) |
| `UseAdaptiveSystems`        | true    | Master switch voor alle adaptieve subsystemen                          |
| `UseMemoryStability`        | true    | Gebruik stabiliteitsdata (indirect via manager)                        |
| `UsePMC`                    | true    | PersonalizedMemoryCalibration gebruiken                                |
| `UsePerformanceTrend`       | true    | Prestatie-trend component meenemen                                     |
| `EnableDiagnosticLogging`   | true    | Schrijf `[RETENTION_DIAG]` regels                                      |
| `LimitDiagnosticPerSession` | 80      | Dagelijkse cap op diagnostische regels                                 |

Configureren (bijv. in `App.xaml.cs` Startup):

```csharp
RetentionFeatureFlags.Configure(
	useAdaptiveSystems: true,
	enableDiagnosticLogging: true,
	limitDiagnosticPerSession: 120
);
```

Counter resetten (bv. bij nieuwe oefen-dag):

```csharp
RetentionFeatureFlags.ResetDiagnosticCounter();
```

### Log Formaat

Eerste regel (header):

```
[RETENTION_DIAG_HEADER] Columns=Context,Section,Difficulty,Reps,BaseTauRaw,DiffMod,RepFactor,DemographicTau,PMC(τ|w),StabilityTau(w),PerfTau(w),AdaptiveConfidence,IntegratedTau,ClampedTau,NextInterval,TargetR*,PredictedR
```

Voorbeeld `TauCalc` regel:

```
[RETENTION_DIAG] TauCalc,7c0d1b4e-...,average,5,3.000,1.000,1.220,3.660,4.100|0.280,3.550|0.400,3.000|0.180,0.630,3.720,3.720,-,0.800,-
```

Betekenis (in volgorde):

1. Context (`TauCalc`, `SimpleTau`, `AdaptUpdate`)
2. Section GUID (of `-`)
3. Difficulty
4. Repetitions
5. `BaseTauRaw` (basis τ voordat modifiers samen zijn toegepast)
6. `DiffMod` (placeholder=1.000 indien niet gereconstrueerd)
7. `RepFactor` (idem; kan 1.000 blijven als niet los herberekend)
8. `DemographicTau` resultaat na demografische personalisatie
9. `PMC(τ|w)` PersonalizedMemoryCalibration bijdrage + gewicht
10. `StabilityTau(w)` Stabiliteit-gebaseerde τ + gewicht
11. `PerfTau(w)` Prestatie-afgeleide τ + gewicht
12. `AdaptiveConfidence` samengestelde betrouwbaarheid 0..1
13. `IntegratedTau` gewogen combinatie vóór clamping
14. `ClampedTau` na normatieve τ-clamp [1,180]
15. `NextInterval` (voor uitbreiding; nu vaak `-`)
16. `TargetR*` retentiedoel voor difficulty
17. `PredictedR` voorspelde retentie bij gekozen interval (indien later toegevoegd)

`SimpleTau` verschijnt wanneer adaptieve systemen uitstaan of er geen sectie-data is.

`AdaptUpdate` lijn voorbeeld:

```
[RETENTION_DIAG] AdaptUpdate,7c0d1b4e-...,-,-,-,-,-,-,-,-,-,-,-,-,-,-,- Perf=6.5 TauMult=1.120 Stability=3.40 Diff=0.180 Reviews=5
```

### Typische Gebruiksscenario’s

1. Debug: Schakel alles aan, bekijk `[RETENTION_DIAG]` output en valideer of τ groeit/krimpt verwacht.
2. Simplificatie: Zet `UseAdaptiveSystems=false` om te vergelijken tegen pure demografie.
3. Volume-control: Verlaag `LimitDiagnosticPerSession` naar 30 voor productie.
4. Analyse: Exporteer log, filter op `TauCalc` → CSV voor spreadsheet.

## v3.0 Demographic Personalization Corrections (Oktober 2025)

### Probleem Geïdentificeerd

Na grondige analyse van het demografische personalisatie-algoritme zijn twee fundamentele problemen ontdekt:

1. **Gender multipliers wetenschappelijk ondeugdelijk**: Effect size d=0.2-0.3 te klein voor praktische toepassing, muziek is hybride vaardigheid (motorisch + declaratief), geen solide wetenschappelijke basis voor gender-verschillen in muzikaal geheugen.

2. **Experience multipliers OMGEKEERD geïmplementeerd**: De originele v2.x implementatie gaf beginners _langere_ intervallen (×1.3) en professionals _kortere_ intervallen (×0.8). Dit is **tegengesteld** aan de encoding strength hypothesis (Bjork & Bjork, 2011) die stelt: sterke encoding → langzamer vergeten → langere optimale intervallen.

### Wetenschappelijke Onderbouwing v3.0

| Theorie                                                | Implicatie                                                                          | v2.x (FOUT)                               | v3.0 (CORRECT)                               |
| ------------------------------------------------------ | ----------------------------------------------------------------------------------- | ----------------------------------------- | -------------------------------------------- |
| **Encoding Strength Hypothesis** (Bjork & Bjork, 2011) | Experts encoderen chunks efficiënter → sterkere memory traces → langere intervallen | Beginner ×1.3<br>Professional ×0.8        | Beginner ×0.8<br>Professional ×1.3           |
| **Desirable Difficulties** (Schmidt & Bjork, 1992)     | Langere intervallen = moeilijkere retrieval = betere consolidatie (voor experts)    | Professionals kregen te korte intervallen | Professionals krijgen nu langste intervallen |
| **Gender in Musical Memory**                           | Effect size d=0.2-0.3 in verbaal geheugen, NIET muziek-specifiek                    | Male ×0.9<br>Female ×1.1                  | **VERWIJDERD**                               |

### Implementatie Details

**Aangepaste Bestanden:**

- `EbbinghausConstants.cs`:
  - `GetGenderAdjustedTau()` **volledig verwijderd**
  - `GetExperienceAdjustedTau()` multipliers **omgekeerd**
  - `GetPersonalizedBaseTau()` en `CalculateAdjustedTau()` signatures: gender parameter verwijderd
- `SettingsManager.cs`: `ProfessionalTauMultiplier = 1.3` toegevoegd
- `AdaptiveTauManager.cs`: Gender parameter verwijderd uit alle methode signatures
- `EbbinghausExtensions.cs`: `CalculateEnhancedTau()` geen gender meer
- `PracticeSessionData.cs`: Gender parameter verwijderd uit tau-berekeningen
- `SettingsWindow.xaml/.cs`: Gender UI **volledig verwijderd** (niet meer zichtbaar voor gebruiker)

**Experience Multipliers v3.0:**

| Niveau       | v2.x (FOUT) | v3.0 (CORRECT) | Rationale                                                              |
| ------------ | ----------- | -------------- | ---------------------------------------------------------------------- |
| Beginner     | ×1.3        | ×0.8           | **40% kortere intervallen** - zwakkere encoding, vaker herhalen nodig  |
| Intermediate | ×1.1        | ×1.0           | Baseline                                                               |
| Advanced     | ×1.0        | ×1.1           | Iets langere intervallen - sterkere encoding                           |
| Professional | ×0.8        | ×1.3           | **63% langere intervallen** - sterkste encoding + desirable difficulty |

**Backward Compatibility:**

- `Gender` property blijft bestaan in `SettingsManager` voor oude JSON files
- Waarde wordt **niet meer gebruikt** in berekeningen
- Oude settings files blijven laden zonder crashes

### Impact op Gebruikers

**Beginners:**

- Zien nu **kortere intervallen** tussen oefensessies (bijv. 3.9d → 2.4d)
- Meer frequent oefenen past beter bij zwakkere memory traces
- Snellere progressie door hogere repetitiefrequentie

**Professionals:**

- Zien nu **langere intervallen** tussen oefensessies (bijv. 2.4d → 3.9d)
- Profiteert van "desirable difficulty" principe
- Efficiënter gebruik van oefentijd door grotere spacing

**Settings UI:**

- Gender keuze **verwijderd** (was toch niet wetenschappelijk onderbouwd)
- Alleen Age en Experience blijven zichtbaar
- UI is nu consistent met wat daadwerkelijk gebruikt wordt

### Bekende Verbeteringspunten (Next Steps)

- (Optioneel) Baseline component logging in `EbbinghausConstants.CalculateAdjustedTau` (open item).
- Invullen `NextInterval`, `TargetR*`, `PredictedR` tijdens daadwerkelijke planning fase.
- UI toggles in `SettingsWindow` voor snelle gebruikerstoegang.
- Aggregatie-statistiek: gemiddelde afwijking tussen voorspelde en gemeten performance -> feedback lus.

---

## ⚠️ ARCHIEF SECTIE - ML.NET INTEGRATIE (VERWIJDERD)

**De volgende sectie beschrijft ML.NET functionaliteit die NIET MEER AANWEZIG is in de huidige codebase.**

---

## ML-Planner Integratie (Feature/ebb-unify-clamps - Oktober 2025)

### ⚠️ **GEARCHIVEERD - Deze functionaliteit is verwijderd in oktober 2025**

### Overzicht

De ML.NET SDCA regressiemodel training was al geïmplementeerd, maar de voorspellingen werden **niet gebruikt** in de praktijk planner. De `PredictNextPracticeDate` methode in `PracticeSessionData.cs` bevatte slechts een stub die `section.Interval` teruggaf. Deze integratie activeert het ML model volledig en sluit alle feature gaps.

### Geïmplementeerde Verbeteringen

**1. Volledige ML Predictie Pipeline**

- `PredictNextPracticeDate()` stub vervangen door complete implementatie (210 regels)
- 21 features geconstrueerd identiek aan training pipeline
- ML.NET prediction engine gebruikt voor daadwerkelijke voorspellingen
- Centrale clamps toegepast via `EbbinghausConstants.ClampIntervalToScientificBounds`
- 3 fallback niveaus geïmplementeerd voor robuustheid

**2. Kwaliteitsfeatures Toegevoegd**

- `TotalFailures` toegevoegd aan ML pipeline (training + predictie)
- `SuccessRatio` toegevoegd aan ML pipeline (training + predictie)
- Features nu volledig consistent tussen training en productie

**3. LoadColumn Conflict Opgelost**

- `RepetitionsPerformanceInteraction` index gewijzigd van `LoadColumn(18)` naar `LoadColumn(20)`
- Voorkomt dubbele index met `MissedSessionsCount`

**4. Verbeterde Observability**

- **Schedule Start Logging**: Model status, R², MAE, globalMLWeight, training datapoints
- **Skip Reason Diagnostics**: Waarom ML wordt overgeslagen (ml_weight_low, history_insufficient, scheduler_null)
- **Per-Section Blend Logging**: Toont algorithmicInterval, mlInterval, globalMLWeight, finalInterval, tau per sectie

**5. Unit Test Coverage**

- Nieuw test project: `ModusPractica.Tests` (xUnit 2.9.2)
- 19 tests verdeeld over 3 test classes:
  - `MLPredictionIntegrationTests`: Gating, clamps, retention targets (8 tests)
  - `MLFeatureConsistencyTests`: Feature list guards (4 tests, waarborgt 21 features)
  - `DateHelperTests`: Datum berekeningen (3 tests)
- **Alle tests slagen** (19/19 passed in 0.8s)

### ML Model Gating Policy

### ⚠️ **GEARCHIVEERD - Deze policy was onderdeel van de verwijderde ML.NET integratie**

Het ML model werd **alleen gebruikt** wanneer aan beide voorwaarden is voldaan:

| Voorwaarde         | Drempelwaarde | Actie bij niet voldoen                  |
| ------------------ | ------------- | --------------------------------------- |
| Usable Datapoints  | ≥ 200         | globalMLWeight = 0.0 (100% algorithmic) |
| Model Quality (R²) | ≥ 0.20        | globalMLWeight = 0.0 (100% algorithmic) |

**Blending Formula** (wanneer ML actief is):

```
finalInterval = mlInterval × globalMLWeight + algorithmicInterval × (1 - globalMLWeight)
```

Waarbij `globalMLWeight` berekend wordt via sigmoid × quality factor (zie `ComputeDynamicMLWeight`).

### Fallback Mechanismen

**5 Veiligheidsniveaus:**

1. **Global Weight Gating**: Als R² < 0.20 of < 200 datapoints → globalMLWeight = 0.0 → 100% algorithmic
2. **Per-Section History Check**: Als sectie < 2 practice sessies → skip ML, gebruik algorithmic
3. **Model Null Check**: Als ML model niet geladen → fallback naar `section.Interval`
4. **Exception Handling**: Bij ML predictie fouten → fallback naar `section.Interval`
5. **Blending Veiligheidsnet**: ML prediction wordt **gemengd** met algorithmic, nooit 100% ML (tenzij weight = 1.0)

**Voorbeeld Log Output bij Slechte ML Prestatie:**

```
[MLSchedule] Model R²=0.15 < MinR² 0.20 → globalMLWeight=0.0 (100% algorithmic fallback)
[MLSchedule] SKIP ML section=SectionName reason=ml_weight_low (weight=0.00)
```

### Centrale Clamps

Alle intervallen (zowel ML als algorithmic) worden begrensd via **één centrale methode**:

**`EbbinghausConstants.ClampIntervalToScientificBounds()`**

| Regel              | Limiet           | Rationale                                  |
| ------------------ | ---------------- | ------------------------------------------ |
| Minimum            | 1 dag            | Minder dan dagelijkse oefening niet zinvol |
| Maximum            | 365 dagen        | Jaarlijkse review grens                    |
| Tau-gebaseerd      | ≤ 5 × τ          | Behoud retentie > 40% (wetenschappelijk)   |
| NaN/Infinity Check | → 1 dag fallback | Numerieke stabiliteit                      |

**Geen lokale caps** elders in de codebase → single source of truth.

### Feature List (21 Features)

De ML model gebruikt 21 features die zowel in training als predictie **identiek** geconstrueerd worden:

**Basis (10):**

1. SectionDifficulty
2. CurrentInterval
3. Tau
4. RepetitionNumber
5. AveragePerformance
6. ConsecutiveFailures
7. TimeSinceLastPractice
8. TotalSuccesses
9. **TotalFailures** ⬅️ **NIEUW**
10. **SuccessRatio** ⬅️ **NIEUW**

**Trends (3):** 11. PerformanceTrend 12. PerformanceStdDev 13. MissedSessionsCount

**Interacties (6):** 14. DifficultyPerformanceInteraction 15. IntervalPerformanceInteraction 16. TauPerformanceInteraction 17. IntervalTauInteraction 18. DifficultyIntervalInteraction 19. **RepetitionsPerformanceInteraction** ⬅️ LoadColumn(20) gefixed

**Tijdsfeatures (2):** 20. CurrentIntervalNormalized 21. TimeSinceLastPracticeNormalized

### Test Resultaten

**Build Status:** ✅ Succesvol (Release & Debug)

```
Build succeeded in 7.5s
  0 Warning(s)
  0 Error(s)
```

**Test Status:** ✅ 19/19 Passed (0.8s)

```
Test summary: total: 19, failed: 0, succeeded: 19, skipped: 0, duration: 0.8s
```

**Coverage:**

- ✅ Gating logic (R² threshold, datapoint minimum)
- ✅ Central clamps ([1,365]d, ≤5×τ)
- ✅ Feature consistency (21 features guard)
- ✅ Fallback mechanisms (model null, exception handling)
- ✅ DateHelper utilities (zero interval, normalization, date calculation)

### Bestanden Gewijzigd

| Bestand                       | Regels | Wijzigingen                                                                |
| ----------------------------- | ------ | -------------------------------------------------------------------------- |
| `PracticeSessionData.cs`      | ~250   | PredictNextPracticeDate implementatie, TotalFailures/SuccessRatio pipeline |
| `MLManager.cs`                | ~40    | Schedule start logging, skip reasons, blend logging                        |
| `ModusPractica.Tests/*.cs`    | ~600   | Nieuw test project met 19 unit tests                                       |
| `CHANGELOG_ML_Integration.md` | ~500   | Uitgebreide changelog met what/why/how secties                             |
| `IMPLEMENTATION_SUMMARY.md`   | ~600   | Technische implementatie details, code snippets, voorbeeld logs            |

### Gebruik in Productie

**Log Monitoring:**

```
[MLSchedule] Starting schedule generation: Model active, R²=0.45 MAE=0.8d, globalMLWeight=0.65, TrainingPoints=350
[MLSchedule] Section='Beethoven Op.109 mvt.1' algorithmicInterval=4.2d mlInterval=5.1d globalMLWeight=0.65 finalInterval=4.8d tau=3.2
```

**Verwacht Gedrag:**

- Bij < 200 sessies: 100% algorithmic (veilige fallback)
- Bij 200-500 sessies: Gradueel ML integratie (weight groeit met data)
- Bij > 500 sessies: Maximale ML invloed (weight ≈ 0.7-0.9 afhankelijk van R²)
- Bij R² daling < 0.20: Automatische terugval naar algorithmic

**Volgende Optimalisaties:**

- Hyperparameter tuning wanneer > 500 sessies beschikbaar
- A/B testing tussen pure algorithmic vs. ML-blended intervals
- Feature importance analyse voor dimensionaliteit reductie

### Documentatie

Voor volledige technische details:

- **CHANGELOG_ML_Integration.md**: Chronologische wijzigingsgeschiedenis
- **IMPLEMENTATION_SUMMARY.md**: Code-level implementatie details met voorbeelden
- **Unit Tests**: `ModusPractica.Tests/MLPredictionIntegrationTests.cs` voor usage patterns

---

## ⚠️ ARCHIEF SECTIE - PRACTICESCHED ULER CONSOLIDATIE (VERWIJDERD MET ML.NET)

**De volgende sectie beschrijft consolidatie van MLManager componenten die NIET MEER AANWEZIG zijn.**

---

## PracticeScheduler Instance Consolidatie (Feature/ebb-unify-clamps - Oktober 2025)

### ⚠️ **GEARCHIVEERD - MLManager.cs en PracticeScheduler zijn verwijderd**

### Probleem Geïdentificeerd

In `MLManager` bestonden twee scheduler instances:

- `_scheduler` (readonly, gecreëerd in constructor, ~8 referenties)
- `_practiceScheduler` (niet-readonly, inline init, 2 referenties alleen voor InitializeModel)

**Gevolg**: Het ML-model werd soms op de ene instance geladen (`_practiceScheduler.InitializeModel()`), terwijl planning/prediction via de andere instance liepen (`_scheduler.PredictNextPracticeDate()`). Dit resulteerde in `_model == null` tijdens predictie → ML weegt niet mee in scheduling.

### Implementatie

**1. Consolidatie (Kleinste Diff Principe)**

- **Behouden**: `_scheduler` (readonly, meeste gebruik)
- **Verwijderd**: `_practiceScheduler` veld declaratie
- **Vervangen**: Beide `_practiceScheduler.InitializeModel(...)` calls → `_scheduler.InitializeModel(...)`
  - In `InitializeForProfile()`
  - In `ReloadModelArtifacts()`

**Validatie**: Grep op `_practiceScheduler` → **0 resultaten** ✅

**2. Thread-Safety Toegevoegd**

In `PracticeScheduler` klasse:

```csharp
private readonly object _modelLock = new object();
```

Beveiligde operaties met `lock(_modelLock)`:

- `InitializeModel()` - Model laden uit bestand
- `TrainModel()` - Model trainen en opslaan
- `PredictNextPracticeDate()` - ML predictie maken

**Rationale**: Voorkom race conditions bij concurrent model load/save/predict tijdens multi-threaded scheduling.

**3. Enhanced Instance Tracking Logging**

Nieuwe logging met `GetHashCode()` voor instance verificatie:

## Verifying scheduling diagnostics

Je kunt eenvoudig controleren of de nieuwe plannerdiagnostiek actief is door een volgende-datum-berekening te triggeren en de per-profiel log te bekijken.

Loglocatie:

- Windows: %APPDATA%\ModusPractica\Profiles\<Profielnaam>\Logs\ml_log.txt

Zo trigger je de logregels:

- Kies een sectie met wat oefengeschiedenis (liefst ≥ 3 sessies) zodat trenddetectie data heeft.
- Start een oefensessie voor die sectie en rond af met een prestatie-score in de uitkomstdialoog.
- Bij het plannen van de volgende datum schrijft de app regels zoals:
  - [R* Nudge] slope/avgPerf-driven: … (als er voldoende geschiedenis is)
  - [PreClamp] … en [PostClamp] …
  - [Summary] … tau_used=… t_raw=… t_final=… clamp_reason=…

Zie je deze regels niet?

- Controleer of de sectie voldoende geschiedenis heeft; gloednieuwe items kunnen de nudge overslaan.
- De logger kan erg spraakzame sessies throttlen; je kunt ml_log.txt verwijderen om te resetten.
- De kleine trend-nudge wordt gestuurd door RetentionFeatureFlags.UsePerformanceTrend (staat standaard aan in deze build).

**`PracticeScheduler.InitializeModel()`:**

```
[SCHEDULER INIT] SchedulerInstanceId=12345678, ProfileName='UserProfile', ModelPath='...', ModelLoaded=true
```

**`MLManager.InitializeForProfile()`:**

```
ML system initialized for profile: UserProfile ... Using SchedulerInstanceId=12345678
```

**`MLManager.GeneratePracticeScheduleWithGlobalML()`:**

```
[SCHEDULE START] UsingSchedulerInstanceId=12345678, ModelLoaded=true, R²=0.85, MAE=0.8d, ...
```

**`MLManager.ReloadModelArtifacts()`:**

```
MLManager: Reloaded ML artifacts for profile 'UserProfile'. Using SchedulerInstanceId=12345678
```

**Doel**: Verifieerbaar maken dat **deze scheduler instance** gebruikt wordt gedurende init → reload → planning → prediction cyclus.

### Verwacht Gedrag (Post-Fix)

Bij normale flow (profielwissel → planning):

```
[SCHEDULER INIT] SchedulerInstanceId=12345678, ProfileName='User', ModelPath='C:\...\spacedrepetition.model', ModelLoaded=true
ML system initialized for profile: User with memory stability tracking. Using SchedulerInstanceId=12345678
[SCHEDULE START] UsingSchedulerInstanceId=12345678, ModelLoaded=true, R²=0.45, MAE=0.8d, GlobalMLWeight=0.65, TrainingPoints=350
```

**Hetzelfde instance-id** verschijnt in alle logs → **garantie dat ML model beschikbaar is voor predictie**.

### Benefits

1. **🔄 Consistentie**: Init, reload, train, predict gebruiken allemaal **dezelfde** scheduler instance
2. **🔒 Thread-Safety**: Model operaties zijn nu beschermd tegen race conditions
3. **📝 Observeerbaarheid**: Instance tracking maakt debugging triviaal
4. **🐛 Bug Fix**: Oorspronkelijke probleem (model null tijdens predictie) is opgelost
5. **🧹 Code Quality**: Overtollig veld verwijderd, architecture is eenvoudiger

### Bestanden Gewijzigd

| Bestand                  | Regels | Wijzigingen                                                   |
| ------------------------ | ------ | ------------------------------------------------------------- |
| `MLManager.cs`           | ~15    | Verwijder `_practiceScheduler`, vervang referenties, logging  |
| `PracticeSessionData.cs` | ~25    | Voeg `_modelLock` toe, wrap operaties, instance tracking logs |

### Definition of Done

- ✅ Slechts één scheduler veld in MLManager (`_scheduler`)
- ✅ Init & reload gebruiken dezelfde instance
- ✅ `PredictNextPracticeDate` kan model laden/benutten (geen null errors)
- ✅ Grep op verwijderde naam geeft 0 resultaten
- ✅ Build slaagt zonder nieuwe warnings in aangepaste bestanden
- ✅ Thread-safety geïmplementeerd met lock guards
- ✅ Instance tracking logging actief op alle kritieke paden

---

## ⚠️ ARCHIEF SECTIE - LOG STATISTICS EN DASHBOARD (MOGELIJK DEELS VERWIJDERD)

**De volgende secties beschrijven MLLogManager functionaliteit die mogelijk deels is verwijderd met ML.NET.**

---

## Log Statistics Archive Systeem (Oktober 2025)

### ⚠️ **STATUS ONBEKEND - Mogelijk verwijderd samen met ML.NET components**

### Overzicht

Het **Statistics Archive System** bewaart automatisch log statistieken elke keer dat `MLLogManager.ClearLog()` wordt aangeroepen. Dit voorkomt data loss en maakt historische trend analyse mogelijk in dashboards.

### Architectuur

**Rolling Window met Hard Limit (Optie A)**

- **Max Snapshots**: 100 (FIFO queue)
- **Bestandsgrootte**: ~50KB JSON
- **Opslag**: `%AppData%\ModusPractica\Profiles\{ProfileName}\Logs\log_statistics_archive.json`

### LogStatisticsSnapshot Structure

```csharp
public class LogStatisticsSnapshot
{
    public DateTime SnapshotDate { get; set; }       // Wanneer archivering plaatsvond
    public DateTime PeriodStart { get; set; }        // Eerste log timestamp
    public DateTime PeriodEnd { get; set; }          // Laatste log timestamp

    // Counts
    public int TotalLogs { get; set; }
    public int SchedulerInits { get; set; }          // [SCHEDULER INIT] regels
    public int MLPredictions { get; set; }           // [ML PREDICT] regels
    public int TrainingSessions { get; set; }        // Training started regels
    public int Errors { get; set; }                  // [Error] level logs
    public int Warnings { get; set; }                // [Warning] level logs

    // Predictions breakdown
    public int RawPredictions { get; set; }          // [ML PREDICT RAW]
    public int ClampedPredictions { get; set; }      // [ML PREDICT CLAMP]
    public int FallbacksUsed { get; set; }           // Model null / insufficient history

    // Instance tracking
    public HashSet<string> UniqueSchedulerInstances { get; set; }  // instance IDs gevonden
}
```

### Automatische Archivering

**Bij `ClearLog()` call:**

1. Maak snapshot van huidige logs (`CreateSnapshot()`)
2. Parse timestamps, tel categorieën, extract instance IDs
3. Voeg snapshot toe aan `_statsHistory`
4. Als lijst > 100 items: verwijder oudste (FIFO)
5. Bewaar naar JSON bestand (`SaveStatsArchive()`)
6. Log archivering actie
7. Wis log entries en bestand

**Voorbeeld log output:**

```
[2025-10-04 14:30:45] [Info] 📊 Statistics archived: 1247 logs from 03-10-2025 09:15:22 to 04-10-2025 14:30:40
```

### API voor Dashboard / Analyse

#### Basis Queries

```csharp
// Laatste 100 snapshots (default voor dashboard)
List<LogStatisticsSnapshot> recent = MLLogManager.Instance.GetStatsHistory();

// Huidige sessie stats (niet gearchiveerd)
LogStatisticsSnapshot current = MLLogManager.Instance.GetCurrentStats();

// Laatste N snapshots
List<LogStatisticsSnapshot> lastWeek = MLLogManager.Instance.GetRecentStats(7);

// Datum range filter
var thisMonth = MLLogManager.Instance.GetStatsByDateRange(
    DateTime.Now.AddDays(-30),
    DateTime.Now
);
```

#### Volledige Historie (Expliciet)

```csharp
// ALLEEN gebruiken wanneer volledige analyse nodig is (kan groot zijn)
List<LogStatisticsSnapshot> fullHistory = MLLogManager.Instance.GetFullStatsHistory();
```

⚠️ **Let op**: Dit retourneert alle 100 snapshots. Gebruik alleen wanneer complete data nodig is (bijvoorbeeld voor export of comprehensive analyse). Voor normale dashboard queries gebruik `GetRecentStats(n)`.

#### CSV Export

```csharp
MLLogManager.Instance.ExportStatsToCSV(@"C:\Temp\ml_statistics.csv");
```

**CSV formaat:**

```csv
SnapshotDate,PeriodStart,PeriodEnd,TotalLogs,SchedulerInits,MLPredictions,TrainingSessions,Errors,Warnings,RawPredictions,ClampedPredictions,FallbacksUsed,UniqueInstances
2025-10-04 14:30:45,2025-10-03 09:15:22,2025-10-04 14:30:40,1247,8,142,3,0,5,142,89,12,1
```

### Dashboard Integration Voorbeeld

**Trend Visualisatie:**

```csharp
var last30Days = MLLogManager.Instance.GetRecentStats(30);

// Bereken trends
int totalPredictions = last30Days.Sum(s => s.MLPredictions);
int totalErrors = last30Days.Sum(s => s.Errors);
double avgFallbackRate = last30Days.Average(s =>
    s.MLPredictions > 0 ? (double)s.FallbacksUsed / s.MLPredictions : 0
);

// Periode vergelijking
var thisWeek = last30Days.TakeLast(7).Sum(s => s.MLPredictions);
var lastWeek = last30Days.Skip(7).Take(7).Sum(s => s.MLPredictions);
double growthRate = ((thisWeek - lastWeek) / (double)lastWeek) * 100;

Console.WriteLine($"Predictions: {thisWeek} ({growthRate:+0;-0}% vs last week)");
Console.WriteLine($"Fallback Rate: {avgFallbackRate:P1}");
Console.WriteLine($"Error Rate: {totalErrors / (double)totalPredictions:P2}");
```

**Instance Verificatie:**

```csharp
var recent = MLLogManager.Instance.GetRecentStats(10);
var uniqueInstances = recent
    .SelectMany(s => s.UniqueSchedulerInstances)
    .Distinct()
    .ToList();

if (uniqueInstances.Count > 1)
{
    Console.WriteLine($"⚠️ WARNING: Multiple scheduler instances detected: {string.Join(", ", uniqueInstances)}");
}
else
{
    Console.WriteLine($"✅ Single scheduler instance confirmed: {uniqueInstances.First()}");
}
```

### Performance Karakteristieken

| Operatie             | Tijd  | Memory Impact | Gebruik                    |
| -------------------- | ----- | ------------- | -------------------------- |
| `CreateSnapshot()`   | ~5ms  | Minimal       | Auto bij ClearLog          |
| `SaveStatsArchive()` | ~10ms | 50KB disk     | Auto bij ClearLog          |
| `LoadStatsArchive()` | ~8ms  | 50KB RAM      | 1x bij InitializeForUser   |
| `GetStatsHistory()`  | <1ms  | List copy     | Dashboard queries          |
| `GetRecentStats(10)` | <1ms  | Small subset  | Frequent dashboard updates |
| `ExportStatsToCSV()` | ~15ms | 100KB disk    | Manual export only         |

**Totaal Footprint**: ~50KB disk + ~50KB RAM (lijst van 100 snapshots)

### Rolling Window Garanties

1. **Max 100 snapshots**: Oudste wordt verwijderd bij overflow (FIFO)
2. **Automatische cleanup**: Geen handmatige maintenance nodig
3. **Archivering vóór wissen**: Geen data loss bij ClearLog
4. **Thread-safe**: Alle operations zijn single-threaded via MLLogManager singleton
5. **Failure tolerant**: Archivering failure blokkeert ClearLog niet (fallback naar console error)

### Bestanden

| Bestand                       | Beschrijving                                                              |
| ----------------------------- | ------------------------------------------------------------------------- |
| `MLLogManager.cs`             | Bevat `LogStatisticsSnapshot` class + archive methodes                    |
| `ml_log.txt`                  | Huidige log entries (gewist bij ClearLog)                                 |
| `log_statistics_archive.json` | Persistent archief van laatste 100 snapshots (blijft bestaan na ClearLog) |

### Toekomstige Uitbreidingen (Optioneel)

Als je meer dan 100 snapshots nodig hebt, implementeer **Rolling Window met Auto-Aggregatie** (zie eerdere discussie):

- **Hourly** (24 items) → laatste 24 uur real-time
- **Daily** (90 items) → laatste 3 maanden details
- **Weekly** (52 items) → laatste jaar trends
- **Monthly** (24 items) → 2 jaar overzicht

Met deze structuur blijft totale grootte onder ~40KB en krijg je 2+ jaar historische data zonder performance impact.

---

## Log Analysis Dashboard (Oktober 2025)

### ⚠️ **STATUS ONBEKEND - Dashboard tab in MLDebugWindow mogelijk deels functioneel**

### Overzicht

Een nieuwe visuele tab in het **MLDebugWindow** voor real-time monitoring van ML logging activiteit en diagnostics. De dashboard visualiseert de gearchiveerde log statistieken via interactieve grafieken en summary cards.

### UI Componenten

**Locatie:** MLDebugWindow → Tab "📊 Log Analysis"

#### 1. Filter Controls

- **Time Range Dropdown**: Selecteer 7, 14, 30, 60 dagen of "All Data"
- **Refresh Button**: Handmatige refresh trigger
- **Export Button**: Exporteer gefilterde data naar CSV

#### 2. Summary Cards (5 Metrics)

Visuele kaarten met color-coded borders:

| Metric            | Kleur  | Beschrijving                |
| ----------------- | ------ | --------------------------- |
| Total Predictions | Blauw  | Aantal ML voorspellingen    |
| Errors            | Rood   | Foutmeldingen in log        |
| Warnings          | Oranje | Waarschuwingen              |
| Fallbacks         | Geel   | Teruggeval naar algorithmic |
| Training Events   | Groen  | Model training sessies      |

#### 3. Timeline Chart

Canvas-based line chart met:

- **X-as**: Tijdlijn (snapshot datums)
- **Y-as**: Predictions count
- **Data points**: Cirkel markers met tooltips
- **Trend line**: Polyline verbinding tussen punten
- **Grid**: Horizontale referentie lijnen
- **Axis Labels**: TextBlock voor X (datums) en Y (values)
- **Data Points**: Ellipse shapes met tooltips
- **Trend Line**: Polyline door alle datapunten
- **Legend**: Rectangle + Line + TextBlock combinatie

#### 4. Details Panel (3 Sections)

**Scheduler Activity:**

- Scheduler Initializations count
- Unique scheduler instances (singleton verificatie)

**Prediction Details:**

- Raw Predictions vs Clamped Predictions
- Fallback Rate percentage

**Data Range:**

- Snapshot date range (From → To)
- Total number of snapshots in view

### Technische Implementatie

#### Auto-Refresh Mechanisme

De tab refresht automatisch wanneer:

1. Gebruiker switcht naar de "Log Analysis" tab
2. Time range dropdown wordt gewijzigd
3. Refresh button wordt geklikt

**Null-Safety:** Alle UI controls worden gevalideerd vóór gebruik om `NullReferenceException` te voorkomen bij window initialisatie.

#### Event Handlers

```csharp
// Dropdown filter
private void CmbLogTimeRange_SelectionChanged(object sender, SelectionChangedEventArgs e)
    → triggers RefreshLogAnalysis()

// Manual refresh
private void BtnRefreshLogStats_Click(object sender, RoutedEventArgs e)
    → triggers RefreshLogAnalysis()

// CSV export
private void BtnExportLogStats_Click(object sender, RoutedEventArgs e)
    → SaveFileDialog + ExportStatsToCSV()
```

#### Data Pipeline

```csharp
RefreshLogAnalysis()
    ↓
GetLogStatsForSelectedRange() // Filter by dropdown
    ↓
MLLogManager.Instance.GetStatsByDateRange() / GetRecentStats()
    ↓
Aggregate statistics (Sum, Average, HashSet merge)
    ↓
Update 15 UI controls (TextBlocks, Canvas)
    ↓
DrawLogTimelineChart() // Render visualization
```

#### Chart Rendering Logic

`DrawLogTimelineChart()` gebruikt WPF Shapes API:

- **Background**: Rectangle met lichte achtergrond
- **Title**: TextBlock met "ML Predictions Over Time"
- **Grid Lines**: Horizontale Line elementen
- **Axis Labels**: TextBlock voor X (datums) en Y (values)
- **Data Points**: Ellipse shapes met tooltips
- **Trend Line**: Polyline door alle datapunten
- **Legend**: Rectangle + Line + TextBlock combinatie

**Responsive Design:** Chart past zich aan aan canvas size (800x400) met margins voor labels.

### Bestanden Gewijzigd

| Bestand                 | Wijzigingen                          | Regels |
| ----------------------- | ------------------------------------ | ------ |
| `MLDebugWindow.xaml`    | Nieuwe TabItem met complete UI       | ~330   |
| `MLDebugWindow.xaml.cs` | Event handlers + visualization logic | ~372   |
| `MLLogManager.cs`       | Query API uitbreiding (reeds done)   | ~200   |

### Gebruik Scenario's

**1. Dagelijkse Monitoring**

```
1. Open MLDebugWindow
2. Ga naar "📊 Log Analysis" tab (auto-refresh)
3. Selecteer "Last 7 Days" filter
4. Bekijk Summary Cards voor quick health check
5. Inspecteer Timeline Chart voor trends
```

**2. Anomalie Detectie**

```
1. Check Errors/Warnings cards (rood/oranje)
2. Bekijk Fallback Rate in Details panel
3. Hoge fallback rate? → Check ML model quality (andere tab)
4. Multiple unique instances? → Singleton issue warning
```

**3. Performance Analyse**

```
1. Selecteer "Last 30 Days" voor monthly overview
2. Exporteer naar CSV voor externe analyse
3. Open CSV in Excel/Python voor diepere insights
4. Vergelijk prediction counts over weeks
```

**4. Development Debugging**

```
1. ClearLog() meerdere keren aanroepen → creates snapshots
2. Refresh dashboard → bekijk nieuwste snapshot
3. Verify dat oude snapshots blijven (FIFO na 100)
4. Check Details panel voor instance GUID consistency
```

### Error Handling

Bij fouten in `RefreshLogAnalysis()`:

```csharp
catch (Exception ex)
{
    MessageBox.Show($"Error refreshing log analysis:\n{ex.Message}",
        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
}
```

Bij export fouten:

```csharp
catch (Exception ex)
{
    MessageBox.Show($"Error exporting statistics:\n{ex.Message}",
        "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
}
```

### Limitaties & Aanbevelingen

**Limitaties:**

- Canvas rendering is synchronous (grote datasets kunnen UI blokkeren)
- Maximaal 100 snapshots in archief (rolling window)
- Geen zoom/pan functionaliteit in chart

**Aanbevelingen:**

1. Gebruik filters om data subset te bekijken (niet altijd "All Data")
2. Voor >100 snapshots: implementeer hierarchische aggregatie
3. Bij performance issues: overweeg async chart rendering
4. CSV export voor complexe analyses buiten applicatie

### Toekomstige Verbeteringen (Optioneel)

**Phase 2 Features:**

- Interactive chart (zoom, pan, hover details)
- Meerdere data series (predictions + errors + warnings op 1 chart)
- Date range picker (custom start/end dates)
- Alert thresholds (notificatie bij hoge error rate)
- Export naar andere formats (JSON, XML)

**Phase 3 Features:**

- Real-time updates (auto-refresh elke N seconden)
- Comparison view (week-over-week, month-over-month)
- Anomaly detection highlights
- Integration met ML model metrics (correlation analyse)

---

## ⚠️ EINDE ARCHIEF SECTIES

**Alle bovenstaande ML.NET-gerelateerde secties zijn historisch archief. De volgende secties beschrijven de ACTIEVE functionaliteit.**

---

## Bouwen & Starten

```
dotnet build
dotnet run
```

(.NET 8 preview melding kan verschijnen — zie Microsoft support policy.)

**Tests Uitvoeren:**

```
dotnet test ModusPractica_2.sln --configuration Release
```

## Contribution Notes

1. Houd alle τ- en interval-clamps centraal via `EbbinghausConstants` (geen lokale caps).
2. Voeg nieuwe bronnen toe aan adaptieve mix alleen via `AdaptiveTauManager` (één integrator).
3. Gebruik prefix `[RETENTION_DIAG]` voor alles wat later analytisch bruikbaar moet zijn.
4. **Stage-Aware Tau**: Gebruik altijd `CalculateAdjustedTau(difficulty, repetitions, practiceScheduleStage)` voor consistente MASTERED groei.
5. **Profiel Isolation**: Gebruik uitsluitend `DataPathProvider` voor alle filesystem operaties - geen hardcoded paden.
6. **Data Migration**: Test nieuwe features met zowel default als custom data locaties.
7. **Lifecycle States**: Respecteer business rules in `SectionLifecycleService` bij state transitions.
8. **Rollback Pattern**: Implementeer rollback functionaliteit voor complexe operaties (zie `PracticeSessionWindow.RollbackSessionSave()`).
9. **Interleaved Practice**: Gebruik `PracticePlaylist.AutoGenerateInterleavedPlaylist()` voor gebalanceerde sessies.
10. **Logging**: Gebruik structured logging met duidelijke prefixes voor verschillende subsystemen.

### Dataopslag en profielen

- Alle gegevens worden per profiel opgeslagen onder de gekozen data root (default: `%APPDATA%\ModusPractica`).
- De applicatie gebruikt een centrale `DataPathProvider` voor alle pad-berekeningen.
- Autocomplete-data is profiel-specifiek om datalekken te voorkomen.
- Tip: Een aangepaste data-root kan ingesteld worden via `DataPathProvider.SetCustomRoot(...)`.

### Stage-Aware MASTERED Intervals

Nieuwe wetenschappelijk onderbouwde progressie voor mastered secties:

- **Stage 3** (eerste mastered): 2.0× baseline (7-10 dagen)
- **Stage 4** (tweede perfect): 2.5× baseline (14-21 dagen)
- **Stage 5+** (volledig geconsolideerd): 3.5× baseline (30-60+ dagen)

Gebaseerd op Bjork & Bjork (2011), Cepeda et al. (2006), Simmons & Duke (2006).

## 📚 Documentatie

Alle technische documentatie, changelogs, en implementatie details zijn te vinden in de [Documentation](Documentation/) map.

**Belangrijke documenten:**

- [Documentation/INDEX.md](Documentation/INDEX.md) - Volledig overzicht van alle documentatie
- [Documentation/CHANGELOG_Autocomplete_Improvements.md](Documentation/CHANGELOG_Autocomplete_Improvements.md) - Laatste wijzigingen (2025-11-05)
- [Documentation/CHANGELOG_Profile_Management.md](Documentation/CHANGELOG_Profile_Management.md) - Profielbeheer systeem
- [Documentation/Ebbinghaus_Implementation_Guide.md](Documentation/Ebbinghaus_Implementation_Guide.md) - Technische implementatie gids

## Licentie

Zie `LICENSE` bestand.

---

## Nieuwe Bestanden (2025-11-03)

- `DataPathProvider.cs` – Centrale filesystem pad-beheer voor profiel-isolatie
- `ProfileConfiguration.cs` – Persistent profiel en data locatie configuratie
- `ProfileSelectorWindow.xaml/.xaml.cs` – Hoofdingang voor profiel selectie en data locatie keuze
- `DataMigrationHelper.cs` – Ondersteuning voor migreren van bestaande data naar nieuwe locaties
- `ProfileInUseDialog.xaml/.xaml.cs` – Dialog voor wanneer profiel niet verwijderd kan worden
- `Documentation/CHANGELOG_Ebb_Unify_Clamps.md` – Documentatie van stage-aware MASTERED interval groei
- `Documentation/CHANGELOG_Profile_Management.md` – Documentatie van multi-user profielbeheer systeem
