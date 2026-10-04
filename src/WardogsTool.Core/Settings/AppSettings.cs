using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using WardogsTool.Core.AntiAfk;
using WardogsTool.Core.Hammer;
using WardogsTool.Core.Magnifier;

namespace WardogsTool.Core.Settings;

/// <summary>
/// Persisted UI state, %AppData%\WardogsTool\settings.json. The anti-AFK fields are kept as the raw
/// text the user typed (like the Python entry boxes) and only validated when anti-AFK starts.
/// </summary>
/// <remarks>
/// Serialized with the built-in DataContractJsonSerializer. It creates objects without running
/// constructors or field initializers, so every class sets its defaults both in its constructor
/// and in an [OnDeserializing] hook — that is what makes a member missing from the file (e.g. the
/// whole magnifier section in a version-1 file) come back as its default.
/// </remarks>
[DataContract]
public sealed class AppSettings
{
    /// <summary>2 added the magnifier section; version-1 files load with the default zoom.</summary>
    public const int CurrentSchemaVersion = 2;

    public AppSettings() => SetDefaults();

    [DataMember(Name = "schemaVersion", Order = 0)] public int SchemaVersion { get; set; }
    [DataMember(Name = "hammer", Order = 1)] public HammerSection Hammer { get; set; } = null!;
    [DataMember(Name = "antiAfk", Order = 2)] public AntiAfkSection AntiAfk { get; set; } = null!;
    [DataMember(Name = "mortar", Order = 3)] public MortarSection Mortar { get; set; } = null!;
    [DataMember(Name = "magnifier", Order = 4)] public MagnifierSection Magnifier { get; set; } = null!;
    [DataMember(Name = "window", Order = 5)] public WindowSection Window { get; set; } = null!;

    [OnDeserializing]
    private void OnDeserializing(StreamingContext context) => SetDefaults();

    private void SetDefaults()
    {
        SchemaVersion = CurrentSchemaVersion;
        Hammer = new();
        AntiAfk = new();
        Mortar = new();
        Magnifier = new();
        Window = new();
    }

    [DataContract]
    public sealed class HammerSection
    {
        public HammerSection() => SetDefaults();

        [DataMember(Name = "holdMs")] public int HoldMs { get; set; }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) => SetDefaults();

        private void SetDefaults() => HoldMs = HammerPresets.SmallMedium.HoldMs;
    }

    [DataContract]
    public sealed class AntiAfkSection
    {
        public AntiAfkSection() => SetDefaults();

        [DataMember(Name = "key", Order = 0)] public string Key { get; set; } = null!;
        [DataMember(Name = "count", Order = 1)] public string Count { get; set; } = null!;
        [DataMember(Name = "gapMs", Order = 2)] public string GapMs { get; set; } = null!;
        [DataMember(Name = "periodSeconds", Order = 3)] public string PeriodSeconds { get; set; } = null!;

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) => SetDefaults();

        private void SetDefaults()
        {
            Key = AntiAfkSettings.DefaultKey;
            Count = AntiAfkSettings.DefaultCount;
            GapMs = AntiAfkSettings.DefaultGapMs;
            PeriodSeconds = AntiAfkSettings.DefaultPeriodSeconds;
        }
    }

    [DataContract]
    public sealed class MortarSection
    {
        public MortarSection() => SetDefaults();

        [DataMember(Name = "position")] public string Position { get; set; } = null!;

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) => SetDefaults();

        private void SetDefaults() => Position = "";
    }

    [DataContract]
    public sealed class MagnifierSection
    {
        public MagnifierSection() => SetDefaults();

        [DataMember(Name = "zoom")] public double Zoom { get; set; }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context) => SetDefaults();

        private void SetDefaults() => Zoom = MagnifierGeometry.DefaultZoom;
    }

    [DataContract]
    public sealed class WindowSection
    {
        [DataMember(Name = "alwaysOnTop", Order = 0)] public bool AlwaysOnTop { get; set; }
        [DataMember(Name = "left", Order = 1)] public double? Left { get; set; }
        [DataMember(Name = "top", Order = 2)] public double? Top { get; set; }
        [DataMember(Name = "selectedTab", Order = 3)] public int SelectedTab { get; set; }
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

    private static readonly DataContractJsonSerializer Serializer = new(typeof(AppSettings));

    /// <summary>Indented UTF-8 JSON (no byte-order mark).</summary>
    public string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, new UTF8Encoding(false), ownsStream: false, indent: true, indentChars: "  "))
        {
            Serializer.WriteObject(writer, this);
            writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Parses settings JSON; throws on malformed input or a non-object document.</summary>
    public static AppSettings FromJson(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return Serializer.ReadObject(stream) as AppSettings
            ?? throw new SerializationException("The settings document is empty (null).");
    }
}

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
            var loaded = AppSettings.FromJson(json);
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
        File.WriteAllText(temp, settings.ToJson());
        // File.Move(..., overwrite) is .NET Core 3.0+; File.Replace is the atomic equivalent here.
        if (File.Exists(FilePath))
            File.Replace(temp, FilePath, null);
        else
            File.Move(temp, FilePath);
    }
}
