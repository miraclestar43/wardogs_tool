using WardogsTool.Core.Input;
using WardogsTool.Core.Timing;

namespace WardogsTool.Core.Hammer;

/// <summary>A hammer hold preset. Only these two exist in the parity version.</summary>
public sealed record HammerPreset(int HoldMs, string Name);

public static class HammerPresets
{
    /// <summary>Fixed gap between mouse-up and the next mouse-down (HAMMER_UP_S in wardogs_tool.py).</summary>
    public const int ReleaseGapMs = 40;

    public static readonly HammerPreset SmallMedium = new(310, "小/中锤");
    public static readonly HammerPreset Large = new(510, "大锤");
    public static readonly IReadOnlyList<HammerPreset> All = [SmallMedium, Large];

    public static HammerPreset FromHoldMs(int holdMs) => All.FirstOrDefault(p => p.HoldMs == holdMs) ?? SmallMedium;
}

public enum HammerState
{
    Stopped,
    /// <summary>The tool is holding the left button down.</summary>
    Holding,
    /// <summary>The left button is up, waiting out the release gap.</summary>
    Releasing,
}

/// <summary>
/// Fast hammer: left down → hold → left up → 40 ms → repeat, on a dedicated worker thread.
/// Port of <c>hammer_loop</c> / <c>start_hammer</c> / <c>stop_hammer</c> in wardogs_tool.py.
/// </summary>
/// <remarks>
/// Semantics kept from the Python tool:
/// <list type="bullet">
/// <item>The first mouse-down is sent immediately on start.</item>
/// <item>Each wait is relative to the end of the previous step (deadline = now + duration, taken
/// after the input call returns). There is no absolute schedule and no catch-up.</item>
/// <item>Start while running does nothing (F9 is start-only).</item>
/// <item>Stop during a hold sends mouse-up at once. Stop during the gap sends nothing extra. The
/// cleanup only releases a button the tool itself pressed — never an unconditional mouse-up.</item>
/// <item>Stop waits up to 1 s for the worker (Python: join(timeout=1)).</item>
/// </list>
/// The worker catches every exception so a failure can never leave the process (or the button)
/// in an undefined state; the last one is exposed as <see cref="LastError"/>.
/// </remarks>
public sealed class HammerEngine
{
    public static readonly TimeSpan StopJoinTimeout = TimeSpan.FromSeconds(1);

    private readonly IMouseInput _mouse;
    private readonly ITimeSource _time;
    private readonly object _gate = new();
    private Thread? _worker;
    private CancellationTokenSource? _stop;
    private volatile HammerState _state = HammerState.Stopped;

    public HammerEngine(IMouseInput mouse, ITimeSource time)
    {
        _mouse = mouse;
        _time = time;
    }

    public HammerState State => _state;

    /// <summary>True while the worker thread is alive (Python: hammer_running()).</summary>
    public bool IsRunning
    {
        get
        {
            lock (_gate)
                return _worker is { IsAlive: true };
        }
    }

    /// <summary>The hold used by the current (or last) run.</summary>
    public int HoldMs { get; private set; }

    public Exception? LastError { get; private set; }

    /// <summary>Starts hammering. Returns false (and changes nothing) if already running.</summary>
    public bool Start(int holdMs)
    {
        if (holdMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(holdMs), holdMs, "Hold must be positive.");

        lock (_gate)
        {
            if (_worker is { IsAlive: true })
                return false;

            HoldMs = holdMs;
            LastError = null;
            var stop = new CancellationTokenSource();
            var hold = TimeSpan.FromMilliseconds(holdMs);
            var worker = new Thread(() => Run(hold, stop.Token))
            {
                IsBackground = true,
                Name = "WardogsTool hammer",
            };
            _stop = stop;
            _worker = worker;
            // Marked as holding before the thread starts so a status read straight after Start()
            // never shows "stopped" for a run that is about to press the button.
            _state = HammerState.Holding;
            worker.Start();
            return true;
        }
    }

    /// <summary>
    /// Stops hammering and waits (up to 1 s) for the worker to release the button. Safe to call
    /// when not running, and from any thread except the worker itself.
    /// </summary>
    /// <returns>true if the worker has exited.</returns>
    public bool Stop()
    {
        Thread? worker;
        lock (_gate)
        {
            _stop?.Cancel();
            worker = _worker;
        }
        if (worker is null)
            return true;
        return worker.Join(StopJoinTimeout);
    }

    private void Run(TimeSpan hold, CancellationToken stop)
    {
        var gap = TimeSpan.FromMilliseconds(HammerPresets.ReleaseGapMs);
        var down = false;
        try
        {
            while (!stop.IsCancellationRequested)
            {
                _state = HammerState.Holding;
                _mouse.LeftDown();
                down = true;
                if (!_time.WaitUntil(_time.Now + hold, stop))
                    break;
                _mouse.LeftUp();
                down = false;
                _state = HammerState.Releasing;
                if (!_time.WaitUntil(_time.Now + gap, stop))
                    break;
            }
        }
        catch (Exception ex)
        {
            LastError = ex;
        }
        finally
        {
            if (down)
            {
                try
                {
                    _mouse.LeftUp();
                }
                catch (Exception ex)
                {
                    LastError ??= ex;
                }
            }
            _state = HammerState.Stopped;
        }
    }
}
