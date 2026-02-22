using System.ComponentModel.DataAnnotations.Schema;

namespace TripPlanner.Web.Models;

public class Place
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PlaceCategory Category { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? GpxTrackId { get; set; }


    // Wishlist association
    public string? WishlistId { get; set; }
    public Wishlist? Wishlist { get; set; }


    // Trip association
    public string? TripId { get; set; }
    public Trip? Trip { get; set; }



    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }



    // Computed property
    public bool HasGpxTrack => !string.IsNullOrEmpty(GpxTrackId);



    [NotMapped]
    public string LatitudeString
    {
        get => Latitude.ToString("N"); set => Latitude = double.Parse(value);
    }

    [NotMapped]
    public string LongitudeString
    {
        get => Longitude.ToString("N"); set => Longitude = double.Parse(value);
    }


}