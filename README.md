# TripPlanner - Personal Travel and Trip Planning Application

A modern Blazor Server application for planning trips and managing travel wishlists with **multi-user support and sharing capabilities** using .NET 10, Entity Framework Core, ASP.NET Core Identity, and Fluent UI components.

## Features

### 1. User Management & Authentication
- **User registration and login** with ASP.NET Core Identity
- Secure password requirements
- Email-based user identification
- User profile with first and last name

### 2. Multiple Wishlists
- **Create multiple wishlists** for different purposes (e.g., "European Destinations", "Beach Vacation Ideas")
- Each wishlist can contain multiple places
- Organize places by wishlist
- **Share wishlists with other users** via email
- View wishlists shared with you
- Full CRUD operations on wishlists

### 3. Place Management
- Save places you want to visit with detailed information
- Each place includes:
  - Name and description
  - Category (Viewpoint, Museum, Restaurant, Nature, etc.)
  - GPS coordinates (Latitude/Longitude)
  - Tags for organization
  - Optional GPX track upload
  - Association with a wishlist
- Filter places by category, tags, or GPX track presence
- View all places in a responsive card grid

### 4. Trip Planning
- Create multi-day trips with detailed itineraries
- **Ownership and sharing**: Each trip has an owner and can be shared with other users
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
- View trips you own or that have been shared with you

### 5. Map Visualization
- Interactive map view (placeholder for Leaflet.js/MapLibre integration)
- Display wishlist places and trip routes
- Filter map content by trip selection
- Side panel with place details

## Technical Architecture

### Clean Architecture
```
TripPlanner.Web/
├── Data/                     # Database context
│   └── ApplicationDbContext.cs
├── Models/                  # Domain entities
│   ├── User.cs             # Identity user with custom properties
│   ├── Wishlist.cs         # Wishlist with sharing support
│   ├── UserWishlist.cs     # Many-to-many: User-Wishlist sharing
│   ├── Place.cs            # Place entity with wishlist association
│   ├── Trip.cs             # Trip with ownership
│   ├── SharedTrip.cs       # Many-to-many: User-Trip sharing
│   ├── GpxTrack.cs
│   └── PlaceCategory.cs
├── Repositories/            # Data access layer
│   ├── IWishlistRepository.cs
│   ├── WishlistRepository.cs
│   ├── IPlaceRepository.cs
│   ├── EfPlaceRepository.cs
│   ├── ITripRepository.cs
│   ├── EfTripRepository.cs
│   ├── IGpxRepository.cs
│   └── EfGpxRepository.cs
│   └── LocalStorage*.cs    # Browser localStorage implementations
├── Services/                # Business logic
│   ├── UserService.cs      # User authentication helpers
│   ├── GpxService.cs       # GPX parsing and calculations
│   └── RoutingService.cs   # Distance and time calculations
└── Components/
    ├── Account/            # Authentication pages
    │   ├── Login.razor
    │   ├── Register.razor
    │   └── Logout.razor
    ├── Layout/
    │   ├── MainLayout.razor
    │   └── NavMenu.razor
    └── Pages/
        ├── Home.razor
        ├── Wishlists/
        │   ├── WishlistsPage.razor      # List and manage wishlists
        │   └── WishlistDetailPage.razor # View and manage places in a wishlist
        ├── Wishlist/
        │   └── WishlistPage.razor       # (Legacy - to be updated)
        ├── Trips/
        │   └── TripsPage.razor
        └── Map/
            └── MapPage.razor
```

### Technologies
- **Framework**: .NET 10, ASP.NET Core Blazor Server
- **Database**: Entity Framework Core 9.0 with SQLite (development) / PostgreSQL-ready
- **Authentication**: ASP.NET Core Identity
- **UI Library**: Microsoft Fluent UI Blazor Components 4.13.2
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

**User** (extends IdentityUser)
```csharp
- Id: string (GUID - from Identity)
- Email: string (from Identity)
- FirstName: string?
- LastName: string?
- Wishlists: List<Wishlist> (owned)
- SharedWishlists: List<UserWishlist> (shared with user)
- OwnedTrips: List<Trip>
- SharedTrips: List<SharedTrip>
```

**Wishlist**
```csharp
- Id: string (GUID)
- Name: string
- Description: string?
- OwnerId: string (FK to User)
- Places: List<Place>
- SharedWith: List<UserWishlist>
- CreatedAt, UpdatedAt: DateTime
```

