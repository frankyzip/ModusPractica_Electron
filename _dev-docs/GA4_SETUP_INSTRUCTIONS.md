# Google Analytics 4 (GA4) Setup Instructies voor ModusPractica

## Status

✅ **GA4 tracking code is geïnstalleerd in alle HTML-bestanden**
✅ **Custom event tracking is toegevoegd aan alle belangrijke acties**
✅ **Developer exclusion werkt automatisch op localhost**

---

## Wat je nog moet doen

### 1. Google Analytics Property aanmaken

1. Ga naar [Google Analytics](https://analytics.google.com/)
2. Klik op **Admin** (tandwiel icoon linksonder)
3. Klik op **+ Create Property**
4. Vul in:
   - **Property name**: ModusPractica
   - **Time zone**: Belgium (GMT+1)
   - **Currency**: EUR - Euro
5. Klik op **Next** → **Create**
6. Kies **Web** als platform
7. Vul in:
   - **Website URL**: https://parturamusic.be
   - **Stream name**: ModusPractica Web App
8. Klik op **Create stream**

### 2. Measurement ID kopiëren

Na het aanmaken zie je een **Measurement ID** in dit formaat: `G-XXXXXXXXXX`

**Kopieer deze ID!**

### 3. Measurement ID plaatsen in je code

Vervang in **ALLE** onderstaande bestanden de tekst `G-XXXXXXXXXX` door je echte Measurement ID:

#### HTML-bestanden:

- ✅ `moduspractica/moduspractica-app.html` (regel ~34)
- ✅ `moduspractica/moduspractica-dashboard.html` (regel ~34)
- ✅ `moduspractica/moduspractica-practice-session.html` (regel ~12)
- ✅ `moduspractica/moduspractica-piece-detail.html` (regel ~12)
- ✅ `moduspractica/moduspractica-calendar.html` (regel ~16)
- ✅ `moduspractica/moduspractica-statistics.html` (regel ~16)

**Zoek in elk bestand naar:**

```html
<script
  async
  src="https://www.googletagmanager.com/gtag/js?id=G-XXXXXXXXXX"
></script>
```

En ook:

```javascript
gtag('config', 'G-XXXXXXXXXX', {
```

**Vervang beide `G-XXXXXXXXXX` door je echte ID.**

### 4. Testen op localhost

1. Start de server: `python -m http.server 8080`
2. Open: `http://localhost:8080/moduspractica/moduspractica-app.html`
3. Open **Browser Console** (F12)
4. Je zou moeten zien: `[GA4] Developer mode - tracking disabled`

Dit betekent dat **je eigen ontwikkelaar-gebruik NIET wordt getracked** 🎉

### 5. Testen op productie

Na deployment naar parturamusic.be:

1. Open de site in een **incognito venster**
2. Open **Browser Console** (F12)
3. Je zou moeten zien: `[GA4] Event tracked: page_view`
4. Ga naar Google Analytics → **Reports** → **Realtime**
5. Je zou jezelf live moeten zien als gebruiker

---

## Wat wordt er getracked?

### Automatisch getrackte events:

#### **Page Views**

- Profile Selection pagina geladen
- Dashboard geladen
- Practice Session geladen

#### **Profile Management**

- `profile_created` - Nieuw profiel aangemaakt (met naam en leeftijdsgroep)
- `profile_selected` - Profiel geselecteerd
- `profile_saved` - Profiel opgeslagen (manual/auto)

#### **Piece Management**

- `piece_added` - Muziekstuk toegevoegd (met titel, componist, aantal secties)
- `piece_deleted` - Muziekstuk verwijderd
- `section_added` - Sectie toegevoegd aan stuk

#### **Practice Sessions**

- `practice_session_started` - Oefensessie gestart (met stuk + sectie info)
- `practice_session_completed` - Sessie voltooid (met duur, performance rating, success rate)
- `practice_session_cancelled` - Sessie geannuleerd (met verstreken tijd)

#### **Ebbinghaus Algorithm**

- `next_review_calculated` - Volgende review datum berekend (met interval en retention target)
- `memory_stability_updated` - Memory stability score bijgewerkt

#### **Data Management**

- `data_exported` - Data geëxporteerd (single-profile of full-backup)
- `data_imported` - Data geïmporteerd

#### **Errors**

- `error_occurred` - Fout opgetreden in de app

---

## Developer Exclusion - Hoe het werkt

De tracking code detecteert automatisch of je op localhost werkt:

```javascript
const isDeveloper =
  window.location.hostname === "localhost" ||
  window.location.hostname === "127.0.0.1" ||
  window.location.hostname === "";
```

**Localhost**: Geen tracking (console log: `[GA4] Developer mode - tracking disabled`)
**Productie**: Volledige tracking actief

Je hoeft **niets** anders te doen! 🎉

---

## Custom Events handmatig tracken (optioneel)

Als je extra events wilt tracken, gebruik dan:

```javascript
// Voorbeeld: Track een custom actie
if (window.ga4Tracker) {
  window.ga4Tracker.trackEvent("custom_action", {
    action_type: "button_click",
    button_name: "special_feature",
  });
}
```

Alle beschikbare functies staan in: `moduspractica/ga4-tracker.js`

---

## Belangrijke tips

### ✅ DO's

- Test altijd eerst op localhost (geen tracking)
- Test daarna in incognito op productie
- Controleer Realtime reports in GA4
- Laat tracking 48 uur draaien voor betrouwbare data

### ❌ DON'Ts

- Vergeet niet je Measurement ID te vervangen!
- Gebruik geen developer browser extensions die tracking blokkeren
- Klik niet teveel zelf op productie (vervuilt data)

---

## Google Analytics 4 Dashboard

### Nuttige rapporten om te checken:

1. **Realtime** - Zie live gebruikers
2. **Events** - Alle getrackte events en hun frequency
3. **User attributes** - Welke browsers/devices gebruiken mensen
4. **Pages and screens** - Meest bezochte pagina's

### Conversies instellen (optioneel):

Ga naar **Admin** → **Events** → Selecteer belangrijk event (bijv. `practice_session_completed`) → Klik **Mark as conversion**

Dit laat je zien hoeveel gebruikers een complete practice sessie voltooien.

---

## Ondersteuning

Als je vragen hebt over:

- **GA4 setup**: Check [Google Analytics Help](https://support.google.com/analytics)
- **Tracking code**: Bekijk `moduspractica/ga4-tracker.js`
- **Event debugging**: Open Browser Console (F12) voor logs

---

## Changelog

**18 november 2025**: Initiële GA4 implementatie

- ✅ GA4 tracking code toegevoegd aan 6 HTML-bestanden
- ✅ Developer exclusion geïmplementeerd (localhost detection)
- ✅ Custom event tracker class gemaakt (`ga4-tracker.js`)
- ✅ 15+ custom events geïmplementeerd voor belangrijke acties
- ✅ Page view tracking voor alle belangrijke pagina's

---

## Checklist voor deployment

- [ ] Measurement ID vervangen in alle 6 HTML-bestanden
- [ ] Testen op localhost (moet "Developer mode" tonen)
- [ ] Deployment naar parturamusic.be
- [ ] Testen op productie in incognito mode
- [ ] Controleren in Google Analytics Realtime
- [ ] 48 uur wachten voor betrouwbare statistieken
- [ ] Belangrijke events markeren als conversions (optioneel)

---

**Status**: 🟢 Klaar voor deployment na Measurement ID update
