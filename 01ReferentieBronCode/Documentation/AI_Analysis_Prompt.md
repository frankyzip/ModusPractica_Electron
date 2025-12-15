# AI Analysis Prompt: ModusPractica Ebbinghaus Spaced Repetition Algorithm

## Context

Ik heb een AI-gestuurde muziekoefen-app ontwikkeld die de Ebbinghaus vergeetcurve gebruikt voor optimale herhaalschema's. Het algoritme is recent uitgebreid met demografische personalisatie. Ik wil graag een grondige technische analyse van de werking en correctheid van deze planner.

## Algoritme Specificaties

### Core Ebbinghaus Implementatie

```
Retentie Functie: R(t) = (0.80 × e^(-t/τ)) + 0.15
- INITIAL_LEARNING_STRENGTH = 0.80 (80%)
- ASYMPTOTIC_RETENTION_BASELINE = 0.15 (15%)
- BASE_TAU_DAYS = 3.0 dagen (72 uur / 24)
```

### Demografische Personalisatie

**Leeftijdsaanpassingen (GetAgeAdjustedBaseTau):**

- 8-25 jaar: 2.0 dagen (snel leren, snel vergeten)
- 26-50 jaar: 3.0 dagen (baseline)
- 51+ jaar: 2.8 dagen (iets frequentere herhaling nodig)

**Geslachtsaanpassingen:**

> ⚠️ **VERWIJDERD IN v3.0** - Gender multipliers zijn niet langer aanwezig in de code. Effect size was te klein (d=0.2-0.3) en wetenschappelijk ondeugdelijk voor muziekvaardigheden.

**Ervaringsaanpassingen (GetExperienceAdjustedTau) - v3.0 GECORRIGEERD:**

- Beginner: τ × 0.8 (kortere intervallen - zwakkere encoding)
- Intermediate: τ × 1.0 (baseline)
- Advanced: τ × 1.1 (licht langere intervallen)
- Professional: τ × 1.3 (langere intervallen - sterkere encoding)

> **BELANGRIJK:** v3.0 correctie heeft de experience multipliers omgedraaid. Zie README.md v3.0 sectie voor volledige uitleg.

### Muziekspecifieke Aanpassingen

```
- MUSIC_MATERIAL_FACTOR = 3.0 (motorische vaardigheden)
- MOTOR_SKILL_FACTOR = 2.5
- REPETITION_STRENGTH_FACTOR = 1.3
```

### Moeilijkheidsaanpassingen

```
- Difficult: τ × 0.6 (40% reductie)
- Average: τ × 1.0 (geen aanpassing)
- Easy: τ × 1.7 (70% verhoging)
- Mastered: τ × 3.5 (250% verhoging)
```

### Retentiedoelen per Moeilijkheid

```
- Difficult: R* = 0.85 (85%)
- Default: R* = 0.80 (80%)
- Easy: R* = 0.70 (70%)
- Mastered: R* = 0.65 (65%)
```

### Veiligheidsbegrenzing

```
Tau grenzen: [1, 180] dagen
Interval grenzen: [1, 365] dagen EN ≤ 5×τ
Repetitie bonus: max 50% (1.0 + min(0.5, bonus × 0.08))
```

### Kern Algoritme Flow

```csharp
1. personalizedBaseTau = GetPersonalizedBaseTau(age, experience) // v3.0: gender removed
2. adjustedTau = personalizedBaseTau × MUSIC_MATERIAL_FACTOR × difficultyModifier
3. if (repetitionCount > 0): adjustedTau × (1 + repetitionBonus)
4. adjustedTau = ClampTauToSafeBounds(adjustedTau) // [1,180]
5. targetInterval = CalculateOptimalInterval(adjustedTau, retentionTarget)
6. finalInterval = ClampIntervalToScientificBounds(targetInterval, tau) // [1,365] & ≤5×τ
```

## Te Analyseren Aspecten

### 1. Wetenschappelijke Correctheid

- **Ebbinghaus Model**: Is de exponentiële vervulfunctie R(t) = (0.80 × e^(-t/τ)) + 0.15 correct geïmplementeerd?
- **Demografische Aanpassingen**: Zijn de leeftijds-, geslachts- en ervaringsaanpassingen wetenschappelijk onderbouwd?
- **Muziekspecifieke Factoren**: Is MUSIC_MATERIAL_FACTOR = 3.0 realistisch voor motorische vaardigheden?
- **Retentiedoelen**: Zijn de verschillende R\* waarden (65%-85%) praktisch haalbaar?

