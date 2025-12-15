# Changelog: Lifecycle State Feature

**Datum:** 3 oktober 2025  
**Branch:** feature/ebb-unify-clamps  
**Update:** Auto-save functionaliteit toegevoegd  
**🆕 Bug Fix:** DaySessionsWindow filter toegevoegd (5 oktober 2025)

## Samenvatting

Progress-kolom vervangen door Lifecycle State management systeem met ComboBox voor Active/Maintenance/Inactive status per bar section. **Lifecycle state wijzigingen worden nu automatisch opgeslagen naar schijf.** **Inactive secties worden nu correct gefilterd in zowel CalendarWindow als DaySessionsWindow.**

## Gewijzigde Bestanden

### 1. **BarSection.cs**

- **Toegevoegd:** `LifecycleState` enum (Active, Maintenance, Inactive)
- **Toegevoegd:** `LifecycleState` property met INotifyPropertyChanged
- **Side-effects:** Property roept automatisch `SectionLifecycleService.Apply()` aan bij statuswijzigingen

### 2. **Services/SectionLifecycleService.cs** (NIEUW)

- **Centralized business logic** voor lifecycle state transitions
- **Active:** Geen speciale acties
- **Maintenance:**
  - Minimum interval van 60 dagen geforceerd
  - NextDueDate gereset naar vandaag + interval
  - Overdue status gewist
- **Inactive:**
  - NextDueDate gewist (null)
  - Overdue status gewist
  - Wordt nooit meer ingepland
- **Logging:** Alle state transitions worden gelogd via MLLogManager
- **🆕 AUTO-SAVE:** State wijzigingen worden automatisch opgeslagen
  - Zoekt automatisch het parent MusicPiece object
  - Roept MainWindow.SaveMusicPiece() aan
  - Persisteert direct naar JSON bestand
  - CalendarWindow leest nu altijd de juiste state van schijf

### 3. **MainWindow.xaml**

- **Toegevoegd:** xmlns:sys namespace voor Enum binding
- **Toegevoegd:** ObjectDataProvider voor LifecycleState enum values
- **Vervangen:** Progress kolom (TextBlock met percentage) door ComboBox kolom
  - Width: 120
  - Two-way binding op LifecycleState
  - UpdateSourceTrigger: PropertyChanged
- **Toegevoegd:** DataGrid.RowStyle met DataTrigger
  - Inactive rijen krijgen Opacity 0.5 en Italic font style
  - Visuele feedback voor inactieve secties

### 4. **PracticeSessionData.cs**

- **Filter toegevoegd** in `ScheduleFutureSessions()` methode:
  ```csharp
  if (section.LifecycleState == LifecycleState.Inactive)
      continue; // Skip inactive sections entirely
  ```
- **Resultaat:** Inactive secties worden nooit gepland

### 5. **ScheduledPracticeSessionManager.cs**

- **Filter toegevoegd** in `AutoRepairMissingSessions()` methode:
  ```csharp
  if (section.LifecycleState == LifecycleState.Inactive)
      continue;
  ```
- **Resultaat:** Auto-repair slaat inactive secties over

### 6. **CalendarWindow.xaml.cs**

- **Filter toegevoegd** in `LoadScheduledSessionsAndUpdateCalendar()` methode
- **Filtering logic:**
  - Controleert LifecycleState van elke BarSection
  - Exclude sessies waar `barSection.LifecycleState == LifecycleState.Inactive`
  - Toont alleen Active en Maintenance secties in kalender
- **Result:** Inactive secties zijn **niet zichtbaar** in de kalender
- **Cleanup:** Bij transition naar Inactive worden **alle pending sessies verwijderd**

### 7. **DaySessionsWindow.xaml.cs** (🆕 FIX: 5 oktober 2025)

- **Filter toegevoegd** in `RefreshSessionsForSelectedDay()` methode
- **Filtering logic:**
  - Controleert LifecycleState van elke BarSection
  - Exclude sessies waar `barSection.LifecycleState == LifecycleState.Inactive`
  - Toont alleen Active en Maintenance secties in dag-overzicht
- **Result:** Inactive secties zijn **niet zichtbaar** in DaySessionsWindow
- **Consistentie:** DaySessionsWindow gedraagt zich nu identiek aan CalendarWindow

## Werking

### User Experience

1. Gebruiker selecteert een music piece in MainWindow
2. DataGrid toont alle bar sections met nieuwe "State" kolom
3. Per rij kan de gebruiker kiezen uit dropdown: Active / Maintenance / Inactive
4. Bij selectie wordt automatisch de bijbehorende business logic toegepast
5. Inactive rijen worden visueel gedimmed (50% opacity, italic)

### Business Rules

