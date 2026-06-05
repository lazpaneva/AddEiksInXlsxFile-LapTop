# MVC Test Reference — AddEiksInXlsxFile

## CustomWebApplicationFactory

```csharp
using AddEiksInXlsxFile.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AddEiksInXlsxFile.Tests.Support;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }
}
```

## Authenticated client

Option A — login through the real form (preferred for Identity):

```csharp
public static async Task<HttpClient> CreateAuthenticatedClientAsync(CustomWebApplicationFactory factory)
{
    var client = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    var loginPage = await client.GetAsync("/Account/Login");
    var html = await loginPage.Content.ReadAsStringAsync();
    var token = AntiforgeryHelper.ExtractToken(html);

    var form = new Dictionary<string, string>
    {
        ["Email"] = "test@example.com",
        ["Password"] = "Test123!",
        ["__RequestVerificationToken"] = token
    };

    // Seed user in factory scope before login if needed
    var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(form));
    return client;
}
```

Option B — test auth handler for controller unit tests:

```csharp
services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "Test";
    options.DefaultChallengeScheme = "Test";
})
.AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
```

## AntiforgeryHelper

```csharp
using System.Text.RegularExpressions;

namespace AddEiksInXlsxFile.Tests.Support;

public static class AntiforgeryHelper
{
    private static readonly Regex TokenRegex = new(
        @"name=""__RequestVerificationToken""\s+type=""hidden""\s+value=""([^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string ExtractToken(string html)
    {
        var match = TokenRegex.Match(html);
        if (!match.Success)
            throw new InvalidOperationException("Antiforgery token not found in HTML.");
        return match.Groups[1].Value;
    }
}
```

## UploadController unit test skeleton

```csharp
using AddEiksInXlsxFile.Controllers;
using AddEiksInXlsxFile.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AddEiksInXlsxFile.Tests.Controllers;

public class UploadControllerTests
{
    private readonly Mock<XlsxService> _xlsx = new();
    private readonly Mock<XlsxProcessingService> _processing = new();
    private readonly Mock<StatisticsService> _stats = new();
    private readonly Mock<SearchService> _search = new();

    private UploadController CreateController() =>
        new(_xlsx.Object, _processing.Object, _stats.Object, _search.Object);

    [Fact]
    public async Task Index_Post_NoFiles_ReturnsViewWithModelError()
    {
        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await controller.Index(null, null);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }
}
```

Note: `StatisticsService` is a concrete class — either use a real instance with InMemory DB in integration tests, or wrap behind an interface if controller unit tests need strict isolation.

## XLSX fixture builder

```csharp
using ClosedXML.Excel;

namespace AddEiksInXlsxFile.Tests.Fixtures;

public static class XlsxFixture
{
    public static string CreateReferenceFile(string dir, string company, string eik)
    {
        var path = Path.Combine(dir, "ref.xlsx");
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Sheet1");
        ws.Cell(1, 1).Value = "Company";
        ws.Cell(1, 2).Value = "EIK";
        ws.Cell(2, 1).Value = company;
        ws.Cell(2, 2).Value = eik;
        wb.SaveAs(path);
        return path;
    }
}
```

## Integration test examples

```csharp
public class UploadPageTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public UploadPageTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Index_Unauthenticated_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await client.GetAsync("/Upload");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location?.OriginalString);
    }
}
```

## SearchController scenarios to cover

| Scenario | Assert |
|----------|--------|
| No result file on disk | 200, empty `SearchViewModel`, info alert in HTML |
| File with `!!!!` rows | Rows populated, only `!!!!` EIK cells listed |
| `SaveOperatorEdits` invalid EIK | JSON `errors` contains validation message |
| `SaveOperatorEdits` valid EIK on `!!!!` cell | Output file created, cell updated |
| `SaveOperatorEdits` on non-`!!!!` cell | Skipped with error |

## AccountController scenarios

| Scenario | Assert |
|----------|--------|
| GET Login | `LoginViewModel` in view |
| POST Login invalid model | Returns view, not redirect |
| POST Login success | Redirect to `/Upload` or local `ReturnUrl` |
| POST Register success | Redirect to `/Upload` |
| POST Logout | Redirect, user signed out |

## Program.cs testability note

Top-level statements require:

```csharp
// File: Program.cs or Program.Visibility.cs in web project
public partial class Program { }
```

Without this, `WebApplicationFactory<Program>` will not compile.
