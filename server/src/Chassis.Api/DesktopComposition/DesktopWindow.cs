using ElectronNET.API;
using ElectronNET.API.Entities;

namespace Chassis.Api;

/// <summary>
/// Creates and owns the single main window. It points at this same ASP.NET Core
/// app's local URL (the default when no URL is passed), so the desktop app shows
/// exactly the SPA the web deployment serves — no separate frontend host (§5.5).
/// </summary>
internal static class DesktopWindow
{
    public static async Task OpenAsync(IHostApplicationLifetime lifetime, ILogger logger)
    {
        Electron.WindowManager.IsQuitOnWindowAllClosed = true;

        var window = await Electron.WindowManager.CreateWindowAsync(new BrowserWindowOptions
        {
            // Show only once the page has painted, to avoid a white flash.
            Show = false,
            Width = 1280,
            Height = 860,
            MinWidth = 940,
            MinHeight = 600,
            Center = true,
            Title = "Chassis",
            BackgroundColor = "#ffffff",
        });

        window.OnReadyToShow += () => window.Show();

        // "One thing to quit" (§5.1): when the window closes, wind down the web
        // host too. In electron-first packaging Electron exits on its own once
        // all windows are gone (IsQuitOnWindowAllClosed); this makes the
        // dotnet-first dev run behave the same way.
        window.OnClosed += lifetime.StopApplication;

        logger.LogInformation("Main window created.");
    }
}
