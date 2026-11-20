
Setup
-----
1. Solution has 2 projects: 
	- TranslinkUI: simple Web UI app to send HTTP GET, decode the response, and display all vehicle-position records associated with the requested route, e.g. Route 99 
	- TranslinkUI.ApiTests: simple API tests to verify HTTP response. 

2. Packages needed for both projects of TranslinkUI and TranslinkUI.ApiTests: 
	- GtfsRealtimeBindings
	- protobuf-net
	- protobuf-net.Core

3. Packages needed for TranslinkUI.ApiTests:
	- Microsoft.Extensions.Configuration
	- Microsoft.Extensions.Configuration.EnvironmentVariables
	- Microsoft.Extensions.Configuration.Json
	- Microsoft.NET.Test.Sdk
	- Microsoft.Playwright.NUnit
	- NUnit
	- NUnit.Analyzers
	- NUnit3TestAdapter

4. ..\TranslinkSolution\TranslinkUI\appsettings.Development.json
	- API settings of BaseUrl and API_Key
	- a few examples of routes info including RouteName and InternalRouteId
	- shared between TranslinkUI and TranslinkUI.ApiTests projects

5. in both projects of TranslinkUI and TranslinkUI.ApiTests, variable requestedRoute can be set to the latest requested route

6. FYI: 
	- IDE: Visual Studio 2026, Edition Community
	- Language: C# 
	- .NET version: 11.0.100


Commands to run projects
-------------------------
1. To run the Web UI app:
in Terminal #1, path of ..\TranslinkUI\  run the command
dotnet run --urls="http://localhost:5252"
in a browser, e.g. Chrome, put the page address of 
http://localhost:5252
refresh the page to get the latest response

2. To run the ApiTests project:
2.1 Option 1: 
run in Test Explorer of Visual Studio Community 
2.2 Option 2: to get test report file
in Terminal #2, path of ..\TranslinkUI.ApiTests\  run the command
dotnet test --logger "html;logfilename=ApiTestResults.html"
in a browser, e.g. Chrome, put the address of test report file
..\TranslinkUI.ApiTests\TestResults\ApiTestResults.html
with a clear PASS/FAIL result and meaningful messages


assumptions and known issues
----------------------------
1. Among GTFS Static of google_transit.zip downloaded from https://www.translink.ca/about-us/doing-business-with-translink/app-developer-resources/gtfs/gtfs-data
routes.txt and route_names_exceptions.txt explain the route names and route Ids, including 99 B-line with routeId 6641.
2. The two projects are independent, and the Web UI app is not a prerequisite for the ApiTests project. The test cases in the ApiTests projects are also independent, and can be run in any order.

References
----------
1. Docs: https://www.translink.ca/about-us/doing-business-with-translink/app-developer-resources/gtfs/gtfs-realtime
2. Endpoint: GET https://gtfsapi.translink.ca/v3/gtfsposition?apikey={API_KEY}
3. API_KEY: https://www.translink.ca/about-us/doing-business-with-translink/app-developer-resources 
