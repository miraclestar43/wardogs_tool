using System.Text.Json;
using System.Text.Json.Serialization;
using WardogsTool.Core.AntiAfk;
using WardogsTool.Core.Hammer;
using WardogsTool.Core.Magnifier;

namespace WardogsTool.Core.Settings;

/// <summary>
/// Persisted UI state, %AppData%\WardogsTool\settings.json. The anti-AFK fields are kept as the raw
/// text the user typed (like the Python entry boxes) and only validated when anti-AFK starts.
/// </summary>
public sealed class AppSettings
{
    /// <summary>2 added the magnifier section; version-1 files load with the default zoom.</summary>
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public HammerSection Hammer { get; set; } = new();
    public AntiAfkSection AntiAfk { get; set; } = new();
    public MortarSection Mortar { get; set; } = new();
    public MagnifierSection Magnifier { get; set; } = new();
    public WindowSection Window { get; set; } = new();

    public sealed class HammerSection
    {
        public int HoldMs { get; set; } = HammerPresets.SmallMedium.HoldMs;
    }

    public sealed class AntiAfkSection
    {
        public string Key { get; set; } = AntiAfkSettings.DefaultKey;
        public string Count { get; set; } = AntiAfkSettings.DefaultCount;
        public string GapMs { get; set; } = AntiAfkSettings.DefaultGapMs;
        public string PeriodSeconds { get; set; } = AntiAfkSettings.DefaultPeriodSeconds;
    }

    public sealed class MortarSection
    {
        public string Position { get; set; } = "";
    }

    public sealed class MagnifierSection
    {
        public double Zoom { get; set; } = MagnifierGeometry.DefaultZoom;
    }

    public sealed class WindowSection
    {
        public bool AlwaysOnTop { get; set; }
        public double? Left { get; set; }
        public double? Top { get; set; }
        public int SelectedTab { get; set; }
    }

    /// <summary>Replaces anything missing or out of range with defaults.</summary>
    public AppSettings Normalized()
    {
        Hammer ??= new();
        AntiAfk ??= new();
        Mortar ??= new();
        Magnifier ??= new();
        Window ??= new();
        Magnifier.Zoom = MagnifierGeometry.NormalizeZoom(Magnifier.Zoom);
        Hammer.HoldMs = HammerPresets.FromHoldMs(Hammer.HoldMs).HoldMs;
        AntiAfk.Key ??= AntiAfkSettings.DefaultKey;
        AntiAfk.Count ??= AntiAfkSettings.DefaultCount;
        AntiAfk.GapMs ??= AntiAfkSettings.DefaultGapMs;
        AntiAfk.PeriodSeconds ??= AntiAfkSettings.DefaultPeriodSeconds;
        Mortar.Position ??= "";
        if (Window.Left is { } l && !DoublePolyfills.IsFinite(l)) Window.Left = null;
        if (Window.Top is { } t && !DoublePolyfills.IsFinite(t)) Window.Top = null;
        if (Window.SelectedTab < 0) Window.SelectedTab = 0;
        SchemaVersion = CurrentSchemaVersion;
        return this;
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;

/// <summary>
/// Loads and saves <see cref="AppSettings"/>. A missing, unreadable, corrupt or newer-schema file
/// never blocks startup: defaults are used, and a corrupt file is kept aside as settings.json.bad.
/// </summary>
public sealed class SettingsStore
{
    public const string FileName = "settings.json";

    public SettingsStore(string directory)
    {
        Directory = directory;
    }

    public string Directory { get; }

    public string FilePath => Path.Combine(Directory, FileName);

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WardogsTool");

    /// <returns>The settings, plus a warning when the file could not be used.</returns>
    public (AppSettings Settings, string? Warning) Load()
    {
        string json;
        try
        {
            if (!File.Exists(FilePath))
                return (new AppSettings(), null);
            json = File.ReadAllText(FilePath);
        }
        catch (Exception ex)
        {
            return (new AppSettings(), $"无法读取设置文件，已使用默认设置：{ex.Message}");
        }

        try
        {
            var loaded = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);
            if (loaded is null)
                throw new JsonException("empty document");
            if (loaded.SchemaVersion > AppSettings.CurrentSchemaVersion)
                return (new AppSettings(), $"设置文件版本 {loaded.SchemaVersion} 比本程序新，已使用默认设置");
            return (loaded.Normalized(), null);
        }
        catch (Exception ex)
        {
            try
            {
                File.Copy(FilePath, FilePath + ".bad", overwrite: true);
            }
            catch
            {
                // Keeping a copy is best effort; startup must not depend on it.
            }
            return (new AppSettings(), $"设置文件已损坏，已使用默认设置（原文件备份为 {FileName}.bad）：{ex.Message}");
        }
    }

    /// <summary>Writes atomically (temp file + replace) so a crash mid-write cannot corrupt the file.</summary>
    public void Save(AppSettings settings)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings));
        // File.Move(..., overwrite) is .NET Core 3.0+; File.Replace is the atomic equivalent here.
        if (File.Exists(FilePath))
            File.Replace(temp, FilePath, null);
        else
            File.Move(temp, FilePath);
    }
}
