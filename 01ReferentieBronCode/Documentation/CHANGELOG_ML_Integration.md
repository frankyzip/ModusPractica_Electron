# CHANGELOG - ML Planner Activatie & Feature Integratie

> **⚠️ VOLLEDIG ARCHIEF DOCUMENT - 2025-10-30**
>
> **Dit volledige changelog document is NIET MEER VAN TOEPASSING.**
>
> ML.NET-onderdelen zijn volledig verwijderd in deze branch (feature/ebb-unify-clamps). Dit changelog beschrijft historische wijzigingen die zijn teruggedraaid en is niet langer relevant voor de huidige applicatie.
>
> **WAAROM BEWAARD:**
>
> - Historische referentie voor toekomstige beslissingen
> - Documentatie van eerder geteste benaderingen
> - Begrip van implementatie-uitdagingen met ML.NET
>
> **HUIDIGE STATUS:**
> De applicatie gebruikt geen ML.NET meer. Alle scheduling gebeurt via wetenschappelijk onderbouwde Ebbinghaus-curve berekeningen met adaptieve personalisatie en stage-aware MASTERED interval groei.

## Versie: ML-Integration-v1.0

## Datum: 3 oktober 2025

## Branch: feature/ebb-unify-clamps

---

## 🎯 Samenvatting

Deze release activeert het getrainde ML-model in de praktijkplanner door het stub-predictiepad te vervangen met echte ML-voorspellingen die worden geblend met algoritmische intervallen. Daarnaast zijn twee kwaliteitsfeatures (`TotalFailures` en `SuccessRatio`) toegevoegd aan de training en predictie pipeline.

### Kernimpact

- **ML-voorspellingen beïnvloeden nu de planning**: `mlInterval` wordt berekend uit het model en geblend met `algorithmicInterval` via een dynamisch ML-gewicht
- **Betere predictiekwaliteit**: Twee nieuwe features (TotalFailures, SuccessRatio) gebaseerd op 85%-succesregel onderzoek
- **Verbeterde observability**: Uitgebreide logging van alle blend-componenten per sectie
- **Robuuste clamps**: Alle ML-voorspellingen passeren door centrale wetenschappelijke clamps ([1, 365] dagen, ≤ 5×τ)

---

## 📋 Wat is er veranderd?

### A) **ML-voorspelling geactiveerd** (PracticeSessionData.cs)

**Voor:**

```csharp
internal DateTime PredictNextPracticeDate(MusicPieceItem piece, BarSection section)
{
    // Simpele fallback implementatie
    return DateTime.Today.AddDays(section.Interval > 0 ? section.Interval : 1);
}
```

**Na:**

- 200+ regels productie-ready implementatie
- Construeert 21 features identiek aan training pipeline
- Gebruikt geladen ML.NET model voor voorspelling
- Past centrale clamps toe via `EbbinghausConstants.ClampIntervalToScientificBounds`
- Robuuste fallbacks (model null, onvoldoende historie, fouten)
- Uitgebreide logging van alle stappen

**Waarom:** De stub blokkeerde het hele ML-systeem. Het model was getraind en geladen, maar werd nooit gebruikt voor voorspellingen.

---

### B) **Quality features toegevoegd** (PracticeSessionData.cs, MLManager.cs)

#### Feature-set uitbreiding (nu 21 features):

1-18. _Bestaande features behouden_ 19. **`TotalFailures`**: Alle failures tijdens sessie (voor succesratio-berekening) 20. **`SuccessRatio`**: `Repetitions / (Repetitions + TotalFailures)` (85%-regel onderzoek) 21. **`RepetitionsPerformanceInteraction`**: Interactie-term

**Toegepast op:**

- Training pipeline: `Concatenate("Features", ...)` in `TrainModel`
- Data conversie: `PracticeHistoryForTraining` → `PracticeSessionData` mapping
- Predictie: Feature constructie in `PredictNextPracticeDate`

