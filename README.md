# Chassis

A project-agnostic template for shipping **one codebase** as both a **desktop app**
(Windows + macOS, via Electron) and a **web app**, backed by a Domain-Driven
Design .NET core and a single React frontend.

- **Backend** — C# / .NET 10, strict DDD layering (Domain → Application →
  Infrastructure → Api), MediatR + FluentValidation, EF Core.
- **Frontend** — one React + TypeScript SPA, served as static files by the same
  ASP.NET Core app in both deployment modes.
- **Desktop** — [Electron.NET](https://github.com/ElectronNET/Electron.NET)
  (`ElectronNET.Core`): ASP.NET Core is the primary process and drives Electron;
  there is no separate Electron `main.ts` to maintain.
- **API contract** — TypeScript client generated from the OpenAPI document
  (`openapi-typescript` + `openapi-fetch`), never hand-written.
- **Multi-tenant from commit one** — every aggregate root carries a `TenantId`;
  a desktop install just uses one fixed value.

The full design rationale is in
[`docs/design_dotnet-electron-react-ddd-template.md`](docs/design_dotnet-electron-react-ddd-template.md).

---

## Repository layout

```
chassis/
├── server/
│   ├── Chassis.slnx                     .NET 10 solution (XML format)
│   ├── Directory.Build.props            shared build settings
│   ├── Directory.Packages.props         central package versions
│   └── src/
│       ├── Chassis.Domain/              entities, value objects, aggregates — no framework deps
│       ├── Chassis.Application/         CQRS-lite use cases (MediatR), validation, DTOs
│       ├── Chassis.Infrastructure/      EF Core, repositories, tenant provider, provider switch
│       ├── Chassis.Infrastructure.Migrations.Sqlite/   SQLite migrations (desktop)
│       ├── Chassis.Infrastructure.Migrations.Npgsql/   PostgreSQL migrations (web)
│       └── Chassis.Api/                 ASP.NET Core — API + OpenAPI + serves the SPA
│           ├── DesktopComposition/      every Electron.NET call lives here (and only here)
│           └── Properties/              electron-builder{,.release}.json, launchSettings
│   └── tests/                           xUnit: Domain / Application / Infrastructure
├── packages/
│   ├── api-client/                      @chassis/api-client — generated TS client
│   └── ui/                              @chassis/ui — the React SPA (Vite)
├── package.json                         npm workspace root (packages/* only)
└── docs/                                the design document
```

**Placeholder domain:** a single `Note` aggregate demonstrates the aggregate
shape and the tenant pattern (`TenantId` on the root, `ITenantProvider`, an EF
Core global query filter, a write-path guard, and domain events dispatched via
MediatR). Replace it with real aggregates when you start building features.

---

## Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 10.0.x |
| Node.js | 22+ |
| npm | 10+ |

Packaging additionally downloads Electron and the `electron-builder` toolchain
on first run. `dotnet-ef` (`dotnet tool install --global dotnet-ef`) is only
needed if you add migrations.

---

## Getting started

```sh
# 1. frontend dependencies (workspace root)
npm install

# 2. run as a plain web app  →  http://localhost:5030
dotnet run --project server/src/Chassis.Api
```

`dotnet build` / `dotnet run` automatically builds `packages/ui` and copies its
`dist/` into `Chassis.Api/wwwroot/`. Skip that step with
`-p:BuildFrontend=false` (the previous build's `wwwroot/` is still served).

Open <http://localhost:5030> for the placeholder Notes screen, or
<http://localhost:5030/scalar/v1> for the API explorer. The OpenAPI document is
served at `/openapi/v1.json` and also written to
`server/src/Chassis.Api/Chassis.Api.json` on every build.

### Frontend-only dev loop

```sh
npm run dev            # Vite dev server on :5173, proxies /api → :5030
```

Run the API (`dotnet run --project server/src/Chassis.Api`) alongside it.

### Run as the desktop app

```sh
dotnet run --project server/src/Chassis.Api -- --desktop
```

Launches Electron pointed at the app's own local URL. All window / menu / IPC /
auto-update code is in `Chassis.Api/DesktopComposition/`; `Program.cs` only
branches on whether to enable it.

---

## Database

One shared database, one schema, row-level `TenantId` isolation (design §4.2).
The provider is chosen in `Program.cs`: `Database:Provider` config if set,
otherwise **SQLite for the desktop build, PostgreSQL for a hosted deployment**.
Local dev (`appsettings.Development.json`) always uses SQLite, so `dotnet run`
needs no database. A PostgreSQL deployment must supply
`ConnectionStrings:Chassis` (e.g. `ConnectionStrings__Chassis=Host=…`).

Migrations are **per-provider assemblies** — `Chassis.Infrastructure.Migrations.Sqlite`
and `.Npgsql`. The schema shape is identical; only the physical column types
differ (`uuid` / `timestamptz` on Postgres, `TEXT` / `INTEGER` on SQLite). After
any model change, add the migration to **both**:

```sh
dotnet build server/Chassis.slnx

ChassisMigrationsProvider=Sqlite  dotnet ef migrations add <Name> \
  --project server/src/Chassis.Infrastructure.Migrations.Sqlite \
  --startup-project server/src/Chassis.Api --no-build -o Migrations
ChassisMigrationsProvider=Postgres dotnet ef migrations add <Name> \
  --project server/src/Chassis.Infrastructure.Migrations.Npgsql \
  --startup-project server/src/Chassis.Api --no-build -o Migrations
```

The app runs `Database.Migrate()` on startup against whichever provider is
configured.

---

## Testing

```sh
dotnet test server/Chassis.slnx     # backend — xUnit + FluentAssertions
npm test                            # frontend — Vitest + React Testing Library
npm run typecheck                   # tsc --noEmit across packages
```

---

## Regenerating the API client

The client types come from `Chassis.Api.json`, which is emitted on every
`dotnet build`. After changing the API surface:

```sh
dotnet build server/Chassis.slnx
npm run generate:api-client         # rewrites packages/api-client/src/schema.d.ts
```

Both `Chassis.Api.json` and `schema.d.ts` are committed, so a fresh checkout can
build the UI without running .NET.

---

## Packaging the desktop app

```sh
dotnet publish server/src/Chassis.Api -c Release -r win-x64 --self-contained false
```

Runs `electron-builder` via MSBuild and produces, under
`server/src/Chassis.Api/bin/Release/net10.0/win-x64/publish/`:

- `Chassis-Setup-<version>.exe` — NSIS installer
- `win-unpacked/` — `Chassis.exe` (Electron) launching the bundled .NET app + SPA

The app version is `<Version>` in `Chassis.Api.csproj`.

### Publishing a release (auto-update)

Auto-update pulls from **GitHub Releases**. The default
`Properties/electron-builder.json` has **no** `publish` block, so a local
`dotnet publish` just builds the installer. To build a release that uploads:

```sh
export GH_TOKEN=...        # a token with repo scope
dotnet publish server/src/Chassis.Api -c Release -r win-x64 --self-contained false \
  -p:ElectronBuilderJson=electron-builder.release.json
```

First set the real `owner` / `repo` in
`Properties/electron-builder.release.json`. Production releases also require
**code signing** (Windows Authenticode, macOS Developer ID + notarization) —
`electron-updater` on macOS will not apply an unsigned update.

---

## Project status

Bootstrapped through §11 steps 1–5 of the design doc:

| Step | State |
|---|---|
| 1 · DDD backend + tenant pattern | ✅ 24 tests |
| 2 · React SPA + generated API client | ✅ 3 tests |
| 3 · API serves the SPA (single origin) | ✅ verified in a browser |
| 4 · Electron.NET via `DesktopComposition` | ✅ launches, loads the SPA |
| 5 · Packaging + auto-update wiring | ✅ installer builds; update code wired |
| 6 · Real product features | — start here |

**Outstanding** (tracked in the design doc):

- §5.3 desktop lifecycle test suite — normal quit / force-kill / quit-mid-write,
  both OSes — deferred, run before shipping to real users.
  `StaleInstanceCleanup` mitigates the orphaned-process failure mode in the
  meantime.
- Auto-update end-to-end: a real signed release cycle against GitHub Releases
  has not been exercised.
- `ClaimsTenantProvider` (web auth), an app icon, and renderer-side wiring for
  the desktop IPC channels are not built yet.
