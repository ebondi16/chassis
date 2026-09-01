using System.Text.Json;

namespace Chassis.Api;

/// <summary>
/// Small per-user desktop preferences file (<c>%LOCALAPPDATA%/Chassis/settings.json</c>
/// and the OS equivalents). Not for secrets — API keys/tokens go in the OS
/// keychain (§9.6). Just user-facing toggles the shell needs before the SPA is up.
/// </summary>
internal sealed class DesktopSettings
{
    private const string AppFolderName = "Chassis";
    private const string FileName = "settings.json";

    /// <summary>The §8 "automatic updates" preference. Default on.</summary>
    public bool AutomaticUpdates { get; set; } = true;

    public static DesktopSettings Load()
    {
        try
        {
            var path = GetPath();
            return File.Exists(path)
                ? JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(path)) ?? new DesktopSettings()
                : new DesktopSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new DesktopSettings();
        }
    }

    public void Save()
    {
        try
        {
            var path = GetPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A preference that fails to persist is not worth crashing the app over.
        }
    }

    private static string GetPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppFolderName,
        FileName);
}
