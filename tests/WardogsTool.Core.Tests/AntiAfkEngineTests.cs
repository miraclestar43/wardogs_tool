using WardogsTool.Core.AntiAfk;

namespace WardogsTool.Core.Tests;

public class AntiAfkEngineTests
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    private static (AntiAfkEngine Engine, FakeTimeSource Time, FakeKeyboard Keyboard, List<LogEntry> Log) Create(double parkAfterMs)
    {
        var log = new List<LogEntry>();
        var time = new FakeTimeSource(log, TimeSpan.FromMilliseconds(parkAfterMs));
        var keyboard = new FakeKeyboard(time);
        return (new AntiAfkEngine(keyboard, time), time, keyboard, log);
    }

    private static string[] Keys(List<LogEntry> log)
    {
        lock (log)
            return log.Where(e => !e.What.StartsWith("wait")).Select(e => $"{e.What} @{e.At.TotalMilliseconds}").ToArray();
    }

    [Fact]
    public void Defaults_match_the_python_tool()
    {
        var ok = AntiAfkSettings.TryValidate(AntiAfkSettings.DefaultKey, AntiAfkSettings.DefaultCount,
            AntiAfkSettings.DefaultGapMs, AntiAfkSettings.DefaultPeriodSeconds, new FakeKeyboard(null), out var plan, out _);
        Assert.True(ok);
        Assert.Equal(new AntiAfkPlan(0x43, 2, 500, 180_000), plan);
        Assert.Equal(50, AntiAfkEngine.KeyHoldMs);
    }

    [Fact]
    public void First_round_comes_one_full_period_after_start_then_repeats()
    {
        var (engine, time, _, log) = Create(parkAfterMs: 7000);
        Assert.True(engine.Start(new AntiAfkPlan(0x43, 2, 500, 3000)));
        Assert.Equal(TimeSpan.FromMilliseconds(3000), engine.NextRoundAt);
        Assert.True(time.Parked.Wait(WaitTimeout));

        Assert.Equal(new[]
        {
            "down 43 @3000", "up 43 @3050", "down 43 @3500", "up 43 @3550",
            "down 43 @6000", "up 43 @6050", "down 43 @6500", "up 43 @6550",
        }, Keys(log));
        Assert.Equal(TimeSpan.FromMilliseconds(9000), engine.NextRoundAt);
        Assert.True(engine.IsRunning);

        Assert.True(engine.Stop());
        Assert.Equal(AntiAfkState.Stopped, engine.State);
        Assert.Null(engine.NextRoundAt);
        Assert.Equal(8, Keys(log).Length);
    }

    [Fact]
    public void Presses_overlap_when_the_gap_is_shorter_than_the_50ms_hold()
    {
        var (engine, time, _, log) = Create(parkAfterMs: 1500);
        engine.Start(new AntiAfkPlan(0x43, 2, 20, 1000));
        Assert.True(time.Parked.Wait(WaitTimeout));
        engine.Stop();
        Assert.Equal(new[] { "down 43 @1000", "down 43 @1020", "up 43 @1050", "up 43 @1070" }, Keys(log));
    }

    [Fact]
    public void Stop_while_a_key_is_down_still_sends_its_key_up_and_cancels_the_rest()
    {
        // gap 30 < hold 50: after the first key-down the next event is the second key-down at
        // 1030, which parks — so stop arrives while the first key is still held.
        var (engine, time, _, log) = Create(parkAfterMs: 1010);
        engine.Start(new AntiAfkPlan(0x43, 2, 30, 1000));
        Assert.True(time.Parked.Wait(WaitTimeout));
        Assert.Equal(new[] { "down 43 @1000" }, Keys(log));

        Assert.True(engine.Stop());
        Assert.Equal(new[] { "down 43 @1000", "up 43 @1050" }, Keys(log));
        Assert.Equal(AntiAfkState.Stopped, engine.State);
    }

    [Fact]
    public void Stop_before_the_first_round_sends_nothing()
    {
        var (engine, time, _, log) = Create(parkAfterMs: 0);
        engine.Start(new AntiAfkPlan(0x43, 2, 500, 180_000));
        Assert.True(time.Parked.Wait(WaitTimeout));
        Assert.True(engine.Stop());
        Assert.Empty(Keys(log));
    }

    [Fact]
    public void Start_while_running_is_ignored_and_restart_after_stop_works()
    {
        var (engine, time, _, _) = Create(parkAfterMs: 0);
        var plan = new AntiAfkPlan(0x43, 2, 500, 3000);
        Assert.True(engine.Start(plan));
        Assert.True(time.Parked.Wait(WaitTimeout));
        Assert.False(engine.Start(plan with { Count = 5 }));
        Assert.Equal(2, engine.Plan!.Count);
        Assert.True(engine.Stop());
        Assert.True(engine.Start(plan with { Count = 5 }));
        Assert.True(engine.Stop());
        Assert.True(engine.Stop());
    }

    [Fact]
    public void Exception_releases_keys_still_held()
    {
        var (engine, _, keyboard, log) = Create(parkAfterMs: 100_000);
        keyboard.ThrowOnDown = 2;
        engine.Start(new AntiAfkPlan(0x43, 2, 20, 1000));
        SpinWait.SpinUntil(() => engine.State == AntiAfkState.Stopped, WaitTimeout);

        Assert.Equal(new[] { "down 43 @1000", "up 43 @1020" }, Keys(log));
        Assert.IsType<InvalidOperationException>(engine.LastError);
    }

    [Fact]
    public void Hammer_and_anti_afk_run_independently()
    {
        var log = new List<LogEntry>();
        var time = new FakeTimeSource(log, TimeSpan.FromMilliseconds(5000));
        var hammer = new Hammer.HammerEngine(new FakeMouse(time), new Timing.SystemTimeSource());
        var afk = new AntiAfkEngine(new FakeKeyboard(time), time);

        Assert.True(afk.Start(new AntiAfkPlan(0x43, 1, 0, 1000)));
        Assert.True(hammer.Start(310));
        Assert.True(time.Parked.Wait(WaitTimeout));
        Assert.True(hammer.IsRunning);
        Assert.True(afk.IsRunning);

        Assert.True(hammer.Stop());
        Assert.True(afk.IsRunning);
        Assert.True(afk.Stop());
        Assert.False(hammer.IsRunning);
    }
}
