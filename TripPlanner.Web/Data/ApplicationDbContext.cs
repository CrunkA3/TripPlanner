using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TripPlanner.Web.Models;

namespace TripPlanner.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{


    public DbSet<Wishlist> Wishlists { get; set; }
    public DbSet<Place> Places { get; set; }
    public DbSet<PlaceImage> PlaceImages { get; set; }
    public DbSet<Trip> Trips { get; set; }
    public DbSet<TripDay> TripDays { get; set; }
    public DbSet<TripPlace> TripPlaces { get; set; }
    public DbSet<Accommodation> Accommodations { get; set; }
    public DbSet<GpxTrack> GpxTracks { get; set; }
    public DbSet<GpxPoint> GpxPoints { get; set; }
    public DbSet<UserWishlist> UserWishlists { get; set; }
    public DbSet<SharedTrip> SharedTrips { get; set; }
    public DbSet<UrlImportJob> UrlImportJobs { get; set; }
    public DbSet<ChatConversation> ChatConversations { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<ChatJob> ChatJobs { get; set; }
    public DbSet<PlaceCollection> PlaceCollections { get; set; }
    public DbSet<PlaceCollectionItem> PlaceCollectionItems { get; set; }



    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Wishlist
        modelBuilder.Entity<Wishlist>();

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

        // Configure PlaceImage relationship
        modelBuilder.Entity<PlaceImage>()
            .HasOne(pi => pi.Place)
            .WithMany(p => p.Images)
            .HasForeignKey(pi => pi.PlaceId)
            .OnDelete(DeleteBehavior.Cascade);

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

        modelBuilder.Entity<TripPlace>()
            .HasOne<TripDay>()
            .WithMany(d => d.Places)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Accommodation
        modelBuilder.Entity<Accommodation>()
            .HasOne(a => a.Trip)
            .WithMany(t => t.Accommodations)
            .HasForeignKey(a => a.TripId)
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

        // Configure UrlImportJob
        modelBuilder.Entity<UrlImportJob>()
            .HasOne(j => j.Wishlist)
            .WithMany()
            .HasForeignKey(j => j.WishlistId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure ChatConversation
        modelBuilder.Entity<ChatConversation>()
            .HasOne(c => c.User)
            .WithMany(u => u.ChatConversations)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure ChatMessage
        modelBuilder.Entity<ChatMessage>()
            .HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Performance indexes
        modelBuilder.Entity<ChatJob>()
            .HasIndex(j => j.Status);

        modelBuilder.Entity<ChatJob>()
            .HasIndex(j => j.ConversationId);

        modelBuilder.Entity<ChatJob>()
            .HasIndex(j => j.UserId);

        modelBuilder.Entity<UrlImportJob>()
            .HasIndex(j => j.Status);

        modelBuilder.Entity<Place>()
            .HasIndex(p => p.GpxTrackId);

        modelBuilder.Entity<Place>()
            .HasOne(p => p.GpxTrack)
            .WithMany()
            .HasForeignKey(p => p.GpxTrackId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure PlaceCollection
        modelBuilder.Entity<PlaceCollection>()
            .HasOne(c => c.Owner)
            .WithMany(u => u.OwnedCollections)
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlaceCollection>()
            .HasIndex(c => c.PublicShareToken)
            .IsUnique()
            .HasFilter("[PublicShareToken] IS NOT NULL");

        // Configure PlaceCollectionItem (composite PK)
        modelBuilder.Entity<PlaceCollectionItem>()
            .HasKey(i => new { i.CollectionId, i.PlaceId });

        modelBuilder.Entity<PlaceCollectionItem>()
            .HasOne(i => i.Collection)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.CollectionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PlaceCollectionItem>()
            .HasOne(i => i.Place)
            .WithMany()
            .HasForeignKey(i => i.PlaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
