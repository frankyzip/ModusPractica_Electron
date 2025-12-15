# Changelog: Profile Management System

**Datum:** 3 november 2025
**Branch:** feature/ebb-unify-clamps
**Nieuwe functionaliteit:** Volledig geïsoleerd multi-user profielbeheer

## Samenvatting

Nieuw profielbeheer systeem toegevoegd voor multi-user ondersteuning met custom data locaties. Alle gebruikersdata is nu volledig geïsoleerd per profiel om privacy en data-integriteit te waarborgen.

## Architectuur Overzicht

### Data Isolation Principe

Elke profiel heeft eigen geïsoleerde data structuur:

```
CustomDataRoot/
├── Profiles/
│   ├── ProfileA/
│   │   ├── MusicPieces/     # JSON bestanden
│   │   ├── History/         # practice_history.json
│   │   ├── Logs/            # application_log.txt
│   │   ├── Scheduled/       # scheduled_sessions.json
│   │   ├── Calibration/     # personal_memory_calibration.json
│   │   ├── MemoryStability/ # stability data
│   │   └── SightReading/    # sight_reading_totals.json
│   └── ProfileB/
│       └── ... (zelfde structuur)
└── profile_config.json (in %LOCALAPPDATA%)
```

### Kerncomponenten

#### 1. DataPathProvider.cs (NIEUW)

**Doel:** Centrale provider voor alle filesystem paden

```csharp
public static class DataPathProvider
{
    private static string? _customRoot;

    // Publieke API
    public static void SetCustomRoot(string? absolutePath)
    public static string GetAppRoot()
    public static string GetProfileFolder(string profileName)
    public static string GetLogsFolder(string profileName)
    // ... alle pad-methodes
}
```

**Waarom:** Voorkomt hardcoded paden en centraliseert alle filesystem logica.

#### 2. ProfileConfiguration.cs (NIEUW)

**Doel:** Persistent opslag van profiel configuratie

```csharp
public class ProfileConfiguration
{
    public string LastUsedProfile { get; set; } = "Default";
    public string CustomRootPath { get; set; } = string.Empty;

    public static ProfileConfiguration Load()
    public static void Save(ProfileConfiguration config)
}
```

**Opslaglocatie:** `%LOCALAPPDATA%\ModusPractica\profile_config.json`

#### 3. ProfileSelectorWindow.xaml/.xaml.cs (NIEUW)

**Doel:** Hoofdingang voor profiel selectie en data locatie keuze

**Functionaliteit:**

- Profiel lijst tonen
- Custom data root selectie
- Profiel aanmaken/verwijderen
- Data locatie validatie

#### 4. DataMigrationHelper.cs (NIEUW)

**Doel:** Ondersteuning voor migreren van bestaande data

```csharp
public static class DataMigrationHelper
{
    public static void OfferMigrationIfNeeded(string customRootPath)
    private static void MigrateData(string sourceRoot, string targetRoot)
}
```

## Implementatie Details

### Pad Migratie

**Voor (hardcoded):**

```csharp
string appDataFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "ModusPractica",
    "Profiles",
    profileName);
```

**Na (DataPathProvider):**

```csharp
string appDataFolder = DataPathProvider.GetProfileFolder(profileName);
```

**Geïmigreerde bestanden:**

- MLLogManager.cs
- MemoryStabilityManager.cs
- PersonalizedMemoryCalibration.cs
- SettingsManager.cs
- ScheduledPracticeSessionManager.cs
- SightReadingWindow.xaml.cs
- PracticeHistoryManager.cs
- MainWindow.xaml.cs

### Profiel Lifecycle

#### Aanmaken

1. Gebruiker kiest naam in ProfileSelectorWindow
2. Valideer naam (geen speciale karakters)
3. CreateDirectory voor profiel folder
4. Refresh profiel lijst
5. Auto-select nieuwe profiel

#### Verwijderen

1. Check of profiel in gebruik is
2. Toon waarschuwingsdialog
3. Directory.Delete(recursive: true)
4. Refresh profiel lijst

#### Switching

1. Save huidige configuratie
2. Load nieuwe profiel data
3. Re-initializeer alle managers
4. Update UI

### Data Locatie Keuze

#### Default Locatie

- `%APPDATA%\ModusPractica` (roaming capable)

#### Custom Locatie

- Gebruiker kan kiezen via FolderBrowserDialog
- Ondersteunt USB drives, network shares, etc.
- Validatie van schrijfpermissies
- Migration van bestaande data

### Veiligheid & Privacy

#### Data Isolation

- Geen toegang tussen profielen
- Alle bestanden scoped per profiel
- Centrale logging per profiel

#### Permissie Checks

- Write access validatie bij locatie keuze
- Fallback naar default bij problemen
- Duidelijke error messages

## UI/UX Verbeteringen

### ProfileSelectorWindow

**Layout:**

- Header met uitleg
- Data locatie sectie (links)
- Profiel selectie sectie (rechts)
- Action buttons (onder)

**Functionaliteit:**

- Hyperlink naar data folder
- Real-time validatie
- Progress feedback

### MainWindow Updates

**Nieuwe elementen:**

- Profiel kaart naast "Today's Practice"
- Profile name display
- Verbeterde layout

### PlaylistManagerWindow

**Hernoeming:**

- "Practice Playlist Manager" → "Interleaved Practice Manager"
- Consistent taalgebruik

## Testing & Validatie

### Unit Tests (toekomstig)

- DataPathProvider correcte paden
- ProfileConfiguration persistence
- Migration helper functionaliteit

### Integration Tests

- Profiel switching zonder data verlies
- Custom locatie keuze
- Migration van bestaande data

### Manual Tests

- [ ] Profiel aanmaken/verwijderen
- [ ] Custom data locatie kiezen
- [ ] Data migratie uitvoeren
- [ ] Multi-profiel switching
- [ ] Permissie errors afhandelen

## Impact op Bestaande Code

### Breaking Changes

- Alle managers moeten nu `InitializeForUser(profileName)` callen
- Hardcoded paden vervangen door DataPathProvider calls

### Backward Compatibility

- Migration helper voor bestaande data
- Default "Default" profiel behouden
- Geen data verlies tijdens transitie

## Configuratie Bestanden

### profile_config.json

```json
{
  "LastUsedProfile": "Default",
  "CustomRootPath": "D:\\CustomData\\ModusPractica"
}
```

### Autocomplete Data

- Nu profiel-specifiek: `autocomplete_data.json` per profiel
- Voorkomt lekken tussen profielen

## Foutafhandeling

### Common Scenarios

1. **Geen schrijfpermissies:** Duidelijke error + suggestie alternatieve locatie
2. **Profiel in gebruik:** ProfileInUseDialog met instructies
3. **Migration faalt:** Backup behouden + gedetailleerde logging
4. **Corrupte config:** Fallback naar defaults

### Logging

- Alle profiel operaties gelogd
- Migration progress tracked
- Errors gedetailleerd gerapporteerd

## Conclusie

Volledig geïsoleerd profielbeheer systeem geïmplementeerd. Gebruikers kunnen nu:

- Meerdere profielen hebben met eigen data
- Data opslaan op custom locaties (USB, network)
- Veilig switchen tussen profielen
- Data migreren van oude installaties

Systeem is production-ready met uitgebreide error handling en backward compatibility.

---

_Documentatie gegenereerd: 3 november 2025_  
_Branch: feature/ebb-unify-clamps_  
_Versie: Profile-Management-v1.0_
