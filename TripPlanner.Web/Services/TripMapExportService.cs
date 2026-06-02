using System.Globalization;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using TripPlanner.Web.Models;
using TripPlanner.Web.Repositories;

namespace TripPlanner.Web.Services;

public sealed class TripMapExportService(IHttpClientFactory httpClientFactory, ILogger<TripMapExportService> logger)
{
    private const int ImageWidth = 1600;
    private const int ImageHeight = 1000;
    private const int TileSize = 256;
    private const int MinZoom = 1;
    private const int MaxZoom = 12;
    private const float MapPadding = 170f;
    private const float LabelPadding = 14f;
    private const float LabelCornerRadius = 16f;
    private const float LabelMaxWidth = 280f;
    private const float FooterReservedHeight = 28f;
    private const string TileUrlTemplate = "https://basemaps.cartocdn.com/light_nolabels/{z}/{x}/{y}.png";
    private const string TileAttribution = "Kartengrundlage © OpenStreetMap-Mitwirkende, © CARTO";

    private static readonly SKColor BackgroundColor = new(246, 248, 251);
    private static readonly SKColor RouteColor = new(0xFF, 0x57, 0x22);
    private static readonly SKColor LabelBackgroundColor = new(255, 255, 255, 242);
    private static readonly SKColor LabelBorderColor = new(31, 41, 55, 36);
    private static readonly SKColor LabelShadowColor = new(15, 23, 42, 28);
    private static readonly SKColor LeaderLineColor = new(71, 85, 105, 180);
    private static readonly SKColor MarkerBorderColor = SKColors.White;
    private static readonly SKColor FallbackTileColor = new(234, 238, 243);

    public async Task<byte[]> RenderTripAsync(Trip trip, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trip);

        var exportItems = BuildExportItems(trip);
        if (exportItems.Count == 0)
        {
            throw new InvalidOperationException("The trip does not contain any exportable places with coordinates.");
        }

        var viewport = CreateViewport(exportItems);

        using var surface = SKSurface.Create(new SKImageInfo(ImageWidth, ImageHeight));
        var canvas = surface.Canvas;
        canvas.Clear(BackgroundColor);

        await DrawBaseMapAsync(canvas, viewport, cancellationToken);
        DrawRoute(canvas, exportItems, viewport);
        DrawMarkersAndLabels(canvas, exportItems, viewport);
        DrawFooter(canvas);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static List<ExportItem> BuildExportItems(Trip trip)
    {
        var items = new List<ExportItem>();

        foreach (var day in trip.Days.OrderBy(d => d.DayNumber))
        {
            foreach (var tripPlace in day.Places
                .Where(tp => tp.Place is not null && HasCoordinates(tp.Place.Latitude, tp.Place.Longitude))
                .OrderBy(tp => tp.ScheduledTime.HasValue ? 0 : 1)
                .ThenBy(tp => tp.ScheduledTime)
                .ThenBy(tp => tp.Order))
            {
                var place = tripPlace.Place!;
                items.Add(new ExportItem(
                    place.Name,
                    FormatPlaceDateTime(tripPlace.ScheduledTime, day.Date),
                    place.Latitude,
                    place.Longitude,
                    ParseColor(IPlaceRepository.GetCategoryColor(place.Category)),
                    tripPlace.ScheduledTime ?? day.Date ?? trip.StartDate ?? trip.CreatedAt,
                    tripPlace.Order,
                    IsAccommodation: false));
            }
        }

        foreach (var accommodation in trip.Accommodations
            .Where(a => HasCoordinates(a.Latitude, a.Longitude))
            .OrderBy(a => a.PlannedCheckIn ?? a.PlannedCheckOut ?? trip.EndDate ?? DateTimeOffset.MaxValue))
        {
            items.Add(new ExportItem(
                accommodation.Name,
                FormatAccommodationDateTime(accommodation),
                accommodation.Latitude,
                accommodation.Longitude,
                ParseColor(IPlaceRepository.GetCategoryColor(PlaceCategory.Accommodation)),
                accommodation.PlannedCheckIn ?? accommodation.PlannedCheckOut ?? trip.EndDate ?? trip.CreatedAt,
                10_000,
                IsAccommodation: true));
        }

        return items
            .OrderBy(i => i.SortKey)
            .ThenBy(i => i.SecondarySort)
            .ToList();
    }

