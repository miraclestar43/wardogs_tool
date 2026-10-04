using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WardogsTool.InputProbe;

/// <summary>Settings a scenario wants the tool under test to start with.</summary>
internal sealed record TargetSettings(int HoldMs = 310, string Key = "c", string Count = "2", string GapMs = "500",
    string PeriodSeconds = "180", bool AlwaysOnTop = false, int X = 20, int Y = 20);

internal interface ITarget
{
    string Name { get; }

    /// <summary>True when F9 cycles OFF → 510 → 310 → OFF instead of starting the selected preset.</summary>
    bool F9Cycles { get; }
    Process Launch(TargetSettings settings);
}

/// <summary>The unmodified Python tool, via tools/parity/python_driver.py.</summary>
internal sealed class PythonTarget(string python, string repoRoot) : ITarget
{
    public string Name => "Python";

    public bool F9Cycles => false;

    public Process Launch(TargetSettings s)
    {
        var driver = Path.Combine(repoRoot, "tools", "parity", "python_driver.py");
        var psi = new ProcessStartInfo(python)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = repoRoot,
        };
        foreach (var a in new[] { driver, "--hold", s.HoldMs.ToString(), "--key", s.Key, "--count", s.Count, "--gap", s.GapMs,
                     "--period", s.PeriodSeconds, "--x", s.X.ToString(), "--y", s.Y.ToString() })
            psi.ArgumentList.Add(a);
        var p = Process.Start(psi)!;
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) Stderr.Add(e.Data); };
        p.OutputDataReceived += (_, _) => { };
        p.BeginErrorReadLine();
        p.BeginOutputReadLine();
        return p;
    }

    /// <summary>Anything Python wrote to stderr (Tk callback tracebacks end up here).</summary>
    public List<string> Stderr { get; } = [];
}

/// <summary>The published WardogsTool.exe; settings are passed through its settings.json.</summary>
internal sealed class CSharpTarget(string exe) : ITarget
{
    public static string SettingsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WardogsTool");

    public static string SettingsFile => Path.Combine(SettingsDir, "settings.json");

    public string Name => "C#";

    public bool F9Cycles => true;

    public Process Launch(TargetSettings s)
    {
        WriteSettings(s);
        return Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false })!;
    }

    public static void WriteSettings(TargetSettings s, string mortarPosition = "", int selectedTab = 0)
    {
        Directory.CreateDirectory(SettingsDir);
        var json = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["hammer"] = new JsonObject { ["holdMs"] = s.HoldMs },
            ["antiAfk"] = new JsonObject { ["key"] = s.Key, ["count"] = s.Count, ["gapMs"] = s.GapMs, ["periodSeconds"] = s.PeriodSeconds },
            ["mortar"] = new JsonObject { ["position"] = mortarPosition },
            ["window"] = new JsonObject { ["alwaysOnTop"] = s.AlwaysOnTop, ["left"] = s.X, ["top"] = s.Y, ["selectedTab"] = selectedTab },
        };
        File.WriteAllText(SettingsFile, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public static JsonNode? ReadSettings() =>
        File.Exists(SettingsFile) ? JsonNode.Parse(File.ReadAllText(SettingsFile)) : null;
}

/// <summary>Keeps the user's real WardogsTool settings out of harm's way during the run.</summary>
internal sealed class SettingsBackup : IDisposable
{
    private readonly string? _saved;
    private readonly HashSet<string> _preexisting = [];

    public SettingsBackup()
    {
        if (File.Exists(CSharpTarget.SettingsFile))
            _saved = File.ReadAllText(CSharpTarget.SettingsFile);
        foreach (var leftover in new[] { ".bad", ".tmp" })
            if (File.Exists(CSharpTarget.SettingsFile + leftover))
                _preexisting.Add(leftover);
    }

    public void Dispose()
    {
        if (_saved is null)
        {
            if (File.Exists(CSharpTarget.SettingsFile)) File.Delete(CSharpTarget.SettingsFile);
        }
        else
        {
            File.WriteAllText(CSharpTarget.SettingsFile, _saved);
        }
        foreach (var leftover in new[] { ".bad", ".tmp" })
            if (!_preexisting.Contains(leftover) && File.Exists(CSharpTarget.SettingsFile + leftover))
                File.Delete(CSharpTarget.SettingsFile + leftover);
    }
}
