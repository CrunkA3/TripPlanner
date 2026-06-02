using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using TripPlanner.Web.Models;
using TripPlanner.Web.Services;

namespace TripPlanner.Tests;

public class TripMapExportServiceTests
{
    private const string TinyPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wn8x1QAAAAASUVORK5CYII=";

    [Fact]
    public async Task RenderTripAsyncReturnsPngBytes()
    {
        var service = new TripMapExportService(new StubHttpClientFactory(), NullLogger<TripMapExportService>.Instance);

        var trip = new Trip
        {
            Name = "Bayern Trip",
            Days =
            [
                new TripDay
                {
                    DayNumber = 1,
                    Date = new DateTimeOffset(2026, 6, 10, 0, 0, 0, TimeSpan.FromHours(2)),
                    Places =
                    [
                        new TripPlace
                        {
                            Order = 1,
                            ScheduledTime = new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.FromHours(2)),
                            Place = new Place
                            {
                                Name = "Marienplatz",
                                Latitude = 48.137154,
                                Longitude = 11.576124,
                                Category = PlaceCategory.Viewpoint
                            }
                        },
                        new TripPlace
                        {
                            Order = 2,
                            ScheduledTime = new DateTimeOffset(2026, 6, 10, 11, 30, 0, TimeSpan.FromHours(2)),
                            Place = new Place
                            {
                                Name = "Englischer Garten",
                                Latitude = 48.164229,
                                Longitude = 11.603241,
                                Category = PlaceCategory.Nature
                            }
                        }
                    ]
                }
            ],
            Accommodations =
            [
                new Accommodation
                {
                    Name = "Hotel Isar",
                    Latitude = 48.1304,
                    Longitude = 11.5878,
                    PlannedCheckIn = new DateTimeOffset(2026, 6, 10, 15, 0, 0, TimeSpan.FromHours(2)),
                    PlannedCheckOut = new DateTimeOffset(2026, 6, 11, 10, 0, 0, TimeSpan.FromHours(2))
                }
            ]
        };

        var bytes = await service.RenderTripAsync(trip);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 8);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes.Take(8).ToArray());
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHttpMessageHandler());
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Convert.FromBase64String(TinyPngBase64))
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return Task.FromResult(response);
        }
    }
}