**Waarom:** TotalFailures en SuccessRatio zijn bewezen voorspellers voor optimale intervallen (research: 85% success rate is optimaal voor consolidatie). Deze features werden al vastgelegd in PracticeHistory maar niet gebruikt door het ML-model.

---

### C) **LoadColumn conflicten opgelost** (PracticeSessionData.cs)

**Voor:**

```csharp
[LoadColumn(18)] public float TotalFailures { get; set; }
[LoadColumn(19)] public float SuccessRatio { get; set; }
[LoadColumn(18)] public float RepetitionsPerformanceInteraction { get; set; } // CONFLICT!
```

**Na:**

```csharp
[LoadColumn(18)] public float TotalFailures { get; set; }
[LoadColumn(19)] public float SuccessRatio { get; set; }
[LoadColumn(20)] public float RepetitionsPerformanceInteraction { get; set; } // Fixed
```

**Waarom:** Dubbele indices veroorzaken verwarring en potentiële fouten, ook al wordt `LoadFromEnumerable` gebruikt.

---

### D) **Verbeterde observability** (MLManager.cs)

#### Nieuwe logging bij scheduling start:

```csharp
[SCHEDULE START] Model=loaded R²=0.4523 MAE=2.34d GlobalMLWeight=0.642 TrainingPoints=487
```

#### Gedetailleerde blend-logging per sectie:

```csharp
[SCHEDULE BLEND] section=abc123 piece='Bach Invention No. 1'
    algorithmicInterval=5.23d mlInterval=7.45d
    globalMLWeight=0.642 finalInterval=6.65d
    nextDate=2025-10-10 tau=3.245
```

#### Skip-reason diagnostics:

```csharp
ML prediction skipped for [piece] - [section]: reason=ml_weight_low(0.045)
ML prediction skipped for [piece] - [section]: reason=history_insufficient(2)
```

**Waarom:** Zonder gedetailleerde logging is het onmogelijk te debuggen waarom de planning al dan niet verandert door ML.

---

### E) **Gating gerespecteerd**

De bestaande gating policy blijft ongewijzigd:

- ≥ 200 bruikbare datapoints
- R² ≥ 0.20 (model quality threshold)
- ≥ 3 sessies per sectie voor feature-berekening

Als de gate niet open staat: `globalMLWeight = 0.0` → pure algoritmische route.

**Waarom:** Geen wijziging nodig; de policy werkte al correct. We zorgen er nu alleen voor dat – wanneer de gate open is – de ML-voorspelling ook daadwerkelijk wordt gebruikt.

---

## 🧪 Tests

### Nieuw testproject: ModusPractica.Tests (xUnit)

**19 tests, alle geslaagd:**

#### ML Weight & Gating (7 tests):

- ✅ MLWeight bij < 200 datapoints → 0.0 (algoritmisch only)
- ✅ MLWeight bij R² < 0.20 → 0.0 (algoritmisch only)
- ✅ MLWeight bij voldoende data & quality → positief (0.0, 0.95]
- ✅ MLWeight bij hoge quality (R² > 0.80) → > 0.80

#### Scientific Clamps (4 tests):

- ✅ Extreme negatieve waarde → clamped to 1.0d
- ✅ Extreme hoge waarde → clamped to ≤ 365d én ≤ 5×τ
- ✅ Geldige waarde → geen wijziging
- ✅ Overschrijding 5×τ → clamped to 5×τ

#### Feature Consistency (4 tests):

- ✅ Training pipeline bevat TotalFailures
- ✅ Training pipeline bevat SuccessRatio
- ✅ Training pipeline bevat RepetitionsPerformanceInteraction
- ✅ Totaal 21 features (guard tegen regressies)

#### DateHelper (3 tests):

- ✅ Correcte datumberekening
- ✅ Zero interval → minimaal 1 dag (planner contract)
- ✅ Time component verwijderd bij normalisatie

#### Retention Targets (1 test):

- ✅ Waarden binnen geldige range (0.50-0.95), settings-agnostic

---

## 📦 Build resultaten

