using ProtoBuf;//Gtfs decoder
using ProtoBuf.Meta;
using ProtoBuf.WellKnownTypes;
using System.Net;
using System.Net.Http;
using System.Text;
using TransitRealtime; //GtfsRealtimeBindings


try
{
    var builder = WebApplication.CreateBuilder(args);
    // Register HttpClient for making external API calls
    builder.Services.AddHttpClient();
    //need to be after services.addhttpclient() otherwise it will throw an error
    var app = builder.Build();

    app.MapGet("/", async (IConfiguration config, IHttpClientFactory httpClientFactory) =>
    {
        // Retrieve the base URL and API key from configuration file appsettings.json
        string? baseUrl = config["ApiSettings:baseUrl"]
            ?? throw new InvalidOperationException("Error: BaseUrl missing in appsettings.json");
        string? path = config["ApiSettings:Path"]
            ?? throw new InvalidOperationException("Error: Path missing in appsettings.json");
        string? apiKey = config["ApiSettings:API_key"]
            ?? throw new InvalidOperationException("Error: API_Key missing in appsettings.json");
        string feedUrl = $"{baseUrl}{path}?apikey={Uri.EscapeDataString(apiKey)}";

        // Retrieve the route name and internal route ID for the required route from configuration
        string? requestedRoute = config["Requested:RouteNumber"]
            ?? throw new InvalidOperationException("Error: Requested RouteNumber missing in appsettings.json");
        string? routeName = config[$"Routes:{requestedRoute}:RouteName"];
        string? internalRouteId = config[$"Routes:{requestedRoute}:InternalRouteId"];

        // Use the injected factory and read the stream from the single response
        using HttpClient client = httpClientFactory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(
            feedUrl,
            HttpCompletionOption.ResponseHeadersRead
        );
        // Handle the response based on the status code
        if (response.StatusCode == HttpStatusCode.OK)
        {
            // duplicate call with HttpClient
            //using Stream stream = await client.GetStreamAsync(feedUrl);
            //Fetch the raw binary stream
            using Stream stream = await response.Content.ReadAsStreamAsync();

            // Decode the GTFS-Realtime Protocol Buffer message
            FeedMessage feed = Serializer.Deserialize<FeedMessage>(stream);

            // Build a minimal HTML response 1st part
            var html = new StringBuilder();
            html.Append("""
            <!DOCTYPE html>
            <html>
            <head>
                <title>TransLink GTFS RT info table</title>
                <style>
                    body { font-family: system-ui, sans-serif; margin: 2rem; }
                    table { border-collapse: collapse; width: 100%; margin-top: 1rem; }
                    th, td { border: 1px solid #ccc; padding: 8px; text-align: left; }
                    th { background-color: #f4f4f4; }
                </style>
            </head>
            <body>
                <h1>TransLink GTFS RT info table</h1>
            """);

            // feed.header info
            html.Append($"<p><strong>Route:</strong> {routeName}</p>");
            var timestamp = DateTimeOffset.FromUnixTimeSeconds((long)feed.Header.Timestamp).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            html.Append($"<p><strong>Feed Timestamp:</strong> {timestamp} | <strong>Total Entities:</strong> {feed.Entities.Count}</p>");

            // Column headers
            html.Append("""
                <table>
                    <thead>
                        <tr>
                            <th>VehicleID</th>
                            <th>RouteID</th>
                            <th>DirectionID</th>
                            <th>Timestamp</th>
                            <th>Latitude</th>
                            <th>Longitude</th>
                        </tr>
                    </thead>
                    <tbody>
            """);

            // Set up data structure to verify and prevent duplicates of Vehicle IDs for the specific route
            var noDuplicateVehicleIds = new HashSet<string>();
            int duplicateVehicleCount = 0;

            // Loop through the entities and extract Vehicle Position data
            foreach (var entity in feed.Entities)
            {
                if (entity.Vehicle != null)
                {
                    if (entity.Vehicle.Trip != null)
                    {
                        if (entity.Vehicle.Trip.RouteId == internalRouteId)
                        {
                            if (entity.Vehicle.Vehicle.Id != null && noDuplicateVehicleIds.Add(entity.Vehicle.Vehicle.Id))
                            {
         
                                var v = entity.Vehicle;
                                var vehicleId = v.Vehicle?.Id ?? "N/A";
                                var routeId = v.Trip?.RouteId ?? "N/A";
                                var directionId = v.Trip?.DirectionId.ToString() ?? "N/A";//1 digit
                                //var timestampVehicle = v.Timestamp ?? "N/A";//original timestamp in seconds since epoch
                                var timestampVehicle = DateTimeOffset.FromUnixTimeSeconds((long)v.Timestamp).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";
                                var lat = v.Position?.Latitude.ToString("F5") ?? "N/A";//5 digits after dot
                                var lon = v.Position?.Longitude.ToString("F5") ?? "N/A";
                                
                                //To-Do: Should remove later
                                //var tripId = v.Trip?.TripId ?? "N/A";
                                //var stopId = v.StopId ?? "N/A";
                                //var currentStop = v.CurrentStopSequence.ToString() ?? "N/A";

                                // Append the vehicle data to the HTML table
                                html.Append($"""
                                     <tr>
                                        <td>{vehicleId}</td>    
                                        <td>{routeId}</td>
                                        <td>{directionId}</td>
                                        <td>{timestampVehicle}</td>
                                        <td>{lat}</td>
                                        <td>{lon}</td>
                                      </tr>
                                     """);

                                continue;
                            }
                            else
                            {
                                // Duplicate vehicle ID found, skipping this entry 
                                duplicateVehicleCount++;
                            }

                        }
                    }
                }
            }

            // Build a minimal HTML response 2nd part
            html.Append("""
                    </tbody>
                </table>
            </body>
            </html>
            """);

            //Count the unique buses reported for this specific route, and duplicate bus count
            html.Append($"<p><strong>Total buses of {routeName}:</strong> {noDuplicateVehicleIds.Count} | <strong>Duplicated  bus count:</strong> {duplicateVehicleCount}</p>");

            return Results.Content(html.ToString(), "text/html");

        }
        else
        {   //e.g. Failed to fetch GTFS-RT feed. Status code: Forbidden
            return Results.Problem($"===>Failed to fetch GTFS-RT feed. Status code: {response.StatusCode}");
        }
    }); 

    app.Run();

}

catch (Exception ex)
{
    Console.WriteLine($"===>UI App startup error: {ex}");
    throw;
}

// Test projects to reference <Program>
public partial class Program { }
