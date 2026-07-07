using TripPlanner.Web.Models;
using TripPlanner.Web.Services;

namespace TripPlanner.Tests;

public class GpxServiceTests
{
    [Fact]
    public void ParseGpxContent_ParsesWaypoints_WhenPresent()
    {
        var service = new GpxService();
        const string gpx = """
            <gpx version="1.1" creator="test" xmlns="http://www.topografix.com/GPX/1/1">
              <wpt lat="48.137" lon="11.575"><name>Start</name></wpt>
              <wpt lat="48.138" lon="11.576"><name>Viewpoint</name></wpt>
              <trk>
                <name>Sample</name>
                <trkseg>
                  <trkpt lat="48.137" lon="11.575"></trkpt>
                  <trkpt lat="48.138" lon="11.576"></trkpt>
                </trkseg>
              </trk>
            </gpx>
            """;

        var track = service.ParseGpxContent(gpx, "sample.gpx");

        Assert.Equal(2, track.Points.Count);
        Assert.Equal(2, track.Waypoints.Count);
        Assert.Equal("Start", track.Waypoints[0].Name);
        Assert.Equal(1, track.Waypoints[0].Order);
        Assert.Equal(2, track.Waypoints[1].Order);
    }

    [Fact]
    public void SerializeToGpx_WritesWaypoints_AndRoundTrips()
    {
        var service = new GpxService();
        var track = new GpxTrack
        {
            Name = "Roundtrip",
            Points =
            [
                new GpxPoint { Latitude = 48.1, Longitude = 11.5, Order = 1 },
                new GpxPoint { Latitude = 48.2, Longitude = 11.6, Order = 2 }
            ],
            Waypoints =
            [
                new GpxWaypoint { Name = "A", Latitude = 48.11, Longitude = 11.51, Order = 1 },
                new GpxWaypoint { Name = "B", Latitude = 48.19, Longitude = 11.59, Order = 2 }
            ]
        };

        var gpx = service.SerializeToGpx(track);
        var parsed = service.ParseGpxContent(gpx, "roundtrip.gpx");

        Assert.Equal(2, parsed.Waypoints.Count);
        Assert.Equal("A", parsed.Waypoints[0].Name);
        Assert.Equal("B", parsed.Waypoints[1].Name);
        Assert.Equal(2, parsed.Points.Count);
    }
}
