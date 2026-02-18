using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Data;

public class ApplicationDbContext : IdentityDbContext<User>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Wishlist> Wishlists { get; set; }
    public DbSet<Place> Places { get; set; }
    public DbSet<Trip> Trips { get; set; }
    public DbSet<TripDay> TripDays { get; set; }
    public DbSet<TripPlace> TripPlaces { get; set; }
    public DbSet<GpxTrack> GpxTracks { get; set; }
    public DbSet<GpxPoint> GpxPoints { get; set; }
    public DbSet<UserWishlist> UserWishlists { get; set; }
    public DbSet<SharedTrip> SharedTrips { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Wishlist
        modelBuilder.Entity<Wishlist>()
            .HasOne(w => w.Owner)
            .WithMany(u => u.Wishlists)
            .HasForeignKey(w => w.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure UserWishlist (many-to-many relationship)
        modelBuilder.Entity<UserWishlist>()
            .HasKey(uw => new { uw.UserId, uw.WishlistId });

        modelBuilder.Entity<UserWishlist>()
            .HasOne(uw => uw.User)
            .WithMany(u => u.SharedWishlists)
            .HasForeignKey(uw => uw.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserWishlist>()
            .HasOne(uw => uw.Wishlist)
            .WithMany(w => w.SharedWith)
            .HasForeignKey(uw => uw.WishlistId)
            .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete cycles

        // Configure Place-Wishlist relationship
        modelBuilder.Entity<Place>()
            .HasOne(p => p.Wishlist)
            .WithMany(w => w.Places)
            .HasForeignKey(p => p.WishlistId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure Trip
        modelBuilder.Entity<Trip>()
            .HasOne(t => t.Owner)
            .WithMany(u => u.OwnedTrips)
            .HasForeignKey(t => t.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure SharedTrip (many-to-many relationship)
        modelBuilder.Entity<SharedTrip>()
            .HasKey(st => new { st.UserId, st.TripId });

        modelBuilder.Entity<SharedTrip>()
            .HasOne(st => st.User)
            .WithMany(u => u.SharedTrips)
            .HasForeignKey(st => st.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SharedTrip>()
            .HasOne(st => st.Trip)
            .WithMany(t => t.SharedWith)
            .HasForeignKey(st => st.TripId)
            .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete cycles

        // Configure TripDay
        modelBuilder.Entity<TripDay>()
            .HasOne<Trip>()
            .WithMany(t => t.Days)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure GpxPoint
        modelBuilder.Entity<GpxPoint>()
            .HasOne<GpxTrack>()
            .WithMany(g => g.Points)
            .HasForeignKey(p => p.GpxTrackId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Tags as JSON
        modelBuilder.Entity<Place>()
            .Property(p => p.Tags)
            .HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
    }
}