### 2. Mathematische Stabiliteit

- **Numerieke Stabiliteit**: Kunnen extreme tau-waarden (1-180 dagen) tot overflow/underflow leiden?
- **Clamping Logic**: Is de τ ∈ [1,180] en interval ∈ [1,365] & ≤5×τ begrenzing correct?
- **Edge Cases**: Hoe gedraagt het algoritme bij extreem hoge/lage repetitiecounts?
- **Convergentie**: Convergeert het algoritme naar stabiele waarden?

### 3. Planningslogica

- **Optimale Timing**: Berekent het algoritme echt het optimale moment (R\* bereikt)?
- **Adaptieve Feedback**: Past het algoritme correct aan op gebruikersprestaties?
- **Interval Progressie**: Volgt de interval-uitbreiding een logische progressie?
- **Performance Degradation**: Hoe reageert het systeem op falende sessies?

### 4. Demografische Validiteit

- **Age Stratification**: Zijn de 2.0/3.0/2.8 dagen leeftijdsgrenzen realistisch?
- **Gender Differences**: ⚠️ NIET MEER VAN TOEPASSING - Gender multipliers zijn verwijderd in v3.0
- **Experience Levels**: Klopt de beginner(0.8x) → professional(1.3x) progressie? (v3.0 GECORRIGEERD)
- **Interaction Effects**: Kunnen combinaties van demografische factoren tot onrealistische tau-waarden leiden?

### 5. Fallback & Error Handling

- **Graceful Degradation**: Valt het systeem correct terug naar baseline bij fouten?
- **Input Validation**: Worden alle edge cases (NaN, Infinity, negatieve waarden) afgevangen?
- **Settings Unavailable**: Werkt het algoritme zonder demografische gegevens?

### 6. Performance & Efficiency

- **Computational Complexity**: Is het algoritme efficiënt genoeg voor real-time gebruik?
- **Memory Footprint**: Zijn er geheugen-leaks of inefficiënties?
- **Scalability**: Schaalt het algoritme naar duizenden gebruikers/sessies?

### 7. Praktische Implementatie

- **User Experience**: Produceert het algoritme intuïtieve herhaalschema's?
- **Learning Curve**: Adapteert het systeem snel genoeg aan individuele gebruikers?
- **Long-term Stability**: Blijven de intervallen stabiel bij langdurig gebruik?

## Specifieke Vragen

1. **Tau-waarde realisme**: Is een BASE_TAU van 3 dagen realistisch voor muzikale motorische vaardigheden, gezien traditioneel Ebbinghaus τ ≈ 1 dag voor verbale informatie?

2. **Demografische interacties**: Kan een 51-jarige professionele muzikant extreme tau-waarden krijgen? (2.8 × 1.3 × 3.0 = ~10.9 dagen) - v3.0: gender factor verwijderd

3. **Interval capping**: Is de "≤ 5×τ" regel wetenschappelijk onderbouwd of arbitrair?

4. **Retentie asymptoot**: Waarom is ASYMPTOTIC_RETENTION_BASELINE = 0.15 gekozen? Is dit realistisch voor motorische vaardigheden?

5. **Performance feedback**: Hoe wordt de tau aangepast op basis van daadwerkelijke gebruikersprestaties? (AdaptiveTauManager, PersonalizedMemoryCalibration)

6. **Difficulty scaling**: Zijn de moeilijkheidsmodifiers (0.6 voor Difficult, 3.5 voor Mastered) experimenteel geverifieerd?

## Verwachte Output

Geef een gestructureerde analyse met:

- ✅ **Correct/Sterk**: Aspecten die wetenschappelijk onderbouwd en goed geïmplementeerd zijn
- ⚠️ **Twijfelachtig**: Aspecten die mogelijk verbeterd kunnen worden
- ❌ **Problematisch**: Duidelijke fouten of risico's
- 🔬 **Onderzoek Nodig**: Aspecten die empirische validatie vereisen

Prioriteer praktische aanbevelingen voor verbetering van de algoritme-nauwkeurigheid en gebruikerservaring.

---

**Technische Context**: C# .NET 8.0 WPF desktop applicatie, real-time spaced repetition scheduling, adaptieve personalisatie via Ebbinghaus-curve met demographic factors (v3.0: gender verwijderd, experience multipliers gecorrigeerd).

> ⚠️ **v3.0 Update Note**: ML.NET integratie is verwijderd. Dit document beschrijft nu het pure Ebbinghaus + adaptive scheduling systeem.
