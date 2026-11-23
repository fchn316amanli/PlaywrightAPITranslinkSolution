
Setup
-----
1. Solution has 3 projects: 
	- TranslinkUI: simple Web UI app to send HTTP GET, decode the response, and display all vehicle-position records associated with the requested route, e.g. Route 99 
	- TranslinkUI.ApiTests.GtfsApiTests: simple API tests to verify HTTP response. 
	- TranslinkUI.ApiTests.PostResponseApiTests: integration tests to verify the HTTP call response of Web UI app.

2. Packages needed for both projects of TranslinkUI and TranslinkUI.ApiTests: 
	- GtfsRealtimeBindings
	- protobuf-net
	- protobuf-net.Core

3. Packages needed for TranslinkUI.ApiTests:
	- Microsoft.AspNetCore.Mvc.Testing
	- Microsoft.Extensions.Configuration
	- Microsoft.Extensions.Configuration.EnvironmentVariables
	- Microsoft.Extensions.Configuration.Json
	- Microsoft.NET.Test.Sdk
	- Microsoft.Playwright
	- Microsoft.Playwright.NUnit
	- Microsoft.Playwright.TestAdapter
	- NUnit
	- NUnit.Analyzers
	- NUnit3TestAdapter
	- WireMock.Net

4. ..\TranslinkSolution\TranslinkUI\appsettings.Development.json
	- API settings of BaseUrl and API_Key
	- requested route number, e.g. 099, according to the task assignment
	- a few examples of routes info including RouteName and InternalRouteId
	- shared between TranslinkUI and TranslinkUI.ApiTests projects

5. ..\TranslinkSolution\TranslinkUI.ApiTests\test.runsettings
	- define Playwright browser, e.g. chromium, and headless mode for the test runs
	- only needed for TranslinkUI.ApiTests.PostResponseApiTests

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
or just 
dotnet run 
and check the console output for port number, e.g. 5252, then in a browser, e.g. Chrome, put the page address of

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
2. The Web UI app project and GtfsApiTests project are independent, and the Web UI app is not a prerequisite for the ApiTests project. The test cases in the ApiTests projects are also independent, and can be run in any order.
3. The PostResponseApiTests project is dependent on the Web UI app project, and will call the Web UI app during the test to trigger the HTTP GET request and verify the response.


References
----------
1. Docs: https://www.translink.ca/about-us/doing-business-with-translink/app-developer-resources/gtfs/gtfs-realtime
2. Endpoint: GET https://gtfsapi.translink.ca/v3/gtfsposition?apikey={API_KEY}
3. API_KEY: https://www.translink.ca/about-us/doing-business-with-translink/app-developer-resources 
