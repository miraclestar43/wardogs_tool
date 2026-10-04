using System.Diagnostics;
using WardogsTool.Core.Input;
using WardogsTool.Core.Settings;
using WardogsTool.Core.Timing;

namespace WardogsTool.Core.Tests;

public class HotkeyEdgeDetectorTests
{
    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void Fires_once_per_press_and_ignores_auto_repeat()
    {
        var d = new HotkeyEdgeDetector();
        Assert.True(d.OnKey(0x78, isBreak: false, Ms(0)));
        Assert.False(d.OnKey(0x78, isBreak: false, Ms(500)));   // initial repeat delay
        Assert.False(d.OnKey(0x78, isBreak: false, Ms(533)));   // repeats
        Assert.False(d.OnKey(0x78, isBreak: false, Ms(566)));
        Assert.False(d.OnKey(0x78, isBreak: true, Ms(600)));
        Assert.True(d.OnKey(0x78, isBreak: false, Ms(700)));    // next press
    }

    [Fact]
    public void Keys_are_tracked_independently()
    {
        var d = new HotkeyEdgeDetector();
        Assert.True(d.OnKey(0x77, false, Ms(0)));
        Assert.True(d.OnKey(0x78, false, Ms(10)));
        Assert.False(d.OnKey(0x77, false, Ms(40)));
        Assert.True(d.OnKey(0x1B, false, Ms(50)));
    }

    [Fact]
    public void Break_without_make_is_harmless()
    {
        var d = new HotkeyEdgeDetector();
        Assert.False(d.OnKey(0x1B, true, Ms(0)));
        Assert.True(d.OnKey(0x1B, false, Ms(1)));
    }