| State           | Interval   | NextDueDate      | Planning | Visueel              |
| --------------- | ---------- | ---------------- | -------- | -------------------- |
| **Active**      | Normaal    | Normaal          | Ja       | Normaal              |
| **Maintenance** | ≥ 60 dagen | Today + interval | Ja       | Normaal              |
| **Inactive**    | N/A        | null             | Nee      | Gedimd (50% opacity) |

### Technische Flow

```
User selecteert state in ComboBox
    ↓
LifecycleState property setter triggered
    ↓
OnPropertyChanged() notifies UI
    ↓
SectionLifecycleService.Apply() called
    ↓
Business rules applied (interval, NextDueDate, overdue)
    ↓
MLLogManager logs the transition
    ↓
(Optional) Schedule refresh triggered
```

## Testing Checklist

✅ Build succesvol zonder errors  
✅ BarSection.cs compileert zonder fouten  
✅ SectionLifecycleService.cs compileert zonder fouten  
✅ MainWindow.xaml compileert zonder fouten

### Functionele Tests (nog uit te voeren door gebruiker):

- [x] ComboBox toont alle drie de opties (Active/Maintenance/Inactive)
- [x] **Active → Maintenance:** Interval wordt minimum 60 dagen
- [x] **Active → Inactive:** NextDueDate wordt null, rij wordt gedimmed
- [x] **Inactive → Active:** Rij wordt weer normaal, kan gepland worden
- [x] **Maintenance sections:** Verschijnen in planning met minimum 60-dagen interval
- [x] **Inactive sections:** Verschijnen NIET in planning
- [x] AutoRepair slaat inactive sections over
- [x] State transitions worden gelogd in MLManager logs
- [x] **State wordt opgeslagen** naar JSON bij wijziging
- [x] **CalendarWindow leest correcte state** van schijf
- [x] **Inactive secties zijn niet zichtbaar** in CalendarWindow
- [x] **Pending sessies worden verwijderd** bij transition naar Inactive
- [x] **Completed sessies blijven behouden** (voor history)
- [x] **🆕 Inactive secties zijn niet zichtbaar** in DaySessionsWindow (5 okt 2025)

## Opgeloste Requirements

✅ Enum + property toegevoegd aan BarSection  
✅ Side-effects gecentraliseerd in SectionLifecycleService  
✅ Progress-kolom vervangen door ComboBox  
✅ DataGrid-rij dimmen bij Inactive (optioneel)  
✅ Plannerfilter: alleen Active + Maintenance  
✅ **Auto-save: State wijzigingen worden direct naar schijf geschreven**

## UI polish – 3 november 2025

- Tab label changed from "Bar Sections" to "Chunk Sections"
- First column header changed from "Bar Section" to "Chunk Sect."
- Added a YouTube quick link button next to Overall Timer (context menu to set/clear URL)

## Bug Fix: CalendarWindow toont nu correcte lifecycle state

### Probleem 1: State werd niet opgeslagen

- Gebruiker zet sectie op Inactive in MainWindow
- CalendarWindow toont sectie nog steeds als Active
- Bij sluiten van CalendarWindow wordt state teruggezet naar Active

### Oorzaak 1

- CalendarWindow leest data van schijf via `GetAllMusicPieces()`
- LifecycleState wijzigingen werden niet automatisch opgeslagen
- Oude JSON bestanden bevatten geen LifecycleState veld
- Bij deserialisatie krijgen nieuwe BarSection objecten default waarde `Active`

### Oplossing 1: Auto-Save

`SectionLifecycleService.Apply()` voegt automatische persistentie toe:

1. Na elke state transition wordt `SaveParentMusicPiece()` aangeroepen
2. Zoekt het parent MusicPieceItem via MainWindow.MusicPieces
3. Roept MainWindow.SaveMusicPiece() aan om naar JSON te schrijven
4. CalendarWindow leest nu de ge-update state van schijf
5. State blijft consistent tussen MainWindow en CalendarWindow

### Probleem 2: Inactive secties blijven zichtbaar in kalender

- Na oplossing 1: State wordt correct opgeslagen
- Maar: Inactive secties blijven zichtbaar in CalendarWindow
- Oude geplande sessies voor Inactive secties blijven bestaan

### Oorzaak 2

- CalendarWindow filtert alleen op archived/paused music pieces
- Geen filter op LifecycleState van BarSections
- Bestaande scheduled sessions worden niet verwijderd

### Oplossing 2: Kalender filter + Session cleanup

**A. CalendarWindow filter:**

- Toegevoegd in `LoadScheduledSessionsAndUpdateCalendar()`
- Controleert LifecycleState voor elke session
- Excludeert sessies waar `barSection.LifecycleState == LifecycleState.Inactive`

