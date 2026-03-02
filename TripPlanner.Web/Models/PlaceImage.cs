namespace TripPlanner.Web.Models;

public class PlaceImage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PlaceId { get; set; } = string.Empty;
    public Place? Place { get; set; }
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public string ImageContentType { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
