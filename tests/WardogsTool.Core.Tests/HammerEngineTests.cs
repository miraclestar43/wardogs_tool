using WardogsTool.Core.Hammer;

namespace WardogsTool.Core.Tests;

public class HammerEngineTests
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private static (HammerEngine Engine, FakeTimeSource Time, FakeMouse Mouse, List<LogEntry> Log) Create(double parkAfterMs)
    {
        var log = new List<LogEntry>();
        var time = new FakeTimeSource(log, TimeSpan.FromMilliseconds(parkAfterMs));
        var mouse = new FakeMouse(time);
        return (new HammerEngine(mouse, time), time, mouse, log);
    }

    private static string[] Lines(List<LogEntry> log)
    {
        lock (log)
            return log.Select(e => $"{e.What} @{e.At.TotalMilliseconds}").ToArray();
    }

    [Fact]
    public void Presets_are_310_and_510_with_a_fixed_40ms_gap()
    {
        Assert.Equal([310, 510], HammerPresets.All.Select(p => p.HoldMs));
        Assert.Equal("小/中锤", HammerPresets.SmallMedium.Name);
        Assert.Equal("大锤", HammerPresets.Large.Name);
        Assert.Equal(40, HammerPresets.ReleaseGapMs);
        Assert.Same(HammerPresets.SmallMedium, HammerPresets.FromHoldMs(123));
        Assert.Same(HammerPresets.Large, HammerPresets.FromHoldMs(510));
    }

    [Fact]
    public void F9_cycles_off_large_small_off_one_step_per_press()
    {
        int? running = null;
        var seen = new List<string>();
        for (var press = 0; press < 6; press++)
        {
            running = HammerPresets.NextHotkeyState(running)?.HoldMs;
            seen.Add(running is { } h ? $"{h}" : "OFF");
        }
        Assert.Equal(["510", "310", "OFF", "510", "310", "OFF"], seen);
        Assert.Same(HammerPresets.Large, HammerPresets.NextHotkeyState(null));
        Assert.Null(HammerPresets.NextHotkeyState(123)); // unknown hold: next press turns it off
    }

    [Fact]
    public void Switching_presets_releases_the_button_before_the_new_cycle_starts()
    {
        // What the F9 switch Large -> Small/Medium does: Stop (mid-hold), then Start(310).
        var (engine, time, _, log) = Create(parkAfterMs: 200);
        engine.Start(510);
        Assert.True(time.Parked.Wait(WaitTimeout));
        Assert.True(engine.Stop());
        Assert.False(engine.IsRunning);
        var afterStop = Lines(log);
        Assert.Equal("up @0", afterStop[^1]); // released before Stop() returned

        Assert.True(engine.Start(310));
        SpinWait.SpinUntil(() => Lines(log).Length > afterStop.Length, WaitTimeout);
        engine.Stop();
        var all = Lines(log).Where(l => !l.StartsWith("wait")).ToArray();
        Assert.Equal(new[] { "down @0", "up @0", "down @0", "up @0" }, all);
        // Never two downs in a row: every down is released before the next one.
        for (var i = 1; i < all.Length; i++)
            Assert.False(all[i].StartsWith("down") && all[i - 1].StartsWith("down"));
    }

    [Fact]
    public void Sequence_is_immediate_down_then_hold_up_gap_repeat()
    {
        var (engine, time, _, log) = Create(parkAfterMs: 1000);
        Assert.True(engine.Start(310));
        Assert.True(time.Parked.Wait(WaitTimeout));

        Assert.Equal(new[]
        {
            "down @0", "wait 310 @0",       // first down is immediate, before any wait
            "up @310", "wait 350 @310",     // 40 ms gap
            "down @350", "wait 660 @350",
            "up @660", "wait 700 @660",
            "down @700", "wait 1010 @700",  // parked here, holding
        }, Lines(log));
        Assert.Equal(HammerState.Holding, engine.State);
        Assert.True(engine.IsRunning);

        Assert.True(engine.Stop());
        Assert.Equal("up @700", Lines(log)[^1]);
        Assert.Equal(HammerState.Stopped, engine.State);
        Assert.False(engine.IsRunning);
    }

    [Fact]
    public void Large_preset_holds_510ms()
    {
        var (engine, time, _, log) = Create(parkAfterMs: 600);
        engine.Start(510);
        Assert.True(time.Parked.Wait(WaitTimeout));
        engine.Stop();
        Assert.Equal(new[] { "down @0", "wait 510 @0", "up @510", "wait 550 @510", "down @550", "wait 1060 @550", "up @550" }, Lines(log));
    }

    [Fact]
    public void Each_wait_is_relative_to_the_end_of_the_previous_step_with_no_catch_up()
    {
        // Every input call takes 7 ms. Deadlines are taken after the call returns, so each cycle
        // stretches by 14 ms instead of being pulled back onto a 350 ms grid.
        var (engine, time, mouse, log) = Create(parkAfterMs: 1000);
        mouse.CallDuration = TimeSpan.FromMilliseconds(7);
        engine.Start(310);
        Assert.True(time.Parked.Wait(WaitTimeout));
        engine.Stop();

        Assert.Equal(new[]
        {
            "down @0", "wait 317 @7",
            "up @317", "wait 364 @324",
            "down @364", "wait 681 @371",
            "up @681", "wait 728 @688",
            "down @728", "wait 1045 @735",
            "up @735",
        }, Lines(log));
    }

    [Fact]
    public void Stop_during_hold_releases_immediately()
    {
        var (engine, time, _, log) = Create(parkAfterMs: 200);
        engine.Start(310);
        Assert.True(time.Parked.Wait(WaitTimeout));
        Assert.Equal(HammerState.Holding, engine.State);

        Assert.True(engine.Stop());
        Assert.Equal(new[] { "down @0", "wait 310 @0", "up @0" }, Lines(log));
    }

    [Fact]
    public void Stop_during_gap_sends_nothing_extra()
    {
        var (engine, time, _, log) = Create(parkAfterMs: 330);
        engine.Start(310);
        Assert.True(time.Parked.Wait(WaitTimeout));
        Assert.Equal(HammerState.Releasing, engine.State);

        Assert.True(engine.Stop());
        var lines = Lines(log);
        Assert.Equal(new[] { "down @0", "wait 310 @0", "up @310", "wait 350 @310" }, lines);
        Assert.Equal(lines.Count(l => l.StartsWith("down")), lines.Count(l => l.StartsWith("up")));
    }

    [Fact]
    public void Start_while_running_is_ignored_and_restart_after_stop_works()
    {
        var (engine, time, _, _) = Create(parkAfterMs: 100);
        Assert.True(engine.Start(310));
        Assert.True(time.Parked.Wait(WaitTimeout));
        Assert.False(engine.Start(510));
        Assert.Equal(310, engine.HoldMs);
        Assert.True(engine.Stop());

        Assert.True(engine.Start(510));
        Assert.Equal(510, engine.HoldMs);
        Assert.True(engine.Stop());
    }

    [Fact]
    public void Stop_without_start_and_double_stop_are_safe()
    {
        var (engine, time, _, log) = Create(parkAfterMs: 100);
        Assert.True(engine.Stop());
        engine.Start(310);
        Assert.True(time.Parked.Wait(WaitTimeout));
        Assert.True(engine.Stop());
        Assert.True(engine.Stop());
        Assert.Equal(1, Lines(log).Count(l => l.StartsWith("up")));
    }

    [Fact]
    public void Exception_while_holding_still_releases_the_button()
    {
        var (engine, time, _, log) = Create(parkAfterMs: 10_000);
        time.ThrowOnWait = 3; // the hold wait of the second cycle
        engine.Start(310);
        Assert.True(SpinWait.SpinUntil(() => !engine.IsRunning, WaitTimeout));

        var lines = Lines(log);
        Assert.Equal(new[] { "down @0", "wait 310 @0", "up @310", "wait 350 @310", "down @350", "wait 660 @350", "up @350" }, lines);
        Assert.IsType<InvalidOperationException>(engine.LastError);
        Assert.Equal(HammerState.Stopped, engine.State);
    }

    [Fact]
    public void Exception_from_mouse_down_does_not_send_an_unmatched_up()
    {
        var (engine, _, mouse, log) = Create(parkAfterMs: 10_000);
        mouse.ThrowOnDown = 2;
        engine.Start(310);
        SpinWait.SpinUntil(() => !engine.IsRunning, WaitTimeout);

        // The second down threw before the button was pressed, so nothing is released for it.
        Assert.Equal(new[] { "down @0", "wait 310 @0", "up @310", "wait 350 @310" }, Lines(log));
        Assert.NotNull(engine.LastError);
    }

    [Fact]
    public void Non_positive_hold_is_rejected()
    {
        var (engine, _, _, _) = Create(parkAfterMs: 100);
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Start(0));
        Assert.False(engine.IsRunning);
    }
}
