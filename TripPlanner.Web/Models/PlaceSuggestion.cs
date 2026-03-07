namespace TripPlanner.Web.Models;

public class PlaceSuggestion
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public List<string> Tags { get; set; } = new();
}
