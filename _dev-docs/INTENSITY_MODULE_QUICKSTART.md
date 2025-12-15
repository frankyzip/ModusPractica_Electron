# Intensity Module - Quick Start Guide

## 🚀 Voor Gebruikers

### Wat doet de Intensity Module?

De Intensity Module helpt je om **efficiënter te oefenen** door:

1. **TDS (Technical Difficulty Score)** te tonen: Hoe goed beheers je dit stuk? (0-100%)
2. **Leerfases** te bepalen: Van "Initial Acquisition" tot "Overlearning"
3. **OLQ (Overlearning Quotum)** te berekenen: Hoeveel herhalingen nodig?
4. **Oefentijd** te voorspellen: Hoe lang duurt deze sessie waarschijnlijk?

### Hoe gebruik je het?

#### Stap 1: Module Inschakelen/Uitschakelen

1. Open het **Dashboard**
2. Klik op **"⚙️ Settings"** rechtsboven
3. Toggle de **"Enable Intensity Module"** schakelaar
   - **AAN** (groen): Guided practice met TDS/OLQ targets
   - **UIT** (grijs): Zelf bepalen hoe lang/intensief je oefent

#### Stap 2: Oefenen met de Module

1. Start een practice session (kies een stuk om te oefenen)
2. Zie je de **Intensity Module Display**:
   - **TDS Bar**: Groene balk toont je success ratio (0-100%)
   - **Learning Phase Badge**: 🎯 → 🌱 → 🔥 → 📈 → 🎓 (5 fases)
   - **OLQ Progress**: Hoeveel correcte herhalingen je hebt (vs. target)
3. De display update **real-time** terwijl je oefent

#### Stap 3: Voortgang Volgen

1. Open het **Dashboard**
2. Scroll naar **"📊 Intensity Module Analytics"**
3. Bekijk je voortgang:
   - **TDS Trends**: Progressie per stuk/sectie (laatste 10 sessies)
   - **Phase Distribution**: Hoeveel stukken in elke leerfase?
   - **Avg Time per CR**: Hoe snel beheers je nieuwe stukken?

---

## 🎓 Leerfases Uitgelegd

De Intensity Module gebruikt vijf leerfasen. Elke fase heeft een vaste **FixedGoal** en dezelfde overlearning-regel: voeg `ceil(initialFailedAttempts × 0.5)` extra correcte herhalingen toe op basis van het aantal fouten vóór de eerste correcte uitvoering.

### 1. 🎯 Initial Acquisition (0-40% TDS)

- **FixedGoal:** 6 correcte herhalingen
- **Focus:** Noten en ritme ontdekken, fouten analyseren
- **Tip:** Neem de tijd; elke fout vóór de eerste correcte herhaling voegt meteen extra repetities toe (vooral nuttig voor diepe motorische imprint)

### 2. 🌱 Refinement (40-70% TDS)

- **FixedGoal:** 7 correcte herhalingen
- **Focus:** Techniek stabiliseren, tempo opbouwen
- **Tip:** Verwacht nog enkele fouten; de overlearning-term houdt rekening met de initiële haperingen zonder je te overladen

### 3. 🔥 Consolidation (70-85% TDS)

- **FixedGoal:** 8 correcte herhalingen
- **Focus:** Automatisering, consistente klankvorming
- **Tip:** Kleine foutjes bij de start vertalen zich naar +1 of +2 extra herhalingen, waardoor je sessie precies afgestemd blijft op de motorische uitdaging

### 4. 📈 Mastery (85-95% TDS)

- **FixedGoal:** 9 correcte herhalingen
- **Focus:** Betrouwbaarheid en expressie op hoog niveau
- **Tip:** Zelfs beperkte startfouten triggeren extra reps (0.5 per fout, naar boven afgerond) voor een robuuste buffer richting performance

### 5. 🎓 Overlearning (95-100% TDS)

- **FixedGoal:** 10 correcte herhalingen
- **Focus:** Stressbestendigheid, concertvoorbereiding
- **Tip:** Extra herhalingen worden alleen nog door startfouten bepaald; het stuk blijft daardoor onderhoudbaar zonder overbodige belasting

---

## 📊 Metrics Uitgelegd