```
✅ Build succeeded (Release & Debug)
✅ 0 nieuwe compiler errors
✅ 0 nieuwe warnings in aangepaste bestanden
✅ Alle 19 unit tests geslaagd
✅ Pre-existente WPF analyzer warnings niet verergerd
```

---

## 📊 Voorbeeldlogregels

### Bij voldoende training data (ML actief):

```
[ML TRAINING START] Total practice history: 523 sessions
[ML TRAINING DATA] Prepared 487 usable training points from 523 sessions
[ML TRAIN] Starting SDCA training with 487 points, L1=1.0, L2=1.0
[ML TRAIN METRICS] R²=0.4523, MAE=2.34d, RMSE=3.12d, Loss=9.74

Global ML Weight: 0.642 based on 487 usable training pairs from 523 total sessions

[SCHEDULE START] Model=loaded R²=0.4523 MAE=2.34d GlobalMLWeight=0.642 TrainingPoints=487

[ML PREDICT RAW] section=a1b2c3d4 mlInterval=7.45d (DiffScore=2.0, PerfRating=10.0, TotalFail=3, SuccessRatio=0.87)
[ML PREDICT RESULT] section=a1b2c3d4 nextDate=2025-10-10 (interval=7.45d, from=2025-10-03)

[SCHEDULE BLEND] section=a1b2c3d4 piece='Bach Invention No. 1'
    algorithmicInterval=5.23d mlInterval=7.45d
    globalMLWeight=0.642 finalInterval=6.65d
    nextDate=2025-10-10 tau=3.245
```

### Bij onvoldoende data (ML inactief):

```
ML Weight = 0: usablePoints=150 (min=200), R²=0.3245 (min=0.20)
ML prediction skipped for Bach Invention No. 1 - Bars 1-8: reason=ml_weight_low(0.000)

[SCHEDULE BLEND] section=x9y8z7 piece='Bach Invention No. 1'
    algorithmicInterval=4.00d mlInterval=4.00d
    globalMLWeight=0.000 finalInterval=4.00d
    nextDate=2025-10-07 tau=2.987
```

### Bij sectie met onvoldoende historie:

```
[ML PREDICT] Insufficient history (2) for section def456, using algorithmic fallback
ML prediction skipped for Chopin Prelude - Bars 9-16: reason=history_insufficient(2)
```

---

## 🔧 Technische details

### Feature constructie (training ↔ predictie consistentie):

**Kritiek:** De feature-volgorde en -betekenis moet **exact** overeenkomen tussen training en predictie.

**Oplossing:**

1. Training: `PrepareTrainingData` (MLManager.cs) → `PracticeHistoryForTraining` → `PracticeSessionData`
2. Predictie: `PredictNextPracticeDate` construeert features direct uit `piece`, `section`, `historyForSection`
3. Test: `MLFeatureConsistencyTests` valideert dat beide 21 features gebruiken

**Features:**

```
1. DifficultyScore (3.0/2.0/1.0/0.5)
2. PreviousInterval (dagen tussen vorige twee sessies)
3. PreviousRepetitions
4. PerformanceRating (10.0/5.0/1.0 obv SessionOutcome)
5. TotalPracticeTime (sum laatste 5 sessies)
6. RepetitionStreakAttempts
7. SessionOutcomeScore (1.0/0.5/0.0)
8. TempoRatio (1.0 default)
9. PreparatoryPhaseDurationMinutes
10. DayOfWeek (0-6)
11. TimeOfDay (uur)
12. DaysSinceLastSession
13. SessionsOnSameDay
14. PerformanceTrend (laatste - eerste van recente 3)
15. AveragePerformance (avg recente 3)
16. DifficultyPerformanceInteraction (diff × perf)
17. TimeOfDayPerformanceInteraction (hour × perf)
18. DayOfWeekPerformanceInteraction (dow × perf)
19. TotalFailures ⭐ NIEUW
20. SuccessRatio ⭐ NIEUW
21. RepetitionsPerformanceInteraction (reps × perf)
```

### Centrale clamps (EbbinghausConstants.ClampIntervalToScientificBounds):

**Toegepast op:**

