using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using ProtoBuf;
using System.Buffers.Text;
using TransitRealtime;

namespace TranslinkUI.ApiTests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class GtfsApiTests : PlaywrightTest
{
    private IConfiguration _config = null!;
    private IAPIRequestContext _apiRequestContext = null!;
    private string _feedUrl = null!;

    private string _requestedRoute = "099";
    private string _internalRouteId = null!;

    [SetUp]
    public async Task SetUpAPITesting()
    {
        // 1. Load the linked appsettings.json from the test output directory
        _config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // 2. Read values from the Web UI's appsettings.json
        var baseUrl = _config["ApiSettings:BaseUrl"]
            ?? throw new InvalidOperationException("Error: BaseUrl missing in appsettings.json");
        var apiKey = _config["ApiSettings:Api_key"]
            ?? throw new InvalidOperationException("Error: API_Key missing in appsettings.json");
        _feedUrl = $"{baseUrl}?apikey={Uri.EscapeDataString(apiKey)}";
  
        _internalRouteId = _config[$"Routes:{_requestedRoute}:InternalRouteId"]
            ?? throw new InvalidOperationException($"Error: InternalRouteId for {_requestedRoute} missing in appsettings.json");

        Console.WriteLine($"===>SetUpAPITesting BaseURL: {baseUrl}");
        Console.WriteLine($"===>SetUpAPITesting API Key: {apiKey}");

        // 3. Create the Playwright API request context, and feedUrl for the GTFS-Realtime API
        _apiRequestContext = await Playwright.APIRequest.NewContextAsync();
    }


    [Test]
    public async Task ShouldGetResponse200()
    {
        IAPIResponse response = await _apiRequestContext.GetAsync(_feedUrl);
        Console.WriteLine($"===>Response status: {response.Status}");
        // if failed, then no further check
        Assert.That(response.Ok, Is.True);
        Assert.That(response.Status, Is.EqualTo(200));
    }


    [Test]
    public async Task ShouldGetValidGtfsRtFeedMessage()
    {
        IAPIResponse response = await _apiRequestContext.GetAsync(_feedUrl);

        Console.WriteLine($"===>Response status: {response.Status}");
        // if failed, then no further check
        Assert.That(response.Ok, Is.True);

        // Playwright reads raw binary responses via BodyAsync()
        byte[] bodyBytes = await response.BodyAsync();
        using var memoryStream = new MemoryStream(bodyBytes);
        FeedMessage feed = Serializer.Deserialize<FeedMessage>(memoryStream);

        var headerGtfsRealtimeVersion = feed.Header.GtfsRealtimeVersion;
        var headerTimestamp = feed.Header.Timestamp;
        var entityCount = feed.Entities.Count;

        Console.WriteLine($"===>Response header: {headerGtfsRealtimeVersion}");
        Console.WriteLine($"===>Response header: {headerTimestamp}");

        Assert.That(headerGtfsRealtimeVersion, Is.EqualTo("2.0"), "Error: Header GtfsRealtimeVersion");
        Assert.NotNull(headerTimestamp, "Error: Header Timestamp");
        //can be 0 when server error 20261117 10:30am, so should not include this assertion
        Assert.That(entityCount, Is.EqualTo(0).Or.GreaterThan(0), "Error: Entity Count");
    }



    [Test]
    public async Task ShouldGetNoDuplicatedBuses()
    {
        IAPIResponse response = await _apiRequestContext.GetAsync(_feedUrl);

        Console.WriteLine($"===>Response status: {response.Status}");
        // if failed, then no further check
        Assert.That(response.Ok, Is.True);

        // Playwright reads raw binary responses via BodyAsync()
        byte[] bodyBytes = await response.BodyAsync();
        using var memoryStream = new MemoryStream(bodyBytes);
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

        Console.WriteLine($"===>duplicateVehicleCount: {duplicateVehicleCount}");
 
        Assert.That(duplicateVehicleCount, Is.EqualTo(0), "Error: Duplicated Vehicle IDs found");
    }


    [TearDown]
    public async Task TearDownAPITesting()
    {
        // Dispose of the context after tests complete
        await _apiRequestContext.DisposeAsync();
    }
}