    [Fact]
    public void A_lost_break_does_not_disable_the_key_forever()
    {
        var d = new HotkeyEdgeDetector();
        Assert.True(d.OnKey(0x78, false, Ms(0)));
        // The break never arrived; a make 1.5 s+ later can't be auto-repeat.
        Assert.True(d.OnKey(0x78, false, Ms(1600)));
        // Held for a long time with repeats: still a single press.
        for (var t = 2100; t < 10_000; t += 33)
            Assert.False(d.OnKey(0x78, false, Ms(t)));
    }
}

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "WardogsToolTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Missing_file_gives_python_defaults()
    {
        var (s, warning) = new SettingsStore(_dir).Load();
        Assert.Null(warning);
        Assert.Equal(2, s.SchemaVersion);
        Assert.Equal(2.0, s.Magnifier.Zoom);
        Assert.Equal(310, s.Hammer.HoldMs);
        Assert.Equal(("c", "2", "500", "180"), (s.AntiAfk.Key, s.AntiAfk.Count, s.AntiAfk.GapMs, s.AntiAfk.PeriodSeconds));
        Assert.Equal("", s.Mortar.Position);
        Assert.False(s.Window.AlwaysOnTop);
        Assert.Null(s.Window.Left);
    }

    [Fact]
    public void Round_trip()
    {
        var store = new SettingsStore(_dir);
        var s = new AppSettings();
        s.Hammer.HoldMs = 510;
        s.AntiAfk.Key = "x";
        s.AntiAfk.Count = "abc";   // raw text survives, validated only at start
        s.Mortar.Position = "100.32 59.45";
        s.Window.AlwaysOnTop = true;
        s.Window.Left = 12.5;
        s.Window.Top = -3;
        s.Window.SelectedTab = 2;
        store.Save(s);

        var (loaded, warning) = store.Load();
        Assert.Null(warning);
        Assert.Equal(510, loaded.Hammer.HoldMs);
        Assert.Equal("x", loaded.AntiAfk.Key);
        Assert.Equal("abc", loaded.AntiAfk.Count);
        Assert.Equal("100.32 59.45", loaded.Mortar.Position);
        Assert.True(loaded.Window.AlwaysOnTop);
        Assert.Equal(12.5, loaded.Window.Left);
        Assert.Equal(-3, loaded.Window.Top);
        Assert.Equal(2, loaded.Window.SelectedTab);
        Assert.Contains("\"schemaVersion\": 2", File.ReadAllText(store.FilePath));
    }

    [Fact]
    public void Magnifier_zoom_round_trips_and_snaps()
    {
        var store = new SettingsStore(_dir);
        var s = new AppSettings();
        s.Magnifier.Zoom = 3.0;
        store.Save(s);
        Assert.Equal(3.0, store.Load().Settings.Magnifier.Zoom);

        File.WriteAllText(store.FilePath, "{\"schemaVersion\":2,\"magnifier\":{\"zoom\":7.3}}");
        Assert.Equal(4.0, store.Load().Settings.Magnifier.Zoom);
    }

    [Fact]
    public void Version_1_file_loads_with_the_default_zoom()
    {
        var store = new SettingsStore(_dir);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.FilePath, "{\"schemaVersion\":1,\"hammer\":{\"holdMs\":510},\"mortar\":{\"position\":\"1 2\"}}");
        var (s, warning) = store.Load();
        Assert.Null(warning);
        Assert.Equal(510, s.Hammer.HoldMs);
        Assert.Equal("1 2", s.Mortar.Position);
        Assert.Equal(2.0, s.Magnifier.Zoom);
        Assert.Equal(2, s.SchemaVersion);
    }

    [Fact]
    public void Unknown_members_are_ignored()
    {
        var store = new SettingsStore(_dir);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.FilePath, "{\"schemaVersion\":2,\"futureThing\":{\"a\":1},\"hammer\":{\"holdMs\":510,\"extra\":true}}");
        var (s, warning) = store.Load();
        Assert.Null(warning);
        Assert.Equal(510, s.Hammer.HoldMs);
    }

    [Fact]
    public void Wrong_value_type_is_treated_as_corrupt()
    {
        var store = new SettingsStore(_dir);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.FilePath, "{\"schemaVersion\":2,\"hammer\":{\"holdMs\":\"abc\"}}");
        var (s, warning) = store.Load();
        Assert.NotNull(warning);
        Assert.Equal(310, s.Hammer.HoldMs);
        Assert.True(File.Exists(store.FilePath + ".bad"));
    }

    [Fact]
    public void Partly_present_sections_keep_defaults_for_missing_members()
    {
        // The serializer skips constructors; defaults must still apply inside a present section.
        var store = new SettingsStore(_dir);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.FilePath, "{\"schemaVersion\":2,\"antiAfk\":{\"key\":\"x\"},\"window\":{\"left\":5}}");
        var (s, _) = store.Load();
        Assert.Equal(("x", "2", "500", "180"), (s.AntiAfk.Key, s.AntiAfk.Count, s.AntiAfk.GapMs, s.AntiAfk.PeriodSeconds));
        Assert.Equal(5, s.Window.Left);
        Assert.Null(s.Window.Top);
        Assert.Equal(2.0, s.Magnifier.Zoom);
    }

    [Fact]
    public void Running_state_is_not_persisted()
    {
        var store = new SettingsStore(_dir);
        store.Save(new AppSettings());
        var json = File.ReadAllText(store.FilePath);
        Assert.DoesNotContain("running", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isOn", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Corrupt_file_falls_back_to_defaults_and_is_kept_aside()
    {
        var store = new SettingsStore(_dir);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.FilePath, "{ this is not json");

        var (s, warning) = store.Load();
        Assert.NotNull(warning);
        Assert.Equal(310, s.Hammer.HoldMs);
        Assert.Equal("{ this is not json", File.ReadAllText(store.FilePath + ".bad"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"hammer\": null, \"antiAfk\": {\"key\": null}}")]
    public void Odd_documents_do_not_break_startup(string json)
    {
        var store = new SettingsStore(_dir);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.FilePath, json);
        var (s, _) = store.Load();
        Assert.Equal(310, s.Hammer.HoldMs);
        Assert.Equal("c", s.AntiAfk.Key);
    }

    [Fact]
    public void Unknown_hold_falls_back_to_the_310_preset()
    {
        var store = new SettingsStore(_dir);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.FilePath, "{\"schemaVersion\":1,\"hammer\":{\"holdMs\":5}}");
        Assert.Equal(310, store.Load().Settings.Hammer.HoldMs);
    }

    [Fact]
    public void Newer_schema_is_not_misread()
    {
        var store = new SettingsStore(_dir);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(store.FilePath, "{\"schemaVersion\":99,\"hammer\":{\"holdMs\":510}}");
        var (s, warning) = store.Load();
        Assert.NotNull(warning);
        Assert.Equal(310, s.Hammer.HoldMs);
    }
}

public class SystemTimeSourceTests
{
    [Fact]
    public void Waits_until_the_deadline()
    {
        var time = new SystemTimeSource();
        using var cts = new CancellationTokenSource();
        var start = time.Now;
        Assert.True(time.WaitUntil(start + TimeSpan.FromMilliseconds(30), cts.Token));
        Assert.True(time.Now - start >= TimeSpan.FromMilliseconds(30));
    }

    [Fact]
    public void Cancellation_wakes_the_wait_immediately()
    {
        var time = new SystemTimeSource();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(20);
        var sw = Stopwatch.StartNew();
        Assert.False(time.WaitUntil(time.Now + TimeSpan.FromSeconds(10), cts.Token));
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Already_cancelled_returns_false_even_if_the_time_is_up()
    {
        var time = new SystemTimeSource();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.False(time.WaitUntil(time.Now - TimeSpan.FromSeconds(1), cts.Token));
    }

    [Fact]
    public void Uncancellable_wait_completes()
    {
        var time = new SystemTimeSource();
        var start = time.Now;
        Assert.True(time.WaitUntil(start + TimeSpan.FromMilliseconds(10), CancellationToken.None));
        Assert.True(time.Now - start >= TimeSpan.FromMilliseconds(10));
    }
}