    private async Task DrawBaseMapAsync(SKCanvas canvas, MapViewport viewport, CancellationToken cancellationToken)
    {
        using var fallbackPaint = new SKPaint
        {
            Color = FallbackTileColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        var maxTileIndex = (1 << viewport.Zoom) - 1;
        var leftTile = (int)Math.Floor(viewport.LeftWorld / TileSize);
        var rightTile = (int)Math.Floor(viewport.RightWorld / TileSize);
        var topTile = Math.Max(0, (int)Math.Floor(viewport.TopWorld / TileSize));
        var bottomTile = Math.Min(maxTileIndex, (int)Math.Floor(viewport.BottomWorld / TileSize));

        var tileTasks = new List<Task<TileImage>>();
        for (var tileY = topTile; tileY <= bottomTile; tileY++)
        {
            for (var tileX = leftTile; tileX <= rightTile; tileX++)
            {
                tileTasks.Add(LoadTileAsync(viewport, tileX, tileY, cancellationToken));
            }
        }

        foreach (var tile in await Task.WhenAll(tileTasks))
        {
            using var bitmap = tile.Bitmap;
            var destination = SKRect.Create(tile.DestinationX, tile.DestinationY, TileSize + 1, TileSize + 1);
            if (bitmap is not null)
            {
                canvas.DrawBitmap(bitmap, destination);
            }
            else
            {
                canvas.DrawRect(destination, fallbackPaint);
            }
        }
    }

    private async Task<TileImage> LoadTileAsync(MapViewport viewport, int tileX, int tileY, CancellationToken cancellationToken)
    {
        var wrappedTileX = WrapTileX(tileX, viewport.Zoom);
        var destinationX = (float)(tileX * TileSize - viewport.LeftWorld);
        var destinationY = (float)(tileY * TileSize - viewport.TopWorld);

        try
        {
            var client = httpClientFactory.CreateClient();
            if (client.DefaultRequestHeaders.UserAgent.Count == 0)
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TripPlanner/1.0 (+https://github.com/CrunkA3/TripPlanner)");
            }

            var url = TileUrlTemplate
                .Replace("{z}", viewport.Zoom.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{x}", wrappedTileX.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{y}", tileY.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

            using var response = await client.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Could not load map tile {Zoom}/{TileX}/{TileY}: {StatusCode}", viewport.Zoom, wrappedTileX, tileY, response.StatusCode);
                return new TileImage(destinationX, destinationY, null);
            }

            var tileBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var bitmap = SKBitmap.Decode(tileBytes);
            return new TileImage(destinationX, destinationY, bitmap);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load map tile {Zoom}/{TileX}/{TileY}", viewport.Zoom, wrappedTileX, tileY);
            return new TileImage(destinationX, destinationY, null);
        }
    }

    private static void DrawRoute(SKCanvas canvas, IReadOnlyList<ExportItem> exportItems, MapViewport viewport)
    {
        var routePoints = exportItems
            .OrderBy(i => i.SortKey)
            .ThenBy(i => i.SecondarySort)
            .Select(i => viewport.Project(i.Latitude, i.Longitude))
            .Where((point, index) => index == 0 || !PointsAlmostEqual(point, viewport.Project(exportItems[index - 1].Latitude, exportItems[index - 1].Longitude)))
            .ToList();

        if (routePoints.Count < 2)
        {
            return;
        }

        using var routePaint = new SKPaint
        {
            Color = RouteColor.WithAlpha(185),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 6,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true
        };

        using var haloPaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(170),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 10,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true
        };

        using var path = new SKPath();
        path.MoveTo(routePoints[0]);
        for (var index = 1; index < routePoints.Count; index++)
        {
            path.LineTo(routePoints[index]);
        }

        canvas.DrawPath(path, haloPaint);
        canvas.DrawPath(path, routePaint);
    }

    private static void DrawMarkersAndLabels(SKCanvas canvas, IReadOnlyList<ExportItem> exportItems, MapViewport viewport)
    {
        using var namePaint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName(null, SKFontStyleWeight.SemiBold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright),
            TextSize = 22
        };
        using var subtitlePaint = new SKPaint
        {
            Color = new SKColor(51, 65, 85),
            IsAntialias = true,
            TextSize = 18
        };
        using var labelFillPaint = new SKPaint { Color = LabelBackgroundColor, IsAntialias = true, Style = SKPaintStyle.Fill };
        using var labelBorderPaint = new SKPaint { Color = LabelBorderColor, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f };
        using var shadowPaint = new SKPaint { Color = LabelShadowColor, IsAntialias = true, Style = SKPaintStyle.Fill };
        using var leaderPaint = new SKPaint
        {
            Color = LeaderLineColor,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            StrokeCap = SKStrokeCap.Round
        };
        using var markerFillPaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        using var markerBorderPaint = new SKPaint { Color = MarkerBorderColor, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 };