- Alle algoritmische intervallen
- Alle ML-voorspellingen
- User overrides
- Foundation/frustration fixed intervals

**Regels:**

1. `interval ∈ [1, 365]` dagen (policy minimum & maximum)
2. `interval ≤ 5 × τ` (theoretische τ-based cap)
3. Bij overschrijding: log reason + geclampt interval

**Reden reasons:**

- `none`: Geen wijziging
- `invalid→min`: NaN/Infinity/≤0 → 1
- `min_consolidation`: < 1 → 1
- `safety_max_365`: > 365 → 365
- `cap_5x_tau`: > 5×τ → 5×τ
- Combinaties mogelijk: `safety_max_365+cap_5x_tau`

---

## 📁 Gewijzigde bestanden

### Core implementatie:

- **PracticeSessionData.cs** (~200 nieuwe regels)

  - `PredictNextPracticeDate` stub → volledige implementatie
  - LoadColumn indices gecorrigeerd (18→20 voor RepetitionsPerformanceInteraction)
  - TrainModel: TotalFailures & SuccessRatio toegevoegd aan Concatenate
  - Data conversie: TotalFailures & SuccessRatio toegevoegd aan mapping

- **MLManager.cs** (~30 nieuwe regels)
  - `GeneratePracticeScheduleWithGlobalML`: model status logging
  - Per sectie: skip reason diagnostics + gedetailleerde blend logging
  - Training data: TotalFailures & SuccessRatio al aanwezig (geen wijziging)

### Tests (nieuw):

- **ModusPractica.Tests/ModusPractica.Tests.csproj** (nieuw project)
- **ModusPractica.Tests/MLPredictionIntegrationTests.cs** (19 tests)
- **ModusPractica_2.sln** (testproject toegevoegd)

### Geen wijzigingen:

- **EbbinghausConstants.cs**: Clamp-methodes ongewijzigd (al compleet)
- **FileLockManager.cs** (DateHelper): Geen wijzigingen nodig
- **PracticeHistory.cs**: TotalFailures & SuccessRatio properties al aanwezig
- **MLModelState.cs, MLLogManager.cs**: Ongewijzigd

---

## ⚠️ Breaking changes

**Geen**: Alle wijzigingen zijn backward compatible.

- Oude modellen (zonder TotalFailures/SuccessRatio) blijven werken (features default to 0)
- Oude practice history zonder deze velden blijft geldig
- API signatures ongewijzigd

---

## 🎯 Definitie van Klaar (DoD) - Status

✅ **Build slaagt** (Release & Debug)  
✅ **Geen nieuwe warnings** in aangepaste bestanden  
✅ **Alle tests groen** (19/19)  
✅ **ML-gate respecteert policy** (≥200, R²≥0.20)  
✅ **Logging toont verschillen** tussen mlInterval en algorithmicInterval  
✅ **Clamps toegepast** op alle ML-voorspellingen  
✅ **Code diff leesbaar** met XML-doc op kernmethodes  
✅ **Feature consistentie** getest en gevalideerd

---

## 🚀 Volgende stappen (optioneel, buiten scope)

1. **Hyperparameter tuning**: Na verzamelen meer data (>500 punten), grid search voor optimale L1/L2
2. **Feature importance analyse**: Bepaal welke features het meest bijdragen (voor toekomstige optimalisatie)
3. **A/B testing**: Vergelijk planning outcomes (ML vs pure algoritmisch) over 1-2 maanden
4. **Model retraining trigger**: Automatisch retrain bij ≥100 nieuwe sessies sinds laatste training
5. **DateHelper refactoring**: Verplaats naar eigen bestand (momenteel in FileLockManager.cs)

---

## 📝 Credits

- **Ebbinghaus-curve integratie**: Wetenschappelijk gefundeerde τ-berekening en clamps
- **85%-regel onderzoek**: TotalFailures & SuccessRatio features
- **Global ML strategy**: Een model leert van alle data, voorspelt voor individuele secties
- **Test-driven**: 19 unit tests garanderen correcte werking

---

**Einde CHANGELOG**
