# IIGO Panel – Copilot Instructions

## Project Overview

IIGO Panel is a self-hosted Blazor Server application (Interactive Server render mode) for managing IIS on Windows Server 2012+. It runs as a Windows Service and targets `net10.0-windows8.0`.

**Solution file:** `IIGO/IIGO.slnx`

## Build Commands

```powershell
# Restore dependencies
dotnet restore IIGO\IIGO.slnx

# Build (CI uses MSBuild, not dotnet build)
msbuild IIGO\IIGO.slnx /verbosity:minimal

# Add an EF Core migration
dotnet ef migrations add <MigrationName> --project IIGO
```

There are no automated tests in this repository.

## Architecture

The solution has three projects:

- **`IIGO/`** – Main Blazor Server app (services, pages, auth, data layer)
- **`Microsoft.Web.Administration/`** – Bundled custom fork of the Microsoft.Web.Administration library used to interact with IIS
- **`Microsoft.Web.Configuration.AppHostFileProvider/`** – Companion config provider for the above

### Key Subsystems

**IIS Management**  
`IISService` wraps `IISWrapper`, which calls into the local `Microsoft.Web.Administration` project (not the NuGet package). All IIS interactions (sites, app pools, bindings) go through `IISService` → `IISWrapper` → `ServerManager`.

**Authorization**  
Feature-based authorization is layered on top of ASP.NET Identity roles:
- `Feature` (static class of string constants) defines all permission keys.
- Each feature is registered as an ASP.NET authorization policy in `Program.cs`.
- `FeatureAuthorizationHandler` grants access if the user is in the `Administrator` role (bypasses DB check) or if a `RolePermission` row exists for the user's role and the required feature.
- New pages must use `@attribute [Authorize(Policy = Feature.XYZ)]`.

**Messenger Services (Notifications)**  
All notification providers (SMTP, SendGrid, Discord, Slack, AWS SES/SNS, Postmark, Google Chat) implement `IMessengerService` and are registered as scoped services. The active provider is resolved at runtime by name via the `ServiceResolver` delegate:
```csharp
var resolver = serviceProvider.GetRequiredService<ServiceResolver>();
var svc = resolver("SMTPService"); // matches IMessengerService.ServiceName
```
New messenger services must extend `ServiceBase` and implement `IMessengerService`. The active service name is stored in `ConfigSetting` under key `"MessengerService"`.

**Configuration / Settings**  
App settings are stored in the `ConfigSetting` SQLite table (key/value pairs). `ServiceBase.GetSetting()` reads a setting and auto-creates it with a default if missing. Seed defaults are applied in `InstallInitialSettings` in `Program.cs`.

**Database**  
SQLite via EF Core. The DB path (`~/Data/cpdata.db`) is resolved relative to the app base directory. EF migrations run automatically on startup (`context.Database.Migrate()`). Add new migrations with `dotnet ef migrations add`.

**Windows Service / Event Log**  
The app registers itself as a Windows Service (`"IIGO Panel Service"`). Errors and significant events are written to the Windows Event Log under source/log name `"IIGO"`. Use `EventLog.WriteEntry(Constants.EventLogSource, ...)` for error logging in services.

## Key Conventions

- **Blazor pages** live in `IIGO/Components/Pages/`. Always add `@rendermode InteractiveServer` and the appropriate `[Authorize]` attribute.
- **Roles:** Two built-in roles: `Administrator` (full access) and `Manager` (configurable per-feature via `RolePermission`). Administrators bypass all feature checks.
- **Adding a feature:** Define a new `const string` in `Feature`, add it to `Feature.All`, authorize pages with it, and seed any default `RolePermission` entries for the Manager role in `InstallInitialSettings`.
- **Nullable reference types** are enabled project-wide (`<Nullable>enable</Nullable>`).
- **`SafeFireAndForget()`** (from `AsyncAwaitBestPractices`) is used for fire-and-forget async calls (e.g., `InstallInitialSettings`).
- **`ex.Demystify()`** (from `Ben.Demystifier`) is used when logging exceptions to improve stack trace readability.
- The CI workflow (`dotnet.yml`) runs on `windows-2025-vs2026` runners; the project requires Windows due to IIS and COM interop dependencies.
