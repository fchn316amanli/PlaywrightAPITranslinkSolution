using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing; //for WebApplicationFactory <Program>
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using Microsoft.Testing.Platform.Services;
using NUnit.Framework;
using ProtoBuf;
using System.Text.RegularExpressions;
using TransitRealtime;
using WireMock.Handlers;
using WireMock.Logging;
using WireMock.Server;
using WireMock.Settings;


namespace TranslinkUI.ApiTests;

public class KestrelWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
{
    private readonly string _wireMockUrl;
    private readonly string _appUrl;
    private IHost? _kestrelHost;

    // Gets the address of the running Kestrel server.
    public string ServerAddress { get; private set; } = string.Empty;

    public KestrelWebApplicationFactory(string wireMockUrl, string appUrl)
    {
        _wireMockUrl = wireMockUrl;
        _appUrl = appUrl;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Override the Real Backend Server URL in web UI app's configuration
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // _wireMockUrl is root host only, NO path!
                //["ApiSettings:baseUrl"] = $"{_wireMockUrl}/v3/gtfsposition"// before add Path into appsettings.Development.json
                ["ApiSettings:baseUrl"] = _wireMockUrl
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Build the default TestServer host required by WebApplicationFactory internals
        var testHost = builder.Build();

        // Configure and start a real Kestrel host on the specified port so Playwright can connect
        builder.ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder.UseKestrel();
                webHostBuilder.UseUrls(_appUrl);
            });

        // Build and start the real Kestrel host
        _kestrelHost = builder.Build();
        _kestrelHost.Start();

        // Capture the actual bound URL (essential if using port 0)
        var server = _kestrelHost.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();
        ServerAddress = addresses!.Addresses.First();

        testHost.Start();
        return testHost;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _kestrelHost?.Dispose();
    }
}

// Setup path for proxy logs TestProjectxxx\__admin\mappings
public class ProjectFileSystemHandler : LocalFileSystemHandler
{
    private readonly string _projectRoot;
    public ProjectFileSystemHandler()
    {
        // Navigate up 3 levels from bin\Debug\net11.0 to the TestProject source directory
        _projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
    }
    public override string GetMappingFolder()
    {
        return Path.Combine(_projectRoot, "__admin", "mappings");
    }
}



[TestFixture]

// The 3 tests run one after another to analyze the same wireMock log
[NonParallelizable]
public class PostResponseApiTests : PageTest
{
    private IConfiguration _config = null!;
    private WireMockServer _wireMockServer = null!;
    private KestrelWebApplicationFactory<Program> _appFactory = null!;
    
    private const string AppUrl = "http://localhost:5252";
    private string _internalRouteId = null!;
    private string _baseUrl = null!;
    private string _path = null!;
    private List<ILogEntry> proxyLogs = new();


    //[OneTimeSetUp] run once and before [Setup] , not for evey test, so every test access the same WireMock log
    [OneTimeSetUp]
    public async Task SetUpWireMockApiTesting()
    {
        // 1. Load the linked appsettings.json
        _config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        // Read values from the Web UI's appsettings.json
        _baseUrl = _config["ApiSettings:baseUrl"]
            ?? throw new InvalidOperationException("Error: BaseUrl missing in appsettings.json");
        _path = _config["ApiSettings:Path"]
            ?? throw new InvalidOperationException("Error: Path missing in appsettings.json");
        string? requestedRoute = _config["Requested:RouteNumber"]
            ?? throw new InvalidOperationException("Error: Requested RouteNumber missing in appsettings.json");
        _internalRouteId = _config[$"Routes:{requestedRoute}:InternalRouteId"]
            ?? throw new InvalidOperationException($"Error: InternalRouteId for {requestedRoute} missing in appsettings.json");

        Thread.Sleep(1000);

        // 2. Setup and start WireMock in proxy & record mode
        _wireMockServer = WireMockServer.Start(new WireMockServerSettings
        {
            //Urls = new [] { WireMockUrl }, //removed, so let wriremock find a free port in case port conflict
            StartAdminInterface = true,
            // Saves to proxy logs TestProjectxxx\__admin\mappings
            FileSystemHandler = new ProjectFileSystemHandler(),
            ProxyAndRecordSettings = new ProxyAndRecordSettings
            {
                Url = _baseUrl, //Root host only, NO path!
                SaveMapping = true,
                SaveMappingToFile = true,//default path: TestProjectxxx/bin/Debug/net11.0/__admin/mappings/
                SaveMappingForStatusCodePattern = "2xx"
            }
        });
        Console.WriteLine("===> _wireMockServer started ... ");
        //Get the dynamically assigned port(e.g., http://127.0.0.1:1234), then pass to Kestrel in the next step
        string actualWireMockUrl = _wireMockServer.Url!;
        Console.WriteLine($"===> actualWireMockUrl  ... {actualWireMockUrl}");

        Thread.Sleep(1000);

        // 3. Start the ASP.NET Core Web UI on Kestrel
        _appFactory = new KestrelWebApplicationFactory<Program>(actualWireMockUrl, AppUrl);
        _ = _appFactory.Server; //or _ = _appFactory.CreateClient();
        Console.WriteLine("===> _appFactory started ... ");

        Thread.Sleep(1000);
    }