        var occupiedLabelRects = new List<SKRect>();
        var safeBounds = new SKRect(16, 16, ImageWidth - 16, ImageHeight - FooterReservedHeight - 16);

        foreach (var item in exportItems)
        {
            var anchor = viewport.Project(item.Latitude, item.Longitude);
            var nameLines = WrapText(item.Name, namePaint, LabelMaxWidth);
            var subtitleLines = WrapText(item.Subtitle, subtitlePaint, LabelMaxWidth);
            var labelLayout = MeasureLabel(nameLines, subtitleLines, namePaint, subtitlePaint);
            var labelRect = FindLabelRect(anchor, labelLayout.Size, safeBounds, occupiedLabelRects);

            var connectionPoint = GetConnectionPoint(labelRect, anchor);
            canvas.DrawLine(anchor, connectionPoint, leaderPaint);

            var shadowRect = labelRect;
            shadowRect.Offset(4, 4);
            canvas.DrawRoundRect(shadowRect, LabelCornerRadius, LabelCornerRadius, shadowPaint);
            canvas.DrawRoundRect(labelRect, LabelCornerRadius, LabelCornerRadius, labelFillPaint);
            canvas.DrawRoundRect(labelRect, LabelCornerRadius, LabelCornerRadius, labelBorderPaint);

            var baselineY = labelRect.Top + LabelPadding - namePaint.FontMetrics.Ascent;
            foreach (var line in nameLines)
            {
                canvas.DrawText(line, labelRect.Left + LabelPadding, baselineY, namePaint);
                baselineY += labelLayout.NameLineHeight;
            }

            if (subtitleLines.Count > 0)
            {
                baselineY += 4;
                foreach (var line in subtitleLines)
                {
                    canvas.DrawText(line, labelRect.Left + LabelPadding, baselineY, subtitlePaint);
                    baselineY += labelLayout.SubtitleLineHeight;
                }
            }

            markerFillPaint.Color = item.Color;
            if (item.IsAccommodation)
            {
                var markerRect = SKRect.Create(anchor.X - 9, anchor.Y - 9, 18, 18);
                canvas.DrawRoundRect(markerRect, 5, 5, markerFillPaint);
                canvas.DrawRoundRect(markerRect, 5, 5, markerBorderPaint);
            }
            else
            {
                canvas.DrawCircle(anchor, 8, markerFillPaint);
                canvas.DrawCircle(anchor, 8, markerBorderPaint);
            }

            occupiedLabelRects.Add(labelRect);
        }
    }

    private static LabelLayout MeasureLabel(
        IReadOnlyList<string> nameLines,
        IReadOnlyList<string> subtitleLines,
        SKPaint namePaint,
        SKPaint subtitlePaint)
    {
        var nameLineHeight = namePaint.TextSize * 1.2f;
        var subtitleLineHeight = subtitlePaint.TextSize * 1.2f;

        var contentWidth = 0f;
        foreach (var line in nameLines)
        {
            contentWidth = Math.Max(contentWidth, namePaint.MeasureText(line));
        }

        foreach (var line in subtitleLines)
        {
            contentWidth = Math.Max(contentWidth, subtitlePaint.MeasureText(line));
        }

        var contentHeight = (nameLines.Count * nameLineHeight) + (subtitleLines.Count * subtitleLineHeight);
        if (subtitleLines.Count > 0)
        {
            contentHeight += 4;
        }

        return new LabelLayout(
            new SKSize(contentWidth + (LabelPadding * 2), contentHeight + (LabelPadding * 2)),
            nameLineHeight,
            subtitleLineHeight);
    }

    private static SKRect FindLabelRect(SKPoint anchor, SKSize labelSize, SKRect safeBounds, IReadOnlyList<SKRect> occupiedLabelRects)
    {
        var directions = new[]
        {
            new CandidateDirection(1, -1),
            new CandidateDirection(1, 0),
            new CandidateDirection(1, 1),
            new CandidateDirection(0, 1),
            new CandidateDirection(-1, 1),
            new CandidateDirection(-1, 0),
            new CandidateDirection(-1, -1),
            new CandidateDirection(0, -1)
        };
        var offsets = new[] { 18f, 42f, 72f, 104f, 138f };

        SKRect? bestRect = null;
        var bestScore = float.MaxValue;

        foreach (var offset in offsets)
        {
            foreach (var direction in directions)
            {
                var candidate = BuildCandidateRect(anchor, labelSize, direction, offset);
                var score = ScoreCandidate(candidate, anchor, safeBounds, occupiedLabelRects, offset);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestRect = candidate;
                }
            }
        }

        return bestRect ?? SKRect.Create(anchor.X + 18, anchor.Y + 18, labelSize.Width, labelSize.Height);
    }

    private static SKRect BuildCandidateRect(SKPoint anchor, SKSize size, CandidateDirection direction, float offset)
    {
        var x = direction.X switch
        {
            > 0 => anchor.X + offset,
            < 0 => anchor.X - offset - size.Width,
            _ => anchor.X - (size.Width / 2)
        };

        var y = direction.Y switch
        {
            > 0 => anchor.Y + offset,
            < 0 => anchor.Y - offset - size.Height,
            _ => anchor.Y - (size.Height / 2)
        };

        return SKRect.Create(x, y, size.Width, size.Height);
    }

    private static float ScoreCandidate(SKRect candidate, SKPoint anchor, SKRect safeBounds, IReadOnlyList<SKRect> occupiedLabelRects, float offset)
    {
        var score = offset;

        if (candidate.Contains(anchor.X, anchor.Y))
        {
            score += 50_000;
        }

        score += GetOutsideArea(candidate, safeBounds) * 100;

        foreach (var occupiedRect in occupiedLabelRects)
        {
            score += GetIntersectionArea(candidate, occupiedRect) * 500;
        }

        return score;
    }

    private static SKPoint GetConnectionPoint(SKRect rect, SKPoint anchor)
    {
        var connectionX = Math.Clamp(anchor.X, rect.Left, rect.Right);
        var connectionY = Math.Clamp(anchor.Y, rect.Top, rect.Bottom);
        return new SKPoint(connectionX, connectionY);
    }

    private static void DrawFooter(SKCanvas canvas)
    {
        using var footerPaint = new SKPaint
        {
            Color = new SKColor(71, 85, 105),
            IsAntialias = true,
            TextSize = 15
        };

        canvas.DrawText(TileAttribution, 20, ImageHeight - 10, footerPaint);
    }

    private static MapViewport CreateViewport(IReadOnlyList<ExportItem> exportItems)
    {
        for (var zoom = MaxZoom; zoom >= MinZoom; zoom--)
        {
            var xCoordinates = exportItems.Select(item => LongitudeToPixelX(item.Longitude, zoom)).ToList();
            var yCoordinates = exportItems.Select(item => LatitudeToPixelY(item.Latitude, zoom)).ToList();

            var spanX = Math.Max(xCoordinates.Max() - xCoordinates.Min(), 100d);
            var spanY = Math.Max(yCoordinates.Max() - yCoordinates.Min(), 100d);

            if (spanX <= ImageWidth - (MapPadding * 2) && spanY <= ImageHeight - (MapPadding * 2))
            {
                return new MapViewport(
                    zoom,
                    (xCoordinates.Min() + xCoordinates.Max()) / 2,
                    (yCoordinates.Min() + yCoordinates.Max()) / 2);
            }
        }

        var minZoom = MinZoom;
        var minXs = exportItems.Select(item => LongitudeToPixelX(item.Longitude, minZoom)).ToList();
        var minYs = exportItems.Select(item => LatitudeToPixelY(item.Latitude, minZoom)).ToList();
        return new MapViewport(
            minZoom,
            (minXs.Min() + minXs.Max()) / 2,
            (minYs.Min() + minYs.Max()) / 2);
    }

    private static string FormatPlaceDateTime(DateTimeOffset? scheduledTime, DateTimeOffset? dayDate)
    {
        if (scheduledTime.HasValue)
        {
            return scheduledTime.Value.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        if (dayDate.HasValue)
        {
            return $"{dayDate.Value:dd.MM.yyyy} · Uhrzeit offen";
        }

        return "Zeitpunkt offen";
    }

    private static string FormatAccommodationDateTime(Accommodation accommodation)
    {
        if (accommodation.PlannedCheckIn.HasValue && accommodation.PlannedCheckOut.HasValue)
        {
            return $"Check-in {accommodation.PlannedCheckIn.Value:dd.MM.yyyy HH:mm} · Check-out {accommodation.PlannedCheckOut.Value:dd.MM.yyyy HH:mm}";
        }

        if (accommodation.PlannedCheckIn.HasValue)
        {
            return $"Check-in {accommodation.PlannedCheckIn.Value:dd.MM.yyyy HH:mm}";
        }

        if (accommodation.PlannedCheckOut.HasValue)
        {
            return $"Check-out {accommodation.PlannedCheckOut.Value:dd.MM.yyyy HH:mm}";
        }

        return "Unterkunft";
    }

    private static List<string> WrapText(string text, SKPaint paint, float maxWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return [text.Trim()];
        }

        var lines = new List<string>();
        var currentLine = words[0];

        for (var index = 1; index < words.Length; index++)
        {
            var candidate = $"{currentLine} {words[index]}";
            if (paint.MeasureText(candidate) <= maxWidth)
            {
                currentLine = candidate;
                continue;
            }

            lines.Add(currentLine);
            currentLine = words[index];
        }

        if (!string.IsNullOrWhiteSpace(currentLine))
        {
            lines.Add(currentLine);
        }

        return lines;
    }

    private static bool HasCoordinates(double latitude, double longitude) =>
        latitude is >= -85 and <= 85 && longitude is >= -180 and <= 180 && !(latitude == 0 && longitude == 0);

    private static SKColor ParseColor(string hexColor) =>
        SKColor.TryParse(hexColor, out var color) ? color : new SKColor(117, 117, 117);

    private static int WrapTileX(int tileX, int zoom)
    {
        var tileCount = 1 << zoom;
        var wrapped = tileX % tileCount;
        return wrapped < 0 ? wrapped + tileCount : wrapped;
    }

    private static bool PointsAlmostEqual(SKPoint first, SKPoint second) =>
        Math.Abs(first.X - second.X) < 0.5f && Math.Abs(first.Y - second.Y) < 0.5f;

    private static float GetOutsideArea(SKRect rect, SKRect bounds)
    {
        var outsideWidth = Math.Max(0, bounds.Left - rect.Left) + Math.Max(0, rect.Right - bounds.Right);
        var outsideHeight = Math.Max(0, bounds.Top - rect.Top) + Math.Max(0, rect.Bottom - bounds.Bottom);
        return (outsideWidth * rect.Height) + (outsideHeight * rect.Width);
    }

    private static float GetIntersectionArea(SKRect first, SKRect second)
    {
        var left = Math.Max(first.Left, second.Left);
        var top = Math.Max(first.Top, second.Top);
        var right = Math.Min(first.Right, second.Right);
        var bottom = Math.Min(first.Bottom, second.Bottom);

        if (right <= left || bottom <= top)
        {
            return 0;
        }

        return (right - left) * (bottom - top);
    }

    private static double LongitudeToPixelX(double longitude, int zoom)
    {
        var mapSize = TileSize * Math.Pow(2, zoom);
        return ((longitude + 180d) / 360d) * mapSize;
    }

    private static double LatitudeToPixelY(double latitude, int zoom)
    {
        var clampedLatitude = Math.Clamp(latitude, -85.05112878d, 85.05112878d);
        var mapSize = TileSize * Math.Pow(2, zoom);
        var sinLatitude = Math.Sin(clampedLatitude * Math.PI / 180d);
        var y = 0.5d - (Math.Log((1 + sinLatitude) / (1 - sinLatitude)) / (4 * Math.PI));
        return y * mapSize;
    }

    private sealed record ExportItem(
        string Name,
        string Subtitle,
        double Latitude,
        double Longitude,
        SKColor Color,
        DateTimeOffset SortKey,
        int SecondarySort,
        bool IsAccommodation);

    private sealed record TileImage(float DestinationX, float DestinationY, SKBitmap? Bitmap);

    private sealed record LabelLayout(SKSize Size, float NameLineHeight, float SubtitleLineHeight);

    private sealed record CandidateDirection(int X, int Y);

    private sealed class MapViewport(int zoom, double centerWorldX, double centerWorldY)
    {
        public int Zoom { get; } = zoom;
        public double LeftWorld => centerWorldX - (ImageWidth / 2d);
        public double TopWorld => centerWorldY - (ImageHeight / 2d);
        public double RightWorld => centerWorldX + (ImageWidth / 2d);
        public double BottomWorld => centerWorldY + (ImageHeight / 2d);

        public SKPoint Project(double latitude, double longitude) =>
            new(
                (float)(LongitudeToPixelX(longitude, Zoom) - LeftWorld),
                (float)(LatitudeToPixelY(latitude, Zoom) - TopWorld));
    }
}
