using ElectronNET.API;

namespace Chassis.Api;

internal enum UpdatePhase
{
    Idle,
    DisabledDevBuild,
    Checking,
    UpToDate,
    AvailableNotDownloaded,
    Downloading,
    ReadyToInstall,
    Error,
}

internal sealed record UpdateState(
    UpdatePhase Phase,
    string? AvailableVersion = null,
    double? DownloadPercent = null,
    string? Error = null);

/// <summary>
/// Wraps <c>Electron.AutoUpdater</c> to the §8 behaviour contract:
/// check on launch and every few hours; if an update exists and the user's
/// "automatic updates" preference is on, download it silently; apply on the
/// <em>next</em> restart, never mid-session; if the preference is off, still
/// surface "update available" but never download without an explicit action.
/// Only <c>DesktopComposition</c> constructs this — it's the §9.4 isolation point.
/// </summary>
internal sealed class DesktopUpdater : IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private readonly ILogger _logger;
    private readonly DesktopSettings _settings;
    private Timer? _timer;

    public DesktopUpdater(ILogger logger, DesktopSettings settings)
    {
        _logger = logger;
        _settings = settings;
    }

    public UpdateState State { get; private set; } = new(UpdatePhase.Idle);

    public bool AutomaticUpdates => _settings.AutomaticUpdates;

    public event Action<UpdateState>? StateChanged;

    public void Start()
    {
        if (!LooksPackaged())
        {
            _logger.LogInformation(
                "Auto-update inactive: this is an unpacked/dev build. electron-updater only runs against an installed app.");
            SetState(new UpdateState(UpdatePhase.DisabledDevBuild));
            return;
        }

        Electron.AutoUpdater.AutoDownload = _settings.AutomaticUpdates;
        Electron.AutoUpdater.AutoInstallOnAppQuit = true; // §8: apply on next restart
        Electron.AutoUpdater.FullChangelog = true;

        Electron.AutoUpdater.OnCheckingForUpdate += () => SetState(State with { Phase = UpdatePhase.Checking });
        Electron.AutoUpdater.OnUpdateNotAvailable += _ =>
            SetState(new UpdateState(UpdatePhase.UpToDate));
        Electron.AutoUpdater.OnUpdateAvailable += info =>
        {
            _logger.LogInformation("Update available: {Version}", info.Version);
            SetState(State with
            {
                Phase = _settings.AutomaticUpdates ? UpdatePhase.Downloading : UpdatePhase.AvailableNotDownloaded,
                AvailableVersion = info.Version,
            });
        };
        Electron.AutoUpdater.OnDownloadProgress += progress =>
            SetState(State with { Phase = UpdatePhase.Downloading, DownloadPercent = progress.Percent });
        Electron.AutoUpdater.OnUpdateDownloaded += info =>
        {
            _logger.LogInformation("Update {Version} downloaded; installs on next restart.", info.Version);
            SetState(State with { Phase = UpdatePhase.ReadyToInstall, AvailableVersion = info.Version });
        };
        Electron.AutoUpdater.OnError += error =>
        {
            _logger.LogWarning("Auto-update error: {Error}", error);
            SetState(State with { Phase = UpdatePhase.Error, Error = error });
        };

        _ = CheckNowAsync();
        _timer = new Timer(_ => _ = CheckNowAsync(), state: null, CheckInterval, CheckInterval);
    }

    public async Task CheckNowAsync()
    {
        try
        {
            await Electron.AutoUpdater.CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed.");
        }
    }

    public void SetAutomaticUpdates(bool enabled)
    {
        _settings.AutomaticUpdates = enabled;
        _settings.Save();
        if (LooksPackaged())
        {
            Electron.AutoUpdater.AutoDownload = enabled;
        }

        _logger.LogInformation("Automatic updates {State}.", enabled ? "enabled" : "disabled");
    }

    /// <summary>Explicit user action only — never called automatically (§8).</summary>
    public void InstallAndRestart() =>
        Electron.AutoUpdater.QuitAndInstall(isSilent: false, isForceRunAfter: true);

    public async Task<string> GetCurrentVersionAsync()
    {
        try
        {
            return await Electron.App.GetVersionAsync();
        }
        catch
        {
            return "0.0.0";
        }
    }

    public void Dispose() => _timer?.Dispose();

    private void SetState(UpdateState next)
    {
        State = next;
        StateChanged?.Invoke(next);
    }

    private static bool LooksPackaged()
    {
        if (Directory.Exists(Path.Combine(AppContext.BaseDirectory, ".electron")))
        {
            return false; // dev: UnpackedDotnetFirst leaves a .electron sibling
        }

        var baseDir = AppContext.BaseDirectory.Replace('\\', '/');
        return !baseDir.Contains("/bin/Debug/") && !baseDir.Contains("/bin/Release/");
    }
}
