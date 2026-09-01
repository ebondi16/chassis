# Chassis — .NET DDD Backend + Electron.NET + React Frontend Template

**Status:** Draft v7 — supersedes v1's `node-api-dotnet` desktop-hosting
decision; folds in post-v2 research on Electron.NET's lifecycle history,
packaging modes, and auto-update support (§5.2, §8, §9); §9's open
questions are now decided (see §9); project named **Chassis**.
**v7 changes:** added §5.8, a decision to detect and kill leftover
processes from a previous run on startup — a self-healing mitigation for
the orphaned-process failure mode §5.7 confirmed, distinct from
Electron's single-instance lock (which doesn't help against a *dead*
process still holding the lock). §9 item 1 now cross-references it. This
does not replace §5.3's deferred full lifecycle test suite.
**v6 changes:** Step 4 (real implementation of the desktop toggle, on
`ElectronNET.Core` 0.5.2 targeting `net10.0`) confirmed the architecture
holds, but found the *tooling* story in §3, §5.6, §8, §10, and §11 was
wrong in several concrete ways — no `electronize` CLI exists, the
`-dotnetpacked` flag is gone in favor of runtime auto-probing, packaging
config lives in `Properties/electron-builder.json` + MSBuild props
rather than `desktop/electron.manifest.json`, and the Socket.IO IPC
bridge is still Socket.IO (rewritten, not replaced). See §5.2's updated
note, the new §5.7, and the rewritten §8 for specifics. Step 4 also
produced the first concrete evidence on §5.3's deferred lifecycle
question: a hard external kill of the .NET host orphaned 4 Electron
children; a normal window-close quit did not. §9 item 1 is updated
accordingly — still deferred, but no longer resting only on absence of
GitHub-issue reports.
**Scope:** Project-agnostic architecture template, not tied to any
specific product. Answers: *"How do I ship one codebase that runs as
both a desktop app (Windows + macOS) and a web app, backed by a proper
DDD .NET core, with the desktop version behaving as a single coordinated
app and shipping updates via GitHub Releases?"*