**Place**
```csharp
- Id: string (GUID)
- Name: string
- Description: string
- Category: PlaceCategory (enum)
- Latitude: double
- Longitude: double
- Tags: List<string>
- GpxTrackId: string? (optional FK)
- WishlistId: string? (optional FK to Wishlist)
- CreatedAt, UpdatedAt: DateTime
```

**Trip**
```csharp
- Id: string (GUID)
- Name: string
- Description: string
- StartDate, EndDate: DateTime?
- OwnerId: string (FK to User)
- Days: List<TripDay>
- UnscheduledPlaces: List<TripPlace>
- SharedWith: List<SharedTrip>
- CreatedAt, UpdatedAt: DateTime
```

**UserWishlist** (Many-to-Many: User ↔ Wishlist)
```csharp
- UserId: string (FK to User)
- WishlistId: string (FK to Wishlist)
- SharedAt: DateTime
```

**SharedTrip** (Many-to-Many: User ↔ Trip)
```csharp
- UserId: string (FK to User)
- TripId: string (FK to Trip)
- SharedAt: DateTime
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
- SQLite (included with EF Core package)

### First Time Setup

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd TripPlanner
   ```

2. **Initialize the database**
   
   The database will be automatically created and migrated on first run. Alternatively, you can manually apply migrations:
   
   ```bash
   cd TripPlanner.Web
   dotnet ef database update
   ```

3. **Run the application**
   ```bash
   cd TripPlanner.Web
   dotnet run
   ```

Navigate to `https://localhost:5001` (or the port shown in console)

### Database Migrations

When models change, create and apply migrations:

```bash
cd TripPlanner.Web

# Create a new migration
dotnet ef migrations add <MigrationName>

# Apply migrations to database
dotnet ef database update

# Revert last migration
dotnet ef migrations remove
```

The database file `tripplanner.db` is stored in the `TripPlanner.Web` directory (excluded from git).

### Database Configuration

By default, the application uses SQLite. The connection string is in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=tripplanner.db"
  }
}
```

**For PostgreSQL** (production):
1. Install `Npgsql.EntityFrameworkCore.PostgreSQL` package
2. Update `Program.cs` to use PostgreSQL:
   ```csharp
   builder.Services.AddDbContext<ApplicationDbContext>(options =>
       options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
   ```
3. Update connection string in `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=tripplanner;Username=postgres;Password=yourpassword"
     }
   }
   ```

### Building
```bash
dotnet build
```

### Running Tests
```bash
dotnet test
```

### Current Status

✅ **Completed**:
- Complete architecture and data models
- User authentication and authorization (ASP.NET Core Identity)
- Database persistence with Entity Framework Core (SQLite)
- Multi-user support with ownership
- Wishlist management with sharing capabilities
- Trip management with sharing capabilities
- Repository pattern with EF Core implementations
- All services implemented with business logic
- Three main pages (Wishlists, Trips, Map)
- Authentication pages (Login, Register)
- Fluent UI integration
- Navigation with auth context

⚠️ **Known Issues**:
- The original WishlistPage needs to be updated or removed (replaced by WishlistsPage and WishlistDetailPage)
- Map integration is still a placeholder

🔄 **To Complete**:
1. Integrate actual map library (Leaflet.js or MapLibre)
2. Implement GPX file upload functionality  
3. Add drag-and-drop for trip planning
4. Add UI for sharing trips with other users
5. Add seed data for demonstration
6. Comprehensive testing
7. Deploy to production with PostgreSQL

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
All data is stored in a **relational database** (SQLite for development, PostgreSQL-ready for production):
- User accounts and authentication data (Identity tables)
- Wishlists with ownership and sharing relationships
- Places associated with wishlists
- Trips with ownership and sharing relationships
- GPX track data

Data persists across sessions and supports multi-user collaboration through sharing.

## Sharing Features

### Wishlist Sharing
1. **Create a wishlist** from the "Wishlists" page
2. **Add places** to your wishlist
3. **Share with others** by entering their email address
4. **Recipients** can view and contribute to the shared wishlist
5. **View shared wishlists** in the "Shared With Me" section

### Trip Sharing
- Each trip has an owner
- Trips can be shared with other users
- Shared users can view trip details and itineraries
- (UI for trip sharing coming soon)

## Security

- **Password requirements**: Minimum 6 characters, requires uppercase, lowercase, and digit
- **Secure authentication** using ASP.NET Core Identity with cookie-based auth
- **Authorization**: Protected pages require login
- **Data isolation**: Users can only access their own data or data explicitly shared with them

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
