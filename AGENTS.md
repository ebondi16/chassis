# Working in this repo

Chassis is a template: one codebase that runs as a **desktop app** (Electron.NET)
and a **web app**, on a DDD .NET 10 core with a single React SPA. See
[`README.md`](README.md) for the tour and
[`docs/design_dotnet-electron-react-ddd-template.md`](docs/design_dotnet-electron-react-ddd-template.md)
(Draft v7) for the rationale — that document is authoritative, and `§`-references
in code comments point into it.

## Commands

```sh
npm install                                   # frontend deps (run once, from repo root)

dotnet build server/Chassis.slnx              # builds everything; also builds packages/ui → wwwroot/
dotnet build server/Chassis.slnx -p:BuildFrontend=false   # skip the SPA build
dotnet test  server/Chassis.slnx              # backend tests (xUnit)
npm test                                      # frontend tests (Vitest)
npm run typecheck                             # tsc --noEmit across packages

dotnet run --project server/src/Chassis.Api               # web app → http://localhost:5030
dotnet run --project server/src/Chassis.Api -- --desktop  # desktop app (Electron; dynamic port)
npm run dev                                               # Vite dev server :5173, proxies /api → :5030

# after any API change:
dotnet build server/Chassis.slnx && npm run generate:api-client

# desktop installer:
dotnet publish server/src/Chassis.Api -c Release -r win-x64 --self-contained false

# new migration — run BOTH providers (migrations are per-provider assemblies):
ChassisMigrationsProvider=Sqlite  dotnet ef migrations add <Name> \
  --project server/src/Chassis.Infrastructure.Migrations.Sqlite \
  --startup-project server/src/Chassis.Api --no-build -o Migrations
ChassisMigrationsProvider=Postgres dotnet ef migrations add <Name> \
  --project server/src/Chassis.Infrastructure.Migrations.Npgsql \
  --startup-project server/src/Chassis.Api --no-build -o Migrations
# (build the solution first so --no-build has fresh output)
```

## Architecture rules — do not break these

- **Dependency direction:** `Domain ← Application ← Infrastructure ← Api`.
  `Chassis.Domain` has **zero** package references beyond the base class library.
- **Abstractions Infrastructure implements** (`INoteRepository`, `IUnitOfWork`)
  live in `Chassis.Application/Abstractions/`. `ITenantProvider` lives in
  `Chassis.Domain/Common/` (tenant scoping is a domain concern); its
  implementations live outside Domain.
- **Multi-tenancy:** every aggregate root carries a `TenantId`, set at creation
  and never reassigned. A new aggregate must implement `IAggregateRoot` and add
  its own `HasQueryFilter(...)` in `ChassisDbContext.OnModelCreating` — the
  write-path tenant guard then covers it for free.
- **Database provider:** SQLite (desktop) or PostgreSQL (web), chosen in
  `Program.cs` — `Database:Provider` config, else `isDesktop ? Sqlite : Postgres`.
  Migrations are **per-provider assemblies** (`Chassis.Infrastructure.Migrations.Sqlite`
  / `.Npgsql`), selected via `MigrationsAssembly(...)` in `AddInfrastructure`.
  Any model change needs a migration added to **both** (see Commands). Local dev
  (`appsettings.Development.json`) always uses SQLite. `ChassisDbContext`
  branches on `Database.IsSqlite()` only for the `DateTimeOffset` store type.
- **Electron is isolated:** every `using ElectronNET` / `Electron.*` call lives
  in `server/src/Chassis.Api/DesktopComposition/`. No other file references
  ElectronNET. `Program.cs` only calls `DesktopComposition.IsDesktopRun(args)`
  and `DesktopComposition.Enable(builder, args)`.
- **Frontend talks to the API only through `@chassis/api-client`** — no
  hand-written `fetch`, no duplicated response types.

## Conventions

- Solution file is `server/Chassis.slnx` (XML format), **not** `.sln`.
- **Central Package Management:** all NuGet versions live in
  `server/Directory.Packages.props`; `.csproj` files carry no `Version=`.
- **Pinned on purpose** (last free/OSS releases): `MediatR` 12.4.1,
  `FluentAssertions` 7.2.2. Don't bump these without checking the license change
  in newer majors. TypeScript is pinned to 5.9 (7.x breaks tooling peer ranges).
- **Generated and committed** — regenerate, never hand-edit:
  `server/src/Chassis.Api/Chassis.Api.json` (OpenAPI, emitted every
  `dotnet build`) and `packages/api-client/src/schema.d.ts`.
- `.NET` projects: nullable enabled, `TreatWarningsAsErrors=true`. Builds must be
  **0 warnings**.
- `packages/api-client` exports TypeScript source directly (no build step);
  consumers bundle it.

## Environment gotchas

- Referencing `ElectronNET.Core` makes **every `dotnet build` of `Chassis.Api`**
  run `npm install` under `bin/.../.electron/` (incremental after the first,
  ~16s cold).
- `Program.cs` guards the desktop branch **and** the startup DB migration behind
  `isDocumentGeneration` (entry assembly == `GetDocument.Insider`) so build-time
  OpenAPI generation doesn't boot the Electron runtime or touch a database.
  Keep that guard.
- Two electron-builder configs: `Properties/electron-builder.json` (no `publish`
  block — local `dotnet publish` builds the installer and exits clean) and
  `Properties/electron-builder.release.json` (GitHub publish — used via
  `-p:ElectronBuilderJson=electron-builder.release.json` + `GH_TOKEN` in CI).
- Desktop mode binds a **dynamic** localhost port (ElectronNET assigns it), not
  5030.
- ElectronNET.Core 0.5.2 has **no `electronize` CLI** and **no `-dotnetpacked`**
  — the runtime auto-probes its startup mode. `docs/…` §5.7 has the details.

## Definition of done for a change

1. `dotnet build server/Chassis.slnx` — 0 warnings, 0 errors.
2. `dotnet test server/Chassis.slnx` — all green; add/adjust tests for behavior changes.
3. `npm test` and `npm run typecheck` — green (if you touched `packages/`).
4. If you changed the API surface: regenerate the client (see Commands) and
   commit both generated files.
5. If you touched `Chassis.Api`: confirm web mode still serves — `GET /` → 200,
   `POST /api/notes` with `{"title":"x","body":""}` → 201.