**What changed from v1:** desktop hosting moved from `node-api-dotnet`
(embedding the .NET CLR inside Electron's main process) to
**[Electron.NET](https://github.com/ElectronNET/Electron.NET)**, which
inverts the control direction: **your ASP.NET Core app is the primary
process, and it drives Electron** via `UseElectron()`, rather than
Electron driving an embedded .NET runtime. Domain/Application/
Infrastructure and the shared React UI are unaffected by this change.

---

## 1. Problem This Template Solves

You want:

1. A **.NET backend** built with real Domain-Driven Design discipline.
2. That backend to run **both as a local desktop app and, later, as a
   hosted multi-tenant web service**, without rewriting the core.
3. A desktop app that behaves like **one coordinated application** to the
   end user — one thing to launch, one thing to quit, updates that don't
   leave anything running behind — even though, mechanically, it's still
   more than one OS process under the hood (§5.2).
4. **One frontend codebase**, not two, so screens aren't built twice for
   desktop vs. web.
5. **Auto-updates published through GitHub Releases**, with a
   user-facing on/off control and no forced mid-session restarts.
6. Cross-platform desktop (**Windows + macOS**) built from a primarily
   .NET/web-technologies skill set.

---

## 2. Architectural Style

```
┌──────────────────────────────────────────────────────────────┐
│ Presentation  (React — one codebase, served as static files    │
│                by the same ASP.NET Core app, both hosts)       │
├──────────────────────────────────────────────────────────────┤
│ API contract  (OpenAPI spec → generated TypeScript client)     │
├──────────────────────────────────────────────────────────────┤
│ Application   (use cases / CQRS handlers / DTOs — C#)          │
├──────────────────────────────────────────────────────────────┤
│ Domain        (entities, value objects, domain services,       │
│                aggregates — no framework dependencies — C#)    │
├──────────────────────────────────────────────────────────────┤
│ Infrastructure (persistence, tenant provider, auth,             │
│                 external integrations — C#)                    │
└──────────────────────────────────────────────────────────────┘
```

**Dependency rule unchanged:** Domain has zero references to
Infrastructure or Presentation; Application depends only on Domain plus
interfaces Infrastructure implements. This is still what makes "local
desktop today, hosted web later" possible without a rewrite.

**What's genuinely new vs. v1:** the presentation layer is no longer
served by a separate Electron process/host project. Because Electron.NET
puts ASP.NET Core in charge, **the same ASP.NET Core app that hosts your
API also serves the built React app as static files**, in both
deployment modes. Desktop and web stop being "two different frontend
hosts" and become closer to "one ASP.NET Core app, with a flag for
whether it should also open an Electron window."

---

## 3. Repo / Solution Structure

```
chassis/
├── server/
│   ├── Chassis.sln
│   ├── src/
│   │   ├── Chassis.Domain/          (no dependencies)
│   │   ├── Chassis.Application/     (depends on Domain)
│   │   ├── Chassis.Infrastructure/  (depends on Application, Domain)
│   │   └── Chassis.Api/             (ASP.NET Core Web API — serves
│   │                                     the built React app as static
│   │                                     files, emits OpenAPI, AND
│   │                                     optionally drives Electron via
│   │                                     ElectronNET.Core — see §5)
│   │       ├── DesktopComposition/      all Electron.NET calls
│   │       │                             (windows, menu, tray, IPC) —
│   │       │                             the isolated module from §9.4;
│   │       │                             Program.cs only calls into it.
│   │       │                             As built in Step 4 (§5.7):
│   │       │                             DesktopComposition.cs
│   │       │                             (IsDesktopRun + Enable + the
│   │       │                             ready-callback), DesktopWindow.cs
│   │       │                             (CreateWindowAsync, close→
│   │       │                             StopApplication), DesktopMenu.cs
│   │       │                             (native app menu), DesktopBridge.cs
│   │       │                             (IPC handlers, §7.3)
│   │       ├── Properties/              electron-builder.json (packaging
│   │       │                             config — see §5.7/§8, this
│   │       │                             replaces the top-level `desktop/`
│   │       │                             folder v4 assumed existed) +
│   │       │                             launchSettings.json with a
│   │       │                             "Desktop" profile (--desktop)
│   │       └── Tenancy/                 ClaimsTenantProvider — web-only,
│   │                                     lives here not in Infrastructure
│   │                                     because of IHttpContextAccessor
│   │                                     (§4.1, §4.2)
│   └── tests/
│       ├── Chassis.Domain.Tests/
│       ├── Chassis.Application.Tests/
│       └── Chassis.Infrastructure.Tests/
├── packages/
│   ├── ui/                              shared React component library
│   └── api-client/                      generated TypeScript client
│                                        from the OpenAPI spec
└── package.json                         workspace root (packages/ only —
                                          no separate `apps/desktop` or
                                          `apps/web` Node app anymore)
```

**Correction from v4 (§5.7):** there is no top-level `desktop/` folder
with an `electron.manifest.json` that a CLI reads. Packaging config
lives inside `Chassis.Api/Properties/electron-builder.json` plus MSBuild
properties in `Chassis.Api.csproj` (`ElectronIcon`, `ElectronVersion`,
`ElectronSingleInstance`, etc.) — see §5.7 and §8.

**The biggest structural change from v1:** there's no longer a separate
Electron `main.ts`/`preload.ts` app you write and maintain, and no
separate `apps/web` Vite host either. `Chassis.Api`'s `Program.cs` is
the single entry point for both deployment modes — it always serves the
same built React bundle and the same API; the only difference is whether
`UseElectron()` is called (§5).

---

## 4. Backend: Domain-Driven Design Core

*(Unchanged from v1 — this section is entirely presentation-agnostic.)*

### 4.1 Layering rules

- **Domain**: entities, value objects, aggregates, domain services,
  domain events. Plain C#, no package references beyond the base class
  library.
- **Application**: CQRS-lite use cases (MediatR), depending only on
  Domain-defined interfaces.
- **Infrastructure**: implements those interfaces — persistence, external
  integrations, tenant resolution, auth. The only layer whose *contents*
  differ between desktop and web deployments. **One exception** (§4.2):
  the web-only `ClaimsTenantProvider` lives in Api instead, because it
  needs `IHttpContextAccessor`, which Infrastructure can't reference
  without taking on a real ASP.NET Core shared-framework dependency.
- **Api**: exposes Application's use cases over HTTP, emits the OpenAPI
  spec, and — new in this revision — also serves the frontend's static
  files and optionally drives Electron (§5). Also where deployment-mode-
  specific composition lives — `DesktopComposition/` for the desktop
  side, `Tenancy/` for the web side (§4.2, §9.4) — kept out of Domain/
  Application/Infrastructure precisely because it's mode-specific.

### 4.2 Multi-tenancy from day one — even for single-tenant apps

**Decision: design multi-tenant from the start; run single-tenant in
practice for as long as that's true.**

- Every aggregate root carries a `TenantId` from the first commit,
  including on a desktop app that will only ever have one tenant.
- `ITenantProvider` is injected wherever tenant scoping matters:
  `LocalFixedTenantProvider` (desktop, constant value) vs.
  `ClaimsTenantProvider` (web, resolved from the authenticated user).
  **Assembly placement, decided:** `ITenantProvider` stays in Domain
  (unchanged). `LocalFixedTenantProvider` lives in **Infrastructure** —
  it's a plain constant, no framework coupling. `ClaimsTenantProvider`
  lives in **`Chassis.Api/Tenancy/`**, not Infrastructure, because it
  needs `IHttpContextAccessor`, and the old lightweight way to reference
  that (`Microsoft.AspNetCore.Http.Abstractions` as a standalone NuGet
  package) has been deprecated for years — the only ways to get it now
  are `<FrameworkReference Include="Microsoft.AspNetCore.App" />` in the
  class library or switching Infrastructure to the `Microsoft.NET.Sdk.Web`
  SDK, both of which couple Infrastructure to the ASP.NET Core shared
  framework itself, not just a NuGet package — precisely what Infrastructure
  is supposed to stay free of (§4.1), since it has to keep compiling and
  running for the desktop deployment, `Infrastructure.Tests`, and any
  future non-ASP.NET-Core host without that dependency. Api already
  carries the full ASP.NET Core framework reference and is already where
  deployment-mode-specific composition lives (`DesktopComposition/` is
  the same pattern on the desktop side, §9.4) — `Tenancy/ClaimsTenantProvider.cs`
  mirrors it for the web side. Register it in `Program.cs` via
  `services.AddScoped<ITenantProvider, ClaimsTenantProvider>()`.
- EF Core global query filters (if using EF Core) enforce `TenantId`
  scoping structurally, not just by convention.
- **Data isolation model for web:** shared database, shared schema,
  row-level `TenantId` filtering — not schema/database-per-tenant.
- **Local → cloud migration:** identical schema between desktop and web
  databases makes this a row export/import, not a data-model conversion.

---

## 5. Desktop Hosting: Electron.NET

### 5.1 The goal, restated honestly

A desktop app that's "a UI plus a separately-running backend" is a
support burden. This template's goal is for the user to experience the
app as **one coordinated program** — but it's worth being precise about
what Electron.NET actually delivers here, versus what it doesn't.

### 5.2 How Electron.NET works, and what it does NOT give you

Electron.NET is **not** single-process embedding. Under the hood, the
Electron/Node side and the .NET/ASP.NET Core side remain **separate OS
processes**, coordinated via an IPC bridge that the library manages for
you. `Program.cs` calls `UseElectron(args, ElectronAppReady)` on the
`WebApplicationBuilder`; inside the `ElectronAppReady` callback, your C#
code calls `Electron.WindowManager.CreateWindowAsync(...)` to open a
window pointed at your own ASP.NET Core app's local URL.

**What you get:** a mature, purpose-built library (7.6k GitHub stars,
actively maintained, latest release mid-2026) that handles the
launch/coordination/window-management plumbing for you, so you're not
hand-rolling a sidecar's spawn/kill lifecycle yourself. You write C# to
open windows, show menus, and access native OS integration — not
JavaScript.

**What you do NOT get, and should not assume:** a guarantee that closing
the app always cleanly terminates every process. This is not a
hypothetical concern — it has a **real, documented history** in this
project:

- Orphaned Node/Electron processes on Windows after stopping via
  Visual Studio/console, severe enough to lock the compiled DLLs and
  block a subsequent rebuild (GitHub Issue #218).
- Processes not fully closing on macOS when the window is closed,
  requiring a manual kill via Activity Monitor (Issue #346, which itself
  references two earlier related issues — one explicitly noted by a
  reporter as "never solved").
- Similar reports on Linux (Issue #226), plus adjacent lifecycle bugs:
  Node process surviving app shutdown (#96), exceptions during startup
  leaving Electron un-exited (#338), and socket/IPC disconnects (#455).
- A port-negotiation race between the internal Socket.IO bridge and
  ASP.NET Core's own port binding on startup (Issue #261).

All of these are against the **older architecture** (a Socket.IO-based
IPC bridge, versions 5.x–19.x, filed 2018–2020) — and **all are now
closed**, though "closed" here only confirms the old architecture's
reports were addressed at some point, not that the current rewrite fixes
anything (there was nothing to fix in the rewrite at the time they
closed).

**What's more informative:** a sweep of the current issue tracker (21
open issues as of this research, both pages reviewed) turned up **no
report matching the orphaned/zombie/hanging-process pattern** since
`ElectronNET.Core` shipped. The closest are a macOS single-instance bug
(#1040) and a slow-startup complaint (#1024) — lifecycle-adjacent, but not
the "process survives quit" failure mode this section is about. That's a
real but soft positive signal, not proof: if the rewrite had reintroduced
the old behavior, it's the kind of bug that gets reported fast, and it
hasn't shown up in the ~8 months since 0.4.0 (Dec 2025) through the
current 0.5.1 (Jun 2026).

The maintainers' own explanation for why this might genuinely be better:
`ElectronNET.Core` supports **8 startup scenarios**, split along two
independent axes — packaged vs. unpackaged, and (separately)
**electron-first vs. dotnet-first** initialization order. In dotnet-first
mode, **.NET launches Electron as its child process** rather than the
reverse, which the maintainers describe as giving .NET "better process
lifecycle management" and "more reliable application termination" — a
plausible structural reason quit-then-relaunch could be less prone to the
old orphaning bugs, since the parent process (.NET) is the one left
holding a handle it can wait on and reap.

**A packaging nuance that matters for §5.6 and §8:** the *default*
**packaged** mode is still **electron-first** — `electron.exe` remains
the real OS-level entry point, launching the .NET app as a child from the
packaged files, structurally identical to how classic (pre-rewrite)
Electron.NET always packaged. Dotnet-first packaging is a separate,
**opt-in** mode (the `-dotnetpacked` flag), not the default. §5.6's code
(`UseElectron(args, ElectronAppReady)`, no `-dotnetpacked`) lands in the
default electron-first packaged mode. This template does not currently
have a reason to opt into `-dotnetpacked`, and doing so would reopen
questions this section otherwise answers — see the auto-update caveat in
§8.

As of this writing, `ElectronNET.Core` is pre-1.0. Two version numbers
have applied at different points in this document's life: `0.5.1` at
the time of the research below, and `0.5.2` — confirmed stable and
explicitly targeting `net10.0` — at Step 4's implementation (§5.7),
resolving what had been an open question about .NET 10 support. Note
that this project's version numbering has been inconsistent across its
published packages, so whatever version is pinned in a `.csproj` should
be independently re-verified as a stable, non-prerelease build before
production use rather than trusted from this document. **Treat
"does quit-then-relaunch behave cleanly on Windows and macOS" as a
claim with decent circumstantial support, not a settled fact** — §5.7
now has one concrete data point (a hard external kill orphaned Electron
children; a normal window-close quit did not), but that's not yet the
full §5.3 test suite (still deferred per §9.1).

### 5.3 Required validation before building on this (go/no-go gate)

Before any real feature work, prototype and explicitly test, on both
target OSes:

1. Normal quit (window close / app menu quit) → immediately relaunch.
   Confirm no port conflicts, no locked files, no leftover processes in
   Task Manager / Activity Monitor.
2. Force-kill the app (Task Manager "End Task" / `kill -9` equivalent) →
   relaunch. This is the scenario most likely to surface an orphaned
   process, since it skips whatever graceful-shutdown hook the library
   relies on.
3. Quit while a request is in flight (e.g., mid-database-write) →
   relaunch, confirm no corrupted state and no hung process.
4. Re-check the issue tracker for lifecycle/orphaned-process reports
   **filed against the Core rewrite** before you build — §5.2's sweep
   found none as of this writing, but that can change, and the tracker
   takes a minute to re-scan.

If any of these fail in practice, the fallback (§5.4) should be treated
as a real, budgeted option — not an emergency improvisation.

**One data point so far, not the suite (§5.7):** during Step 4,
`Stop-Process`-ing the .NET host directly (not a clean window-close
quit) left 4 Electron children orphaned, requiring a force-kill — item 2
above, reproduced incidentally rather than as a deliberate test run. A
normal window-close quit (→ `OnClosed` → `StopApplication`) did **not**
trigger it. This is exactly the §5.2 failure mode, on the current
release, not just the old architecture — it makes paying off the rest
of this section before shipping more concrete, not less. Items 1, 3,
and 4 are still outstanding.

### 5.4 Fallback plan: plain Electron + a hand-managed sidecar

If Electron.NET's lifecycle coordination proves too fragile, fall back to
a traditional Electron shell that spawns `Chassis.Api` as an explicit
child process and manages its lifecycle directly:

- Electron's main process (`main.ts`, back to writing this yourself)
  spawns the API as a child process on launch, and kills it explicitly in
  `app.on('before-quit')` — no automatic library coordination, but full
  visibility and control over exactly what happens and when.
- Because the API is always exposed over plain `http://localhost` HTTP
  regardless of hosting method, the React UI, the API contract (§6), and
  the Application/Domain/Infrastructure layers **don't change at all** if
  this fallback is taken — only how the process is launched and torn
  down changes. This is why the fallback is cheap rather than a rewrite.
- This trades Electron.NET's "someone else maintains the coordination
  code" convenience for "you own the coordination code, and can debug it
  yourself line by line" — a reasonable trade if the library's lifecycle
  handling doesn't hold up under §5.3's testing.

### 5.5 Static-file serving (how the React UI actually gets to the user)

`Chassis.Api` serves the built React bundle directly:

```csharp
app.UseStaticFiles();                 // serves wwwroot/*
app.MapFallbackToFile("index.html");  // SPA client-side routing
```

The React build output (`packages/ui`'s bundled `dist/`) is copied into
`Chassis.Api/wwwroot/` as a build step. Electron.NET's window simply
points at this same app's local URL (`http://localhost:{port}/`) — there
is no separate static file server, no separate Node process serving
anything. In the web deployment, the identical `wwwroot/` output is
served by the identical `UseStaticFiles()`/`MapFallbackToFile()` call,
just running on a real deployed server instead of Electron.NET's local
instance, with `UseElectron()` simply not invoked (§5.6).

### 5.6 Toggling Electron on/off from one composition root

```csharp
var builder = WebApplication.CreateBuilder(args);
// ... shared service registration (Application, Infrastructure, etc.) ...

var runningInElectron = args.Contains("--electron") || /* build-time flag */;

if (runningInElectron)
{
    builder.Services.AddElectron();
    builder.UseElectron(args, () => DesktopComposition.ConfigureAsync());
    // DesktopComposition (§3, §9.4) owns every Electron.WindowManager /
    // menu / tray / IPC call — Program.cs never calls Electron.* directly.
}

var app = builder.Build();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");
// ... API endpoint mappings ...
app.Run();
```

This is the concrete mechanism behind §2's "one ASP.NET Core app, with a
flag for whether it should also open an Electron window" — desktop and
web are now the *same deployable*, differing by one startup branch and
which artifact you package, rather than two separate host projects.

**Correction from v4 — the actual toggle, as built in Step 4 (§5.7):**
the real branch guards against more than just "is this a desktop run,"
because referencing `ElectronNET.Core` makes every `dotnet build` also
boot Electron-adjacent tooling unless told not to:

```csharp
if (!isDocumentGeneration && DesktopComposition.IsDesktopRun(args))
    DesktopComposition.Enable(builder, args);
```

`IsDesktopRun(args)` is `args.Contains("--desktop") ||
HybridSupport.IsElectronActive` — everything Electron-specific still
lives behind `DesktopComposition`, matching §9.4's intent, but
`Program.cs` also needs an `isDocumentGeneration` guard so that
build-time OpenAPI introspection (and EF migrations tooling) doesn't
inadvertently boot the Electron runtime. This wasn't anticipated in v4's
`args.Contains("--electron")` sketch.

### 5.7 Divergences confirmed during implementation (Step 4)

Step 4 wired the desktop toggle end to end against `ElectronNET.Core`
0.5.2 (stable, targets `net10.0`) and exercised it with `dotnet run --
--desktop`. The architecture in §2–§5.6 held — Electron launched, loaded
the SPA, and the SPA round-tripped through the tenant-scoped API exactly
as designed. But several tooling details this document asserted turned
out not to match the current release:

| §8/§11 said | Reality in 0.5.2 |
|---|---|
| `electronize init` / `electronize build` / `electronize start` CLI | No such CLI exists. Dev is plain `dotnet run`; packaging is `dotnet publish`, which triggers MSBuild targets that shell out to `npx electron-builder`. |
| `-dotnetpacked` opt-in flag (§5.2) | Gone. The runtime auto-probes and picks a startup mode itself — `UnpackedDotnetFirst`, electron-first, or packaged — rather than the two-flag scheme v4 described. |
| `desktop/electron.manifest.json` (§3) | `Chassis.Api/Properties/electron-builder.json` plus MSBuild properties directly in the `.csproj` (`ElectronIcon`, `ElectronVersion=30.4.0`, `ElectronSingleInstance`, etc.). |
| "`electron-updater` should not be needed" (§8) | True only from the C# side — `electron-updater@6.6.2` is bundled into the generated Electron host regardless. |
| Socket.IO bridge was the old, superseded architecture (§5.2) | Still Socket.IO (4.8.1) under `ElectronNET.Core` — rewritten, not replaced. |

Two build-time side effects worth knowing before adopting this: (1)
merely referencing `ElectronNET.Core` makes every `dotnet build` of
`Chassis.Api` run `npm install` under `bin/.../.electron/` (incremental
after the first run, ~16s cold); (2) the package injects assembly
metadata at build time, which is part of why the
`isDocumentGeneration` guard above exists.

**What this means for the rest of the document:** §3's repo layout and
§8's packaging/auto-update section are corrected below to match. Nothing
here changes §2's architectural decision or §4's DDD layering — this is
entirely a tooling-surface correction, confined to how the desktop
artifact is built and packaged.

### 5.8 Stale-instance cleanup on startup

**Decision: on startup, detect and kill leftover processes from a
previous run before launching a new instance.** This is a mitigation for
§5.3/§5.7's confirmed failure mode (a hard kill of the .NET host orphans
Electron children), not a substitute for it — §5.3's full suite is still
the thing to run before shipping. But the failure mode isn't limited to
someone deliberately running `Stop-Process`: a crash, a forced "End
Task," an OS update killing background processes, antivirus
interference, or a container/VM reset all leave the same kind of
orphaned tree behind, silently, without the user doing anything unusual.
Self-healing on the next launch is worth building independently of
whether §5.3 ultimately passes.

**This is a different problem from Electron's single-instance lock.**
`app.requestSingleInstanceLock()` (surfaced via the `ElectronSingleInstance`
MSBuild property noted in §5.7) stops two *live* instances from running
at once. It does not help here: if an orphaned Electron process is still
alive, the lock is still held, and a relaunch just collides with a
corpse that's still holding the door rather than replacing it. The two
mechanisms are complementary, not overlapping.

**Design:**

- **Track PIDs across restarts.** After the new Electron window comes
  up, write a small marker file to a per-user app-data location (e.g.
  `%LOCALAPPDATA%/Chassis/instance.lock` on Windows,
  `~/Library/Application Support/Chassis/instance.lock` on macOS)
  recording the .NET host PID, the Electron PID(s), and each process's
  start time. Delete it on a clean quit (the existing `DesktopWindow.cs`
  close → `StopApplication` path).
- **On the next startup, before anything else launches**, check for that
  file. PIDs get recycled by the OS, so don't trust a bare PID match —
  compare the recorded start time (or `Process.MainModule.FileName`)
  against the live process before treating it as stale-and-yours.
- **Kill the whole tree, not just the tracked PID.** `Process.Kill(entireProcessTree: true)`
  (.NET Core 3.0+) handles Electron's own child processes (GPU process,
  renderer, etc.) in one call — this is why the force-kill in §5.7 left
  4 processes behind rather than 1.
- **Never match on process name alone.** `electron.exe`/`Electron` is
  shared by a large number of unrelated installed apps. Path-based
  matching (the process is running from inside *this app's* own
  install/publish directory) plus the marker file's start-time check are
  what make this safe; name-only matching risks killing someone else's
  app.

```csharp
// Runs before Electron is launched. Plain System.Diagnostics — no
// `using ElectronNET` needed — so per §9.4 this stays inside
// DesktopComposition rather than leaking into Program.cs.
static void CleanupStaleInstance()
{
    var lockPath = GetInstanceLockPath();
    if (!File.Exists(lockPath)) return;

    var stale = JsonSerializer.Deserialize<InstanceMarker>(File.ReadAllText(lockPath));
    foreach (var pid in stale.Pids)
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            if (proc.StartTime == stale.StartTimes[pid] &&
                proc.MainModule?.FileName.StartsWith(AppContext.BaseDirectory) == true)
            {
                proc.Kill(entireProcessTree: true);
            }
        }
        catch (ArgumentException) { /* already gone, fine */ }
    }
    File.Delete(lockPath);
}
```

Call this once, first thing, inside `DesktopComposition.Enable(...)` (or
immediately before it) — before the single-instance lock check and
before any window is created — then write the fresh marker file once
the new window is up.

**Known limitations, not blockers:**

- There's a narrow race if two launches happen back-to-back, but for an
  app a human double-clicks, this isn't a realistic concern the way it
  might be for a long-running service.
- This makes the force-kill scenario self-healing on the *next* launch —
  it does not by itself tell you whether the orphaning has other side
  effects (locked files, corrupted state) in the interim. §5.3's full
  suite is still what answers that.

---

## 6. API Contract: OpenAPI → Generated TypeScript

- `Chassis.Api` emits an OpenAPI spec from its endpoints via
  `Microsoft.AspNetCore.OpenApi` (or Swashbuckle).
- `packages/api-client` is **generated, not hand-written**, via
  **`openapi-typescript` + `openapi-fetch`** — decided in §9.5 as the
  template default, chosen for zero runtime/no framework lock-in over
  `orval`'s generated-hooks approach. Regenerate on every API change.
  A given downstream project can swap in `orval` (or another generator)
  if it's committed to React + TanStack Query from day one and wants
  hooks generated in the same pass — see §9.5 for that tradeoff.
- `packages/ui` imports only from `packages/api-client` — never
  hand-rolled `fetch` calls or manually duplicated type shapes.

---

## 7. Frontend: One React Codebase, Served by One Backend

### 7.1 Why this works

The UI (`packages/ui`) is a static SPA. Its build output doesn't know or
care who serves it — the same `dist/` folder becomes `wwwroot/` content
for **both** deployment modes now, since both are the same ASP.NET Core
app (§5.5, §5.6). This is a simpler story than v1's "two separate host
projects, config-driven differences" — there's really only one host now.

### 7.2 The remaining seams

| Seam | Desktop | Web |
|---|---|---|
| API base URL (as seen by the React app) | Relative — same origin, since the API and the UI are the same app | Relative — same origin, if API and web frontend are deployed together; a separate origin only if you choose to split them for scaling |
| Auth | None (or a local PIN) | Real login flow (OAuth/Identity) |
| Desktop-only actions (native menu items, update check) | Available via Electron.NET's IPC bridge (§7.3) | Not available — components check for bridge presence and no-op/hide if absent |
| `UseElectron()` call | Invoked | Not invoked |

### 7.3 Desktop-only capabilities: Electron.NET's IPC bridge

Electron.NET provides its own `IpcMain`/renderer-side bridging so the
.NET side can communicate with the React app running in the Electron
window, without hand-writing a `contextBridge`/`preload.ts` file the way
a plain-Electron setup would need. **Verify the exact current API against
Electron.NET's docs when implementing this** — the specifics may have
shifted with the Core rewrite — but the shape is: your C# code
(`Electron.IpcMain.On(...)` or equivalent) sends/receives messages that
the React app listens for via Electron.NET's injected renderer script.

If that built-in bridge doesn't cleanly cover a specific need, a thin
custom preload script remains an option — same narrow-surface principle
as a plain-Electron setup (expose only what's needed, nothing broader).

**Confirmed in Step 4 (§5.7):** the bridge is still Socket.IO under the
hood (4.8.1) — rewritten, not replaced, contrary to this document's
earlier framing of Socket.IO as strictly "the old architecture" (§5.2).
`Chassis.Api/DesktopComposition/DesktopBridge.cs` demonstrates the
pattern with a single handler, `chassis:app-info`, and is where any
further IPC handlers should live — keeping the narrow-surface principle
above.

---

## 8. Packaging & Auto-Update

- **Packaging — corrected from v4 by Step 4 (§5.7):** there is no
  `electronize` CLI in `ElectronNET.Core` 0.5.2, and no
  `desktop/electron.manifest.json`. In practice: **dev** is plain
  `dotnet run` (optionally `-- --desktop`, or the "Desktop" profile in
  `launchSettings.json`); **packaging** is `dotnet publish`, which runs
  MSBuild targets that shell out to `npx electron-builder`. Config lives
  in `Chassis.Api/Properties/electron-builder.json` plus MSBuild
  properties in `Chassis.Api.csproj` (`ElectronIcon`, `ElectronVersion`,
  `ElectronSingleInstance`, etc.), not a manifest file a separate CLI
  reads. The `-dotnetpacked` opt-in flag this document previously
  described is also gone — the runtime auto-probes and picks a startup
  mode (`UnpackedDotnetFirst`, electron-first, or packaged) rather than
  branching on that flag. §8's electron-first-vs-`-dotnetpacked`
  distinction (below) should be read as **historical context for why
  the updater assumption holds**, not as a flag you still pass.
- **Auto-update: confirmed as a built-in feature, conditionally —
  packaging-tooling correction still pending verification.**
  Electron.NET ships a native `Electron.AutoUpdater` API —
  `AutoDownload` / `AutoInstallOnAppQuit` properties, GitHub-provider
  settings (`AllowPrerelease`, `FullChangelog`), and the full
  `CheckForUpdatesAsync()` / `CheckForUpdatesAndNotifyAsync()` /
  `DownloadUpdateAsync()` / `QuitAndInstall()` method set — which maps
  onto the behavior contract below almost exactly out of the box.
  **Correction from v4:** "`electron-updater` should not be needed as a
  separate dependency" is only true from the C# side — Step 4 found
  `electron-updater@6.6.2` bundled into the generated Electron host
  regardless, as part of the `electron-builder`-produced tooling. It
  isn't something you add, but it is present, and worth knowing about if
  you're auditing dependencies or debugging update behavior from the
  Node side.

  **The condition:** this API, like Electron's underlying updater
  tooling (Squirrel on Windows, the macOS equivalent), assumes
  `electron.exe` is the real OS-level entry point of the installed app —
  which is true for the **default electron-first packaged mode** this
  template uses (§5.2, §5.6). Step 4 confirmed the runtime now
  auto-probes its startup mode rather than accepting an explicit
  `-dotnetpacked` flag; **which of the auto-probed modes a real
  `electron-builder` package lands in, and whether `Electron.AutoUpdater`
  still holds under it, has not yet been verified end to end** — that
  verification is what §11 step 5 and the spike below are for. Until
  then, treat the updater's applicability under the actual packaging
  pipeline as unconfirmed, not as settled by this section.
- **Behavior contract:**
  - Check for updates on launch and periodically.
  - If available and the "automatic updates" preference is on: download
    silently in the background.
  - Apply on the **next app restart** — never mid-session.
  - If the preference is off: still check and surface "update available,"
    but never download/install without an explicit user action.
  - Display the app version from the packaged artifact's own reported
    version (`Electron.App.GetVersionAsync()` or equivalent — confirm the
    exact current method name against the installed package version)
    rather than a separately hand-tracked string.
- **Before relying on this in production:** Windows Authenticode signing
  and macOS Developer ID signing + notarization are both required for a
  trustworthy update-integrity story, regardless of which update
  mechanism is used.
- **Still worth a spike (§11 step 5):** confirm `Electron.AutoUpdater`'s
  actual method surface against the installed package version — wiki
  docs can drift from code — and run one real update cycle end to end
  (old build → new GitHub Release → app picks it up → applies on
  restart) before trusting the contract above in production. Since
  Step 4 (§5.7), this spike also needs to run against the real
  `dotnet publish` → `electron-builder` pipeline rather than the
  `electronize build` this document originally assumed, since that's
  what actually produces the installer now.

---

## 9. Decisions (formerly "Open Questions") — locked in for this template

Each item below was an open question as of Draft v3. All six are now
decided; this section records the decision and the reasoning, not just
the risk.

1. **Process lifecycle — risk accepted and now partially evidenced, still
   not fully tested.** §5.2's older-architecture issues are closed and
   predate the rewrite; the current tracker shows no fresh reports of the
   same pattern. **Update from Step 4 (§5.7):** an incidental
   `Stop-Process` kill of the .NET host did orphan 4 Electron children —
   the exact §5.2 failure mode, on the current release. A normal
   window-close quit did not reproduce it. That's one real data point,
   not the full suite, and it cuts against treating the deferral as
   low-risk by default. **Still deferring §5.3's full go/no-go suite
   rather than blocking on it today**, but the case for running it before
   real desktop features accumulate is now stronger than "no GitHub
   issues found" alone. §5.3 stays in this document as the test to run
   **before shipping the desktop build to real users**. If it fails,
   §5.4's fallback is still the plan. **§5.8 (new) adds a self-healing
   mitigation** — detect and kill leftover processes from a previous run
   on startup — which reduces the user-facing consequence of orphaning
   without resolving whether the orphaning itself is acceptable; §5.3
   still answers that question.
2. **Auto-update — confirmed at the API level; packaging-pipeline
   verification still outstanding.** `Electron.AutoUpdater` (§8) is a
   real native API and covers the behavior contract. The standing
   condition — staying on the default electron-first packaged mode so
   the updater's assumptions hold — no longer maps cleanly onto an
   explicit `-dotnetpacked` flag, since Step 4 (§5.7) found the runtime
   auto-probes its startup mode instead. Whether a real
   `dotnet publish` → `electron-builder` package lands in a mode where
   the updater's assumptions hold has not yet been verified end to end —
   that's §8's outstanding spike, not a settled fact.
3. **Maintainer bandwidth — not a concern for this template.** Four
   maintainers, active release cadence, modest triaged backlog (§5.2).
   Not being tracked as a risk going forward.
4. **Coupling risk — keep an isolated desktop-composition module.**
   Decision confirmed: all Electron.NET-specific calls
   (`Electron.WindowManager`, menu, tray, IPC handlers) live in one
   clearly-bounded module, never spread through `Program.cs` and never
   reaching into Application/Domain. Concretely, for this template:
   `Chassis.Api/DesktopComposition/` holds window creation, menu
   setup, and IPC handler registration; `Program.cs`'s `ElectronAppReady`
   callback (§5.6) does nothing but call into that module — e.g.
   `DesktopComposition.Configure(app)` — so the composition root stays a
   one-line branch, not a growing block of Electron calls. Same
   discipline v1 asked for around `contextBridge`, relocated to this
   coupling point.
5. **API contract codegen: `openapi-typescript` + `openapi-fetch`,
   default for the template.** Recommendation, and why: this is a
   template meant to seed apps with different needs, not one app with a
   fixed stack, so the default should carry the least opinion. `openapi-typescript`
   generates types only — no runtime, no generated hooks, no
   framework lock-in — paired with `openapi-fetch` as a thin typed
   wrapper. That fits §6/§7's rule ("`packages/ui` imports only from
   `packages/api-client`, never hand-rolled `fetch` calls") cleanly:
   `api-client` exports typed fetch functions, and any given downstream
   project can layer TanStack Query (or nothing) on top without the
   codegen pipeline caring. **Reconsider per project**, not as a template
   default: if a specific project is committed to React + TanStack Query
   from day one and wants generated hooks, Zod schemas, and MSW mocks in
   one pass, `orval` is the better fit there (v8, stable, and still the
   most common choice for that combination as of mid-2026) — swap it in
   for that project rather than changing the template default. NSwag and
   newer entrants (`@hey-api/openapi-ts`, Kubb) were also reviewed;
   neither offers a compelling enough reason over the two above to be
   this template's default.
6. **Credential/secret storage on desktop — OS keychain, confirmed.** Any
   API keys or tokens stored locally use the OS keychain (Windows
   Credential Manager / macOS Keychain), never plain-text local storage.
   Unchanged from v1; no further action needed beyond implementing it
   per project.

---

## 10. Technology Stack Summary

| Layer | Technology |
|---|---|
| Language/runtime (backend) | C# / .NET 10 (LTS, released Nov 2025) |
| Domain | Plain C# class library, no framework dependencies |
| Application | MediatR (CQRS-lite), FluentValidation |
| API | ASP.NET Core Web API, emits OpenAPI spec, serves static frontend files |
| Persistence | EF Core (or your ORM of choice); SQLite (desktop) / PostgreSQL (web) |
| Frontend framework | React + TypeScript, one codebase, served by the same backend in both modes |
| API contract | Generated TypeScript client from OpenAPI (`packages/api-client`), via `openapi-typescript` + `openapi-fetch` — template default, see §9.5 |
| Desktop shell | Electron, driven by **Electron.NET** (`ElectronNET.Core` + `ElectronNET.Core.AspNet`) |
| Desktop packaging | `dotnet publish` → MSBuild-triggered `npx electron-builder`; config in `Properties/electron-builder.json` + `.csproj` MSBuild props — corrected from v4's `electronize` CLI assumption, see §5.7 |
| Desktop auto-update | Native `Electron.AutoUpdater` API — confirmed at the API level (§8); packaging-pipeline verification outstanding (§9.2); `electron-updater@6.6.2` is bundled into the generated host regardless (§5.7) |
| Web shell | Same ASP.NET Core app, `UseElectron()` simply not called |
| Auth (web) | ASP.NET Core Identity + OIDC provider |
| Backend testing | xUnit, FluentAssertions |
| Frontend testing | Vitest, React Testing Library |
| Monorepo tooling | npm/pnpm workspaces (now scoped to `packages/` only) |

---

## 11. Bootstrapping a New Project From This Design

1. Stand up `server/` (Domain → Application → Infrastructure → Api) with
   a placeholder aggregate demonstrating the `TenantId` pattern.
2. Stand up `packages/ui` with one placeholder screen and
   `packages/api-client` generating against the placeholder API via
   `openapi-typescript` + `openapi-fetch` (§6, §9.5).
3. Wire `Chassis.Api` to serve `packages/ui`'s build output as static
   files (§5.5) — confirm this works as a plain web app **before**
   adding Electron.NET at all.
4. Add `ElectronNET.Core`/`ElectronNET.Core.AspNet`, wire up
   `UseElectron()` through the `DesktopComposition` module (§3, §5.6,
   §9.4) rather than inline in `Program.cs` — as built in Step 4, this
   is `DesktopComposition.IsDesktopRun(args)` / `.Enable(builder, args)`,
   guarded so build-time OpenAPI/EF-migration tooling doesn't boot
   Electron (§5.6, §5.7). Per §9.1, **§5.3's lifecycle test suite is
   deferred, not required at this step** — build desktop features on the
   accepted-risk assumption that quit/relaunch is clean, but note §5.7's
   force-kill result before deciding how long to defer, and run §5.3 in
   full before this template's desktop build goes in front of real
   users.
5. Wire up packaging (`dotnet publish`, which triggers the
   MSBuild-driven `npx electron-builder` step, config in
   `Properties/electron-builder.json` + `.csproj` MSBuild props — see
   §5.7, not the `electronize build` CLI v4 described) and confirm the
   update behavior contract (§8) end to end using the native
   `Electron.AutoUpdater` API against a real GitHub Release, rather than
   assuming the wiki's method surface matches the installed package
   version or that the updater's electron-first assumption holds under
   the real packaging pipeline (§9.2).
6. Only then start building actual product features.
7. **Before shipping the desktop build to real users:** run §5.3's full
   lifecycle test suite (normal quit, force-kill, quit-mid-write — both
   OSes) — the one deferral from §9.1 that still has to be paid off.
