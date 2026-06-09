## TL;DR
Create a project-wide AGENTS.md file for the ASP.NET Core MVC application that documents the architectural vision, core goals, data flow, and key constraints. This will guide VS Code agents on how to navigate and contribute to the EIK-matching file upload application.

## Project Summary
- **Name**: AddEiksInXlsxFile
- **Type**: ASP.NET Core MVC application
- **Core Feature**: Upload two XLSX files, match company names, copy EIK values, generate statistics
- **Constraints**: No database, file-based processing only

## AGENTS.md Structure

### 1. Project Vision & Purpose
- Clear statement of what the application does (XLSX EIK matching and transfer)
- Primary user workflow: upload 2 files → matching → download result
- Key stakeholder benefit: automated EIK population from reference file

### 2. Core Architecture
**Components to describe**:
- **Upload Controller/View**: Handles two simultaneous file uploads, form validation
- **Matching Engine**: Logic to match company names across datasets
- **Data Models**: 
  - In-memory data structures (no DB) for company records, EIK values, match results
  - XLSX serialization/deserialization approach
- **Export Service**: Generates downloadable result file with populated EIK values
- **Statistics Service**: Calculates and formats matching statistics

**Key files to reference** (pattern-based, not implementation specifics):
- Controllers/*Upload* — entry point for user workflows
- Services/*Match* — matching logic implementation
- Services/*Export* — XLSX generation with ClosedXML
- Models/* — data structures for companies and matches
- Views/Upload/* — user-facing forms

### 3. Data Flow Diagram (Text-based in AGENTS.md)
```
User Upload (XLSX files)
  ↓
File Parsing (Extract company names, EIK values)
  ↓
Matching Algorithm (Compare companies file1 ↔ file2)
  ↓
Results Compilation (Successful matches, unmatched entries)
  ↓
XLSX Generation (Populate File2 with EIK from File1)
  ↓
Statistics Calculation (Match count, success rate, unmatched summary)
  ↓
Download Result. Statistics only visible in window.
```

### 4. Technology Stack & Libraries
- ASP.NET Core MVC (.NET 8+ or specified version)
- XLSX handling: ClosedXML
- No Entity Framework or databases
- In-memory processing only

### 5. Key Conventions & Constraints
- **File Upload Limits**: Specify max file size, accepted formats (XLSX only)
- **Name Matching Strategy**: Case-insensitive, exact match after clearing all types of dashes, quotes, periods, commas, multiple spaces with one, etc.
The following rules should also be added: if there are identical company names with different identifiers, do not fill in the new UIC column; if there is an empty field in the UIC, leave it blank; if there is an undiscovered company, put "!!!!"
- **EIK Format**: 9, 10, or 13 digit numeric string, or blank (validate format before processing). If the EIK value is invalid, it should not be copied to the result file and should be counted as an unmatched entry.
- **Statistics Tracked**: Total companies, matched count, success rate, unmatched details
- **Error Handling**: Invalid file format, empty files, malformed XLSX, encoding issues

### 6. Known Limitations & Trade-offs
- **No persistence**: Results exist only during session (no save-to-DB)
- **Memory constraints**: Large files may cause performance issues (document expected limits)
- **No user authentication**: File uploads are public (if relevant)
- **Single-session workflows**: Users must complete upload → download in one session

### 7. Build & Run Commands
- `dotnet build`
- `dotnet run`
- Test command (if applicable): `dotnet test`
- Expected entry point/URL: `http://localhost:*/Upload` (or your route)

### 8. Testing & Quality
- Unit tests for matching algorithm (critical path)
- Integration tests for XLSX parsing and generation
- Edge cases: empty files, mismatched columns

---

## Decisions & Assumptions
- **File Format**: XLSX only (not CSV or other formats)
- **Name Matching**: Approach TBD in implementation (exact, case-insensitive, fuzzy, Levenshtein distance, etc.)
- **Statistics Output**: Separate report in window, not included in result file
- **Session State**: In-memory during HTTP request lifecycle; no state persistence across sessions

## Implementation Steps (for user to execute)
1. Create `.github/` folder in project root (if not exists)
2. Create `AGENTS.md` at root
3. Fill in sections with project-specific details using the template outline above
4. Reference actual file paths and class names from the implementation
5. Validate YAML frontmatter if using any metadata

## Authentication & Statistics (recommended additions)

1. Add Identity & EF Core packages to the project:
  - `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
  - `Microsoft.EntityFrameworkCore.SqlServer`
  - `Microsoft.EntityFrameworkCore.Tools`

2. Create an `ApplicationDbContext : IdentityDbContext<ApplicationUser>` that includes a `DbSet<ProcessingStatistics>`.

3. Define `ProcessingStatistics` model with fields such as:
  - `Id` (int, PK)
  - `UserId` (string, nullable)
  - `TimestampUtc` (DateTime)
  - `InputFile1` (string)
  - `InputFile2` (string)
  - `OutputFilePath` (string)
  - `TotalRows` (int)
  - `MatchedCount` (int)
  - `SuccessRate` (decimal)
  - `ErrorMessage` (string, nullable)

4. Register Identity and `ApplicationDbContext` in `Program.cs` with a connection string for SQL Server Express, for example:

  `Server=.\SQLEXPRESS;Database=AddEiksDb;Trusted_Connection=True`

5. Create and apply EF Core migrations:
  - `dotnet ef migrations add InitialIdentity`
  - `dotnet ef database update`

6. Seed roles (`Admin`, `User`) and an initial admin account on startup. Use `IServiceProvider` scope in `Program.cs` to run seeding logic.

7. Add authentication UI and controllers:
  - `AccountController` with `Login`, `Logout` (and optional `Register`) actions and views.
  - Protect upload/processing actions with `[Authorize]`. Restrict admin pages with `[Authorize(Roles = "Admin")]`.

8. Integrate statistics recording:
  - Inject a `StatisticsService` into the processing flow. After `ProcessAndSort` completes (or fails), create and save a `ProcessingStatistics` record with relevant fields and the current `User.Identity.Name` or `UserId`.
  - Provide an admin page that lists and filters `ProcessingStatistics` entries.

9. Operational recommendations:
  - Store connection strings in `appsettings.Development.json` and production secrets in user secrets or environment variables.
  - Use migrations and backups for schema changes.
  - Consider retention policies for statistics (archive or delete old records periodically).

10. Verification steps for auth + stats:
  - Confirm Identity schema is created in SQL Server Express after migrations.
  - Log in as an Admin and verify role-restricted pages are accessible.
  - Run a processing job while authenticated and verify a `ProcessingStatistics` record is created and visible in the admin UI.

## Verification
- File created at correct location (root or `.github/`)
- All required sections present: Vision, Architecture, Data Flow, Stack, Conventions, Constraints, Build Commands
- No merge conflicts with existing docs (README, CONTRIBUTING, etc.)
- Agent can read and understand the structure (no syntax errors)
