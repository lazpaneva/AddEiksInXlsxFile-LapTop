---
name: generate-mvc-tests
description: Generates xUnit tests for ASP.NET Core MVC controllers, views, and services in AddEiksInXlsxFile. Creates or extends the test project, unit tests for Services/, controller tests with Moq, and integration tests with WebApplicationFactory. Use when the user asks to add, generate, or write MVC tests, controller tests, integration tests, or test coverage for Upload, Search, Account, or related services.
---

# Generate MVC Tests

Generate tests that match this project's architecture: thin controllers, business logic in `Services/`, Identity auth, global antiforgery, and ClosedXML file processing.

## Before writing tests

1. Read the target controller, view model, and injected services.
2. Read [.cursor/rules/check-mvc-pages.mdc](../../rules/check-mvc-pages.mdc) — tests should assert the behaviors that rule enforces.
3. Prefer testing **services directly** for matching, normalization, and XLSX logic; use controller/integration tests for HTTP, auth, and view wiring.

## Test project bootstrap

If `AddEiksInXlsxFile.Tests` does not exist, create it:

```bash
dotnet new xunit -n AddEiksInXlsxFile.Tests -o AddEiksInXlsxFile.Tests
dotnet sln add AddEiksInXlsxFile.Tests/AddEiksInXlsxFile.Tests.csproj
dotnet add AddEiksInXlsxFile.Tests reference AddEiksInXlsxFile.csproj
dotnet add AddEiksInXlsxFile.Tests package Moq
dotnet add AddEiksInXlsxFile.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet add AddEiksInXlsxFile.Tests package Microsoft.EntityFrameworkCore.InMemory
```

Add to the web project `.csproj`:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="AddEiksInXlsxFile.Tests" />
</ItemGroup>
```

Add `public partial class Program { }` to the web app (separate file is fine) so `WebApplicationFactory<Program>` compiles.

**Folder layout:**

```
AddEiksInXlsxFile.Tests/
├── Controllers/
├── Services/
├── Integration/
├── Fixtures/          # sample .xlsx files built in tests or copied
└── Support/           # WebApplicationFactory, auth helpers, antiforgery helpers
```

## What to test where

| Layer | Test type | Focus |
|-------|-----------|-------|
| `StringNormalizationService`, `XlsxProcessingService` | Unit | Normalization, EIK rules, duplicate names, `!!!!`, empty UIC |
| Other `Services/*` | Unit | Pure logic; mock `XlsxService` paths when needed |
| `UploadController`, `SearchController`, `AccountController` | Unit (Moq) | Action results, redirects, ModelState, JSON vs View branches |
| End-to-end HTTP | Integration | Auth gates, GET pages render, POST with antiforgery, file upload |

Do **not** duplicate service assertions in controller tests — mock the service and assert the controller orchestrates correctly.

## Unit test patterns

### Static services

```csharp
[Theory]
[InlineData("  \"ACME\"  ", "ACME")]
[InlineData("Foo–Bar", "Foo-Bar")]
public void NormalizeCompanyName_strips_quotes_and_dashes(string input, string expected)
{
    Assert.Equal(expected, StringNormalizationService.NormalizeCompanyName(input));
}
```

### Controllers with Moq

- Instantiate controller with mocked dependencies.
- Set `ControllerContext` when testing `User`, `Request.Headers`, or antiforgery-dependent code:

```csharp
controller.ControllerContext = new ControllerContext
{
    HttpContext = new DefaultHttpContext { User = userPrincipal }
};
```

- Assert result types: `ViewResult`, `RedirectToActionResult`, `JsonResult`, `FileResult`, `NotFoundResult`, `BadRequestResult`.
- For `View(model)`: assert model type matches the view model in `Models/`.
- For validation failures: assert `ViewResult` (not redirect) and `ModelState` errors.

### XLSX service tests

Build workbooks in memory with ClosedXML; save to a temp directory; point `XlsxService` at that directory or pass filenames it resolves.

Cover AGENTS.md rules:

- Valid EIK: 9 or 12 digits only
- Invalid EIK → not copied, counted unmatched
- Duplicate normalized names with different EIKs → do not fill UIC
- Empty UIC in source → leave blank
- No match → `!!!!`

## Integration test patterns

Use a custom factory (see [reference.md](reference.md)) that:

- Replaces SQL Server with **EF Core InMemory** for `ApplicationDbContext`
- Skips or no-ops startup seeding failures
- Provides a test auth handler or logs in via `Account/Login`

**Auth expectations for this app:**

| Route | Expected without login | Expected with login |
|-------|------------------------|---------------------|
| `/Account/Login`, `/Account/Register` | 200 | 200 |
| `/Upload`, `/Search` | 302 → Login | 200 |

**Antiforgery:** Global `AutoValidateAntiforgeryTokenAttribute` is enabled. Integration POST tests must either:

1. GET the form page, parse `__RequestVerificationToken`, include it in POST, or
2. Use a helper that extracts the token from HTML (see `Support/AntiforgeryHelper.cs` in reference).

**Empty-page contract** (from check-mvc-pages rule):

- GET `/Search` with no file → 200, HTML contains title/nav, not a blank body
- Controller returns initialized view models (`new SearchViewModel { Rows = new List<SearchRow>() }`), never bare `View()`

## Naming and structure

```
{ClassUnderTest}Tests.cs
{Method}_{Scenario}_{ExpectedOutcome}()
```

Examples:

- `Index_Get_ReturnsViewWithEmptyUploadForm`
- `Index_Post_NoFiles_AddsModelErrorAndReturnsView`
- `Index_Post_Start_RecordsStatisticsAndReturnsResultView`
- `Download_MissingFile_ReturnsNotFound`

## Workflow checklist

Copy and track:

```
Task Progress:
- [ ] Test project exists and builds
- [ ] Service unit tests for changed business logic
- [ ] Controller unit tests for new/changed actions
- [ ] Integration tests for auth + GET render + critical POST
- [ ] XLSX fixtures cover happy path and edge cases
- [ ] dotnet test passes
```

## Verification

Always run:

```bash
dotnet test AddEiksInXlsxFile.Tests/AddEiksInXlsxFile.Tests.csproj
```

Fix failing tests before finishing. Do not weaken assertions to make tests pass.

## Scope limits

- Add tests for the requested controller/service only unless the user asks for full coverage.
- Do not refactor production code unless required for testability (`partial Program`, extract hard-coded paths).
- Do not commit unless the user asks.

## Additional resources

- Factory, auth, and antiforgery helpers: [reference.md](reference.md)
- MVC page checklist: [.cursor/rules/check-mvc-pages.mdc](../../rules/check-mvc-pages.mdc)
- Business rules: [AGENTS.md](../../../AGENTS.md)