### TDS (Technical Difficulty Score)

**Formule:** `TDS = CR / (CR + FA)`

- **CR** = Correct Responses (aantal goede herhalingen)
- **FA** = False Attempts (aantal fouten)
- **TDS** = Success ratio (0.0 - 1.0, getoond als 0-100%)

**Voorbeeld:**

- 8 correcte herhalingen, 2 fouten
- TDS = 8 / (8 + 2) = 0.8 = **80%**

**Interpretatie:**

- **0-40%**: Nog veel fouten, focus op basics
- **40-70%**: Stuk wordt geleerd, tempo opbouwen
- **70-85%**: Goed onder controle, verfijnen
- **85-95%**: Bijna perfect, automatiseren
- **95-100%**: Concert-ready, overlearning

### OLQ (Overlearning Quotum)

**Concept:** Hoeveel correcte herhalingen zijn nodig om een stuk **stressbestendig** te consolideren?

**Formule:**

```
OLQ_target = FixedGoal_phase + ceil(initialFailedAttempts × 0.5)
```

- **FixedGoal_phase:** Vast aantal correcte herhalingen per leerfase (6 → 10 zoals hierboven).
- **initialFailedAttempts:** Aantal mislukte pogingen vóór de eerste correcte herhaling in dezelfde sessie.
- **ceil(...):** Naar boven afronden zodat elke paar fouten garant staat voor minstens één extra correcte herhaling.

**Belangrijke spelregels:**

- De eerste correcte herhaling telt meteen als repetitie #1 richting het OLQ-doel.
- Fouten na de eerste correcte herhaling beïnvloeden het OLQ niet meer; ze worden wel gebruikt voor planning en analyse in de SRS-kern.
- Hoe zwaarder de start, hoe groter de overlearning-term — volledig in lijn met Dr. Molly Gebrian's aanbeveling om 50% extra herhalingen te oefenen bovenop het basisdoel.

**Voorbeeld:** Refinement-fase (FixedGoal = 7) met vier mislukte pogingen vóór de eerste correcte herhaling:

```
OLQ_target = 7 + ceil(4 × 0.5) = 7 + 2 = 9 correcte herhalingen
```

**Bron:** Dr. Molly Gebrian (2013) - "The benefits of overlearning in musical motor skills"

### T̄_CR (Average Time per Correct Response)

**Concept:** Hoeveel tijd kost het gemiddeld om 1 correcte herhaling te doen?

**Berekening:**

- Exponential moving average (EMA) met α = 0.3
- Per sessie: `Nieuwe T̄_CR = 0.3 × (Sessieduur / CR) + 0.7 × Oude T̄_CR`

**Voorbeeld:**

- Sessie 1: 120 seconden, 10 CR → T̄_CR = 12.0 s/CR
- Sessie 2: 180 seconden, 15 CR → Nieuwe T̄_CR = 0.3×12 + 0.7×12 = **12.0 s/CR**
- Sessie 3: 240 seconden, 12 CR → Nieuwe T̄_CR = 0.3×20 + 0.7×12 = **14.4 s/CR**

**Gebruik:** Predict sessieduur: `Duration = OLQ × T̄_CR`

---

## 🛠️ Adaptive Calibration

### Wat leert de module?

De module **leert** hoe jij oefent:

1. **Per stuk/sectie**: Niet alle stukken zijn even moeilijk
2. **Over tijd**: Je wordt sneller naarmate je meer oefent
3. **Persoonlijk**: Jouw leertempo ≠ anderen

### Data die wordt bijgehouden:

```
Per sectie:
- avgTimePerCR: Gemiddelde tijd per correcte herhaling (seconden)
- sessionCount: Aantal keer dat je dit stuk geoefend hebt
- lastUpdated: Laatste update timestamp
- sectionName: Naam van het stuk/sectie (voor herkenning)
```

### Privacy:

- **100% lokaal**: Data blijft in jouw browser (localStorage)
- **Geen server**: Niets wordt geüpload naar internet
- **Per profiel**: Elk profiel heeft eigen calibratie data

---

## 🎨 Dashboard Analytics Gids

### TDS Trends Chart

**Wat zie je?**