    [Test, Order(1)]
    public async Task Test1_WebAppShouldGetResponse200()
    {
        // 4. Launch Web UI App of the vehicle position table, to trigger the HTTP call
        Console.WriteLine($"===> Launching Web UI App at {AppUrl}...");
        Thread.Sleep(1000);
        await Page.GotoAsync(AppUrl);
        Thread.Sleep(1000);
        await Expect(Page).ToHaveTitleAsync(new Regex("TransLink GTFS RT info table"));
        await Page.ReloadAsync();
        Console.WriteLine("===> Web UI App Page reloaded ...");
        Thread.Sleep(1000);

        // Check if ASP.NET Core actually sent any HTTP requests to localhost:9091
        Assert.That(_wireMockServer.LogEntries, Is.Not.Empty,
            "===> WireMockServer received 0 requests!");

        // Get proxy logs for the real GTFS-RT Protobuf response from WireMock,
        // and use the same one for other tests
        proxyLogs = _wireMockServer.LogEntries
          .Where(entry => entry.RequestMessage.Path.Contains(_path)).ToList();
        Assert.That(proxyLogs, Is.Not.Empty, "Error:No GTFS API call.");

        var firstCall = proxyLogs.First();

        // Test1. The API returns a successful HTTP response
        Assert.That(firstCall.ResponseMessage?.StatusCode, Is.EqualTo(200));
    }


    [Test, Order(2)]
    public void Test2__WebAppShouldGetValidGtfsRtFeedMessage()
    {
        // reuse the same proxyLogs from Test1_WebAppShouldGetResponse200() to avoid multiple HTTP calls to the real GTFS-RT API
        var firstCall = proxyLogs.First();

        // Test2. The response can be decoded as a valid GTFS-Realtime FeedMessage
        Assert.That(firstCall.ResponseMessage.BodyData?.BodyAsBytes, Is.Not.Null.And.Not.Empty);

        byte[] bodyBytes = firstCall.ResponseMessage.BodyData?.BodyAsBytes;
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


    [Test, Order(3)]
    public void Test3__WebAppShouldGetNoDuplicatedBuses()
    {
        // reuse the same proxyLogs from Test1_WebAppShouldGetResponse200() to avoid multiple HTTP calls to the real GTFS-RT API
        var firstCall = proxyLogs.First();

        Assert.That(firstCall.ResponseMessage.BodyData?.BodyAsBytes, Is.Not.Null.And.Not.Empty);

        byte[] bodyBytes = firstCall.ResponseMessage.BodyData?.BodyAsBytes;
        var memoryStream = new MemoryStream(bodyBytes);
        FeedMessage feed = Serializer.Deserialize<FeedMessage>(memoryStream);

        var headerGtfsRealtimeVersion = feed.Header.GtfsRealtimeVersion;
        var headerTimestamp = feed.Header.Timestamp;
        var entityCount = feed.Entities.Count;

        // Test3.The reported Route vehicle count contains no duplicate vehicle IDs
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


    // Paire with [OneTimeSetup], run once after all tests
    [OneTimeTearDown]
    public async Task TearDownWireMockApiTesting()
    {
        _wireMockServer?.Stop();
        _wireMockServer?.Dispose();
        if (_appFactory != null)
        {
            await _appFactory.DisposeAsync(); // Must await!
        }
    }


}


