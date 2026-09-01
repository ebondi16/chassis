using System.Diagnostics;
using System.Text.Json;

namespace Chassis.Api;

/// <summary>
/// Self-healing for the orphaned-process failure mode (§5.7/§5.8): a hard kill
/// of the .NET host — crash, "End Task", OS update, AV, VM reset — leaves
/// Electron's child tree alive, and Electron's own single-instance lock does not
/// help because a *dead* host's children still hold it.
///
/// On each desktop launch, before Electron starts, we look for a marker file
/// from the previous run and kill anything from it that is still alive *and*
/// still ours (start-time match + running from inside this app's own directory —
/// never a bare PID or process-name match, since <c>electron</c> is shared by
/// many unrelated apps). Plain <see cref="System.Diagnostics"/> only — no
/// <c>using ElectronNET</c> — so per §9.4 it belongs in DesktopComposition.
///
/// This reduces the user-facing consequence of orphaning; it is not a substitute
/// for §5.3's deferred lifecycle suite, which is what says whether the orphaning
/// has other effects (locked files, corrupt state) in the interim.
/// </summary>
internal static class StaleInstanceCleanup
{
    private const string AppFolderName = "Chassis";
    private const string MarkerFileName = "instance.lock";

    private sealed record TrackedProcess(int Pid, string ExecutablePath, long StartTimeUtcTicks, string Kind);

    private sealed record InstanceMarker(DateTimeOffset WrittenAtUtc, IReadOnlyList<TrackedProcess> Processes);

    /// <summary>
    /// Runs first thing in <see cref="DesktopComposition.Enable"/> — before the
    /// single-instance lock check and before any window is created.
    /// </summary>
    public static void KillLeftoversFromPreviousRun()
    {
        var markerPath = GetMarkerPath();

        InstanceMarker? marker;
        try
        {
            if (!File.Exists(markerPath))
            {
                return;
            }

            marker = JsonSerializer.Deserialize<InstanceMarker>(File.ReadAllText(markerPath));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupt or unreadable marker is not worth failing a launch over.
            TryDelete(markerPath);
            return;
        }

        var currentPid = Environment.ProcessId;

        foreach (var tracked in marker?.Processes ?? [])
        {
            if (tracked.Pid == currentPid)
            {
                continue;
            }

            try
            {
                using var process = Process.GetProcessById(tracked.Pid);

                if (process.StartTime.ToUniversalTime().Ticks != tracked.StartTimeUtcTicks)
                {
                    continue; // PID was recycled by the OS — not the process we tracked.
                }

                if (!IsInsideThisApp(SafeGetExecutablePath(process)))
                {
                    continue; // Running from somewhere else — not ours to kill.
                }

                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
                Console.Error.WriteLine(
                    $"[Chassis.Desktop] killed leftover {tracked.Kind} process {tracked.Pid} from a previous run.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                // Already gone between reading the marker and acting on it — fine.
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"[Chassis.Desktop] could not clean up process {tracked.Pid}: {ex.Message}");
            }
        }

        TryDelete(markerPath);
    }

    /// <summary>
    /// Records the live process tree (this host plus every Electron process
    /// running from inside this app's directory). Called once the window is up.
    /// </summary>
    public static void WriteMarker(ILogger logger)
    {
        try
        {
            var tracked = new List<TrackedProcess> { Describe(Process.GetCurrentProcess(), "host") };

            foreach (var electron in Process.GetProcessesByName("electron"))
            {
                using (electron)
                {
                    var path = SafeGetExecutablePath(electron);
                    if (IsInsideThisApp(path))
                    {
                        tracked.Add(Describe(electron, "electron", path));
                    }
                }
            }

            var markerPath = GetMarkerPath();
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath)!);
            File.WriteAllText(
                markerPath,
                JsonSerializer.Serialize(new InstanceMarker(DateTimeOffset.UtcNow, tracked)));

            logger.LogInformation(
                "Wrote instance marker for {Count} process(es) to {Path}", tracked.Count, markerPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not write the desktop instance marker; stale-cleanup will no-op next launch.");
        }
    }

    /// <summary>Deletes the marker on a clean shutdown (menu quit, window close, Ctrl+C).</summary>
    public static void ClearMarker(ILogger logger)
    {
        var markerPath = GetMarkerPath();
        if (TryDelete(markerPath))
        {
            logger.LogInformation("Cleared instance marker on clean shutdown.");
        }
    }

    private static TrackedProcess Describe(Process process, string kind, string? knownPath = null) =>
        new(process.Id, knownPath ?? SafeGetExecutablePath(process) ?? string.Empty,
            process.StartTime.ToUniversalTime().Ticks, kind);

    private static string? SafeGetExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception)
        {
            return null; // access denied / process exited / not supported
        }
    }

    private static bool IsInsideThisApp(string? executablePath)
    {
        if (string.IsNullOrEmpty(executablePath))
        {
            return false;
        }

        var appRoot = Path.GetFullPath(AppContext.BaseDirectory);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return Path.GetFullPath(executablePath).StartsWith(appRoot, comparison);
    }

    private static string GetMarkerPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppFolderName,
        MarkerFileName);

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // best effort
        }

        return false;
    }
}