- Lijst van alle stukken/secties die je geoefend hebt
- Per stuk: huidige TDS percentage + leerfase
- Trend dots: 10 meest recente sessies (kleur = TDS level)

**Kleuren:**

- 🟢 **Groen**: TDS ≥ 70% (high)
- 🟡 **Geel**: TDS 40-70% (medium)
- 🔴 **Rood**: TDS < 40% (low)
- 💚 **Current** (groot + glow): Meest recente sessie

**Interpretatie:**

- **Oplopende trend** (rood → geel → groen): Stuk wordt geleerd 📈
- **Stagnatie** (geel → geel → geel): Mogelijk te snel tempo? 🤔
- **Dalende trend** (groen → geel → rood): Stuk wordt vergeten? 📉

### Phase Distribution Chart

**Wat zie je?**

- Bar chart met 5 leerfases
- Percentage + count van stukken per fase

**Ideale distributie:**

- **Beginners**: Meeste stukken in Initial/Early Learning
- **Gevorderden**: Meeste stukken in Active Practice/Consolidation
- **Professionals**: Veel stukken in Overlearning (concert repertoire)

**Gebruik:**

- Te veel in Initial Acquisition? → Focus op basics
- Niets in Overlearning? → Kies stukken om te perfectioneren
- Balans? → Goed bezig! 🎉

### Average Time per CR Chart

**Wat zie je?**

- Global average: Gemiddelde T̄_CR over alle stukken
- Top 5 stukken: Meest geoefende stukken met hun T̄_CR

**Interpretatie:**

- **Hoge T̄_CR** (> 20 seconden): Moeilijke stukken of langzaam tempo
- **Lage T̄_CR** (< 10 seconden): Makkelijke stukken of snel geleerd
- **Daling over tijd**: Je wordt sneller! 🚀

**Gebruik:**

- Predict hoeveel tijd je nodig hebt voor nieuwe stukken
- Vergelijk moeilijkheid van verschillende stukken
- Track je algemene voortgang als muzikant

---

## 🧪 Browser Console Commands

Voor developers of power users:

### Check Module Status

```javascript
const profileId = localStorage.getItem("mp_currentProfile");
const settings = JSON.parse(
  localStorage.getItem(`mp_${profileId}_intensitySettings`) || "{}"
);
console.log("Module enabled:", settings.enabled);
```

### View Adaptive Calibration Data

```javascript
const profileId = localStorage.getItem("mp_currentProfile");
const calibration = JSON.parse(
  localStorage.getItem(`mp_${profileId}_adaptiveTimeCalibration`) || "{}"
);
console.table(calibration);
```

### Manual Refresh Analytics

```javascript
refreshIntensityAnalytics();
```

### Test IntensityModule Directly

```javascript
const im = new IntensityModule();
console.log("TDS:", im.calculateTDS(8, 2)); // 0.8
console.log("Phase:", im.getLearningPhase(0.8)); // Consolidation
console.log(
  "OLQ:",
  im.calculateOLQ(0.8, { correctRepetitions: 10, failedAttempts: 2 })
);
```

### Show Intensity Report (During Practice Session)

```javascript
window.showIntensityReport();
```

---

## ❓ FAQ

### Moet ik de module altijd AAN hebben?

**Nee!** De module is optioneel:

- **Aanzetten** als je:

  - Een beginner bent en sturing wil
  - Nieuwe stukken leert
  - Wil weten hoeveel herhalingen genoeg zijn
  - Data-driven wil oefenen

- **Uitzetten** als je:
  - Ervaren muzikant bent
  - Zelf je oefentijd bepaalt
  - Alleen de Ebbinghaus SRS wil gebruiken
  - Geen guided practice wil

### Hoe vaak moet ik een stuk oefenen volgens de module?

**Antwoord:** Hangt af van je TDS én het aantal fouten vóór de eerste correcte herhaling. De module gebruikt een vaste **FixedGoal** per fase plus `ceil(initialFailedAttempts × 0.5)` extra reps.

