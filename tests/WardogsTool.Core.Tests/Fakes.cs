using WardogsTool.Core.Input;
using WardogsTool.Core.Timing;

namespace WardogsTool.Core.Tests;

/// <summary>One line of the shared log that fakes write to, in the order things happened.</summary>
public readonly record struct LogEntry(string What, TimeSpan At);

/// <summary>
/// A fake clock that jumps straight to each deadline, so state machines run instantly and
/// deterministically. A cancellable wait whose deadline is past <see cref="ParkAfter"/> "parks":
/// it signals <see cref="Parked"/> and blocks until cancelled. That freezes the worker at a known
/// point so a test can inspect it and then call Stop().
/// </summary>
public sealed class FakeTimeSource : ITimeSource
{
    private readonly object _gate = new();
    private TimeSpan _now;

    public FakeTimeSource(List<LogEntry> log, TimeSpan parkAfter)
    {
        Log = log;
        ParkAfter = parkAfter;
    }

    public List<LogEntry> Log { get; }
    public TimeSpan ParkAfter { get; }
    public ManualResetEventSlim Parked { get; } = new();
    /// <summary>Throw from the n-th wait (1-based), to test cleanup on exceptions.</summary>
    public int ThrowOnWait { get; set; }
    private int _waits;

    public TimeSpan Now
    {
        get { lock (_gate) return _now; }
    }

    /// <summary>Simulates time spent inside an input call.</summary>
    public void Advance(TimeSpan by)
    {
        lock (_gate) _now += by;
    }

    public bool WaitUntil(TimeSpan deadline, CancellationToken cancellation)
    {
        lock (_gate)
        {
            _waits++;
            Log.Add(new LogEntry(cancellation.CanBeCanceled ? $"wait {deadline.TotalMilliseconds}" : $"wait! {deadline.TotalMilliseconds}", _now));
            if (_waits == ThrowOnWait)
                throw new InvalidOperationException("injected failure");
        }
        if (cancellation.IsCancellationRequested)
            return false;
        if (cancellation.CanBeCanceled && deadline > ParkAfter)
        {
            Parked.Set();
            cancellation.WaitHandle.WaitOne();
            return false;
        }
        lock (_gate)
        {
            if (deadline > _now)
                _now = deadline;
        }
        return !cancellation.IsCancellationRequested;
    }
}

public sealed class FakeMouse : IMouseInput
{
    private readonly FakeTimeSource _time;

    public FakeMouse(FakeTimeSource time) => _time = time;

    public TimeSpan CallDuration { get; set; }
    /// <summary>Throw from the n-th LeftDown (1-based).</summary>
    public int ThrowOnDown { get; set; }
    private int _downs;

    public void LeftDown()
    {
        lock (_time.Log)
        {
            _downs++;
            if (_downs == ThrowOnDown)
                throw new InvalidOperationException("injected failure");
            _time.Log.Add(new LogEntry("down", _time.Now));
        }
        _time.Advance(CallDuration);
    }

    public void LeftUp()
    {
        lock (_time.Log)
            _time.Log.Add(new LogEntry("up", _time.Now));
        _time.Advance(CallDuration);
    }
}

public sealed class FakeKeyboard : IKeyboardInput
{
    private readonly FakeTimeSource? _time;
    private readonly IReadOnlyDictionary<char, byte> _keys;

    public FakeKeyboard(FakeTimeSource? time, IReadOnlyDictionary<char, byte>? keys = null)
    {
        _time = time;
        _keys = keys ?? new Dictionary<char, byte> { ['c'] = 0x43, ['C'] = 0x43 };
    }

    /// <summary>Throw from the n-th KeyDown (1-based).</summary>
    public int ThrowOnDown { get; set; }
    private int _downs;

    public bool TryGetVirtualKey(char c, out byte virtualKey) => _keys.TryGetValue(c, out virtualKey);

    /// <summary>US-layout set-1 scan codes for the keys the tests use.</summary>
    public byte GetScanCode(byte virtualKey) => virtualKey switch { 0x43 => 0x2E, 0x31 => 0x02, 0xBF => 0x35, _ => 0 };

    public void KeyDown(byte virtualKey, byte scanCode)
    {
        lock (_time!.Log)
        {
            _downs++;
            if (_downs == ThrowOnDown)
                throw new InvalidOperationException("injected failure");
            _time.Log.Add(new LogEntry($"down {virtualKey:X2}", _time.Now));
        }
    }

    public void KeyUp(byte virtualKey, byte scanCode)
    {
        lock (_time!.Log)
            _time.Log.Add(new LogEntry($"up {virtualKey:X2}", _time.Now));
    }
}
