using WardogsTool.Core.Input;
using WardogsTool.Core.Timing;

namespace WardogsTool.Core.AntiAfk;

public enum AntiAfkState
{
    Stopped,
    /// <summary>Started; presses are scheduled.</summary>
    Running,
    /// <summary>Stop requested; only key-ups that are already due are still being sent.</summary>
    Stopping,
}

/// <summary>
/// Anti-AFK: every period, press a key <c>Count</c> times, <c>GapMs</c> apart, each press held
/// 50 ms. Port of <c>start_afk</c> / <c>schedule_cycle</c> / <c>cycle</c> / <c>tap</c> /
/// <c>stop_afk</c> in wardogs_tool.py.
/// </summary>
/// <remarks>
/// The Python tool schedules everything with Tk's <c>after()</c>; this worker replays the same
/// timeline as an event queue:
/// <list type="bullet">
/// <item>Start schedules the first round one full period later — not immediately.</item>
/// <item>When a round runs at time T (actual, not planned), presses go out at T + i·gap and the
/// next round is scheduled at T + period.</item>
/// <item>Every key-down schedules its key-up 50 ms after the key-down call returns. With
/// gap &lt; 50 ms presses overlap, exactly as in Python.</item>
/// <item>Stop drops pending rounds and key-downs. Key-ups for keys already down still go out on
/// time, so no key is left stuck.</item>
/// </list>
/// </remarks>
public sealed class AntiAfkEngine
{
    /// <summary>KEY_HOLD_MS in wardogs_tool.py: shorter presses are missed by some games.</summary>
    public const int KeyHoldMs = 50;

    public static readonly TimeSpan StopJoinTimeout = TimeSpan.FromSeconds(1);

    private enum EventKind { Round, KeyDown, KeyUp }

    private readonly record struct ScheduledEvent(TimeSpan Due, long Sequence, EventKind Kind);

    private readonly IKeyboardInput _keyboard;
    private readonly ITimeSource _time;
    private readonly object _gate = new();
    private Thread? _worker;
    private CancellationTokenSource? _stop;
    private volatile AntiAfkState _state = AntiAfkState.Stopped;
    private long _nextRoundTicks = -1;

    public AntiAfkEngine(IKeyboardInput keyboard, ITimeSource time)
    {
        _keyboard = keyboard;
        _time = time;
    }

    public AntiAfkState State => _state;

    /// <summary>Python's afk_running: true from Start until Stop is requested.</summary>
    public bool IsRunning => _state == AntiAfkState.Running;

    public AntiAfkPlan? Plan { get; private set; }

    public Exception? LastError { get; private set; }

    /// <summary>When the next round starts, on <see cref="ITimeSource.Now"/>'s clock (Python: next_press).</summary>
    public TimeSpan? NextRoundAt
    {
        get
        {
            var ticks = Interlocked.Read(ref _nextRoundTicks);
            return ticks < 0 || _state != AntiAfkState.Running ? null : TimeSpan.FromTicks(ticks);
        }
    }

    /// <summary>Starts the schedule. Returns false (and changes nothing) if already running.</summary>
    public bool Start(AntiAfkPlan plan)
    {
        lock (_gate)
        {
            if (_worker is { IsAlive: true })
                return false;

            Plan = plan;
            LastError = null;
            var stop = new CancellationTokenSource();
            var firstRound = _time.Now + TimeSpan.FromMilliseconds(plan.PeriodMs);
            Interlocked.Exchange(ref _nextRoundTicks, firstRound.Ticks);
            var worker = new Thread(() => Run(plan, firstRound, stop.Token))
            {
                IsBackground = true,
                Name = "WardogsTool anti-AFK",
            };
            _stop = stop;
            _worker = worker;
            _state = AntiAfkState.Running;
            worker.Start();
            return true;
        }
    }

    /// <summary>
    /// Stops the schedule and waits (up to 1 s) for outstanding key-ups. Safe to call when not
    /// running.
    /// </summary>
    /// <returns>true if the worker has exited.</returns>
    public bool Stop()
    {
        Thread? worker;
        lock (_gate)
        {
            if (_stop is { IsCancellationRequested: false } stop)
            {
                if (_state == AntiAfkState.Running)
                    _state = AntiAfkState.Stopping;
                stop.Cancel();
            }
            worker = _worker;
        }
        Interlocked.Exchange(ref _nextRoundTicks, -1);
        if (worker is null)
            return true;
        return worker.Join(StopJoinTimeout);
    }

    private void Run(AntiAfkPlan plan, TimeSpan firstRound, CancellationToken stop)
    {
        var queue = new List<ScheduledEvent>();
        long sequence = 0;
        var keysDown = 0;
        void Schedule(TimeSpan due, EventKind kind) => queue.Add(new ScheduledEvent(due, sequence++, kind));

        try
        {
            Schedule(firstRound, EventKind.Round);
            while (queue.Count > 0)
            {
                // Earliest due first; ties in scheduling order (Tk's after() behaves the same).
                var next = queue.MinBy(e => (e.Due, e.Sequence));
                var mustRun = next.Kind == EventKind.KeyUp;
                if (!_time.WaitUntil(next.Due, mustRun ? CancellationToken.None : stop))
                {
                    // Stopped: forget rounds and key-downs, keep the key-ups that are owed.
                    queue.RemoveAll(e => e.Kind != EventKind.KeyUp);
                    continue;
                }
                queue.Remove(next);

                switch (next.Kind)
                {
                    case EventKind.Round:
                        var now = _time.Now;
                        for (var i = 0; i < plan.Count; i++)
                            Schedule(now + TimeSpan.FromMilliseconds(i * plan.GapMs), EventKind.KeyDown);
                        var nextRound = now + TimeSpan.FromMilliseconds(plan.PeriodMs);
                        Schedule(nextRound, EventKind.Round);
                        if (!stop.IsCancellationRequested)
                            Interlocked.Exchange(ref _nextRoundTicks, nextRound.Ticks);
                        break;

                    case EventKind.KeyDown:
                        _keyboard.KeyDown(plan.VirtualKey);
                        keysDown++;
                        Schedule(_time.Now + TimeSpan.FromMilliseconds(KeyHoldMs), EventKind.KeyUp);
                        break;

                    case EventKind.KeyUp:
                        _keyboard.KeyUp(plan.VirtualKey);
                        keysDown--;
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            LastError = ex;
        }
        finally
        {
            // Only reached early on an exception: release whatever this worker still holds down.
            for (; keysDown > 0; keysDown--)
            {
                try
                {
                    _keyboard.KeyUp(plan.VirtualKey);
                }
                catch (Exception ex)
                {
                    LastError ??= ex;
                    break;
                }
            }
            Interlocked.Exchange(ref _nextRoundTicks, -1);
            _state = AntiAfkState.Stopped;
        }
    }
}
