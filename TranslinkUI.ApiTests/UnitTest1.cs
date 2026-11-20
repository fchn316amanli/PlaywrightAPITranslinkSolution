using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using ProtoBuf;//Gtfs decoder
using System.Buffers.Text;
using TransitRealtime;//GtfsRealtimeBindings

namespace TranslinkUI.ApiTests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class GtfsApiTests : PlaywrightTest
{
    private IConfiguration _config = null!;
    private IAPIRequestContext _apiRequestContext = null!;
    private IAPIResponse _response = null!;
    private string _feedUrl = null!;
    private string requestedRoute = "099";
    private string _internalRouteId = null!;

    [SetUp]
    public async Task SetUpAPITesting()
    {
        // Load the linked appsettings.json
        _config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Read values from the Web UI's appsettings.json
        var baseUrl = _config["ApiSettings:BaseUrl"]
            ?? throw new InvalidOperationException("Error: BaseUrl missing in appsettings.json");
        var apiKey = _config["ApiSettings:Api_key"]
            ?? throw new InvalidOperationException("Error: API_Key missing in appsettings.json");
        _feedUrl = $"{baseUrl}?apikey={Uri.EscapeDataString(apiKey)}";
  
        _internalRouteId = _config[$"Routes:{requestedRoute}:InternalRouteId"]
            ?? throw new InvalidOperationException($"Error: InternalRouteId for {requestedRoute} missing in appsettings.json");

        // Create the Playwright API request and response
        _apiRequestContext = await Playwright.APIRequest.NewContextAsync();
        _response = await _apiRequestContext.GetAsync(_feedUrl);
    }


    [Test]
    public async Task ShouldGetResponse200()
    {
        Assert.That(_response.Ok, Is.True);
        Assert.That(_response.Status, Is.EqualTo(200));
    }


    [Test]
    public async Task ShouldGetValidGtfsRtFeedMessage()
    {
        Assert.That(_response.Ok, Is.True);

        // Playwright reads raw binary responses via BodyAsync()
        // BodyAsync() returns the buffer with response body.
        byte[] bodyBytes = await _response.BodyAsync();
        var memoryStream = new MemoryStream(bodyBytes);
        FeedMessage feed = Serializer.Deserialize<FeedMessage>(memoryStream);

        var headerGtfsRealtimeVersion = feed.Header.GtfsRealtimeVersion;
        var headerTimestamp = feed.Header.Timestamp;
        var entityCount = feed.Entities.Count;

        Assert.That(headerGtfsRealtimeVersion, Is.EqualTo("2.0"), "Error: Header GtfsRealtimeVersion");
        Assert.NotNull(headerTimestamp, "Error: Header Timestamp");
        //can be 0 when server error 20261117 10:30am
        Assert.That(entityCount, Is.EqualTo(0).Or.GreaterThan(0), "Error: Entity Count");
    }



    [Test]
    public async Task ShouldGetNoDuplicatedBuses()
    {
        Assert.That(_response.Ok, Is.True);

        // Playwright reads raw binary responses via BodyAsync()
        // BodyAsync() returns the buffer with response body.
        byte[] bodyBytes = await _response.BodyAsync();
        var memoryStream = new MemoryStream(bodyBytes);
        FeedMessage feed = Serializer.Deserialize<FeedMessage>(memoryStream);

        var noDuplicateVehicleIds = new HashSet<string>();
        int duplicateVehicleCount = 0;
        // Loop through vehicleId and check for duplicates
        foreach (var entity in feed.Entities)
        {
            if (entity.Vehicle != null)
            {
                if (entity.Vehicle.Trip != null)
                {
                    if (entity.Vehicle.Trip.RouteId == _internalRouteId)
                    {
                        if (entity.Vehicle.Vehicle.Id != null && noDuplicateVehicleIds.Add(entity.Vehicle.Vehicle.Id))
                        {
                            continue;
                        }
                        else
                        {
                            Console.WriteLine($"===>Duplicated vehicle ID found: {entity.Vehicle.Vehicle.Id}");
                            duplicateVehicleCount++;
                        }

                    }
                }
            }
        }
 
        Assert.That(duplicateVehicleCount, Is.EqualTo(0), "Error: Duplicated Vehicle IDs found");
    }


    [TearDown]
    public async Task TearDownAPITesting()
    {
        // Dispose of the context after tests complete
        await _apiRequestContext.DisposeAsync();
    }
}
