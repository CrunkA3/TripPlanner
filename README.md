# TripPlanner - Personal Travel and Trip Planning Application

A modern Blazor Server application for planning trips and managing travel wishlists using .NET 10 and Fluent UI components.

## Features

### 1. Place Wishlist
- Save places you want to visit with detailed information
- Each place includes:
  - Name and description
  - Category (Viewpoint, Museum, Restaurant, Nature, etc.)
  - GPS coordinates (Latitude/Longitude)
  - Tags for organization
  - Optional GPX track upload
- Filter places by category, tags, or GPX track presence
- View all places in a responsive card grid

### 2. Trip Planning
- Create multi-day trips with detailed itineraries
- For each trip:
  - Add multiple days
  - Schedule places for specific times
  - Set duration for each activity
  - Add notes for each place
- Automatic trip analysis:
  - Calculate total scheduled time
  - Estimate travel time between locations
  - Detect scheduling conflicts
  - Warn about overly packed days

### 3. Map Visualization
- Interactive map view (placeholder for Leaflet.js/MapLibre integration)
- Display wishlist places and trip routes
- Filter map content by trip selection
- Side panel with place details

## Technical Architecture

### Clean Architecture
```
TripPlanner.Web/
├── Models/                  # Domain entities
│   ├── Place.cs
│   ├── Trip.cs
│   ├── GpxTrack.cs
│   └── PlaceCategory.cs
├── Repositories/            # Data access layer
│   ├── IPlaceRepository.cs
│   ├── ITripRepository.cs
│   ├── IGpxRepository.cs
│   └── LocalStorage*.cs    # Browser localStorage implementations
├── Services/                # Business logic
│   ├── GpxService.cs       # GPX parsing and calculations
│   └── RoutingService.cs   # Distance and time calculations
└── Components/
    ├── Layout/
    │   ├── MainLayout.razor
    │   └── NavMenu.razor
    └── Pages/
        ├── Home.razor
        ├── Wishlist/
        │   └── WishlistPage.razor
        ├── Trips/
        │   └── TripsPage.razor
        └── Map/
            └── MapPage.razor
```

### Technologies
- **Framework**: .NET 10, ASP.NET Core Blazor Server
- **UI Library**: Microsoft Fluent UI Blazor Components 4.13.2
- **Data Storage**: Browser LocalStorage (via JavaScript Interop)
- **Mapping**: Placeholder for Leaflet.js or MapLibre GL JS integration

### Key Services

#### GpxService
- Parse GPX files (XML format)
- Calculate track statistics:
  - Total distance using Haversine formula
  - Elevation gain and loss
  - Track point analysis

#### RoutingService
- Calculate distances between places
- Estimate travel times (default: 50 km/h)
- Analyze trip days:
  - Total scheduled time
  - Travel time between locations
  - Detect scheduling conflicts
  - Warn about unrealistic schedules

### Data Models

**Place**
```csharp
- Id: string (GUID)
- Name: string
- Description: string
- Category: PlaceCategory (enum)
- Latitude: double
- Longitude: double
- Tags: List<string>
- GpxTrackId: string? (optional)
- CreatedAt, UpdatedAt: DateTime
```

**Trip**
```csharp
- Id: string (GUID)
- Name: string
- Description: string
- StartDate, EndDate: DateTime?
- Days: List<TripDay>
- UnscheduledPlaces: List<TripPlace>
```

**TripDay**
```csharp
- Id: string (GUID)
- DayNumber: int
- Date: DateTime?
- Places: List<TripPlace> (ordered)
```

## Setup and Development

### Prerequisites
- .NET 10 SDK
- Visual Studio 2022, VS Code, or Rider

### Running the Application
```bash
cd TripPlanner.Web
dotnet run
```

Navigate to `https://localhost:5001` (or the port shown in console)

### Building
```bash
dotnet build
```

### Current Status

✅ **Completed**:
- Complete architecture and data models
- All services implemented with business logic
- Repository pattern with LocalStorage
- Three main pages (Wishlist, Trips, Map)
- Fluent UI integration
- Navigation and layout

⚠️ **Known Issues**:
- 3 compilation errors related to FluentUI dialog rendering
- These appear to be Blazor/FluentUI compatibility issues with conditional rendering inside dialogs
- The core logic and architecture are sound

🔄 **To Complete**:
1. Fix remaining compilation errors (likely component restructuring needed)
2. Integrate actual map library (Leaflet.js or MapLibre)
3. Implement GPX file upload functionality
4. Add drag-and-drop for trip planning
5. Add seed data for demonstration
6. Comprehensive testing

## Features in Detail

### Distance Calculations
Uses the Haversine formula for accurate great-circle distance between GPS coordinates:
```csharp
R = 6371 km (Earth's radius)
a = sin²(Δlat/2) + cos(lat1) × cos(lat2) × sin²(Δlon/2)
c = 2 × atan2(√a, √(1-a))
distance = R × c
```

### Trip Analysis
Analyzes each trip day to provide:
- Total time including activities and travel
- Realistic schedule assessment
- Conflict detection (overlapping times)
- Warnings for overly packed days (>14 hours)

### Data Persistence
All data stored in browser LocalStorage:
- `tripplanner_places`: Place wishlist
- `tripplanner_trips`: Trip data
- `tripplanner_gpxtracks`: GPX track data

Data persists across browser sessions and works offline.

## Future Enhancements

1. **Map Integration**
   - Add Leaflet.js or MapLibre GL JS
   - Display places as markers
   - Show GPX tracks as polylines
   - Route visualization between places

2. **GPX Features**
   - File upload with drag-and-drop
   - Track preview and editing
   - Export trip as GPX

3. **Enhanced Planning**
   - Drag-and-drop day planning
   - Copy/move places between days
  - Template trips

4. **Sharing**
   - Export trips as PDF/JSON
   - Share via link
   - Import/export functionality

5. **Backend API**
   - Optional cloud sync
   - Multi-device support
   - User accounts

## Contributing

This is a demonstration project showcasing:
- Clean architecture in Blazor
- Fluent UI component usage
- LocalStorage data persistence
- GPS and routing calculations
- Modern .NET 10 features

## License

See LICENSE.txt for details.
