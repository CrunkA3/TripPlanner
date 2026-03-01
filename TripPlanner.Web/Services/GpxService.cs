using System.Xml.Linq;
using TripPlanner.Web.Models;
using System.Globalization;

namespace TripPlanner.Web.Services;

public class GpxService
{
    public GpxTrack ParseGpxContent(string gpxContent, string fileName)
    {
        var doc = XDocument.Parse(gpxContent);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        var track = new GpxTrack
        {
            Name = fileName
        };

        var trkElement = doc.Descendants(ns + "trk").FirstOrDefault();
        if (trkElement != null)
        {
            track.Name = trkElement.Element(ns + "name")?.Value ?? fileName;
            track.Description = trkElement.Element(ns + "desc")?.Value;
        }

        var trackPoints = doc.Descendants(ns + "trkpt").ToList();
        var order = 0;
        foreach (var trkpt in trackPoints)
        {
            var lat = double.Parse(trkpt.Attribute("lat")?.Value ?? "0", CultureInfo.InvariantCulture);
            var lon = double.Parse(trkpt.Attribute("lon")?.Value ?? "0", CultureInfo.InvariantCulture);
            var ele = trkpt.Element(ns + "ele");
            var time = trkpt.Element(ns + "time");
            order++;

            track.Points.Add(new GpxPoint
            {
                Order = order,
                Latitude = lat,
                Longitude = lon,
                Elevation = ele != null ? double.Parse(ele.Value, CultureInfo.InvariantCulture) : null,
                Time = time != null ? DateTime.Parse(time.Value, CultureInfo.InvariantCulture) : null
            });
        }

        CalculateTrackStatistics(track);
        return track;
    }

    private void CalculateTrackStatistics(GpxTrack track)
    {
        if (track.Points.Count < 2)
            return;

        double totalDistance = 0;
        double elevationGain = 0;
        double elevationLoss = 0;

        for (int i = 1; i < track.Points.Count; i++)
        {
            var p1 = track.Points[i - 1];
            var p2 = track.Points[i];

            // Calculate distance using Haversine formula
            totalDistance += CalculateDistance(p1.Latitude, p1.Longitude, p2.Latitude, p2.Longitude);

            // Calculate elevation changes
            if (p1.Elevation.HasValue && p2.Elevation.HasValue)
            {
                var elevDiff = p2.Elevation.Value - p1.Elevation.Value;
                if (elevDiff > 0)
                    elevationGain += elevDiff;
                else
                    elevationLoss += Math.Abs(elevDiff);
            }
        }

        track.TotalDistance = totalDistance;
        track.ElevationGain = elevationGain;
        track.ElevationLoss = elevationLoss;
    }

    public double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        // Haversine formula
        const double R = 6371; // Earth's radius in kilometers

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}