- **TDS < 40% (Initial Acquisition):** FixedGoal = 6 + 50% van je startfouten → typisch 6-9 correcte herhalingen
- **TDS 40-70% (Refinement):** FixedGoal = 7 + 50% van je startfouten → typisch 7-10 correcte herhalingen
- **TDS 70-85% (Consolidation):** FixedGoal = 8 + 50% van je startfouten → typisch 8-11 correcte herhalingen
- **TDS 85-95% (Mastery):** FixedGoal = 9 + 50% van je startfouten → typisch 9-12 correcte herhalingen
- **TDS 95-100% (Overlearning):** FixedGoal = 10 + 50% van je startfouten → typisch 10-13 correcte herhalingen

De exacte waarde groeit enkel mee met het aantal fouten vóór je eerste succesvolle poging. Zodra je een correctie uitvoert, staat het doel vast voor de rest van de sessie.

### Wat als ik niet genoeg tijd heb voor alle OLQ herhalingen?

**Geen probleem!** De module geeft een **aanbeveling**, geen verplichting:

- Stop wanneer je wilt (fatigue is real!)
- De Ebbinghaus SRS zorgt voor spaced repetition
- Volgende sessie komt het stuk weer terug
- Progressie wordt bijgehouden, niet gestraft voor korte sessies

### Hoe weet de module hoe lang een sessie duurt?

**Adaptive learning:**

1. **Eerste keer**: Default 15 seconden per herhaling
2. **Na sessie**: Meet werkelijke tijd ÷ aantal herhalingen
3. **Update**: Exponential moving average (30% nieuw, 70% oud)
4. **Volgende keer**: Gebruik geleerde T̄_CR voor voorspelling

### Kan ik de data exporteren?

**Nog niet**, maar staat op de roadmap:

- **Fase 2 Feature**: CSV export van TDS trends + calibration data
- **Workaround**: Browser console → `localStorage` copy-pasten

### Werkt het offline?

**Ja!** De module is 100% client-side:

- Geen internet nodig na eerste load
- Data in browser localStorage
- Berekeningen in JavaScript (geen server calls)

---

## 📚 Wetenschappelijke Bronnen

1. **Dr. Molly Gebrian (2013)**

   - "The benefits of overlearning in musical motor skills"
   - Overlearning framework (OLQ concept)

2. **Motor Learning Research**

   - 5 leerfases (Initial → Overlearning)
   - Spaced repetition + overlearning = permanent memory

3. **Ebbinghaus Forgetting Curve (1885)**
   - Foundation voor SRS scheduling
   - Complementary met Intensity Module

---

## 🆘 Problemen Oplossen

### Module display verschijnt niet tijdens practice

**Check:**

1. Is de module **enabled** in Settings?
2. Browser console errors? (F12 → Console tab)
3. `IntensityModule.js` geladen? (check Network tab)

**Fix:**

- Refresh de pagina (Ctrl+F5 / Cmd+Shift+R)
- Open Settings → Toggle UIT en weer AAN

### Analytics sectie is leeg

**Oorzaak:** Nog geen practice sessions voltooid

**Fix:**

- Voltooi minimaal 1 practice session
- Check dat je CR > 0 had in de sessie
- Refresh dashboard (F5)

### TDS percentage klopt niet

**Check:**

1. Console logs: `CR = X, FA = Y, TDS = Z%`
2. Formule: `TDS = CR / (CR + FA)`

**Voorbeeld:**

- CR = 5, FA = 5 → TDS = 50% ✅
- CR = 9, FA = 1 → TDS = 90% ✅

### T̄_CR lijkt te hoog/laag

**Oorzaak:** Adaptive calibration heeft meer data nodig

**Fix:**

- Voltooi 3-5 sessies voor accurate calibration
- Check of je niet pauzeerde tijdens timen (T̄_CR stijgt)
- Exponential moving average convergeert langzaam (by design)

---

## 📞 Support

Vragen of bugs? Check:

1. **Documentation**: `INTENSITY_MODULE_README.md` (API reference)
2. **Implementation**: `INTENSITY_MODULE_IMPLEMENTATION.md` (technical details)
3. **Examples**: `INTENSITY_MODULE_EXAMPLES.md` (code samples)
4. **Roadmap**: `INTENSITY_MODULE_ROADMAP.md` (future features)

---

**Version**: 1.1.0  
**Last Updated**: January 2025  
**Status**: ✅ Fully Operational
