using ElectronNET.API;

namespace Chassis.Api;

/// <summary>
/// C#-side IPC handlers exposed to the renderer via ElectronNET's built-in
/// bridge (§7.3) — no hand-written <c>preload.ts</c>/<c>contextBridge</c>.
/// Confirmed in Step 4: the bridge is Socket.IO under the hood (rewritten, not
/// replaced). UI components must treat every channel here as optional and
/// no-op / hide on the web, where the bridge is absent (§7.2).
///
/// Renderer side:
/// <c>await window.electron.ipcRenderer.invoke("chassis:app-info")</c>,
/// <c>window.electron.ipcRenderer.on("chassis:update-changed", cb)</c>.
/// </summary>
internal static class DesktopBridge
{
    public static void RegisterHandlers(IServiceProvider services, DesktopUpdater updater)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Chassis.Desktop.Ipc");

        Electron.IpcMain.Handle("chassis:app-info", async _ =>
        {
            var version = await updater.GetCurrentVersionAsync();
            return new
            {
                version,
                os = Environment.OSVersion.Platform.ToString(),
                isDesktop = true,
            };
        });

        // --- Auto-update (§8) --------------------------------------------------
        Electron.IpcMain.Handle("chassis:update-status", async _ =>
        {
            var version = await updater.GetCurrentVersionAsync();
            return Describe(updater, version);
        });

        Electron.IpcMain.Handle("chassis:update-check", async _ =>
        {
            await updater.CheckNowAsync();
            return updater.State.Phase.ToString();
        });

        Electron.IpcMain.Handle("chassis:update-set-auto", _ =>
        {
            // The renderer sends a bare boolean; ElectronNET hands it back boxed.
            var enabled = _ is bool b ? b : Convert.ToBoolean(_);
            updater.SetAutomaticUpdates(enabled);
            return Task.FromResult<object>(enabled);
        });

        Electron.IpcMain.Handle("chassis:update-install", _ =>
        {
            updater.InstallAndRestart();
            return Task.FromResult<object>(true);
        });

        // Push state changes to every open window so the UI can react live.
        updater.StateChanged += state =>
        {
            foreach (var window in Electron.WindowManager.BrowserWindows)
            {
                Electron.IpcMain.Send(window, "chassis:update-changed", new
                {
                    phase = state.Phase.ToString(),
                    availableVersion = state.AvailableVersion,
                    downloadPercent = state.DownloadPercent,
                    error = state.Error,
                });
            }

            logger.LogDebug("IPC chassis:update-changed → {Phase}", state.Phase);
        };
    }

    private static object Describe(DesktopUpdater updater, string currentVersion) => new
    {
        currentVersion,
        automaticUpdates = updater.AutomaticUpdates,
        phase = updater.State.Phase.ToString(),
        availableVersion = updater.State.AvailableVersion,
        downloadPercent = updater.State.DownloadPercent,
        error = updater.State.Error,
    };
}
