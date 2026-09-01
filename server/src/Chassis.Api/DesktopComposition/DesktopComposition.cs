using ElectronNET.API;

namespace Chassis.Api;

/// <summary>
/// The one place in the codebase allowed to reference ElectronNET (template
/// §9.4). <c>Program.cs</c> calls only <see cref="IsDesktopRun"/> and
/// <see cref="Enable"/>; nothing in Domain / Application / Infrastructure — and
/// no other file in the API — has a <c>using ElectronNET</c>. Window creation,
/// the native menu, and IPC handlers live in the sibling files.
/// </summary>
internal static class DesktopComposition
{
    /// <summary>Arg that opts a run into desktop mode (see the "Desktop" launch profile).</summary>
    public const string DesktopArg = "--desktop";

    /// <summary>
    /// True when this process should run as the Electron desktop app rather than
    /// a plain web server: either launched with <c>--desktop</c> (dotnet-first
    /// dev run) or started by Electron itself (packaged, electron-first — §5.2).
    /// </summary>
    public static bool IsDesktopRun(string[] args) =>
        args.Contains(DesktopArg) || HybridSupport.IsElectronActive;

    /// <summary>
    /// Turns this ASP.NET Core app into the desktop shell: registers Electron
    /// services and hands ElectronNET a ready-callback that opens the window
    /// once Kestrel is listening (§5.6). Call from <c>Program.cs</c> only.
    /// </summary>
    public static void Enable(WebApplicationBuilder builder, string[] args)
    {
        // First thing, before Electron launches or its single-instance lock is
        // taken: clear any process tree orphaned by a previous run (§5.8).
        StaleInstanceCleanup.KillLeftoversFromPreviousRun();

        builder.Services.AddElectron();
        builder.UseElectron(args, ConfigureAsync);
    }

    /// <summary>
    /// ElectronNET ready-callback. Runs after the server is up; the
    /// <see cref="IServiceProvider"/> is the built app's container.
    /// </summary>
    private static async Task ConfigureAsync(IServiceProvider services)
    {
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("Chassis.Desktop");
        var lifetime = services.GetRequiredService<IHostApplicationLifetime>();

        var updater = new DesktopUpdater(loggerFactory.CreateLogger("Chassis.Desktop.Update"), DesktopSettings.Load());

        DesktopMenu.Install();
        DesktopBridge.RegisterHandlers(services, updater);
        await DesktopWindow.OpenAsync(lifetime, logger);

        // Record the live process tree so the next launch can clean it up if this
        // one dies without a clean shutdown; drop the marker when we exit cleanly
        // (window close → StopApplication → ApplicationStopping; ProcessExit
        // covers Ctrl+C and a normal return from app.Run()). ClearMarker is
        // idempotent, so registering both hooks is safe.
        StaleInstanceCleanup.WriteMarker(logger);
        lifetime.ApplicationStopping.Register(() =>
        {
            StaleInstanceCleanup.ClearMarker(logger);
            updater.Dispose();
        });
        AppDomain.CurrentDomain.ProcessExit += (_, _) => StaleInstanceCleanup.ClearMarker(logger);

        // Start checking for updates on launch + on an interval (§8).
        updater.Start();

        logger.LogInformation("Chassis desktop shell ready.");
    }
}