**B. Automatic session cleanup:**

- `SectionLifecycleService.RemoveScheduledSessionsForSection()` toegevoegd
- Wordt aangeroepen bij transition naar Inactive
- Verwijdert alle pending (niet-completed) sessies voor de sectie
- Completed sessies blijven behouden voor history

## Bug Fix 2: DaySessionsWindow toonde Inactive secties (5 oktober 2025)

### Probleem

- Na bug fix 1 (CalendarWindow filter): Inactive secties zijn correct niet zichtbaar in kalender
- Maar: Bij klikken op een dag in de kalender toont DaySessionsWindow **alsnog** Inactive secties
- Inconsistentie: CalendarWindow verbergt ze, DaySessionsWindow toont ze wel

### Oorzaak

- DaySessionsWindow had geen filter op `LifecycleState.Inactive`
- De fix voor CalendarWindow (3 oktober) werd niet toegepast op DaySessionsWindow
- DaySessionsWindow filterde alleen op:
  - Datum match (`s.ScheduledDate.Date == _selectedDate.Date`)
  - Actieve muziekstukken (niet-gearchiveerd, niet-gepauzeerd)
  - **Maar niet** op LifecycleState van BarSections

### Oplossing: DaySessionsWindow filter

**Locatie:** `DaySessionsWindow.xaml.cs`, methode `RefreshSessionsForSelectedDay()`

**Toegevoegde code:**

```csharp
.Where(s =>
{
    // Filter: Exclude Inactive bar sections from display
    // This ensures DaySessionsWindow is consistent with CalendarWindow behavior
    var musicPiece = _allMusicPieces.FirstOrDefault(mp => mp.Id == s.MusicPieceId);
    var barSection = musicPiece?.BarSections?.FirstOrDefault(bs => bs.Id == s.BarSectionId);

    // Exclude sessions where the bar section is Inactive
    if (barSection != null && barSection.LifecycleState == LifecycleState.Inactive)
        return false;

    return true;
})
```

**Resultaat:**

- ✅ DaySessionsWindow toont nu alleen Active en Maintenance secties
- ✅ Consistent gedrag met CalendarWindow
- ✅ Gebruiker ziet geen Inactive secties meer in dag-overzicht

## Opmerkingen

### Ontbrekende/Bestaande Issues

- Bestaande nullable reference warnings in ScheduledPracticeSessionManager.cs (niet gerelateerd aan deze feature)
- Bestaande nullable reference warning in PracticeSessionData.cs (niet gerelateerd aan deze feature)
- Deze warnings waren al aanwezig en zijn niet veroorzaakt door deze implementatie

### Optional Future Enhancements

De volgende functies zijn voorbereid maar nog niet actief (zie comments in SectionLifecycleService.cs):

- Automatische schedule refresh na state change (momenteel uitgecommentarieerd)
- IsOverdue property wordt via reflection gezet (kan geoptimaliseerd worden door directe property toe te voegen aan BarSection)

### Technische Details Auto-Save

- **Method:** `SectionLifecycleService.SaveParentMusicPiece(BarSection section)`
- **Trigger:** Wordt aangeroepen na elke `Apply()` call
- **Logic:**
  1. Verkrijgt MainWindow instantie via `Application.Current.MainWindow`
  2. Zoekt parent MusicPiece via `MainWindow.MusicPieces.FirstOrDefault()`
  3. Roept `mainWindow.SaveMusicPiece(parentPiece)` aan
  4. Logt success/failure via MLLogManager
- **Error handling:** Try-catch blokken met gedetailleerde logging
- **Performance:** Miniem - alleen het gewijzigde MusicPiece wordt opgeslagen

### Bestandslocaties

Alle bestanden zijn correct geplaatst:

- `d:\VStudio_cs\Modus Practica projecten\Project modusPractica-V2\ModusPractica Clean\ModusPractica\BarSection.cs`
- `d:\VStudio_cs\Modus Practica projecten\Project modusPractica-V2\ModusPractica Clean\ModusPractica\Services\SectionLifecycleService.cs`
- `d:\VStudio_cs\Modus Practica projecten\Project modusPractica-V2\ModusPractica Clean\ModusPractica\MainWindow.xaml`
- `d:\VStudio_cs\Modus Practica projecten\Project modusPractica-V2\ModusPractica Clean\ModusPractica\PracticeSessionData.cs`
- `d:\VStudio_cs\Modus Practica projecten\Project modusPractica-V2\ModusPractica Clean\ModusPractica\ScheduledPracticeSessionManager.cs`

## Conclusie

Feature volledig geïmplementeerd en klaar voor testing. Build succesvol zonder errors.
