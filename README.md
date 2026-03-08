# TripPlanner – Persönliche Reiseplanung

Eine moderne **Blazor Server**-Webanwendung zur Reiseplanung und Verwaltung von Reisewunschlisten – mit **Multi-User-Unterstützung, Sharing-Funktionen, KI-Assistent und MCP-Server-Integration** auf Basis von .NET 10, Entity Framework Core, ASP.NET Core Identity und Fluent UI Components.

## Screenshots

### Startseite
![Startseite](https://github.com/user-attachments/assets/4b741916-4910-464d-8e9f-8b8d9d511401)

### Registrierung
![Registrierung](https://github.com/user-attachments/assets/21436adc-170b-4a02-8671-b8247edc7ac1)

### Anmeldung
![Anmeldung](https://github.com/user-attachments/assets/0601eaa9-e6eb-4b02-bf80-12e34f0f56c9)

### Wunschlisten
![Wunschlisten](https://github.com/user-attachments/assets/6a236f13-8d5d-4b62-a928-af2940cb847e)

### Reisen
![Reisen](https://github.com/user-attachments/assets/7e49bee7-de4b-4f4d-8a5a-20c5473c5224)

---

## Features

### 1. Benutzerverwaltung & Authentifizierung
- **Registrierung und Login** mit ASP.NET Core Identity
- Sichere Passwortanforderungen
- E-Mail-basierte Benutzeridentifikation
- Optionale Angabe des Home-Standorts (Koordinaten + Name)
- **Passkey-Unterstützung** (WebAuthn) für passwortlose Anmeldung
- Verwaltung von Passkeys unter „Account → Passkeys"

### 2. Wunschlisten (Wishlists)
- **Mehrere Wunschlisten erstellen** für verschiedene Reiseziele (z. B. „Europareise 2025", „Strandurlaub-Ideen")
- Jede Wunschliste kann mehrere Orte enthalten
- **Wunschlisten mit anderen Nutzern teilen** (per E-Mail-Adresse)
- Gemeinsames Anzeigen und Bearbeiten geteilter Listen
- Vollständige CRUD-Operationen für Wunschlisten

### 3. Orte verwalten (Places)
- Orte mit ausführlichen Informationen speichern:
  - Name und Beschreibung
  - Kategorie (Aussichtspunkt, Museum, Restaurant, Natur usw.)
  - GPS-Koordinaten (Breiten-/Längengrad)
  - Tags zur Organisation
  - Optionales Bild (Upload oder URL)
  - Optionaler GPX-Track-Upload
  - Zuordnung zu einer Wunschliste oder Reise
  - Besuchsdatum (von/bis)
  - Notizen und optionale URL
- Orte nach Kategorie, Tags oder GPX-Track filtern
- Responsive Kachel-Ansicht aller Orte
- **Ort per URL importieren**: URL einer Webseite eingeben → KI analysiert die Seite und befüllt die Ortsdaten automatisch

### 4. Reiseplanung (Trips)
- Mehrtägige Reisen mit detaillierten Tagesitinerarien erstellen
- **Eigentümerschaft und Sharing**: Jede Reise hat einen Besitzer und kann mit anderen geteilt werden
- Pro Reise:
  - Mehrere Tage hinzufügen
  - Orte für bestimmte Zeiten planen
  - Dauer je Aktivität festlegen
  - Notizen pro Ort hinterlegen
  - Unterkünfte mit Check-in/-out-Zeiten verwalten
- Automatische Reiseanalyse:
  - Gesamtdauer berechnen
  - Fahrtzeiten zwischen Orten schätzen
  - Planungskonflikte erkennen
  - Warnung bei überfüllten Tagen
- **Wettervorhersage** direkt im Reiseplan (14-Tage-Prognose via Open-Meteo)
- Eigene und geteilte Reisen in einer Übersicht

### 5. Kartenansicht (Map)
- Interaktive Kartenansicht (MapLibre GL JS Integration)
- Orte der Wunschlisten und Reiserouten anzeigen
- Karteninhalt nach Reise filtern
- Seitenpanel mit Ortsdetails

### 6. KI-Assistent (AI Trip Assistant)
- **Chat-Interface** unter `/chat` mit einem lokalen Sprachmodell (Ollama)
- Der Assistent kann Reisen, Wunschlisten und Orte verwalten:
  - Reisen und Wunschlisten auflisten, anlegen, bearbeiten und löschen
  - Orte anlegen, bearbeiten, löschen und nach Kategorie filtern
- Conversation-History je Browser-Tab (isoliert pro Sitzung)
- Konfigurierbar über `appsettings.json` (Modell, Basis-URL, History-Länge)
- Standard-Modell: `llama3.2`

### 7. MCP-Server (Model Context Protocol)
- **MCP-Endpunkt** unter `/mcp` (HTTP-Transport)
- Ermöglicht externen KI-Agenten (z. B. Claude Desktop) den Zugriff auf TripPlanner-Daten
- Verfügbare Tool-Gruppen:
  - **Trips**: `list_trips`, `get_trip`, `create_trip`, `update_trip`, `delete_trip`
  - **Wishlists**: `list_wishlists`, `get_wishlist`, `create_wishlist`, `update_wishlist`, `delete_wishlist`
  - **Places**: `list_places`, `get_place`, `create_place`, `update_place`, `delete_place`
- **API-Key-Authentifizierung**: Schlüssel generieren unter „Account → MCP API Key"
  - Der Schlüssel wird als SHA-256-Hash gespeichert (Klartext nur einmalig angezeigt)
  - Bearer-Token-Authentifizierung über den `Authorization`-Header
- MCP-Client-Konfiguration (z. B. für Claude Desktop):
  ```json
  {
    "mcpServers": {
      "tripplanner": {
        "type": "http",
        "url": "https://<your-host>/mcp",
        "headers": {
          "Authorization": "Bearer <your-api-key>"
        }
      }
    }
  }
  ```

---

## Technische Architektur

### Projektstruktur
```
TripPlanner.Web/
├── Auth/                     # Authentifizierungslogik
│   └── McpApiKeyAuthHandler.cs  # Bearer-API-Key-Auth für MCP
├── Data/                     # Datenbankkontext
│   └── ApplicationDbContext.cs
├── McpTools/                 # MCP-Tool-Implementierungen
│   ├── PlaceMcpTools.cs      # CRUD-Tools für Orte
│   ├── TripMcpTools.cs       # CRUD-Tools für Reisen
│   └── WishlistMcpTools.cs   # CRUD-Tools für Wunschlisten
├── Models/                   # Domain-Entities
│   ├── ApplicationUser.cs    # Identity-User mit McpApiKeyHash, DisplayName
│   ├── Wishlist.cs           # Wunschliste mit Sharing-Unterstützung
│   ├── Place.cs              # Ort mit Wunschlisten-/Reisezuordnung
│   ├── PlaceImage.cs         # Bilddaten eines Ortes
│   ├── PlaceSuggestion.cs    # KI-generierter Ortsvorschlag
│   ├── PlaceAnalysisResult.cs # Ergebnis der URL-Analyse
│   ├── Trip.cs               # Reise mit Eigentümerschaft
│   ├── SharedTrip.cs         # m:n User-Trip-Sharing
│   ├── Accommodation.cs      # Unterkunft mit Check-in/-out
│   ├── UrlImportJob.cs       # Hintergrundjob für URL-Import
│   ├── UrlImportJobStatus.cs # Status des Import-Jobs
│   ├── GpxTrack.cs
│   └── PlaceCategory.cs
├── Repositories/             # Datenzugriffsschicht
│   ├── IWishlistRepository.cs / WishlistRepository.cs
│   ├── IPlaceRepository.cs   / PlaceRepository.cs
│   ├── ITripRepository.cs    / EfTripRepository.cs
│   ├── IGpxRepository.cs     / EfGpxRepository.cs
│   └── IUrlImportJobRepository.cs / UrlImportJobRepository.cs
├── Services/                 # Geschäftslogik
│   ├── UserService.cs        # Authentifizierungs-Hilfsmethoden
│   ├── GpxService.cs         # GPX-Parsing und Berechnungen
│   ├── RoutingService.cs     # Distanz- und Zeitberechnungen
│   ├── OllamaChatService.cs  # KI-Chatbot (Ollama)
│   ├── OllamaPlaceAnalysisService.cs # KI-gestützte URL-Analyse
│   ├── UrlImportBackgroundService.cs # Hintergrunddienst für URL-Importe
│   ├── NominatimGeocodingService.cs  # Geocoding via OpenStreetMap
│   ├── WeatherService.cs     # Wettervorhersage via Open-Meteo
│   ├── IGeocodingService.cs
│   └── IPlaceAnalysisService.cs
└── Components/
    ├── Account/              # Authentifizierungsseiten (Login, Register, …)
    │   └── Pages/Manage/
    │       ├── ApiKey.razor  # MCP API-Key-Verwaltung
    │       ├── HomeLocation.razor
    │       └── Passkeys.razor
    ├── Layout/               # MainLayout.razor, NavMenu.razor
    └── Pages/
        ├── Home.razor
        ├── Chat/             # ChatPage (KI-Assistent)
        ├── Places/           # PlacesPage
        ├── Wishlists/        # WishlistsPage, WishlistDetailPage
        ├── Trips/            # TripsPage, TripPlanPage
        └── Map/              # MapPage
```

### Technologien
- **Framework**: .NET 10, ASP.NET Core Blazor Server
- **Datenbank**: Entity Framework Core 10.0.3 mit **SQL Server**
- **Authentifizierung**: ASP.NET Core Identity (Cookie-basiert) + Passkeys (WebAuthn) + MCP API-Key
- **UI-Bibliothek**: Microsoft Fluent UI Blazor Components 4.14.0
- **Karte**: MapLibre GL JS (Integration in MapPage)
- **KI/LLM**: Ollama (lokales Sprachmodell, Standard: `llama3.2`)
- **MCP**: ModelContextProtocol.AspNetCore 1.0.0
- **Geocoding**: OpenStreetMap Nominatim API
- **Wetter**: Open-Meteo API (kostenlos, keine Registrierung erforderlich)
- **Orchestrierung**: .NET Aspire 13.0 (`TripPlanner.AppHost`)

### Wichtige Services

#### GpxService
- GPX-Dateien parsen (XML-Format)
- Track-Statistiken berechnen:
  - Gesamtdistanz (Haversine-Formel)
  - Höhengewinn und -verlust
  - Track-Punkt-Analyse

#### RoutingService
- Distanzen zwischen Orten berechnen
- Fahrtzeiten schätzen (Standard: 50 km/h)
- Tagesanalyse pro Reise:
  - Gesamtdauer (Aktivitäten + Fahrten)
  - Planungskonflikte erkennen
  - Warnung bei unrealistischen Zeitplänen

#### OllamaChatService
- Kommunikation mit lokalem Ollama-Server (`/api/chat`)
- Integrierte Tool-Calls für Reisen, Wunschlisten und Orte (CRUD-Operationen, s. MCP-Sektion)
- Conversation-History pro Blazor-Circuit (Browser-Tab-Isolierung)
- Konfigurierbar über `Ollama`-Sektion in `appsettings.json`

#### OllamaPlaceAnalysisService
- Ruft URL ab und extrahiert Text der Webseite
- Sendet den Text an Ollama und bittet um JSON-Ortsvorschlag
- Nutzt NominatimGeocodingService für Koordinaten-Ermittlung
- Ergebnis wird als `PlaceAnalysisResult` zurückgegeben

#### WeatherService
- Ruft 14-Tage-Wettervorhersage für GPS-Koordinaten ab
- Verwendet die kostenlose Open-Meteo API (kein API-Key erforderlich)
- In-Memory-Cache zur Vermeidung doppelter Anfragen

#### NominatimGeocodingService
- Wandelt Ortsnamen in GPS-Koordinaten um
- Verwendet die kostenlose OpenStreetMap Nominatim API

### Datenmodelle

**ApplicationUser** (erweitert IdentityUser)
```csharp
- Id: string (GUID – aus Identity)
- Email: string (aus Identity)
- DisplayName: string?
- HomeLatitude: double?
- HomeLongitude: double?
- HomeLocationName: string?
- McpApiKeyHash: string?  // SHA-256-Hash des MCP API-Keys
- Wishlists: List<Wishlist>
- SharedWishlists: List<UserWishlist>
- OwnedTrips: List<Trip>
- SharedTrips: List<SharedTrip>
```

**Wishlist**
```csharp
- Id: string (GUID)
- Name: string
- Description: string?
- Places: List<Place>
- SharedWith: List<UserWishlist>  // inkl. ShareLevel (Owner/Viewer)
- CreatedAt, UpdatedAt: DateTime
```

**Place**
```csharp
- Id: string (GUID)
- Name: string
- Description: string
- Category: PlaceCategory (enum)
- Latitude, Longitude: double
- Tags: List<string>
- Notes: string?
- Url: string?
- VisitDate: DateTime? / VisitDateEnd: DateTime?
- GpxTrackId: string? (optional)
- WishlistId: string? (optional)
- TripId: string? (optional)
- NeedsReview: bool  // true wenn per URL-Import erstellt
- CreatedAt, UpdatedAt: DateTime
```

**Trip**
```csharp
- Id: string (GUID)
- Name: string
- Description: string
- StartDate, EndDate: DateTime?
- OwnerId: string (FK to ApplicationUser)
- Days: List<TripDay>
- UnscheduledPlaces: List<TripPlace>
- Accommodations: List<Accommodation>
- SharedWith: List<SharedTrip>
- CreatedAt, UpdatedAt: DateTime
```

**Accommodation**
```csharp
- Id: string (GUID)
- TripId: string (FK to Trip)
- Name: string
- Address: string?
- PlannedCheckIn, PlannedCheckOut: DateTime?
- EarliestCheckIn: TimeOnly?
- LatestCheckOut: TimeOnly?
- Latitude, Longitude: double
- Link: string?
- Notes: string?
```

**UrlImportJob**
```csharp
- Id: string (GUID)
- WishlistId: string (FK to Wishlist)
- Url: string
- Status: UrlImportJobStatus (Pending / Processing / Completed / Failed)
- CreatedPlaceId: string?  // nach erfolgreicher Verarbeitung
- AiPrompt: string?
- AiResponse: string?
- CreatedByUserId: string
- CreatedAt: DateTime / ProcessedAt: DateTime?
```

**UserWishlist** (m:n User ↔ Wishlist)
```csharp
- UserId: string (FK)
- WishlistId: string (FK)
- Level: ShareLevel (Owner / Viewer)
- SharedAt: DateTime
```

**SharedTrip** (m:n User ↔ Trip)
```csharp
- UserId: string (FK)
- TripId: string (FK)
- SharedAt: DateTime
```

---

## Einrichtung & Entwicklung

### Voraussetzungen
- .NET 10 SDK
- **SQL Server** (lokal oder per Docker)
- Visual Studio 2022, VS Code oder Rider
- **Ollama** (optional, für KI-Assistent und URL-Import)

### Erste Schritte

1. **Repository klonen**
   ```bash
   git clone <repository-url>
   cd TripPlanner
   ```

2. **SQL Server starten** (z. B. per Docker)
   ```bash
   docker run -d --name tripplanner-sqlserver \
     -e "ACCEPT_EULA=Y" \
     -e "SA_PASSWORD=YourStrong!Passw0rd" \
     -p 1433:1433 \
     mcr.microsoft.com/mssql/server:2022-latest
   ```

3. **Verbindungszeichenfolge anpassen** (`TripPlanner.Web/appsettings.json`):
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost,1433;Database=TripPlannerDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=true"
     }
   }
   ```
   
   Alternativ per lokalem SQL Server Express (Windows):
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=TripPlannerDb;Trusted_Connection=True;MultipleActiveResultSets=true"
     }
   }
   ```

4. **Ollama einrichten** (optional, für KI-Funktionen):
   ```bash
   # Ollama installieren: https://ollama.com
   ollama pull llama3.2
   ollama serve
   ```
   
   Konfiguration in `TripPlanner.Web/appsettings.json`:
   ```json
   {
     "Ollama": {
       "BaseUrl": "http://localhost:11434",
       "Model": "llama3.2",
       "MaxHistoryMessages": 40
     }
   }
   ```

5. **Anwendung starten**
   ```bash
   cd TripPlanner.Web
   dotnet run
   ```
   
   Migrationen werden beim ersten Start automatisch angewendet.

6. Unter `http://localhost:5278` aufrufen und registrieren.

### Ausführen mit .NET Aspire (orchestriert)
```bash
cd TripPlanner.AppHost
dotnet run
```
Startet `apiservice` und `webfrontend` mit Health Checks.

### Docker Compose
```bash
docker-compose up --build
```
Startet SQL Server + Webanwendung; Web erreichbar auf Port `8980` (HTTP) und `8981` (HTTPS).

### Datenbankmigrationen
Immer aus dem `TripPlanner.Web`-Verzeichnis ausführen:
```bash
cd TripPlanner.Web

# Neue Migration erstellen
dotnet ef migrations add <MigrationName>

# Migrationen anwenden
dotnet ef database update

# Letzte Migration rückgängig machen
dotnet ef migrations remove
```

### Build
```bash
dotnet build
```

### Tests ausführen
```bash
dotnet test
```
> **Hinweis**: Integrationstests nutzen `Aspire.Hosting.Testing` und benötigen eine erreichbare SQL Server-Instanz.

---

## Aktueller Status

✅ **Umgesetzt**:
- Vollständige Architektur und Datenmodelle
- Benutzerauthentifizierung und -autorisierung (ASP.NET Core Identity)
- Passkey-Unterstützung (WebAuthn) für passwortlose Anmeldung
- Datenbankpersistenz mit Entity Framework Core (SQL Server)
- Multi-User-Unterstützung mit Eigentümerschaft
- Wunschlisten-Verwaltung mit Sharing
- Reiseplanung mit Sharing und Tagesitinerarien
- Unterkunftsverwaltung pro Reise
- Repository-Pattern mit EF Core
- Alle Services mit Geschäftslogik implementiert
- Hauptseiten: Wishlists, Trips (inkl. TripPlan), Map, Places, Chat
- Authentifizierungsseiten (Login, Register, Profilverwaltung)
- Fluent UI Integration
- Parallax-Heldenbereich auf der Startseite
- Kartenansicht mit MapLibre GL JS
- **KI-Assistent** (Ollama-Chat) mit Trip/Wishlist/Places-Tool-Calls
- **MCP-Server** (`/mcp`) mit API-Key-Authentifizierung
- **URL-Import**: KI-gestützte Ortsextraktion aus Webseiten
- **Geocoding** via OpenStreetMap Nominatim
- **Wettervorhersage** via Open-Meteo (14 Tage)

⚠️ **Bekannte Einschränkungen**:
- Ältere `WishlistPage` (`/wishlist`) noch vorhanden (ersetzt durch `WishlistsPage` / `WishlistDetailPage`)
- KI-Funktionen erfordern eine laufende Ollama-Instanz

🔄 **Noch ausstehend**:
1. Drag-and-Drop für Tagesplanung
2. PDF-/JSON-Export von Reisen
3. Seed-Daten für Demo-Zwecke
4. Umfassende Tests

---

## Sharing-Funktionen

### Wunschlisten teilen
1. **Wunschliste erstellen** auf der „Wishlists"-Seite
2. **Orte hinzufügen**
3. **Teilen**: E-Mail-Adresse des Empfängers eingeben
4. **Empfänger** kann die geteilte Liste einsehen und bearbeiten
5. Eigentümer-Kennzeichnung über `ShareLevel.Owner`

### Reisen teilen
- Jede Reise hat einen Eigentümer (`OwnerId`)
- Reisen können mit anderen Nutzern geteilt werden (Dialog im Trips-Bereich)
- Geteilte Nutzer können Reisedetails und Itinerarien einsehen

---

## Sicherheit

- **Passwortanforderungen**: Mindestens 6 Zeichen, Groß-/Kleinbuchstaben und Ziffern erforderlich
- **Sichere Authentifizierung** via ASP.NET Core Identity mit Cookie-basierter Authentifizierung
- **Passkeys**: WebAuthn-basierte passwortlose Anmeldung
- **Autorisierung**: Alle Seiten (außer Home, Login, Register) erfordern Anmeldung
- **Datenisolation**: Nutzer sehen nur eigene oder explizit geteilte Daten
- **MCP API-Key**: SHA-256-Hash im Klartext niemals gespeichert; Constant-Time-Vergleich zur Timing-Angriff-Prävention

---

## Distanzberechnung

Verwendet die Haversine-Formel für genaue Großkreisabstände zwischen GPS-Koordinaten:
```
R = 6371 km (Erdradius)
a = sin²(Δlat/2) + cos(lat1) × cos(lat2) × sin²(Δlon/2)
c = 2 × atan2(√a, √(1-a))
Distanz = R × c
```

---

## Lizenz

Siehe LICENSE.txt für Details.
